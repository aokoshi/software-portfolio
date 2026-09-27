import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { openDatabase } from './lib/database.mjs';
import { createApi } from './lib/api.mjs';

const project = fileURLToPath(new URL('./', import.meta.url));
export function createApplication({ databasePath = resolve(project,'data/delo.sqlite'), secureCookies = false, publicOrigin = '' } = {}) {
  const db = openDatabase(databasePath);
  const api = createApi(db, { secureCookies, publicOrigin });
  const assets = { '/':'index.html', '/index.html':'index.html', '/styles.css':'styles.css', '/app.js':'app.js' };
  const types = { 'index.html':'text/html; charset=utf-8', 'styles.css':'text/css; charset=utf-8', 'app.js':'text/javascript; charset=utf-8' };
  const server = createServer(async (req,res) => {
    res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('Referrer-Policy','same-origin');
    res.setHeader('X-Frame-Options','DENY');
    res.setHeader('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'");
    try {
      const url = new URL(req.url,'http://localhost');
      if (url.pathname.startsWith('/api/')) return await api(req,res,url);
      const asset = assets[url.pathname];
      if (!asset || !['GET','HEAD'].includes(req.method)) { res.writeHead(404); res.end('Not found'); return; }
      const bytes = await readFile(resolve(project,'dist',asset));
      res.writeHead(200, {'Content-Type':types[asset], 'Cache-Control':'no-cache'});
      res.end(req.method === 'HEAD' ? undefined : bytes);
    } catch { res.writeHead(500); res.end('Server error'); }
  });
  server.requestTimeout = 15000;
  server.headersTimeout = 10000;
  return { server, db, async close() { await new Promise((resolve,reject) => server.close(error => error ? reject(error) : resolve())); db.close(); } };
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const app = createApplication({ databasePath: process.env.DELO_DB || resolve(project,'data/delo.sqlite'), secureCookies: process.env.NODE_ENV === 'production', publicOrigin: process.env.PUBLIC_ORIGIN || '' });
  const port = Number(process.env.PORT || 4173);
  app.server.listen(port,process.env.HOST || '127.0.0.1',() => console.log(`Local: http://127.0.0.1:${port}`));
  app.server.on('error',error => { console.error(error.message); app.db.close(); process.exitCode=1; });
  for (const signal of ['SIGINT','SIGTERM']) process.on(signal,() => app.close().then(() => process.exit(0)));
}
