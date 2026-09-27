import http from 'node:http';
import {createReadStream,statSync} from 'node:fs';
import {resolve,extname,sep} from 'node:path';
const root=resolve('dist');
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.png':'image/png','.ico':'image/x-icon','.wav':'audio/wav','.zip':'application/zip'};
const server=http.createServer((req,res)=>{
 try{const name=decodeURIComponent(new URL(req.url,'http://localhost').pathname);const file=resolve(root,'.'+(name==='/'?'/index.html':name));
 if(!file.startsWith(root+sep)){res.writeHead(403).end();return}
 const stat=statSync(file);if(!stat.isFile())throw Error();
 const headers={'Content-Type':types[extname(file)]||'application/octet-stream','Content-Length':stat.size};
 res.writeHead(200,headers);if(req.method==='HEAD')res.end();else createReadStream(file).pipe(res);
 }catch{res.writeHead(404).end('Not found')}
});server.listen(0,'127.0.0.1',()=>console.log('Local: http://127.0.0.1:'+server.address().port));
