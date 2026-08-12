// Where can you see through the wave?
//
// The sky and everything in it is hidden and the background set to magenta, a colour the
// water cannot produce. Every magenta pixel is then a place where neither the sea nor the
// lip covered the frame — a hole. Reported as a map so the hole can be located, not just
// counted, because the last two fixes were aimed at the wrong place.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { PNG } from 'pngjs';
import { readFileSync } from 'node:fs';
const ROOT = new URL('.', import.meta.url).pathname;
const server = createServer(async (req, res) => {
  const p = decodeURIComponent(req.url.split('?')[0]);
  try { const b = await readFile(join(ROOT, p === '/' ? 'index.html' : p)); res.writeHead(200).end(b); }
  catch { res.writeHead(404).end('x'); }
});
await new Promise(r => server.listen(8781, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8781/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
// The gap is intermittent: it depends where the SWELL happens to be under the lip, and the
// swell scrolls with distance. One sample says nothing — a single reading has come back both
// 5461 and 0 at the same odometer. Sweep instead.
const DISTS = (process.env.DISTS || '900,1100,1250,1400,1550,1700,1850,2000').split(',').map(Number);
await page.evaluate(() => window.__surf.setSky(false));
const shots = [];
for (const d of DISTS) {
  await page.evaluate(dd => { window.__surf.setDist(dd); window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); }, d);
  await page.waitForTimeout(5200);
  const f = join(ROOT, `shot_hole_${d}.png`);
  await page.screenshot({ path: f });
  shots.push([d, f]);
}
await browser.close(); server.close();

let worstN = 0, worstD = 0, totalN = 0;
for (const [dist, file] of shots) {
const p = PNG.sync.read(readFileSync(file));
const isHole = (x, y) => { const i = (p.width * y + x) << 2;
  return p.data[i] > 200 && p.data[i+1] < 90 && p.data[i+2] > 200; };
const y0 = Math.round(p.height * 0.09), y1 = Math.round(p.height * 0.78);
let n = 0, tot = 0, minx = 1e9, maxx = -1, miny = 1e9, maxy = -1;
for (let y = y0; y < y1; y++) for (let x = 0; x < p.width; x++) {
  tot++;
  if (isHole(x, y)) { n++; minx=Math.min(minx,x); maxx=Math.max(maxx,x); miny=Math.min(miny,y); maxy=Math.max(maxy,y); }
}
console.log(`${String(dist).padStart(5)} m : ${String(n).padStart(6)} see-through px (${(n/tot*100).toFixed(2)}%)` +
            (n ? `   box x ${minx}..${maxx} y ${miny}..${maxy}` : ''));
totalN += n; if (n > worstN) { worstN = n; worstD = dist; }
}
console.log(`worst ${worstN} px at ${worstD} m; total across the sweep ${totalN}`);
