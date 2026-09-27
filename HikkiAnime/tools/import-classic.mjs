import {readFile,writeFile} from 'node:fs/promises';
const metadataUrl='https://commons.wikimedia.org/w/api.php?action=query&titles=File%3ANamakura_Gatana_with_music.webm&prop=videoinfo&viprop=url%7Cderivatives%7Cextmetadata&viurlwidth=640&format=json';
let metadata;
if(process.argv.includes('--cached'))metadata=JSON.parse((await readFile('.sites-runtime/wikimedia.json','utf8')).replace(/^\uFEFF/,''));
else{const r=await fetch(metadataUrl,{headers:{'User-Agent':'HikkiAnimePortfolio/1.0'},signal:AbortSignal.timeout(20000)});if(!r.ok)throw Error('Wikimedia metadata unavailable');metadata=await r.json();}
const info=Object.values(metadata.query.pages)[0].videoinfo[0];
const media=info.derivatives.find(x=>x.transcodekey==='480p.vp9.webm');
if(!media)throw Error('Expected video derivative not available');
const classic={series:{'6654':[{number:1,title:'Тупой меч — полный фильм',provider:'wikimedia',mediaUrl:media.src,mime:'video/webm',source:'https://commons.wikimedia.org/wiki/File:Namakura_Gatana_with_music.webm',thumbnail:info.thumburl,publisher:'Wikimedia Commons',originalTitle:'Namakura Gatana (1917)',language:'Немое кино · музыкальное сопровождение',credit:'Фильм: Jun’ichi Kōuchi (1917), public domain согласно Commons. Музыка: «Ishikari Lore» — Kevin MacLeod (incompetech.com), CC BY 3.0. Версия опубликована Di (they-them); HikkiAnime не изменяет видео.',licenseUrl:'https://creativecommons.org/licenses/by/3.0/',musicSource:'https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100192'}]}};
await writeFile('dist/data/classic.json',JSON.stringify(classic,null,2));
const registry=JSON.parse(await readFile('dist/data/episodes.json','utf8'));
Object.assign(registry.series,classic.series);
await writeFile('dist/data/episodes.json',JSON.stringify(registry,null,2));
console.log('Classic film source and attribution added.');
