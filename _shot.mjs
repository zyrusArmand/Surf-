import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
const ROOT='/home/user/Surf-/';
const MIME={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.css':'text/css','.glb':'model/gltf-binary','.png':'image/png','.json':'application/json'};
const server=createServer(async(req,res)=>{const p=decodeURIComponent(req.url.split('?')[0]);
 const f=join(ROOT,normalize(p==='/'?'/index.html':p).replace(/^(\.\.[/\\])+/,''));
 try{const b=await readFile(f);res.writeHead(200,{'Content-Type':MIME[extname(f)]||'application/octet-stream'});res.end(b);}catch{res.writeHead(404).end('nf');}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const PORT=server.address().port;
const browser=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
 args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--disable-gpu-sandbox','--no-sandbox']});
const page=await browser.newPage({viewport:{width:430,height:860},deviceScaleFactor:2});
page.on('pageerror',e=>console.log('PAGEERROR',e.message));
page.on('console',m=>{const t=m.text(); if(m.type()==='error'&&!/404/.test(t))console.log('CONSOLE',t.slice(0,240));});
await page.goto(`http://127.0.0.1:${PORT}/index.html#debug`);
await page.waitForFunction(()=>typeof window.__surf==='object'&&window.__surf.state,null,{timeout:60000});
await page.waitForTimeout(3500);
const OUT='/tmp/claude-0/-home-user-Surf-/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/scratchpad/';
console.log('fx', await page.evaluate(()=>window.__surf.fx?window.__surf.fx():'no hook'));
await page.screenshot({path:OUT+'menu.png'});
await page.evaluate(()=>{document.getElementById('startBtn').click();window.__surf.invuln(true);});
const shots=[0,0];
for(let i=0;i<14;i++){
  await page.evaluate(()=>window.__surf.tick(18));
  const st=await page.evaluate(()=>{const s=window.__surf.state();return {d:Math.round(s.dist),run:s.running,w:s.wipe,c:s.crateHeld||s.crateOn};});
  console.log(JSON.stringify(st));
  if(st.d>900&&!shots[0]){ await page.waitForTimeout(2600); shots[0]=1;
    console.log('shot A at',st.d); await page.screenshot({path:OUT+'ride1.png'}); }
  if(st.d>3000&&!shots[1]){ await page.waitForTimeout(2600); shots[1]=1;
    console.log('shot B at',st.d); await page.screenshot({path:OUT+'ride2.png'}); break; }
}
await browser.close(); server.close();
