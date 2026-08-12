// How far is the board from the LIP MESH when it is supposed to be riding it?
//
// contact.mjs only proved that off the lip he is on the water. It assumed the ring's radius
// equals the lip's inner surface and never checked — so "on the lip" could mean anywhere,
// including hanging in the middle of the tube. This measures the real distance to the mesh.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
const ROOT = new URL('.', import.meta.url).pathname;
const server = createServer(async (req, res) => {
  const p = decodeURIComponent(req.url.split('?')[0]);
  try { const b = await readFile(join(ROOT, p === '/' ? 'index.html' : p)); res.writeHead(200).end(b); }
  catch { res.writeHead(404).end('x'); }
});
await new Promise(r => server.listen(8783, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8783/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); });
await page.waitForTimeout(5000);
console.log(' angle  onLip   to lip mesh   to sea   nearest surface');
let worst = 0;
for (let i = 0; i < 12; i++) {
  const a = i * Math.PI * 2 / 12;
  const r = await page.evaluate(async a => {
    window.__surf.setTubeAngle(a);
    await new Promise(r => setTimeout(r, 2300));
    const s = window.__surf.state();
    const sl = window.__surf.curlSlice(s.pz);
    // rider into the shell's local frame, then nearest point on this slice's cross-section
    const rf = sl.riderF, ry = (s.py - sl.baseY) / (sl.scaleY || 1);
    let best = 1e9;
    for (const p of sl.pts) best = Math.min(best, Math.hypot(p.f - rf, p.y - ry));
    return { onLip: s.swOnLip, lip: best * Math.abs(sl.scaleY || 1),
             sea: s.py - window.__surf.sampleWave(s.px, s.pz).sea };
  }, a);
  const near = Math.min(r.lip, Math.abs(r.sea));
  if (near > worst) worst = near;
  console.log(`${a.toFixed(2).padStart(6)} ${String(r.onLip).padStart(6)} ${r.lip.toFixed(2).padStart(13)} ${r.sea.toFixed(2).padStart(8)} ${near.toFixed(2).padStart(16)}`);
}
console.log(`\nworst distance to ANY surface: ${worst.toFixed(2)} m`);
await browser.close(); server.close();
