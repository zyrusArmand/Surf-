// Does the lip read as the same water as the sea it grows out of?
//
// The seam probe compared the two sides of the crest from INSIDE the barrel, and passed,
// while from outside the lip was plainly a different substance from the ocean. So this
// compares the lip against open water instead: the curl is hidden and shown, the pixels that
// change are exactly the lip, and everything unchanged well away from it is sea.
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
await new Promise(r => server.listen(8776, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8776/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 3.2); });
await page.waitForTimeout(7000);
await page.screenshot({ path: join(ROOT, 'shot_w_on.png') });
await page.evaluate(() => window.__surf.setCurl(false));
await page.waitForTimeout(5000);
await page.screenshot({ path: join(ROOT, 'shot_w_off.png') });
await browser.close(); server.close();

const on = PNG.sync.read(readFileSync(join(ROOT, 'shot_w_on.png')));
const off = PNG.sync.read(readFileSync(join(ROOT, 'shot_w_off.png')));
const px = (p, i) => [p.data[i], p.data[i+1], p.data[i+2]];
const lip = [0,0,0], sea = [0,0,0]; let nl = 0, ns = 0;
const y0 = Math.round(on.height*0.09), y1 = Math.round(on.height*0.76);
for (let y = y0; y < y1; y++) for (let x = 0; x < on.width; x++) {
  const i = (on.width*y + x) << 2;
  const a = px(on, i), b = px(off, i);
  const diff = Math.max(...[0,1,2].map(k => Math.abs(a[k]-b[k])));
  if (diff > 24) { for (const k of [0,1,2]) lip[k] += a[k]; nl++; }             // the lip itself
  else if (diff < 4 && a[2] > a[0] + 18 && (a[0]+a[1]+a[2])/3 < 205) { for (const k of [0,1,2]) sea[k] += a[k]; ns++; }  // untouched water
}
const m = (a, n) => a.map(v => Math.round(v/Math.max(1,n)));
const L = m(lip, nl), S = m(sea, ns);
const hex = c => '#' + c.map(v => v.toString(16).padStart(2,'0')).join('');
const lum = c => 0.2126*c[0] + 0.7152*c[1] + 0.0722*c[2];
console.log(`lip  ${hex(L)}  ${L}   (${nl} px)`);
console.log(`sea  ${hex(S)}  ${S}   (${ns} px)`);
console.log(`difference  R ${L[0]-S[0]}  G ${L[1]-S[1]}  B ${L[2]-S[2]}   luminance ${(lum(L)-lum(S)).toFixed(1)}`);
