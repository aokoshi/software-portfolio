'use strict';
const $ = selector => document.querySelector(selector);
const escapeHTML = value => String(value ?? '').replace(/[&<>"']/g, character => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
const htmlCache = new Map();
function setHTML(selector,html) { if(htmlCache.get(selector) !== html) { $(selector).innerHTML = html; htmlCache.set(selector,html); } }
const money = value => new Intl.NumberFormat('ru-RU').format(value) + ' ₸';
const date = value => new Date(value).toLocaleString('ru-RU',{day:'2-digit',month:'short',hour:'2-digit',minute:'2-digit'});
const statuses = {new:'Новый',in_progress:'В работе',delivered:'На проверке',completed:'Завершён',cancelled:'Отменён'};
const state = { user:null, meta:{categories:{},logos:[]}, view:'catalog', filters:{q:'',category:'all',min:'',max:'',days:'',sort:'new',page:1}, services:[], mine:[], orders:[], conversations:[], conversation:null, lastMessage:0, pendingService:null };
let pageGeneration=0, catalogGeneration=0, chatGeneration=0, pollBusy=false, returnFocus;
async function api(path,method='GET',data) {
  const response=await fetch('/api'+path,{method,credentials:'same-origin',headers:method==='GET'?{}:{'Content-Type':'application/json','X-Delo-Request':'1'},body:data===undefined?undefined:JSON.stringify(data)});
  const payload=await response.json();
  if(!response.ok) {
    if(response.status===401 && state.user && !['/login','/register'].includes(path)) {
      state.user=null; state.conversations=[]; state.conversation=null; pageGeneration++; chatGeneration++;
      $('#modal').close(); renderHeader(); navigate('catalog');
    }
    throw new Error(payload.error||'Не удалось выполнить запрос.');
  }
  return payload;
}
function notify(message) {$('#notice').textContent=message;$('#notice').hidden=!message;}
function modalError(message) {$('#modal-error').textContent=message;$('#modal-error').hidden=!message;}
function showModal(html) {
  if(!$('#modal').open) returnFocus=document.activeElement;
  $('#modal-body').innerHTML=html;modalError('');
  if(!$('#modal').open)$('#modal').showModal();
  document.body.classList.add('modal-open');$('#modal').scrollTop=0;
  ($('#modal-body').querySelector('input:not([type="radio"]),textarea')||$('#close-modal')).focus();
}
$('#close-modal').addEventListener('click',()=>$('#modal').close());
$('#modal').addEventListener('close',()=>{document.body.classList.remove('modal-open');$('#modal-body').replaceChildren();returnFocus?.isConnected&&returnFocus.focus();});
function navigate(view) {if(location.hash==='#'+view)renderPage();else location.hash=view;}
function renderHeader() {
  const links=[['catalog','Каталог']];
  if(state.user){links.push(['orders','Мои заказы'],['messages','Сообщения']);if(state.user.role==='seller')links.push(['services','Мои услуги']);}
  const unread=state.conversations.reduce((sum,c)=>sum+c.unread,0);
  const navigationHTML=links.map(([key,label])=>`<a href="#${key}" ${state.view===key?'aria-current="page"':''}>${label}${key==='messages'&&unread?` <span class="unread">${unread}</span>`:''}</a>`).join('');
  setHTML('#navigation',navigationHTML);
  const accountHTML=state.user?`<div class="account-name">${escapeHTML(state.user.name)}<small>${state.user.role==='seller'?'Продавец':'Клиент'}</small></div><button class="secondary compact" data-action="logout">Выйти</button>`:`<button class="text-button" data-action="login">Войти</button><button class="primary compact" data-action="register">Регистрация</button>`;
  setHTML('#account',accountHTML);
}
function logoView(id,small=false) {
  const logo=state.meta.logos.find(l=>l.id===id)||state.meta.logos[0];
  return `<span class="service-logo ${escapeHTML(logo.color)} ${small?'small':''}" aria-label="${escapeHTML(logo.name)}">${escapeHTML(logo.symbol)}</span>`;
}
function empty(title,description,action='') {return `<div class="empty"><h3>${escapeHTML(title)}</h3><p>${escapeHTML(description)}</p>${action}</div>`;}
function serviceCard(service,own=false) {
  return `<article class="service-card"><div class="service-art ${escapeHTML(state.meta.logos.find(l=>l.id===service.logo)?.color||'blue')}">${logoView(service.logo)}<span>${escapeHTML(state.meta.categories[service.category])}</span></div><div class="card-content"><div class="card-meta"><span>${service.days} дн.</span>${own?`<span class="tag">${service.active?'Опубликована':'Скрыта'}</span>`:`<span>${escapeHTML(service.seller_name)}</span>`}</div><h3>${escapeHTML(service.title)}</h3><p class="description">${escapeHTML(service.description.slice(0,140))}${service.description.length>140?'…':''}</p><div class="card-bottom"><strong class="price">${money(service.price)}</strong>${own?`<button class="card-button" data-action="edit" data-id="${service.id}">Изменить</button>`:`<button class="card-button" data-action="details" data-id="${service.id}">Подробнее ↗</button>`}</div>${own?`<button class="text-button visibility-button" data-action="visibility" data-id="${service.id}">${service.active?'Снять с публикации':'Опубликовать'}</button>`:''}</div></article>`;
}
async function renderPage() {
  const generation=++pageGeneration;chatGeneration++;
  state.view=(location.hash.slice(1)||'catalog').split('?')[0];
  if(!['catalog','orders','messages','services'].includes(state.view))state.view='catalog';
  notify('');renderHeader();
  if(state.view!=='catalog'&&!state.user){$('#main').innerHTML=`<section class="wrap page">${empty('Войдите в аккаунт','Здесь будут ваши заказы, услуги и переписки.','<button class="primary" data-action="login">Войти</button>')}</section>`;return;}
  if(state.view==='catalog'){renderCatalogPage();await loadCatalog();return;}
  $('#main').innerHTML='<section class="wrap page loading" role="status">Загружаем…</section>';
  try {
    if(state.view==='services') {
      if(state.user.role!=='seller'){navigate('catalog');return;}
      const data=await api('/my-services');if(generation!==pageGeneration)return;state.mine=data.services;
      $('#main').innerHTML=`<section class="wrap page"><div class="section-heading"><div><p class="eyebrow">КАБИНЕТ ПРОДАВЦА</p><h1 class="page-title">Мои услуги</h1><p class="muted">Управляйте предложениями и находите новых клиентов.</p></div><button class="primary" data-action="create">+ Создать услугу</button></div><div class="service-grid">${state.mine.map(s=>serviceCard(s,true)).join('')}</div>${!state.mine.length?empty('Начните с первой услуги','Расскажите, что вы делаете, укажите цену и выберите логотип.','<button class="primary" data-action="create">Создать услугу</button>'):''}</section>`;
    }
    if(state.view==='orders') {
      const data=await api('/orders');if(generation!==pageGeneration)return;state.orders=data.orders;
      $('#main').innerHTML=`<section class="wrap page"><div class="section-heading"><div><p class="eyebrow">${state.user.role==='seller'?'ВХОДЯЩИЕ ЗАКАЗЫ':'ЛИЧНЫЙ КАБИНЕТ'}</p><h1 class="page-title">Мои заказы</h1></div><button class="secondary" data-action="refresh">Обновить</button></div><div class="order-filters" role="group" aria-label="Статус заказа"><button class="chip active" data-action="order-filter" data-status="all" aria-pressed="true">Все</button>${Object.entries(statuses).map(([key,label])=>`<button class="chip" data-action="order-filter" data-status="${key}" aria-pressed="false">${label}</button>`).join('')}</div><div id="orders-list"></div></section>`;renderOrders('all');
    }
    if(state.view==='messages') {
      const data=await api('/conversations');if(generation!==pageGeneration)return;state.conversations=data.conversations;renderHeader();
      $('#main').innerHTML=`<section class="wrap page"><p class="eyebrow">НА СВЯЗИ</p><h1 class="page-title">Сообщения</h1><div class="messenger"><aside id="conversation-list" class="conversation-list" aria-label="Переписки"></aside><section id="chat" class="chat">${empty('Выберите переписку','Обсудите задачу, уточните детали и договоритесь о результате.')}</section></div></section>`;renderConversations();
      if(state.conversation&&state.conversations.some(c=>c.id===state.conversation))await openChat(state.conversation);
    }
  } catch(error){if(generation===pageGeneration){notify(error.message);$('#main').innerHTML=`<section class="wrap page">${empty('Не удалось загрузить страницу','Проверьте подключение и повторите попытку.','<button class="secondary" data-action="refresh">Повторить</button>')}</section>`;}}
}
function renderCatalogPage() {
  const f=state.filters;
  $('#main').innerHTML=`<section class="hero wrap"><div class="hero-copy"><p class="eyebrow">МЕНЬШЕ РУТИНЫ. БОЛЬШЕ ДЕЛА.</p><h1>У вас идея.<br>У нас — <span>решение.</span></h1><p class="intro">Найдите исполнителя, обсудите задачу<br>и следите за работой в одном месте.</p><a class="primary" href="#catalog-list">Найти услугу ↗</a></div><aside class="hero-feature"><div class="feature-top"><span class="pill">ВАШ СЛЕДУЮЩИЙ ШАГ</span><span>дело®</span></div><div class="feature-center"><p>Талант есть.<br>Пусть будут<br>и <em>клиенты.</em></p></div><div class="feature-bottom"><span>Создайте услугу и начните общение</span><button class="round-button" data-action="become-seller" aria-label="Предлагать услуги">↗</button></div></aside></section><section class="catalog wrap" id="catalog-list"><div class="section-heading"><div><p class="eyebrow">ОТ ИДЕИ К РЕЗУЛЬТАТУ</p><h2>Что сделаем?</h2></div><span class="catalog-note">Прямое общение с исполнителем</span></div><form id="filters" class="filter-panel"><label class="filter-search">Поиск<input type="search" name="q" maxlength="100" value="${escapeHTML(f.q)}" placeholder="Услуга, задача или имя продавца"></label><label>Категория<select name="category"><option value="all">Все категории</option>${Object.entries(state.meta.categories).map(([key,label])=>`<option value="${key}" ${f.category===key?'selected':''}>${label}</option>`).join('')}</select></label><label>Цена от, ₸<input type="number" min="0" max="10000000" step="1" name="min" value="${escapeHTML(f.min)}" placeholder="0"></label><label>Цена до, ₸<input type="number" min="0" max="10000000" step="1" name="max" value="${escapeHTML(f.max)}" placeholder="Любая"></label><label>Срок до<select name="days">${[['','Любой'],['3','3 дней'],['7','7 дней'],['14','14 дней'],['30','30 дней']].map(([key,label])=>`<option value="${key}" ${f.days===key?'selected':''}>${label}</option>`).join('')}</select></label><label>Сортировка<select name="sort">${[['new','Сначала новые'],['price-asc','Сначала дешевле'],['price-desc','Сначала дороже'],['days','Сначала быстрее']].map(([key,label])=>`<option value="${key}" ${f.sort===key?'selected':''}>${label}</option>`).join('')}</select></label><div class="filter-buttons"><button class="primary compact" type="submit">Найти</button><button class="secondary compact" type="button" data-action="reset-filters">Сбросить</button></div></form><p id="result-count" class="results-line" role="status">Загружаем…</p><div id="service-grid" class="service-grid"></div><div id="catalog-empty"></div><nav id="pagination" class="pagination" aria-label="Страницы каталога"></nav></section><section class="demo-note wrap"><span class="demo-label">ДЕЛО</span><p>Заказы и переписки сохраняются в аккаунте. Встроенная оплата пока не подключена.</p></section>`;
}
async function loadCatalog() {
  const generation=++catalogGeneration;
  try {
    const data=await api('/services?'+new URLSearchParams(state.filters));
    if(generation!==catalogGeneration||state.view!=='catalog'||!$('#service-grid'))return;
    state.services=data.services;
    $('#service-grid').innerHTML=data.services.map(s=>serviceCard(s)).join('');$('#result-count').textContent=`Найдено услуг: ${data.total}`;
    $('#catalog-empty').innerHTML=data.total?'':empty('Пока ничего не нашлось','Измените фильтры. Если вы продавец — опубликуйте первую услугу.');
    $('#pagination').innerHTML=data.pages>1?`<button class="secondary" data-action="page" data-page="${data.page-1}" ${data.page===1?'disabled':''}>← Назад</button><span>${data.page} / ${data.pages}</span><button class="secondary" data-action="page" data-page="${data.page+1}" ${data.page>=data.pages?'disabled':''}>Далее →</button>`:'';
  } catch(error){if(generation===catalogGeneration&&state.view==='catalog'){notify(error.message);$('#result-count').textContent='Не удалось обновить каталог';}}
}
function showAuth(mode='login',role='client') {
  const register=mode==='register';
  showModal(`<p class="eyebrow">ДОБРО ПОЖАЛОВАТЬ В ДЕЛО</p><h2 id="modal-title">${register?'Создать аккаунт':'С возвращением'}</h2><form id="auth-form" data-mode="${mode}">${register?`<label>Ваше имя<input name="name" minlength="2" maxlength="80" required autocomplete="name"></label><fieldset class="role-picker"><legend>Как вы будете пользоваться сервисом?</legend><label><input type="radio" name="role" value="client" ${role==='client'?'checked':''}>Клиент<small>Ищу и заказываю услуги</small></label><label><input type="radio" name="role" value="seller" ${role==='seller'?'checked':''}>Продавец<small>Предлагаю свои услуги</small></label></fieldset>`:''}<label>Email<input type="email" name="email" required maxlength="160" autocomplete="email"></label><label>Пароль<input type="password" name="password" minlength="10" maxlength="128" required autocomplete="${register?'new-password':'current-password'}"><small>От 10 до 128 символов</small></label>${register?'<label>Повторите пароль<input type="password" name="confirm" minlength="10" maxlength="128" required autocomplete="new-password"></label>':''}<button class="primary full" type="submit">${register?'Зарегистрироваться':'Войти'}</button></form><button class="text-button auth-switch" data-action="${register?'login':'register'}">${register?'Уже есть аккаунт? Войти':'Нет аккаунта? Зарегистрироваться'}</button>`);
}
async function showDetails(id) {
  const {service:s}=await api('/services/'+id);
  const canOrder=!state.user||state.user.role==='client';
  showModal(`<div class="detail-logo">${logoView(s.logo,true)}<p class="eyebrow">${escapeHTML(state.meta.categories[s.category])}</p></div><h2 id="modal-title">${escapeHTML(s.title)}</h2><p class="muted">Исполнитель: ${escapeHTML(s.seller_name)}</p><p class="preserve-text">${escapeHTML(s.description)}</p><div class="detail-facts"><strong class="price">${money(s.price)}</strong><span>Срок: ${s.days} дн.</span></div>${canOrder?`<div class="button-row"><button class="primary" data-action="order" data-id="${s.id}">Заказать услугу</button><button class="secondary" data-action="contact" data-id="${s.id}">Написать продавцу</button></div>`:`<p class="form-note">Заказы оформляют аккаунты клиентов.</p>`}<p class="form-note">Оплата через сайт не подключена. Согласуйте задачу с продавцом до начала работы.</p>`);
}
function requireClient(id) {
  if(!state.user){state.pendingService=id;showAuth();return false;}
  if(state.user.role!=='client')throw new Error('Оформлять заказы и начинать переписки могут клиенты.');
  return true;
}
async function orderForm(id) {
  if(!requireClient(id))return;
  const {service:s}=await api('/services/'+id);
  showModal(`<p class="eyebrow">НОВЫЙ ЗАКАЗ</p><h2 id="modal-title">Расскажите о задаче</h2><p class="muted">${escapeHTML(s.title)} · ${money(s.price)} · ${s.days} дн.</p><form id="order-form" data-id="${s.id}" data-key="${crypto.randomUUID()}"><label>Бриф<textarea name="brief" rows="6" minlength="20" maxlength="5000" required placeholder="Что нужно сделать, для кого и какой результат вы ожидаете?"></textarea></label><p class="form-note">Заказ появится у продавца. Бриф будет отправлен в вашу переписку. Деньги не списываются.</p><button class="primary full" type="submit">Создать заказ</button></form>`);
}
function editService(id) {
  const s=id?state.mine.find(s=>s.id===id):{title:'',description:'',price:'',days:'',category:'design',logo:'design'};
  if(!s)throw new Error('Услуга не найдена. Обновите страницу.');
  showModal(`<p class="eyebrow">КАБИНЕТ ПРОДАВЦА</p><h2 id="modal-title">${id?'Редактировать услугу':'Новая услуга'}</h2><form id="service-form" data-id="${id||''}"><label>Название<input name="title" value="${escapeHTML(s.title)}" required minlength="5" maxlength="100" placeholder="Например, разработаю лендинг"></label><label>Категория<select name="category">${Object.entries(state.meta.categories).map(([key,label])=>`<option value="${key}" ${s.category===key?'selected':''}>${label}</option>`).join('')}</select></label><label>Описание и состав работы<textarea name="description" rows="5" minlength="30" maxlength="5000" required placeholder="Что входит в услугу? Что клиент получит в результате?">${escapeHTML(s.description)}</textarea></label><div class="form-columns"><label>Цена, ₸<input type="number" name="price" value="${s.price}" min="100" max="10000000" step="1" required></label><label>Срок, дней<input type="number" name="days" value="${s.days}" min="1" max="365" step="1" required></label></div><fieldset class="logo-picker"><legend>Логотип услуги</legend><p class="form-note">Выберите один из логотипов сайта.</p><div class="logo-options">${state.meta.logos.map(l=>`<label><input type="radio" name="logo" value="${l.id}" ${s.logo===l.id?'checked':''} required>${logoView(l.id,true)}<span>${escapeHTML(l.name)}</span></label>`).join('')}</div></fieldset><p class="form-note">${id?'Изменения не меняют цену и сроки уже созданных заказов.':'Услуга появится в каталоге сразу после создания.'}</p><button class="primary full" type="submit">${id?'Сохранить изменения':'Опубликовать услугу'}</button></form>`);
}
function renderOrders(status) {
  const items=state.orders.filter(o=>status==='all'||o.status===status);
  $('#orders-list').innerHTML=items.length?items.map(o=>{
    const seller=state.user.role==='seller';
    const actions=seller?{new:[['in_progress','Принять заказ'],['cancelled','Отклонить']],in_progress:[['delivered','Передать на проверку']]}:{new:[['cancelled','Отменить заказ']],delivered:[['completed','Принять работу'],['in_progress','На доработку']]};
    return `<article class="order-card"><div class="order-top"><span class="status status-${o.status}">${statuses[o.status]}</span><span class="muted">№ ${o.id.slice(0,8)} · ${date(o.created_at)}</span></div><h3>${escapeHTML(o.title)}</h3><p class="muted">${seller?'Клиент':'Продавец'}: ${escapeHTML(seller?o.client_name:o.seller_name)} · ${money(o.price)} · ${o.days} дн.</p><details><summary>Бриф заказа</summary><p class="preserve-text">${escapeHTML(o.brief)}</p></details><div class="button-row"><button class="secondary" data-action="open-conversation" data-id="${o.conversation_id}">Переписка</button>${(actions[o.status]||[]).map(([key,label])=>`<button class="${key==='cancelled'?'secondary':'primary compact'}" data-action="order-status" data-id="${o.id}" data-status="${key}">${label}</button>`).join('')}</div></article>`;
  }).join(''):empty('Заказов пока нет',status==='all'?(state.user.role==='seller'?'Когда клиент закажет вашу услугу, заказ появится здесь.':'Найдите услугу в каталоге и отправьте продавцу бриф.'):'Нет заказов с выбранным статусом.');
}
function renderConversations() {
  if(!$('#conversation-list'))return;
  $('#conversation-list').innerHTML=state.conversations.length?state.conversations.map(c=>`<button class="conversation ${state.conversation===c.id?'selected':''}" data-action="chat" data-id="${c.id}" ${state.conversation===c.id?'aria-current="true"':''}><strong>${escapeHTML(state.user.role==='client'?c.seller_name:c.client_name)} ${c.unread?`<span class="unread">${c.unread}</span>`:''}</strong><span>${escapeHTML(c.title)}</span><small>${escapeHTML((c.last_message||'Начните разговор').slice(0,70))}</small></button>`).join(''):'<p class="muted no-conversations">Переписок пока нет. Клиент может начать разговор из карточки услуги.</p>';
}
async function openChat(id) {
  const c=state.conversations.find(c=>c.id===id);if(!c)return;
  state.conversation=id;state.lastMessage=0;const generation=++chatGeneration;renderConversations();
  $('#chat').innerHTML=`<header class="chat-header"><h2>${escapeHTML(state.user.role==='client'?c.seller_name:c.client_name)}</h2><p>${escapeHTML(c.title)}</p></header><div id="chat-messages" class="chat-messages" role="log" aria-label="История переписки" aria-live="polite"><p class="muted" id="chat-empty">Загружаем сообщения…</p></div><p id="chat-error" class="inline-error" role="alert" hidden></p><form id="message-form" class="message-form"><label class="sr-only" for="message-input">Сообщение</label><textarea id="message-input" name="body" rows="2" maxlength="5000" required placeholder="Напишите сообщение…"></textarea><button class="primary compact" type="submit">Отправить</button></form>`;
  await loadMessages(generation);
}
async function loadMessages(generation=chatGeneration) {
  const id=state.conversation;if(!id||!$('#chat-messages'))return;
  let more=true;
  while(more) {
    const data=await api(`/conversations/${id}/messages?after=${state.lastMessage}`);
    if(generation!==chatGeneration||state.view!=='messages'||state.conversation!==id)return;
    const container=$('#chat-messages');const nearBottom=container.scrollHeight-container.scrollTop-container.clientHeight<100||state.lastMessage===0;
    $('#chat-empty')?.remove();
    for(const m of data.messages) {
      if(m.id<=state.lastMessage)continue;
      const item=document.createElement('article');item.className='message '+(m.sender_id===state.user.id?'mine':'');
      const body=document.createElement('p');body.textContent=m.body;
      const time=document.createElement('time');time.dateTime=m.created_at;time.textContent=date(m.created_at);
      item.append(body,time);container.append(item);state.lastMessage=m.id;
    }
    if(!container.children.length){const p=document.createElement('p');p.id='chat-empty';p.className='muted';p.textContent='Начните разговор: уточните задачу или задайте вопрос.';container.append(p);}
    if(nearBottom)container.scrollTop=container.scrollHeight;
    more=data.hasMore;
  }
  if(state.lastMessage&&generation===chatGeneration&&!document.hidden) {
    await api(`/conversations/${id}/read`,'POST',{lastId:state.lastMessage});
    if(generation!==chatGeneration)return;
    const c=state.conversations.find(c=>c.id===id);if(c)c.unread=0;renderHeader();renderConversations();
  }
  if($('#chat-error'))$('#chat-error').hidden=true;
}
async function contactService(id) {
  if(!requireClient(id))return;
  const {conversation}=await api('/conversations','POST',{serviceId:id});state.conversation=conversation.id;$('#modal').close();navigate('messages');
}
document.addEventListener('click',async event=>{
  const button=event.target.closest('[data-action]');if(!button)return;
  const action=button.dataset.action,id=button.dataset.id;
  try {
    if(action==='login')showAuth();
    if(action==='register')showAuth('register');
    if(action==='become-seller'){if(!state.user)showAuth('register','seller');else if(state.user.role==='seller')navigate('services');else notify('Сейчас вы вошли как клиент. Для продажи услуг зарегистрируйте отдельный аккаунт продавца.');}
    if(action==='logout'){await api('/logout','POST',{});state.user=null;state.conversations=[];state.conversation=null;state.pendingService=null;$('#modal').close();navigate('catalog');}
    if(action==='details')await showDetails(id);
    if(action==='order')await orderForm(id);
    if(action==='contact')await contactService(id);
    if(action==='create')editService();
    if(action==='edit')editService(id);
    if(action==='visibility'){button.disabled=true;const s=state.mine.find(s=>s.id===id);await api('/services/'+id,'PATCH',{active:!s.active});await renderPage();}
    if(action==='refresh')await renderPage();
    if(action==='reset-filters'){state.filters={q:'',category:'all',min:'',max:'',days:'',sort:'new',page:1};renderCatalogPage();await loadCatalog();}
    if(action==='page'){state.filters.page=Number(button.dataset.page);await loadCatalog();$('#catalog-list').scrollIntoView({block:'start'});}
    if(action==='order-filter'){document.querySelectorAll('[data-action="order-filter"]').forEach(b=>{b.classList.toggle('active',b===button);b.setAttribute('aria-pressed',String(b===button));});renderOrders(button.dataset.status);}
    if(action==='order-status'){button.disabled=true;await api('/orders/'+id,'PATCH',{status:button.dataset.status});await renderPage();}
    if(action==='open-conversation'){state.conversation=id;navigate('messages');}
    if(action==='chat')await openChat(id);
  }catch(error){if($('#modal').open)modalError(error.message);else notify(error.message);}finally{if(button.isConnected)button.disabled=false;}
});
document.addEventListener('submit',async event=>{
  event.preventDefault();const form=event.target;if(!(form instanceof HTMLFormElement))return;
  const submit=form.querySelector('[type="submit"]');if(submit?.disabled)return;
  if(submit)submit.disabled=true;modalError('');notify('');const data=Object.fromEntries(new FormData(form));
  try {
    if(form.id==='filters'){state.filters={...data,page:1};await loadCatalog();}
    if(form.id==='auth-form') {
      if(form.dataset.mode==='register'&&data.password!==data.confirm)throw new Error('Пароли не совпадают.');
      const result=await api('/'+form.dataset.mode,'POST',data);state.user=result.user;state.conversations=[];state.conversation=null;$('#modal').close();
      const pending=state.pendingService;state.pendingService=null;
      if(pending){navigate('catalog');await showDetails(pending);}else navigate(state.user.role==='seller'?'services':'catalog');
    }
    if(form.id==='service-form') {
      data.price=Number(data.price);data.days=Number(data.days);
      await api('/services'+(form.dataset.id?'/'+form.dataset.id:''),form.dataset.id?'PATCH':'POST',data);
      $('#modal').close();await renderPage();notify('Услуга сохранена.');
    }
    if(form.id==='order-form') {
      await api('/orders','POST',{serviceId:form.dataset.id,brief:data.brief,requestKey:form.dataset.key});
      $('#modal').close();navigate('orders');
    }
    if(form.id==='message-form') {
      const id=state.conversation,generation=chatGeneration;
      await api(`/conversations/${id}/messages`,'POST',{body:data.body});
      if(generation===chatGeneration){form.reset();await loadMessages(generation);$('#message-input')?.focus();}
    }
  }catch(error){if(form.id==='message-form'&&$('#chat-error')){$('#chat-error').hidden=false;$('#chat-error').textContent=error.message;}else if($('#modal').open)modalError(error.message);else notify(error.message);}finally{if(submit?.isConnected)submit.disabled=false;}
});
window.addEventListener('hashchange',()=>{
  // An anchor within the catalog is not a separate application page.
  if(['#catalog-list','#main'].includes(location.hash))return;
  renderPage();
});
async function poll() {
  if(!state.user||document.hidden||pollBusy)return;pollBusy=true;
  const userId=state.user.id;
  try {
    const data=await api('/conversations');if(state.user?.id!==userId)return;
    state.conversations=data.conversations;renderHeader();
    if(state.view==='messages'){renderConversations();await loadMessages();}
  }catch(error){if(state.view==='messages'&&$('#chat-error')){$('#chat-error').textContent=error.message;$('#chat-error').hidden=false;}}finally{pollBusy=false;}
}
async function start() {
  try {const [meta,session]=await Promise.all([api('/meta'),api('/me')]);state.meta=meta;state.user=session.user;await renderPage();await poll();}
  catch(error){notify('Не удалось подключиться к серверу. Запустите node server.mjs и обновите страницу.');$('#main').innerHTML='<section class="wrap page"><h1 class="page-title">Сервер недоступен</h1><p>Откройте сайт через локальный сервер, а не как HTML-файл.</p></section>';}
}
start();setInterval(poll,5000);
