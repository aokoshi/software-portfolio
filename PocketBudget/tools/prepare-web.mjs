import {readFileSync,writeFileSync,readdirSync,copyFileSync} from 'node:fs';
import path from 'node:path';import {createHash} from 'node:crypto';
const config=JSON.parse(readFileSync('app.json','utf8').replace(/^\uFEFF/,'')).expo;
for(const size of [192,512])copyFileSync('assets/icon-'+size+'.png','dist/icon-'+size+'.png');
const manifest={id:'/',name:config.name,short_name:config.name,start_url:'/',scope:'/',display:'standalone',background_color:'#F7F7FB',theme_color:config.web.themeColor,icons:[192,512].map(n=>({src:'icon-'+n+'.png',sizes:n+'x'+n,type:'image/png',purpose:'any'}))};
writeFileSync('dist/manifest.webmanifest',JSON.stringify(manifest,null,2));
let html=readFileSync('dist/index.html','utf8').replace(/<link rel="(?:manifest|apple-touch-icon)"[^>]*>/g,'').replace(/<meta name="(?:apple-mobile-web-app-capable|theme-color)"[^>]*>/g,'').replace(/<script>if\('serviceWorker'[\s\S]*?<\/script>/g,'');
html=html.replace('<html lang="en">','<html lang="ru">').replace('</head>',`<link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/icon-192.png"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="theme-color" content="${config.web.themeColor}"></head>`).replace('</body>',`<script>if('serviceWorker' in navigator && window.isSecureContext){window.addEventListener('load',()=>navigator.serviceWorker.register('/sw.js').catch(()=>{}))}</script></body>`);
writeFileSync('dist/index.html',html);
function walk(dir){return readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name).replaceAll('\\','/').replace(/^dist\//,'/')])}
const files=walk('dist').filter(p=>!p.endsWith('sw.js')&&!p.endsWith('metadata.json')&&!p.endsWith('.hbc'));
const bundle=files.find(f=>f.includes('/js/web/'))??Date.now().toString();
const cache=config.slug+'-'+createHash('sha256').update(bundle+html).digest('hex').slice(0,12);
writeFileSync('dist/sw.js',`const CACHE=${JSON.stringify(cache)},FILES=${JSON.stringify(files)};self.addEventListener('install',e=>e.waitUntil(caches.open(CACHE).then(c=>c.addAll(FILES))));self.addEventListener('activate',e=>e.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(k=>k.startsWith(${JSON.stringify(config.slug+'-')})&&k!==CACHE).map(k=>caches.delete(k))))));self.addEventListener('fetch',e=>{if(e.request.method!=='GET'||new URL(e.request.url).origin!==location.origin)return;if(e.request.mode==='navigate'){e.respondWith(fetch(e.request).catch(()=>caches.match('/index.html')));return;}e.respondWith(caches.match(e.request).then(hit=>hit||fetch(e.request)));});`);
console.log(config.name+': installable web build prepared; offline works on HTTPS or localhost.');
