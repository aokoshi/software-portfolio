import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readdir, unlink, rmdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { once } from 'node:events';
import { createApplication } from '../server.mjs';

test('Marketplace: accounts, ownership, services, orders, conversations and persistence', async t => {
  const directory = await mkdtemp(join(tmpdir(),'delo-test-'));
  const databasePath = join(directory,'test.sqlite');
  let app = createApplication({ databasePath });
  let base;
  async function listen() { app.server.listen(0,'127.0.0.1'); await once(app.server,'listening'); base=`http://127.0.0.1:${app.server.address().port}`; }
  await listen();
  const seller={},client={},stranger={},otherSeller={};
  async function request(actor,path,method='GET',body,extraHeaders={}) {
    const res=await fetch(base+'/api'+path,{method,headers:{...(actor.cookie?{Cookie:actor.cookie}:{}),...(method!=='GET'?{'Content-Type':'application/json',Origin:base,'X-Delo-Request':'1'}:{}),...extraHeaders},body:body===undefined?undefined:JSON.stringify(body)});
    const cookie=res.headers.get('set-cookie');if(cookie)actor.cookie=cookie.split(';')[0];
    return {status:res.status,body:await res.json(),headers:res.headers};
  }
  async function register(actor,name,email,role) {
    const response=await request(actor,'/register','POST',{name,email,password:'Correct horse 2026!',role});
    assert.equal(response.status,201);actor.id=response.body.user.id;return response;
  }
  const serviceData={title:'Лендинг для вашего проекта',description:'Адаптивная страница с пятью блоками, исходным кодом и двумя раундами правок.',category:'development',logo:'code',price:65000,days:7};
  let serviceId,conversationId,orderId,clientCookie;
  try {
    await t.test('Registration, session cookie and password hashing',async()=>{
      const response=await register(seller,'Продавец','seller@example.test','seller');
      assert.match(response.headers.get('set-cookie'),/HttpOnly/);assert.match(response.headers.get('set-cookie'),/SameSite=Strict/);
      await register(client,'Клиент','client@example.test','client');await register(stranger,'Чужой клиент','stranger@example.test','client');await register(otherSeller,'Другой продавец','other@example.test','seller');
      assert.equal((await request(client,'/me')).body.user.role,'client');
      const row=app.db.prepare('SELECT password_hash FROM users WHERE id=?').get(seller.id);
      assert.ok(!row.password_hash.includes('Correct horse'));assert.match(row.password_hash,/^[0-9a-f]{32}:[0-9a-f]{128}$/);
      assert.equal((await request({},'/register','POST',{name:'Test',email:'seller@example.test',password:'Correct horse 2026!',role:'seller'})).status,409);
      assert.equal((await request({},'/register','POST',{name:'Test',email:'invalid@example.test',password:'short',role:'admin'})).status,400);
      assert.equal((await request({},'/login','POST',{email:'client@example.test',password:'Wrong password 2026'})).status,401);
      assert.equal((await request({},'/orders')).status,401);
    });
    await t.test('Seller-only creation and server logo allowlist',async()=>{
      assert.equal((await request(client,'/services','POST',serviceData)).status,403);
      assert.equal((await request(seller,'/services','POST',{...serviceData,logo:'https://outside.example/image.png'})).status,400);
      assert.equal((await request(seller,'/services','POST',{...serviceData,price:-1})).status,400);
      const response=await request(seller,'/services','POST',serviceData);assert.equal(response.status,201);serviceId=response.body.service.id;
      assert.equal((await request(seller,'/my-services')).body.services.length,1);
      assert.equal((await request(otherSeller,'/my-services')).body.services.length,0);
      assert.equal((await request(otherSeller,'/services/'+serviceId,'PATCH',{...serviceData,title:'Чужое изменение'})).status,403);
    });
    await t.test('Unicode search, price, category, duration, sorting and input errors',async()=>{
      await request(seller,'/services','POST',{...serviceData,title:'Дизайн логотипа',category:'design',logo:'design',price:12000,days:2});
      const search=await request({},'/services?'+new URLSearchParams({q:'ЛЕНДИНГ',category:'development',min:'60000',max:'70000',days:'7'}));
      assert.equal(search.body.total,1);assert.equal(search.body.services[0].id,serviceId);
      assert.equal((await request({},'/services?days=3')).body.total,1);
      assert.equal((await request({},'/services?sort=price-asc')).body.services[0].price,12000);
      assert.equal((await request({},'/services?sort=price-desc')).body.services[0].price,65000);
      assert.equal((await request({},'/services?min=90000&max=1')).status,400);
      assert.equal((await request({},'/services?category=__proto__')).status,400);
      assert.equal((await request({},'/services?q=%27%20OR%201%3D1--')).body.total,0);
    });
    await t.test('Conversations, messages, unread state and participant isolation',async()=>{
      const response=await request(client,'/conversations','POST',{serviceId});assert.equal(response.status,200);conversationId=response.body.conversation.id;
      assert.equal((await request(client,'/conversations','POST',{serviceId})).body.conversation.id,conversationId);
      assert.equal((await request(seller,'/conversations','POST',{serviceId})).status,403);
      assert.equal((await request(stranger,`/conversations/${conversationId}/messages`)).status,404);
      assert.equal((await request(stranger,`/conversations/${conversationId}/messages`,'POST',{body:'Чужой текст'})).status,404);
      assert.equal((await request(client,`/conversations/${conversationId}/messages`,'POST',{body:'   '})).status,400);
      const first=await request(client,`/conversations/${conversationId}/messages`,'POST',{body:'Здравствуйте! Нужен сайт для проекта.'});assert.equal(first.status,201);
      const messageId=first.body.message.id;
      assert.equal((await request(seller,'/conversations')).body.conversations[0].unread,1);
      assert.equal((await request(seller,`/conversations/${conversationId}/read`,'POST',{lastId:messageId})).status,200);
      assert.equal((await request(seller,'/conversations')).body.conversations[0].unread,0);
      const reply=await request(seller,`/conversations/${conversationId}/messages`,'POST',{body:'Здравствуйте, расскажите подробнее.'});assert.equal(reply.status,201);
      assert.equal((await request(client,`/conversations/${conversationId}/messages?after=${messageId}`)).body.messages.length,1);
      assert.equal((await request(stranger,'/conversations')).body.conversations.length,0);
      assert.equal((await request(otherSeller,'/conversations')).body.conversations.length,0);
    });
    await t.test('Order creation is idempotent and preserves a price snapshot',async()=>{
      const body={serviceId,brief:'Разработать адаптивный лендинг для моего учебного проекта.',requestKey:'request-key-order-001'};
      const first=await request(client,'/orders','POST',body);assert.equal(first.status,201);orderId=first.body.order.id;assert.equal(first.body.order.conversation_id,conversationId);
      const repeated=await request(client,'/orders','POST',body);assert.equal(repeated.status,200);assert.equal(repeated.body.order.id,orderId);
      assert.equal((await request(client,'/orders','POST',{...body,brief:'Другое описание задачи для того же ключа.'})).status,409);
      assert.equal((await request(seller,'/orders','POST',body)).status,403);
      await request(seller,'/services/'+serviceId,'PATCH',{...serviceData,price:99000,days:10});
      const clientOrders=(await request(client,'/orders')).body.orders;assert.equal(clientOrders.length,1);assert.equal(clientOrders[0].price,65000);assert.equal(clientOrders[0].days,7);
      assert.equal((await request(seller,'/orders')).body.orders.length,1);assert.equal((await request(stranger,'/orders')).body.orders.length,0);
      assert.equal((await request(stranger,'/orders/'+orderId,'PATCH',{status:'cancelled'})).status,404);
    });
    await t.test('Role-specific order lifecycle including revisions and terminal states',async()=>{
      const change=(actor,status)=>request(actor,'/orders/'+orderId,'PATCH',{status});
      assert.equal((await change(client,'in_progress')).status,409);
      assert.equal((await change(seller,'in_progress')).status,200);
      assert.equal((await change(client,'cancelled')).status,409);
      assert.equal((await change(seller,'delivered')).status,200);
      assert.equal((await change(client,'in_progress')).status,200);
      assert.equal((await change(seller,'delivered')).status,200);
      assert.equal((await change(client,'completed')).status,200);
      assert.equal((await change(seller,'in_progress')).status,409);
    });
    await t.test('Unpublishing preserves orders and conversations; republishing works',async()=>{
      assert.equal((await request(seller,'/services/'+serviceId,'PATCH',{active:false})).status,200);
      assert.equal((await request(client,'/services/'+serviceId)).status,404);
      assert.equal((await request(seller,'/my-services')).body.services.find(s=>s.id===serviceId).active,0);
      assert.equal((await request(client,'/orders','POST',{serviceId,brief:'Повторный заказ, который не должен создаться.',requestKey:'request-key-order-002'})).status,404);
      assert.equal((await request(client,`/conversations/${conversationId}/messages`)).status,200);
      assert.equal((await request(seller,'/services/'+serviceId,'PATCH',{active:true})).status,200);
      assert.equal((await request(client,'/services/'+serviceId)).status,200);
    });
    await t.test('Cross-origin requests, private files and message pagination',async()=>{
      assert.equal((await request(client,'/conversations','POST',{serviceId},{Origin:'https://evil.example'})).status,403);
      assert.equal((await request(client,'/conversations','POST',{serviceId},{'X-Delo-Request':''})).status,403);
      for(const path of ['/data/delo.sqlite','/lib/auth.mjs','/.openai/hosting.json','/server.mjs'])assert.equal((await fetch(base+path)).status,404);
      const insert=app.db.prepare('INSERT INTO messages(conversation_id,sender_id,body) VALUES (?,?,?)');
      for(let n=0;n<105;n++)insert.run(conversationId,seller.id,'Сообщение '+n);
      const first=(await request(client,`/conversations/${conversationId}/messages`)).body;assert.equal(first.messages.length,100);assert.equal(first.hasMore,true);
      const second=(await request(client,`/conversations/${conversationId}/messages?after=${first.messages.at(-1).id}`)).body;assert.ok(second.messages.length>0);assert.equal(second.hasMore,false);
    });
    await t.test('Data and session persist across restart; logout revokes the token',async()=>{
      clientCookie=client.cookie;
      await app.close();app=createApplication({databasePath});await listen();
      assert.equal((await request(client,'/me')).body.user.id,client.id);
      assert.equal((await request(client,'/orders')).body.orders[0].status,'completed');
      assert.equal((await request(seller,'/my-services')).body.services.length,2);
      assert.ok((await request(client,`/conversations/${conversationId}/messages`)).body.messages.length>0);
      assert.equal((await request(client,'/logout','POST',{})).status,200);
      assert.equal((await request({cookie:clientCookie},'/me')).body.user,null);
      const login=await request(client,'/login','POST',{email:'CLIENT@example.test',password:'Correct horse 2026!'});assert.equal(login.status,200);
      app.db.prepare('UPDATE sessions SET expires_at=0 WHERE user_id=?').run(client.id);
      assert.equal((await request(client,'/me')).body.user,null);
    });
  } finally {
    await app.close();
    for(const filename of await readdir(directory)) await unlink(join(directory,filename));
    await rmdir(directory);
  }
});
