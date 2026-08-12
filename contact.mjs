// How far is the board from anything solid, all the way round the ring?
//
// "Floating" is measurable: at each angle, take the distance from the board down to the sea
// beneath it, and say whether the lip covers that angle at all. Where the lip does not cover
// and the board is not on the water, it is in mid-air — which is the bug this measures.
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
await new Promise(r => server.listen(8782, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8782/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); });
await page.waitForTimeout(5000);
console.log(' angle   onLip   height over sea   verdict');
let floating = 0;
for (let i = 0; i < 12; i++) {
  const a = i * Math.PI * 2 / 12;
  const r = await page.evaluate(async a => {
    window.__surf.setTubeAngle(a);
    await new Promise(r => setTimeout(r, 2300));
    const s = window.__surf.state();
    return { s, sea: window.__surf.sampleWave(s.px, s.pz).sea };
  }, a);
  const over = r.s.py - r.sea;
  // On the lip he is meant to be clear of the sea — that is the overhang. Off it he must be
  // on the water, so anything more than a board's draft above it is floating.
  const bad = !r.s.swOnLip && over > 1.2;
  if (bad) floating++;
  console.log(`${(a).toFixed(2).padStart(6)}  ${String(r.s.swOnLip).padStart(6)}   ${over.toFixed(2).padStart(14)}   ${bad ? 'FLOATING' : 'ok'}`);
}
console.log(floating ? `\n${floating} of 12 angles floating in mid-air` : '\nin contact all the way round');
await browser.close(); server.close();
