import { randomUUID } from 'node:crypto';
import { categories, logos, transaction } from './database.mjs';
import { hashPassword, verifyPassword, createSession, sessionUser, readToken, digest, cookie } from './auth.mjs';

class HttpError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}
const fail = (status, message) => { throw new HttpError(status, message); };
function text(value, label, min, max) {
  if (typeof value !== 'string' || value.trim().length < min || value.trim().length > max) fail(400, `${label}: от ${min} до ${max} символов.`);
  return value.trim();
}
function integer(value, label, min, max) {
  if (!Number.isSafeInteger(value) || value < min || value > max) fail(400, `${label}: целое число от ${min} до ${max}.`);
  return value;
}
async function readBody(req) {
  if (!req.headers['content-type']?.startsWith('application/json')) fail(415, 'Ожидается JSON.');
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    chunks.push(chunk);
    size += chunk.length;
    if (size > 32768) fail(413, 'Слишком большой запрос.');
  }
  try {
    const body = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    if (!body || Array.isArray(body) || typeof body !== 'object') fail(400, 'Некорректные данные.');
    return body;
  } catch { fail(400, 'Некорректный JSON.'); }
}
function publicUser(user) { return { id: user.id, name: user.name, email: user.email, role: user.role }; }
function validateService(body) {
  const title = text(body.title, 'Название', 5, 100);
  const description = text(body.description, 'Описание', 30, 5000);
  if (!Object.hasOwn(categories, body.category)) fail(400, 'Выберите категорию из списка.');
  if (!logos.some(logo => logo.id === body.logo)) fail(400, 'Выберите логотип из предложенного набора.');
  return { title, description, category: body.category, logo: body.logo,
    price: integer(body.price, 'Цена', 100, 10000000), days: integer(body.days, 'Срок', 1, 365) };
}

