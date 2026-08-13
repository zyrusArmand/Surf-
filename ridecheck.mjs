// Sample a REAL ride, not a warped snapshot.
//
// Every check so far has set the tube angle by hand, waited for it to settle, and measured.
// That keeps passing while actual play does not, which means the failure is in a state the
// warp never produces. So: press the button, let the wave run, and sample continuously —
// is the ring even engaged, and how far from any surface does the board actually get?
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
await new Promise(r => server.listen(8784, '127.0.0.1', r));
const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 440, height: 950 } });
page.on('pageerror', e => console.log('PAGEERROR:', e.message));
await page.goto('http://127.0.0.1:8784/index.html#debug', { waitUntil: 'load' });
await page.waitForTimeout(3500);
await page.click('#waveBtn');                       // the real sequence, from the real button

// steer continuously, the way a player holding a turn would
await page.evaluate(() => {
  window.__surfSteer = setInterval(() => {
    const e = new KeyboardEvent('keydown', { code: 'ArrowLeft', bubbles: true });
    document.dispatchEvent(e); window.dispatchEvent(e);
  }, 120);
});

const rows = [];
const t0 = Date.now();
while (Date.now() - t0 < 260000) {
  const r = await page.evaluate(() => {
    const s = window.__surf.state();
    if (s.swPh !== 3) return { ph: s.swPh };
    const sl = window.__surf.curlSlice(s.pz);
    const rf = sl.riderF, ry = (s.py - sl.baseY) / (sl.scaleY || 1);
    let lip = 1e9;
    for (const p of sl.pts) lip = Math.min(lip, Math.hypot(p.f - rf, p.y - ry));
    lip *= Math.abs(sl.scaleY || 1);
    const over = s.py - window.__surf.sampleWave(s.px, s.pz).sea;
    return { ph: s.swPh, tube: s.swTubeRide, onLip: s.swOnLip, air: s.airborne,
             ang: s.swAng, py: s.py, lip, over, toSurface: Math.min(lip, Math.abs(over)),
             dbg: window.__surf.ringDebug() };
  });
  if (r.ph === 3) rows.push(r);
  if (rows.length > 90) break;
  await page.waitForTimeout(700);
}
await browser.close(); server.close();

if (!rows.length) { console.log('never reached RIDE'); process.exit(0); }
const off = rows.filter(r => !r.tube);
const bad = rows.filter(r => r.toSurface > 1.0);
console.log(`samples in RIDE: ${rows.length}`);
console.log(`ring NOT engaged: ${off.length} (${(off.length/rows.length*100).toFixed(0)}%)`);
console.log(`airborne:         ${rows.filter(r=>r.air).length}`);
console.log(`more than 1 m from any surface: ${bad.length} (${(bad.length/rows.length*100).toFixed(0)}%)`);
const worst = rows.reduce((a,b) => b.toSurface > a.toSurface ? b : a);
console.log(`worst: ${worst.toSurface.toFixed(2)} m  (tube=${worst.tube} onLip=${worst.onLip} air=${worst.air} ang=${worst.ang?.toFixed(2)} py=${worst.py.toFixed(2)})`);
console.log('\nworst offenders (ring radius vs where the mesh actually is):');
for (const r of rows.slice().sort((a,b)=>b.toSurface-a.toSurface).slice(0,10)) {
  const d=r.dbg;
  console.log(`  toSurface=${r.toSurface.toFixed(2).padStart(5)} onLip=${String(d.onLip).padStart(5)} ang=${d.ang.toFixed(2).padStart(6)} pz=${d.pz.toFixed(2).padStart(5)}  ringR=${d.ringR.toFixed(2).padStart(5)} rRider=${d.rRider.toFixed(2).padStart(5)} rMesh=${d.rMesh.toFixed(2).padStart(5)} toMesh=${d.toMesh.toFixed(2)}`);
}
