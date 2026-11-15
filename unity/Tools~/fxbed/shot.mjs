import { createRequire } from 'node:module'; import { createServer } from 'node:http'; import { readFile } from 'node:fs/promises';
const { chromium } = createRequire(import.meta.url)('playwright');
const srv=createServer(async(q,s)=>{try{const f='.'+decodeURIComponent(q.url.split('?')[0]);const b=await readFile(f);s.writeHead(200,{'Content-Type':f.endsWith('.html')?'text/html':f.endsWith('.js')?'text/javascript':'application/octet-stream'});s.end(b);}catch{s.writeHead(404).end();}});
await new Promise(r=>srv.listen(0,'127.0.0.1',r));
const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const p=await b.newPage({viewport:{width:390,height:844}}); p.on('console',m=>console.log('console:',m.text())); p.on('pageerror',e=>console.log('err',e.message));
const a=process.argv.slice(2);
for(let i=0;i<a.length;i+=2){ await p.goto(`http://127.0.0.1:${srv.address().port}/trail.html?${a[i+1]}`); await p.waitForFunction(()=>window.done); await p.screenshot({path:a[i]}); }
await b.close(); srv.close();