export function createApi(db, { secureCookies = false, publicOrigin = '' } = {}) {
  const attempts = new Map();
  // A precomputed dummy hash makes unknown-email login follow the same password path.
  const dummyHash = hashPassword(randomUUID());
  function limit(key, max, period) {
    const now = Date.now();
    if (attempts.size > 10000) for (const [k, v] of attempts) if (v.until < now) attempts.delete(k);
    const entry = attempts.get(key);
    if (!entry || entry.until < now) { attempts.set(key, { count: 1, until: now + period }); return; }
    if (++entry.count > max) fail(429, 'Слишком много попыток. Попробуйте чуть позже.');
  }
  const serviceById = id => db.prepare(`SELECT s.*,u.name seller_name FROM services s JOIN users u ON u.id=s.seller_id WHERE s.id=?`).get(id);
  function conversationFor(service, clientId) {
    const existing = db.prepare('SELECT * FROM conversations WHERE service_id=? AND client_id=?').get(service.id, clientId);
    if (existing) return existing;
    const id = randomUUID();
    db.prepare('INSERT INTO conversations(id,service_id,client_id,seller_id) VALUES (?,?,?,?)').run(id, service.id, clientId, service.seller_id);
    return db.prepare('SELECT * FROM conversations WHERE id=?').get(id);
  }
  function ownConversation(id, user) {
    const conversation = db.prepare('SELECT * FROM conversations WHERE id=? AND (client_id=? OR seller_id=?)').get(id, user.id, user.id);
    if (!conversation) fail(404, 'Переписка не найдена.');
    return conversation;
  }
  return async function api(req, res, url) {
    const send = (status, payload, headers = {}) => {
      res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', ...headers });
      res.end(JSON.stringify(payload));
    };
    try {
      const method = req.method;
      if (!['GET', 'POST', 'PATCH'].includes(method)) fail(405, 'Метод не поддерживается.');
      const mutating = method !== 'GET';
      if (mutating) {
        const origin = publicOrigin || `${secureCookies ? 'https' : 'http'}://${req.headers.host}`;
        if (req.headers.origin !== origin || req.headers['x-delo-request'] !== '1') fail(403, 'Запрос должен быть отправлен с этого сайта.');
      }
      const path = url.pathname;
      const user = sessionUser(db, req);
      const requireUser = (role) => {
        if (!user) fail(401, 'Войдите в аккаунт.');
        if (role && user.role !== role) fail(403, role === 'seller' ? 'Доступно только продавцам.' : 'Доступно только клиентам.');
        return user;
      };
      if (path === '/api/meta' && method === 'GET') return send(200, { categories, logos });
      if (path === '/api/me' && method === 'GET') return send(200, { user });
      if (['/api/register', '/api/login'].includes(path) && method === 'POST') {
        limit(`auth:${req.socket.remoteAddress}`, 25, 15 * 60000);
        const body = await readBody(req);
        const email = text(body.email, 'Email', 5, 160).toLowerCase();
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) fail(400, 'Введите корректный email.');
        limit(`email:${email}`, 10, 15 * 60000);
        if (typeof body.password !== 'string' || body.password.length < 10 || body.password.length > 128) fail(400, 'Пароль должен содержать от 10 до 128 символов.');
        let account;
        if (path === '/api/register') {
          const name = text(body.name, 'Имя', 2, 80);
          if (!['client', 'seller'].includes(body.role)) fail(400, 'Выберите роль: клиент или продавец.');
          const passwordHash = await hashPassword(body.password);
          if (db.prepare('SELECT id FROM users WHERE email=?').get(email)) fail(409, 'Этот email уже зарегистрирован.');
          account = { id: randomUUID(), name, email, role: body.role };
          db.prepare('INSERT INTO users(id,name,email,password_hash,role) VALUES (?,?,?,?,?)').run(account.id, name, email, passwordHash, body.role);
        } else {
          account = db.prepare('SELECT * FROM users WHERE email=?').get(email);
          const valid = await verifyPassword(body.password, account?.password_hash || await dummyHash);
          if (!account || !valid) fail(401, 'Неверный email или пароль.');
        }
        // Rotate any current session when signing in or switching accounts.
        if (readToken(req)) db.prepare('DELETE FROM sessions WHERE token_hash=?').run(digest(readToken(req)));
        const token = createSession(db, account.id);
        return send(path === '/api/register' ? 201 : 200, { user: publicUser(account) }, { 'Set-Cookie': cookie(token, secureCookies) });
      }
      if (path === '/api/logout' && method === 'POST') {
        db.prepare('DELETE FROM sessions WHERE token_hash=?').run(digest(readToken(req)));
        return send(200, { ok: true }, { 'Set-Cookie': cookie('', secureCookies) });
      }
      if (path === '/api/services' && method === 'GET') {
        const p = url.searchParams;
        const query = (p.get('q') || '').trim().toLocaleLowerCase('ru');
        if (query.length > 100) fail(400, 'Поисковый запрос слишком длинный.');
        const category = p.get('category') || 'all';
        if (category !== 'all' && !Object.hasOwn(categories, category)) fail(400, 'Неизвестная категория.');
        const numeric = (name, fallback, max) => p.get(name) ? integer(Number(p.get(name)), name, 0, max) : fallback;
        const min = numeric('min', 0, 10000000), max = numeric('max', 10000000, 10000000), days = numeric('days', 365, 365);
        if (min > max) fail(400, 'Минимальная цена больше максимальной.');
        const page = integer(Number(p.get('page') || 1), 'Страница', 1, 100000);
        const sort = p.get('sort') || 'new';
        if (!['new','price-asc','price-desc','days'].includes(sort)) fail(400, 'Неизвестная сортировка.');
        // Filtering text in JS preserves Unicode case folding for Cyrillic with stock SQLite.
        let items = db.prepare(`SELECT s.*,u.name seller_name FROM services s JOIN users u ON u.id=s.seller_id
          WHERE s.active=1 AND s.price BETWEEN ? AND ? AND s.days<=? ORDER BY s.created_at DESC,s.id`).all(min, max, days);
        items = items.filter(s => (category === 'all' || s.category === category) && `${s.title} ${s.description} ${s.seller_name}`.toLocaleLowerCase('ru').includes(query));
        if (sort === 'price-asc') items.sort((a,b) => a.price-b.price);
        if (sort === 'price-desc') items.sort((a,b) => b.price-a.price);
        if (sort === 'days') items.sort((a,b) => a.days-b.days);
        return send(200, { total: items.length, page, pages: Math.ceil(items.length / 12), services: items.slice((page-1)*12,page*12) });
      }
      if (path === '/api/my-services' && method === 'GET') {
        requireUser('seller');
        return send(200, { services: db.prepare('SELECT * FROM services WHERE seller_id=? ORDER BY created_at DESC,id').all(user.id) });
      }
      if (path === '/api/services' && method === 'POST') {
        requireUser('seller'); limit(`service:${user.id}`, 30, 60000);
        const service = validateService(await readBody(req));
        const id = randomUUID();
        db.prepare('INSERT INTO services(id,seller_id,title,description,category,logo,price,days) VALUES (?,?,?,?,?,?,?,?)')
          .run(id, user.id, service.title, service.description, service.category, service.logo, service.price, service.days);
        return send(201, { service: serviceById(id) });
      }
      const serviceMatch = path.match(/^\/api\/services\/([a-f0-9-]+)$/);
      if (serviceMatch) {
        const service = serviceById(serviceMatch[1]);
        if (!service || (!service.active && service.seller_id !== user?.id)) fail(404, 'Услуга не найдена или снята с публикации.');
        if (method === 'GET') return send(200, { service });
        if (method === 'PATCH') {
          requireUser('seller');
          if (service.seller_id !== user.id) fail(403, 'Можно изменять только свои услуги.');
          const body = await readBody(req);
          if (Object.keys(body).length === 1 && typeof body.active === 'boolean') {
            db.prepare('UPDATE services SET active=? WHERE id=?').run(Number(body.active), service.id);
          } else {
            const next = validateService(body);
            db.prepare('UPDATE services SET title=?,description=?,category=?,logo=?,price=?,days=? WHERE id=?')
              .run(next.title, next.description, next.category, next.logo, next.price, next.days, service.id);
          }
          return send(200, { service: serviceById(service.id) });
        }
      }
      if (path === '/api/orders' && method === 'POST') {
        requireUser('client'); limit(`order:${user.id}`, 15, 60000);
        const body = await readBody(req);
        const brief = text(body.brief, 'Бриф', 20, 5000);
        const key = text(body.requestKey, 'Ключ запроса', 16, 80);
        const existing = db.prepare('SELECT * FROM orders WHERE client_id=? AND request_key=?').get(user.id, key);
        if (existing) {
          if (existing.service_id !== body.serviceId || existing.brief !== brief) fail(409, 'Ключ запроса уже использован.');
          return send(200, { order: existing });
        }
        const service = typeof body.serviceId === 'string' ? serviceById(body.serviceId) : null;
        if (!service?.active) fail(404, 'Услуга недоступна.');
        const order = transaction(db, () => {
          const conversation = conversationFor(service, user.id);
          const id = randomUUID();
          db.prepare(`INSERT INTO orders(id,service_id,client_id,seller_id,conversation_id,title,price,days,brief,request_key) VALUES (?,?,?,?,?,?,?,?,?,?)`)
            .run(id,service.id,user.id,service.seller_id,conversation.id,service.title,service.price,service.days,brief,key);
          db.prepare('INSERT INTO messages(conversation_id,sender_id,body) VALUES (?,?,?)')
            .run(conversation.id,user.id,`Заказ «${service.title}»\n\n${brief}`);
          return db.prepare('SELECT * FROM orders WHERE id=?').get(id);
        });
        return send(201, { order });
      }
      if (path === '/api/orders' && method === 'GET') {
        requireUser();
        const orders = db.prepare(`SELECT o.*,c.name client_name,s.name seller_name FROM orders o
          JOIN users c ON c.id=o.client_id JOIN users s ON s.id=o.seller_id
          WHERE o.client_id=? OR o.seller_id=? ORDER BY o.created_at DESC,o.id`).all(user.id,user.id);
        return send(200, { orders });
      }
      const orderMatch = path.match(/^\/api\/orders\/([a-f0-9-]+)$/);
      if (orderMatch && method === 'PATCH') {
        requireUser();
        const body = await readBody(req);
        const order = db.prepare('SELECT * FROM orders WHERE id=? AND (client_id=? OR seller_id=?)').get(orderMatch[1],user.id,user.id);
        if (!order) fail(404, 'Заказ не найден.');
        const allowed = user.role === 'seller'
          ? { new:['in_progress','cancelled'], in_progress:['delivered'], delivered:[], completed:[], cancelled:[] }
          : { new:['cancelled'], in_progress:[], delivered:['completed','in_progress'], completed:[], cancelled:[] };
        if (!allowed[order.status].includes(body.status)) fail(409, 'Этот переход статуса недоступен. Обновите список заказов.');
        db.prepare('UPDATE orders SET status=? WHERE id=?').run(body.status,order.id);
        return send(200, { order: { ...order, status: body.status } });
      }
      if (path === '/api/conversations' && method === 'POST') {
        requireUser('client'); limit(`conversation:${user.id}`, 30, 60000);
        const body = await readBody(req);
        const service = typeof body.serviceId === 'string' ? serviceById(body.serviceId) : null;
        if (!service?.active) fail(404, 'Услуга недоступна.');
        return send(200, { conversation: conversationFor(service, user.id) });
      }
      if (path === '/api/conversations' && method === 'GET') {
        requireUser();
        const conversations = db.prepare(`SELECT c.*,s.title,client.name client_name,seller.name seller_name,
          (SELECT body FROM messages WHERE conversation_id=c.id ORDER BY id DESC LIMIT 1) last_message,
          (SELECT MAX(id) FROM messages WHERE conversation_id=c.id) last_id,
          (SELECT COUNT(*) FROM messages m WHERE m.conversation_id=c.id AND m.sender_id!=?
            AND m.id>COALESCE((SELECT last_message_id FROM conversation_reads r WHERE r.conversation_id=c.id AND r.user_id=?),0)) unread
          FROM conversations c JOIN services s ON s.id=c.service_id JOIN users client ON client.id=c.client_id JOIN users seller ON seller.id=c.seller_id
          WHERE c.client_id=? OR c.seller_id=? ORDER BY last_id DESC,c.created_at DESC`).all(user.id,user.id,user.id,user.id);
        return send(200, { conversations });
      }
      const messagesMatch = path.match(/^\/api\/conversations\/([a-f0-9-]+)\/messages$/);
      if (messagesMatch) {
        requireUser(); ownConversation(messagesMatch[1], user);
        if (method === 'GET') {
          const after = integer(Number(url.searchParams.get('after') || 0), 'Номер сообщения', 0, Number.MAX_SAFE_INTEGER);
          const messages = db.prepare(`SELECT m.*,u.name sender_name FROM messages m JOIN users u ON u.id=m.sender_id
            WHERE m.conversation_id=? AND m.id>? ORDER BY m.id LIMIT 100`).all(messagesMatch[1],after);
          return send(200, { messages, hasMore: messages.length === 100 });
        }
        if (method === 'POST') {
          limit(`message:${user.id}`, 60, 60000);
          const body = await readBody(req);
          const content = text(body.body, 'Сообщение', 1, 5000);
          const result = db.prepare('INSERT INTO messages(conversation_id,sender_id,body) VALUES (?,?,?)').run(messagesMatch[1],user.id,content);
          return send(201, { message: db.prepare('SELECT * FROM messages WHERE id=?').get(result.lastInsertRowid) });
        }
      }
      const readMatch = path.match(/^\/api\/conversations\/([a-f0-9-]+)\/read$/);
      if (readMatch && method === 'POST') {
        requireUser(); ownConversation(readMatch[1], user);
        const body = await readBody(req);
        const lastId = integer(body.lastId, 'Номер сообщения', 1, Number.MAX_SAFE_INTEGER);
        if (!db.prepare('SELECT id FROM messages WHERE id=? AND conversation_id=?').get(lastId,readMatch[1])) fail(400, 'Сообщение не найдено.');
        db.prepare(`INSERT INTO conversation_reads(conversation_id,user_id,last_message_id) VALUES (?,?,?)
          ON CONFLICT(conversation_id,user_id) DO UPDATE SET last_message_id=MAX(last_message_id,excluded.last_message_id)`)
          .run(readMatch[1],user.id,lastId);
        return send(200, { ok: true });
      }
      fail(404, 'Адрес не найден.');
    } catch (error) {
      if (!error.status) console.error('API error:', error);
      send(error.status || 500, { error: error.status ? error.message : 'Ошибка сервера. Попробуйте позже.' });
    }
  };
}
