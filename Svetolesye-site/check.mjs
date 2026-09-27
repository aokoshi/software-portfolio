import fs from 'node:fs';
import path from 'node:path';
const html=fs.readFileSync('dist/index.html','utf8');
const refs=[...html.matchAll(/(?:src|href)="([^"#]+)"/g)].map(m=>m[1]).filter(p=>!/^https?:/.test(p));
for(const ref of refs)if(!fs.existsSync(path.join('dist',ref)))throw Error('Missing '+ref);
for(const name of ['moss','spark','drop','verdant','flare','tide','world','coast','summit'])if(!fs.existsSync('dist/assets/'+name+'.png'))throw Error(name);
console.log('All '+refs.length+' local references and dynamic assets verified.');
