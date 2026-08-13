// Does the rider stand on his board against the wall, or hang off the outside of it?
//
// Measured off the rig's real world quaternion, not the Euler angles meant to produce it:
// the rider's up must point at the tube's axis all the way round, the way a skateboarder's
// head points at the middle of a full pipe. 1 is upright on the wall; 0 is sideways to it.
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
await new Promise(r => server.listen(8785, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8785/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); });
await page.waitForTimeout(5000);
console.log(' angle  onLip   rider-up . inward   verdict');
let worst = 2;
for (let i = 0; i < 10; i++) {
  const a = 1.9 + i * 0.38;                 // sweep across the lip's arc, where he is on a wall
  const r = await page.evaluate(async a => {
    window.__surf.setTubeAngle(a);
    await new Promise(r => setTimeout(r, 2200));
    return window.__surf.riderUp();
  }, a);
  if (i === 2 || i === 6) await page.screenshot({ path: `${ROOT}shot_pipe${i}.png` });
  if (r.onLip && r.dot < worst) worst = r.dot;
  console.log(`${a.toFixed(2).padStart(6)} ${String(r.onLip).padStart(6)} ${r.dot.toFixed(3).padStart(18)}   ${!r.onLip ? '(open water)' : r.dot > 0.9 ? 'standing on it' : 'NOT UPRIGHT ON THE WALL'}`);
}
console.log(`\nworst alignment while on the lip: ${worst.toFixed(3)} (1.0 = standing on the board against the wall)`);
await browser.close(); server.close();
