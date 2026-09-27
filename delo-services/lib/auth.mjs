import { scrypt, randomBytes, createHash, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';
const derive = promisify(scrypt);
const options = { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 };
export async function hashPassword(password) {
  const salt = randomBytes(16).toString('hex');
  const key = await derive(password, salt, 64, options);
  return `${salt}:${key.toString('hex')}`;
}
export async function verifyPassword(password, encoded) {
  const [salt, hex] = encoded.split(':');
  const actual = await derive(password, salt, 64, options);
  const expected = Buffer.from(hex, 'hex');
  return expected.length === actual.length && timingSafeEqual(expected, actual);
}
export const digest = token => createHash('sha256').update(token).digest('hex');
export function createSession(db, userId) {
  const token = randomBytes(32).toString('hex');
  db.prepare('DELETE FROM sessions WHERE expires_at < ?').run(Date.now());
  db.prepare('INSERT INTO sessions(token_hash,user_id,expires_at) VALUES (?,?,?)').run(digest(token), userId, Date.now() + 7 * 86400000);
  return token;
}
export function readToken(req) {
  return (req.headers.cookie || '').split(';').map(part => part.trim()).find(part => part.startsWith('delo_session='))?.slice(13) || '';
}
export function sessionUser(db, req) {
  const token = readToken(req);
  if (!/^[a-f0-9]{64}$/.test(token)) return null;
  return db.prepare(`SELECT u.id,u.name,u.email,u.role FROM sessions s JOIN users u ON u.id=s.user_id
    WHERE s.token_hash=? AND s.expires_at>?`).get(digest(token), Date.now()) || null;
}
export function cookie(token, secure = false) {
  return `delo_session=${token}; HttpOnly; SameSite=Strict; Path=/; Max-Age=${token ? 604800 : 0}${secure ? '; Secure' : ''}`;
}
