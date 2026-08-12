// Measure the colour on each side of the join between the lip and the water.
//
// The crest is where the two surfaces meet, so it is projected to screen coordinates from
// the wave's own numbers and the capture is sampled a few pixels either side of it. Judging
// this by eye is exactly how the last two "fixes" in this file went wrong.
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
await new Promise(r => server.listen(8775, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8775/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#startBtn');
await page.waitForTimeout(2500);
await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); });
await page.waitForTimeout(6000);
const marks = await page.evaluate(() => {
  const s = window.__surf.state(), out = [];
  for (const z of [-26,-23,-20,-17,-14,-11,-8,-5,-2,1]) out.push({ z, p: window.__surf.project(s.swX, s.swA, z) });
  return out.filter(m => m.p.front);
});
const file = join(ROOT, 'shot_seam.png');
await page.screenshot({ path: file });
await browser.close(); server.close();

const png = PNG.sync.read(readFileSync(file));
const at = (x, y) => { const i = (png.width * Math.round(y) + Math.round(x)) << 2; return [png.data[i], png.data[i+1], png.data[i+2]]; };
const band = (x, y, dy) => {           // average a short vertical run, dy px away from the seam
  let r = 0, g = 0, b = 0, n = 0;
  for (let k = 3; k < 12; k++) { const [R, G, B] = at(x, y + Math.sign(dy) * k); r += R; g += G; b += B; n++; }
  return [r/n, g/n, b/n].map(v => Math.round(v));
};
const hex = c => '#' + c.map(v => v.toString(16).padStart(2, '0')).join('');
console.log('crest z    above (lip)        below (water)       dR,dG,dB');
let worst = 0; const deltas = [];
for (const m of marks) {
  const x = m.p.x * png.width, y = m.p.y * png.height;
  if (x < 2 || x > png.width - 2 || y < 14 || y > png.height - 14) continue;
  const up = band(x, y, -1), dn = band(x, y, 1);
  // Looking down the tube the crest line eventually crosses the mouth, and below it is then
  // sky rather than water. A near-neutral bright sample is that, not a colour mismatch.
  const neutral = Math.max(...dn) - Math.min(...dn) < 30 && (dn[0]+dn[1]+dn[2])/3 > 185;
  if (neutral) { console.log(`${String(m.z).padStart(4)}      (sky through the mouth, skipped)`); continue; }
  const d = [0,1,2].map(i => up[i] - dn[i]);
  const mag = Math.max(...d.map(Math.abs));
  worst = Math.max(worst, mag); deltas.push(d);
  console.log(`${String(m.z).padStart(4)}      ${hex(up)} ${String(up).padEnd(16)} ${hex(dn)} ${String(dn).padEnd(16)} ${d.join(',')}`);
}
// A single worst sample is a noisy statistic: the foam texture alone swings a reading by
// twenty either way. What matters is whether there is a systematic step across the join —
// the mean signed difference — with the spread reported next to it as the noise floor.
const mean = a => a.reduce((x, y) => x + y, 0) / (a.length || 1);
for (const [i, name] of [[0,'R'],[1,'G'],[2,'B']]) {
  const col = deltas.map(d => d[i]);
  console.log(`  ${name}: bias ${mean(col).toFixed(1).padStart(6)}   mean|d| ${mean(col.map(Math.abs)).toFixed(1).padStart(5)}   over ${col.length} samples`);
}
console.log(`worst single sample (noise-dominated): ${worst}`);
// Matching the two sides is only half of it: a channel sitting at its ceiling matches
// everything and shows nothing. This is the guard against fixing the seam by blowing out.
{
  let clip = 0, n = 0;
  const y0 = Math.round(png.height*0.10), y1 = Math.round(png.height*0.72);
  for (let y = y0; y < y1; y++) for (let x = 0; x < png.width; x += 2) {
    const [R,G,B] = at(x,y); n++;
    if (G >= 254 || B >= 254) clip++;
  }
  console.log(`green/blue at the ceiling over the wave: ${(clip/n*100).toFixed(1)}%`);
}
