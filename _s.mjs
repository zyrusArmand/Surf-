import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
const ROOT = '/home/user/Surf-/';
const MIME = { '.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.css':'text/css','.png':'image/png','.json':'application/json','.glb':'model/gltf-binary' };
const server = createServer(async (req,res)=>{
  const q = decodeURIComponent(req.url.split('?')[0]);
  const f = join(ROOT, normalize(q==='/'?'/index.html':q).replace(/^(\.\.[/\\])+/,''));
  try { const b = await readFile(f); res.writeHead(200,{'Content-Type':MIME[extname(f)]||'application/octet-stream'}); res.end(b); }
  catch { res.writeHead(404).end('nf'); }
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const PORT = server.address().port;
const browser = await chromium.launch({ executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--disable-gpu-sandbox','--no-sandbox'] });
const page = await browser.newPage({ viewport:{ width:1024, height:640 }, deviceScaleFactor:1 });
page.on('pageerror',e=>console.log('PAGEERROR',e.message));
await page.goto(`http://127.0.0.1:${PORT}/index.html#debug`);
await page.waitForFunction(()=>typeof window.__surf==='object'&&window.__surf.state, null, {timeout:120000});
await page.waitForTimeout(6000);
const r = await page.evaluate(async () => {
  const out = [];
  for (let i = 0; i < 12 && !window.__surf.beachNow(); i++) {
    document.getElementById('menuBtn').click();
    await new Promise(r2 => setTimeout(r2, 150));
  }
  await new Promise(r2 => setTimeout(r2, 200));
  const snap = tag => { const f = window.__surf.menuFrame();
    out.push([tag, window.__surf.beachNow(), f ? (f.rider?('rider '+f.rider.h):'no rider') + ' pull '+f.pull : 'NULL']); };
  snap('start');
  window.__surf.menuLens(undefined, undefined, undefined);
  await new Promise(r2 => setTimeout(r2, 200)); snap('pull kept');
  window.__surf.menuLens(undefined, undefined, 0);
  await new Promise(r2 => setTimeout(r2, 200)); snap('pull 0');
  await new Promise(r2 => setTimeout(r2, 1000)); snap('pull 0 +1s');
  window.__surf.menuLens(undefined, undefined, 0.8);
  await new Promise(r2 => setTimeout(r2, 200)); snap('back to 0.8');
  return out;
});
for (const l of r) console.log(l.join(' | '));
await browser.close(); server.close();
