import {readFile,writeFile} from 'node:fs/promises';
const episodes=[['rYDmL5VH-uk','Письмо'],['ZOSoQxSHKKE','Тренировка'],['P0cXpOnfX6I','Друг'],['gfsT2uqJPOc','Вечерние волны'],['liZuy6ufYMs','Помощница'],['CIxj6cUMm7M','Лунный свет'],['3-144RjSRWI','Небо']];
const result=[];
for(const [id,title] of episodes){
 const source=`https://www.youtube.com/watch?v=${id}`;
 const r=await fetch('https://www.youtube.com/oembed?format=json&url='+encodeURIComponent(source),{signal:AbortSignal.timeout(15000)});
 if(!r.ok)throw Error('Cannot verify episode '+id+': '+r.status);
 const d=await r.json();
 if(!/official.*pok[eé]mon|pok[eé]mon.*official/i.test(d.author_name))throw Error('Unexpected publisher: '+d.author_name);
 result.push({number:result.length+1,title,videoId:id,source,thumbnail:d.thumbnail_url,publisher:d.author_name,publisherUrl:d.author_url,originalTitle:d.title,language:'Английский'});
}
const classic=await readFile('dist/data/classic.json','utf8').then(JSON.parse).catch(()=>({series:{}}));
await writeFile('dist/data/episodes.json',JSON.stringify({checkedAt:new Date().toISOString().slice(0,10),series:{'40861':result,...classic.series}},null,2));
console.log(JSON.stringify(result.map(({number,originalTitle,publisher})=>({number,originalTitle,publisher})),null,2));
