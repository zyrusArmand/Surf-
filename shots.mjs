// Barrel screenshotter. Warps straight into a phase and captures the frame, at a phone's
// aspect so it matches what is actually being looked at on a device.
//
// Usage: node shots.mjs [tag]   -> writes shot_<tag>_<phase>.png in the repo root (gitignored)
//
// The renderer is swiftshader at ~3 fps, so every capture waits several real seconds for
// frames to land. It is slow and it is still far faster than riding out to a wave.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join } from 'node:path';

const ROOT = new URL('.', import.meta.url).pathname;
const TAG = process.argv[2] || 'now';
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.glb': 'model/gltf-binary' };

const server = createServer(async (req, res) => {
  const p = decodeURIComponent(req.url.split('?')[0]);
  try {
    const body = await readFile(join(ROOT, p === '/' ? 'index.html' : p));
    res.writeHead(200, { 'Content-Type': MIME[extname(p)] || 'application/octet-stream' });
    res.end(body);
  } catch { res.writeHead(404).end('x'); }
});
await new Promise(r => server.listen(8770, '127.0.0.1', r));

const browser = await chromium.launch({
  executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'],
});
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8770/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(4000);
await page.click('#startBtn');
await page.waitForTimeout(2500);

// Pixel statistics matter more than the picture: "flat" is a measurable claim. Regions are
// fractions of the frame so they survive a viewport change.
const stats = async (label, box) => await page.evaluate(async ([label, box]) => {
  const c = document.querySelector('canvas');
  const g = document.createElement('canvas');
  const W = Math.round(c.width * box.w), H = Math.round(c.height * box.h);
  g.width = W; g.height = H;
  g.getContext('2d').drawImage(c, Math.round(c.width * box.x), Math.round(c.height * box.y), W, H, 0, 0, W, H);
  const d = g.getContext('2d').getImageData(0, 0, W, H).data;
  let n = 0, sum = 0, sum2 = 0, hi = 0;
  const lum = [];
  for (let i = 0; i < d.length; i += 4) {
    const l = 0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2];
    lum.push(l); sum += l; sum2 += l * l; n++; if (l > 235) hi++;
  }
  const mean = sum / n, sd = Math.sqrt(Math.max(0, sum2 / n - mean * mean));
  return { label, mean: +mean.toFixed(1), sd: +sd.toFixed(1), nearWhite: +(hi / n * 100).toFixed(1) };
}, [label, box]);

for (const phase of process.env.PHASES ? process.env.PHASES.split(',') : ['SWELL', 'RIDE']) {
  await page.evaluate(p => { window.__surf.armSetWave(); window.__surf.warpSetWave(p); }, phase);
  await page.waitForTimeout(6000);
  await page.screenshot({ path: join(ROOT, `shot_${TAG}_${phase}.png`) });
  if (process.env.SPLIT) {          // same frame with the lip held down, to tell the two apart
    await page.evaluate(() => window.__surf.setCurl(false));
    await page.waitForTimeout(5000);
    await page.screenshot({ path: join(ROOT, `shot_${TAG}_${phase}_nocurl.png`) });
    await page.evaluate(() => window.__surf.setCurl(true));
    await page.waitForTimeout(3000);
  }
  console.log(phase, JSON.stringify(await stats('full', { x: 0, y: 0, w: 1, h: 1 })));
  console.log(phase, JSON.stringify(await stats('lower-left', { x: 0, y: 0.5, w: 0.5, h: 0.5 })));
  console.log(phase, JSON.stringify(await stats('centre', { x: 0.3, y: 0.3, w: 0.4, h: 0.4 })));
  // Is the rider actually on screen? The board is the bright shape; the pug is the dark one.
  const rider = await page.evaluate(() => window.__surf.riderOnScreen ? window.__surf.riderOnScreen() : null);
  if (rider) console.log(phase, 'rider', JSON.stringify(rider));
}
await browser.close();
server.close();
