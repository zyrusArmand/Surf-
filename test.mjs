// Smoke test: load the game, start a run, confirm nothing throws and the run advances.
//
// Self-contained on purpose — it serves the repo root itself rather than assuming a
// python http.server is already up on :8767, because the one thing worse than a failing
// test is a test that fails because nobody started the server.
//
// Chromium is the pre-installed build at PLAYWRIGHT_BROWSERS_PATH; the bundled version
// playwright expects is NOT downloaded here, so the executable is passed explicitly.
// WebGL runs on swiftshader, which is ~3 fps, and the game clamps dt — so simulated time
// runs at roughly a tenth of wall clock. Budget accordingly; don't tighten these waits.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';

const ROOT = new URL('.', import.meta.url).pathname;
const PORT = 8767;
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
               '.css': 'text/css', '.glb': 'model/gltf-binary', '.png': 'image/png',
               '.json': 'application/json' };

const server = createServer(async (req, res) => {
  const path = decodeURIComponent(req.url.split('?')[0]);
  const file = join(ROOT, normalize(path === '/' ? '/index.html' : path).replace(/^(\.\.[/\\])+/, ''));
  try {
    const body = await readFile(file);
    res.writeHead(200, { 'Content-Type': MIME[extname(file)] || 'application/octet-stream' });
    res.end(body);
  } catch {
    res.writeHead(404).end('not found');
  }
});
await new Promise(r => server.listen(PORT, '127.0.0.1', r));

const browser = await chromium.launch({
  executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
         '--disable-gpu-sandbox', '--no-sandbox'],
});
const page = await browser.newPage({ viewport: { width: 1024, height: 640 } });

// models/*.glb are OPTIONAL overrides — drop one in and it replaces the built-in shape,
// leave it out and the procedural version stands (models/README.md). Eight of the nine
// are absent by design, so their 404s are the documented path, not a fault. Anything
// else that 404s is a genuinely missing asset.
const EXPECTED_404 = /\/models\/[a-z]+\.glb$/;

// Shader compile failures do NOT throw. The program fails, three.js writes it to
// console.error, and the mesh simply stops being drawn — state still says it is visible,
// the geometry is still right, and there is nothing on screen. That cost a long detour
// chasing a camera bug that was a missing uniform declaration, so it gets its own check.
const shaderErrors = [];
const errors = [];
page.on('pageerror', e => errors.push(`pageerror: ${e.message}`));
page.on('console', m => { if (/shader error|not compiled|undeclared identifier/i.test(m.text()))
  shaderErrors.push(m.text().split('\n').slice(0, 3).join(' | ').slice(0, 220)); });
page.on('console', m => {
  // The bare "Failed to load resource" line carries no URL, so it cannot be told apart
  // from a real failure here; the response handler below is what judges those.
  if (m.type() === 'error' && !/Failed to load resource/.test(m.text())) errors.push(`console: ${m.text()}`);
});
page.on('requestfailed', r => errors.push(`request failed: ${r.url()} (${r.failure()?.errorText})`));
// A 404 is not a "failed request" as far as playwright is concerned — it is a perfectly
// successful response that happens to be nothing. Catch those separately or a missing
// asset shows up only as an anonymous console line with no URL attached.
page.on('response', r => {
  if (r.status() >= 400 && !EXPECTED_404.test(new URL(r.url()).pathname)) errors.push(`HTTP ${r.status()}: ${r.url()}`);
});

let failed = 0;
const check = (ok, label, detail = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}${detail ? ` — ${detail}` : ''}`);
  if (!ok) failed++;
};

await page.goto(`http://127.0.0.1:${PORT}/index.html#debug`, { waitUntil: 'load' });
await page.waitForFunction(() => typeof window.VERSION === 'string' || document.querySelector('#startBtn'), null, { timeout: 30000 });

const version = await page.evaluate(() => {
  const el = [...document.querySelectorAll('*')].find(e => /^v\d+\.\d+\.\d+$/.test(e.textContent.trim()));
  return el ? el.textContent.trim() : null;
});
console.log(`version on page: ${version ?? '(not found)'}`);

// The renderer is the thing most likely to be silently dead: a WebGL context that never
// came up still leaves the DOM looking perfectly healthy.
const canvas = await page.evaluate(() => {
  const c = document.querySelector('canvas');
  return c ? { w: c.width, h: c.height } : null;
});
check(!!canvas && canvas.w > 0 && canvas.h > 0, 'canvas present and sized', canvas ? `${canvas.w}x${canvas.h}` : 'no canvas');

// ---------- the menus ----------
// Cheap, but this is the part a player meets first, and a shop that will not open is as
// broken as a wave that will not break.
for (const [openSel, panelSel, closeSel, label] of [
  ['#shopBtn', '#shop', '#shopClose', 'beach shop'],
  ['#recordsBtn', '#stats', '#stClose', 'stats'],
]) {
  await page.click(openSel);
  await page.waitForTimeout(400);
  const opened = await page.$eval(panelSel, e => !e.classList.contains('hidden'));
  await page.click(closeSel);
  await page.waitForTimeout(400);
  const closed = await page.$eval(panelSel, e => e.classList.contains('hidden'));
  check(opened && closed, `${label} opens and closes`, `opened=${opened} closed=${closed}`);
}

await page.click('#startBtn');
await page.waitForTimeout(3000);
const d0 = await page.textContent('#hDist');
await page.waitForTimeout(12000);
const d1 = await page.textContent('#hDist');
const m = s => parseFloat(String(s).replace(/[^\d.]/g, '')) || 0;
check(m(d1) > m(d0), 'HUD distance advancing', `${d0} -> ${d1}`);

const speed = await page.textContent('#hSpeed');
check(m(speed) > 0, 'speed non-zero', speed);

// Frames actually being drawn, not just state being ticked.
const frames = await page.evaluate(() => new Promise(res => {
  let n = 0; const t0 = performance.now();
  const tick = () => { n++; performance.now() - t0 < 3000 ? requestAnimationFrame(tick) : res(n); };
  requestAnimationFrame(tick);
}));
check(frames > 3, 'frames rendering', `${frames} in 3s`);

// ---------- the set wave ----------
// It fires past 600 m and runs ~25 s of sim time, which is minutes of wall clock here, so
// the debug hook arms it and warps into the phase under test.
const hasHook = await page.evaluate(() => typeof window.__surf === 'object');
check(hasHook, 'debug hook present under #debug');

if (hasHook) {
  const ph = await page.evaluate(async () => {
    window.__surf.armSetWave();
    window.__surf.warpSetWave('RIDE');
    await new Promise(r => setTimeout(r, 1200));
    return window.__surf.state();
  });
  check(ph.swPh === 3, 'set wave reaches RIDE', `swPh=${ph.swPh}`);
  check(ph.swA > 6, 'wave stands to full height in the barrel', `swA=${ph.swA?.toFixed(2)}`);
  check(ph.curlVisible === true, 'curl mesh visible during the ride');
  check(Number.isFinite(ph.roofY) && Number.isFinite(ph.tubeC) && Number.isFinite(ph.px),
        'wave geometry and rider position finite',
        `roofY=${ph.roofY} tubeC=${ph.tubeC} px=${ph.px}`);

  // Riding in the pocket has to pay, and the wave has to end rather than hang.
  const rode = await page.evaluate(async () => {
    const s0 = window.__surf.state();
    await new Promise(r => setTimeout(r, 4000));
    return { s0, s1: window.__surf.state() };
  });
  check(rode.s1.score > rode.s0.score || rode.s1.wipe, 'ride scores (or ends in a wipeout)',
        `${Math.round(rode.s0.score)} -> ${Math.round(rode.s1.score)} wipe=${rode.s1.wipe}`);

  const out = await page.evaluate(async () => {
    window.__surf.warpSetWave('EXIT');
    await new Promise(r => setTimeout(r, 6000));
    return window.__surf.state();
  });
  check(out.swPh === -1 || out.swPh === 4, 'wave closes out and returns to idle', `swPh=${out.swPh}`);

  // The height field is the wave the board actually rides; the shader mirrors it. If this
  // ever returns NaN the physics and the visuals are both wrong and nothing looks amiss.
  const samples = await page.evaluate(() => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    const s = window.__surf.state(), out = [];
    for (let dz = -120; dz <= 30; dz += 30)
      for (let dx = -20; dx <= 20; dx += 10) out.push(window.__surf.sampleWave(s.swX + dx, dz));
    return out;
  });
  check(samples.every(s => Number.isFinite(s.set) && Number.isFinite(s.sea)),
        'wave height finite across the face', `${samples.length} samples`);
}

// ---------- the wave button ----------
// It has to summon a wave from a standing start, without the 600 m the wave normally
// needs, and it has to be a one-off: the run goes back to its own rhythm afterwards.
{
  await page.evaluate(() => location.reload());
  await page.waitForSelector('#waveBtn', { timeout: 30000 });
  const beside = await page.evaluate(() => {
    const a = document.querySelector('#startBtn').getBoundingClientRect();
    const b = document.querySelector('#waveBtn').getBoundingClientRect();
    return { sameRow: Math.abs((a.top + a.height / 2) - (b.top + b.height / 2)) < 4,
             rightOf: b.left >= a.right - 1, square: Math.abs(b.width - b.height) < 3, w: b.width };
  });
  check(beside.sameRow && beside.rightOf, 'wave button sits beside Play',
        `sameRow=${beside.sameRow} rightOf=${beside.rightOf}`);
  check(beside.square && beside.w > 20, 'wave button is a square icon', `${Math.round(beside.w)}px`);

  await page.click('#waveBtn');
  // Two seconds of SIM time, and headless runs the sim at ~10% of wall clock.
  const fired = await page.evaluate(async () => {
    const t0 = Date.now(); let seen = null, dist = null;
    while (Date.now() - t0 < 60000) {
      const s = window.__surf.state();
      if (s.swPh !== -1) { seen = s.swPh; dist = s.dist; break; }
      await new Promise(r => setTimeout(r, 250));
    }
    return { seen, dist };
  });
  check(fired.seen !== null, 'wave button fires a wave from a standing start',
        `phase=${fired.seen} at ${Math.round(fired.dist)} m`);
  check(fired.dist !== null && fired.dist < 600, 'and does it well short of the usual 600 m',
        `${Math.round(fired.dist)} m`);
  const oneShot = await page.evaluate(() => window.__surf.state().swPh !== -1);
  check(oneShot, 'wave still running after the flag is cleared');
}

// ---------- the peel ----------
// The wave must present a different section along its length: unbroken wall ahead of the
// break, a barrel at it, collapse behind. Before this it showed the same section everywhere
// at once, which is a tunnel rather than a wave that is breaking.
{
  const peel = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    await new Promise(r => setTimeout(r, 1200));
    const s = window.__surf.state(), out = {};
    for (const z of [-120, -30, 0, 30]) {
      const w = window.__surf.sampleWave(s.swX - 2, z);
      const sl = window.__surf.curlSlice(z);
      out[z] = { setH: w.set, peelB: w.peelB, reach: Math.max(...sl.pts.map(p => p.f)) };
    }
    // At the CREST line, at the rider's own z. Sampling at the rider's x instead reads a
    // couple of metres down the face — a small number by design, not a collapsed wave.
    return { out, swA: s.swA, riderH: window.__surf.sampleWave(s.swX, s.pz).set };
  });
  const at = z => peel.out[z];
  check(at(-120).reach < 2 && at(0).reach > 10,
        'lip is unbroken wall down the line and thrown at the break',
        `reach ${at(-120).reach.toFixed(2)} at z=-120 vs ${at(0).reach.toFixed(2)} at z=0`);
  check(at(-30).reach > at(-120).reach && at(-30).reach < at(0).reach,
        'and pitches out progressively between the two', `${at(-30).reach.toFixed(2)} at z=-30`);
  check(at(30).setH < at(0).setH * 0.65, 'the section collapses behind the break',
        `${at(0).setH.toFixed(2)} -> ${at(30).setH.toFixed(2)}`);
  check(at(-120).setH > at(0).setH * 0.9, 'but stands at full height ahead of it — an unbroken wave is not a small one',
        `${at(-120).setH.toFixed(2)} vs ${at(0).setH.toFixed(2)}`);
  // The trap this cost a session to find: waveH's z is the SWELL's scrolling frame
  // (dist*WAVE_DRIFT + worldZ), not world z. Feed that straight to the peel and the rider
  // is permanently past the break — the wave looks right and the water under the board
  // collapses. The rider sits at z~0, so the water beneath him must be at full height.
  check(peel.riderH > peel.swA * 0.9,
        'the water under the rider is the wave that is drawn, not a collapsed one',
        `${peel.riderH.toFixed(2)} against swA ${peel.swA.toFixed(2)}`);
}

// ---------- riding the ring ----------
// Inside the barrel the lane is the tube's circumference, not a line across the water. You
// can go all the way round — up the face, across the roof upside down, down the curtain —
// and nothing in there can throw you off.
{
  const ring = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    await new Promise(r => setTimeout(r, 1500));
    const out = { start: window.__surf.state(), at: {} };
    for (const [name, a] of [['pocket', 0], ['face', Math.PI / 2], ['roof', Math.PI], ['curtain', Math.PI * 1.5]]) {
      window.__surf.setTubeAngle(a);
      // He is eased onto the ring rather than snapped to it, and this browser runs the sim
      // at about a tenth of wall clock, so settling takes seconds of real time here.
      await new Promise(r => setTimeout(r, 2600));
      out.at[name] = window.__surf.state();
    }
    return out;
  });
  const A = ring.at;
  check(A.pocket.swTubeRide === true, 'the ring is live inside the barrel');
  check(A.roof.py > A.pocket.py + 4, 'riding round takes you up to the roof',
        `${A.pocket.py.toFixed(2)} in the pocket -> ${A.roof.py.toFixed(2)} at the top`);
  check(Math.abs(A.roof.py - A.roof.roofY) < 1.2, 'and the top of the ring IS the underside of the lip',
        `${A.roof.py.toFixed(2)} against a roof at ${A.roof.roofY.toFixed(2)}`);
  check(Math.abs(A.face.px - A.curtain.px) > 4, 'the ring carries you across the tube, not just up it',
        `${A.face.px.toFixed(2)} vs ${A.curtain.px.toFixed(2)}`);
  check(Object.values(A).every(s => !s.wipe && s.swPh === 3 && !s.airborne),
        'nothing round the ring wipes you out or counts as air');

  // Nowhere on the ring may he be in mid-air. The lip is only an arc — 232 degrees at the
  // break, as little as 102 down the line — and the rest of the circle is the barrel's open
  // mouth. Treating the ring as a full circle hung him in the middle of the tube touching
  // nothing; off the lip he has to be on the water.
  const contact = await page.evaluate(async () => {
    const out = [];
    for (let i = 0; i < 8; i++) {
      window.__surf.setTubeAngle(i * Math.PI * 2 / 8);
      // He is EASED onto the ring, and this browser runs the sim at about a tenth of wall
      // clock, so settling takes a couple of real seconds. At 1400 ms the reading was 0.95 m
      // — mid-ease, not a gap. Measure where he ends up, not where he is on the way.
      await new Promise(r => setTimeout(r, 2400));
      const s = window.__surf.state();
      const sl = window.__surf.curlSlice(s.pz);
      const rf = sl.riderF, ry = (s.py - sl.baseY) / (sl.scaleY || 1);
      let lip = 1e9;
      for (const p of sl.pts) lip = Math.min(lip, Math.hypot(p.f - rf, p.y - ry));
      lip *= Math.abs(sl.scaleY || 1);
      const over = s.py - window.__surf.sampleWave(s.px, s.pz).sea;
      out.push({ onLip: s.swOnLip, over, toSurface: Math.min(lip, Math.abs(over)) });
    }
    return out;
  });
  const midAir = contact.filter(c => !c.onLip && c.over > 1.2);
  check(midAir.length === 0, 'never in mid-air on the ring',
        midAir.length ? `${midAir.length} of ${contact.length} angles off the lip AND off the water`
                      : `${contact.filter(c => c.onLip).length} of ${contact.length} angles on the lip, rest on the water`);
  // And "on the lip" has to mean TOUCHING it. The previous version of this check only proved
  // that off the lip he was on the water, and assumed the ring's radius matched the lip's
  // surface — it did not, by a constant 1.7 m, so he rode a circle through open air inside
  // the tube and every test passed. Distance to the actual mesh is the thing to assert.
  const far = contact.filter(c => c.toSurface > 0.85);
  check(far.length === 0, 'the board is touching whatever it is riding',
        `worst ${Math.max(...contact.map(c => c.toSurface)).toFixed(2)} m from any surface`);

  // A skateboarder in a full pipe stands on his board with the board against the wall and
  // his head toward the middle. The sign of the bank was inverted, which put his head on the
  // wall and the board on the inside of him — measured off the rig's real world quaternion,
  // his up ran between -0.23 and +0.09 against the inward direction, i.e. anywhere but at
  // the axis. Taken from the transform, not from the Euler angles meant to produce it.
  const upright = await page.evaluate(async () => {
    const out = [];
    for (let i = 0; i < 6; i++) {
      window.__surf.setTubeAngle(2.0 + i * 0.55);
      await new Promise(r => setTimeout(r, 1600));
      const u = window.__surf.riderUp();
      if (u.onLip) out.push(u.dot);
    }
    return out;
  });
  check(upright.length > 0 && Math.min(...upright) > 0.9,
        'the rider stands on his board against the wall, not on the wall himself',
        `worst up·inward ${upright.length ? Math.min(...upright).toFixed(3) : 'n/a'} over ${upright.length} samples`);

  // A whole turn pays, and the ride survives it.
  const looped = await page.evaluate(async () => {
    const s0 = window.__surf.state();
    for (let i = 1; i <= 8; i++) { window.__surf.setTubeAngle(i * Math.PI / 4); await new Promise(r => setTimeout(r, 260)); }
    return { s0, s1: window.__surf.state() };
  });
  check(looped.s1.swPh === 3 && !looped.s1.wipe, 'a full loop does not end the ride');
}

// ---------- an actual ride, not a warped snapshot ----------
// Every static check above sets an angle by hand and lets it settle. That is exactly the
// state in which the worst bug of this whole run was invisible: easing the position toward a
// point travelling round a circle cuts inward, and a settled angle has no angular velocity to
// cut against. Static tests read 0.6 m while real play was 3.5 m out. So the suite rides.
{
  const ride = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    await new Promise(r => setTimeout(r, 1500));
    const seen = [];
    for (let i = 0; i < 22; i++) {
      // keep him moving round the ring the way a held turn would
      window.__surf.setTubeAngle(window.__surf.state().swAng - 0.55);
      await new Promise(r => setTimeout(r, 260));
      const s = window.__surf.state();
      if (s.swPh !== 3 || !s.swTubeRide) continue;
      const sl = window.__surf.curlSlice(s.pz);
      const rf = sl.riderF, ry = (s.py - sl.baseY) / (sl.scaleY || 1);
      let lip = 1e9;
      for (const p of sl.pts) lip = Math.min(lip, Math.hypot(p.f - rf, p.y - ry));
      lip *= Math.abs(sl.scaleY || 1);
      const over = Math.abs(s.py - window.__surf.sampleWave(s.px, s.pz).sea);
      seen.push(Math.min(lip, over));
    }
    return seen;
  });
  const worstMoving = ride.length ? Math.max(...ride) : 0;
  const adrift = ride.filter(d => d > 1.6).length;
  check(ride.length > 8, 'the ring stays engaged while turning', `${ride.length} moving samples`);
  check(adrift === 0, 'and stays on the wave while MOVING, not just when parked',
        `worst ${worstMoving.toFixed(2)} m over ${ride.length} samples`);
}

// ---------- the wave comes to you ----------
// It used to stand up in a fixed lane and leave you to go and find the pocket, so the
// biggest thing in the game could arrive and pass you by. It forms around the rider now.
{
  const formed = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.9);
    await new Promise(r => setTimeout(r, 2500));
    const s = window.__surf.state();
    return { pocket: s.tubeC, px: s.px, phase: s.swPh };
  });
  check(Math.abs(formed.pocket - formed.px) < 4.0,
        'the pocket closes on wherever the rider actually is',
        `pocket at ${formed.pocket.toFixed(2)}, rider at ${formed.px.toFixed(2)}`);
}

// ---------- you have to actually reach the jellyfish ----------
// The obstacle test upstream is a footprint on the water, x and z only. Every other obstacle
// pays for that with its own height gate; the jelly had none, so passing metres above one
// still threw you — a bounce out of clear air at the top of a jump.
{
  const heights = await page.evaluate(async () => {
    const out = [];
    // any set wave still running would keep the ring physics in charge of py
    window.__surf.warpSetWave('EXIT');
    await new Promise(r => setTimeout(r, 3000));
    for (const h of [1.0, 4.0, 8.0]) {
      const s0 = window.__surf.state();
      const j = window.__surf.spawn('jelly', 0, -26);
      let bounced = false;
      for (let i = 0; i < 70; i++) {
        window.__surf.setRiderY(j.y + h);
        await new Promise(r => setTimeout(r, 45));
        const s = window.__surf.state();
        if (s.jellyHits > s0.jellyHits) { bounced = true; break; }   // counted, not inferred
        if (s.wipe) break;
      }
      out.push({ h, bounced });
    }
    return out;
  });
  const low = heights.find(r => r.h === 1.0), mid = heights.find(r => r.h === 4.0),
        high = heights.find(r => r.h === 8.0);
  check(low?.bounced === true, 'a jellyfish you actually touch still bounces you',
        `at 1 m over it: ${low?.bounced ? 'bounced' : 'nothing'}`);
  check(mid?.bounced === false && high?.bounced === false,
        'and one you fly over does not', `4 m: ${mid?.bounced ? 'BOUNCED' : 'clean'}, 8 m: ${high?.bounced ? 'BOUNCED' : 'clean'}`);
}

// ---------- you can see out of the barrel ----------
// The camera sits inside the shell, so the near wall wraps round the lens. It is cut away
// while riding the tube; when the cut was keyed on how high the rider had climbed it sat at
// zero for the whole bottom half of the ring and the frame was a flat blue wall.
{
  const cut = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    // The cut eases in at 5/s of SIM time, which is well under a second in the game and
    // many wall seconds here: swiftshader manages about three frames a second and dt is
    // clamped, so the sim runs at roughly a tenth of the clock. Wait for it to settle
    // rather than assuming a fixed delay — the claim is where it ends up, not how fast.
    const settle = async () => {
      for (let i = 0; i < 120; i++) {
        await new Promise(r => setTimeout(r, 250));
        if (window.__surf.state().swCut > 0.97) break;
      }
      return window.__surf.state();
    };
    await settle();
    const out = [];
    for (const a of [0, 1.2, 2.4, 3.6, 4.8]) {
      window.__surf.setTubeAngle(a);
      await new Promise(r => setTimeout(r, 350));
      const s = window.__surf.state();
      out.push({ a, cut: +s.swCut.toFixed(2), tube: s.swTubeRide });
    }
    return out;
  });
  const worst = cut.reduce((w, r) => r.cut < w.cut ? r : w, cut[0]);
  check(cut.every(r => r.tube && r.cut > 0.9), 'the near wall is cut away all the way round the ring',
        `worst cut ${worst.cut} at ${worst.a} rad`);
}

// ---------- the sky stays in the sky ----------
// Sun, moon, stars, cloud and gulls are transparent materials, so three.js draws them after
// the whole opaque pass. With the depth test off that put them ON TOP of the water: out on
// the flat nobody notices, but inside a barrel the sun was painted across the inside of the
// wave. They are depth-tested and ordered after the lip instead.
{
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  const skyBlock = src.slice(src.indexOf('const skyLayer=new THREE.Group()'),
                             src.indexOf('// One gull:') + 900);
  check(!/depthTest:false/.test(skyBlock), 'nothing in the sky skips the depth test',
        `${(skyBlock.match(/depthTest:(true|false)/g) || []).length} sky materials checked`);
  const order = +(/sunDisc\.renderOrder=(-?[\d.]+)/.exec(src)?.[1] ?? -1);
  const curlOrder = +(/curl\.renderOrder=(-?[\d.]+)/.exec(src)?.[1] ?? 1e9);
  check(order > curlOrder, 'and the sun draws after the lip, so the lip\'s depth is down first',
        `sun ${order} vs lip ${curlOrder}`);
}

// ---------- the barrel's one hazard says what it is ----------
// Wreckage had no entry in the wipeout table, so it fell back to foam's — a fuel drum to the
// chest announced itself as "ATE IT!" and the stats logged it as "Ate it". It is the only
// thing left that can end a barrel ride, so it is the one that most needs to be legible.
{
  const named = await page.evaluate(() => ({
    hasWipe: typeof window.__surf === 'object',
  }));
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  const wipeBlock = src.slice(src.indexOf('const WIPE={'), src.indexOf('const WIPE={') + 1400);
  check(/debris:\s*\{/.test(wipeBlock), 'wreckage has its own wipeout, not foam\'s',
        /debris:\s*\{[^}]*msg:'([^']+)'/.exec(wipeBlock)?.[1] ?? 'missing');
  check(/debris:'[^']+'/.test(src.slice(src.indexOf('const DEATH_NAME='), src.indexOf('const DEATH_NAME=') + 400)),
        'and its own name in the stats', 'so a run does not end as "Ate it"');
  void named;
}

// ---------- the two copies of the wave ----------
// The set wave lives twice: setWaveH() in JS, which the board rides, and the matching
// block in the ocean's GLSL vertex shader, which is what you see. They are separate code
// and nothing but discipline keeps them equal — so drift here is silent, and shows up as
// the board riding a wave that is not the one on screen. The shader spells the constants
// as literals, so this checks the literals are still the numbers JS is using.
{
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  const num = re => { const m = src.match(re); return m ? m[1] : null; };
  const face = num(/const SW_FACE=([\d.]+)/);
  const back = num(/const SW_BACK=([\d.]+)/);
  const glsl = src.slice(src.indexOf('if(uSwA>0.0015){'), src.indexOf('// phase of the two main swells'));
  // Either spelling counts: the number itself, or — better — the constant interpolated in,
  // which cannot drift at all. This check exists to catch the copies diverging, so a change
  // that makes divergence impossible should pass it, not fail it.
  const agrees = (n, name) => n && (new RegExp(`[^\\d.]${n.replace('.', '\\.')}[^\\d]`).test(glsl)
                                    || glsl.includes(`\${${name}.toFixed(1)}`));
  check(!!face && !!back, 'wave constants found in JS', `SW_FACE=${face} SW_BACK=${back}`);
  check(agrees(face, 'SW_FACE'), 'shader face width matches SW_FACE', `${face}`);
  check(agrees(back, 'SW_BACK'), 'shader shoulder width matches SW_BACK', `${back}`);
  // The face exponent is the shape itself; the shader repeats it in both the height and
  // its derivative, so it appears twice.
  const expo = num(/f=Math\.pow\(k,([\d.]+)\)/);
  check(expo && glsl.includes(`pow(k,${expo})`), 'shader face exponent matches JS', `${expo}`);
  // The peel's constants are interpolated into the shader rather than repeated, which is the
  // real fix for drift — but the collapse curve is still written out twice, once in
  // swPeelB() and once as GLSL, so the shape itself still needs watching.
  check(/\$\{SW_BRK\.toFixed\(1\)\}/.test(glsl) && /\$\{SW_COLL\.toFixed\(1\)\}/.test(glsl),
        'shader interpolates the peel constants instead of copying them');
  check(/0\.55\*b/.test(glsl) && /1-0\.55\*swPeelB/.test(src),
        'the collapse depth is the same in both copies of the wave');
  check(/dhz \+= uSwA/.test(glsl),
        'the wave contributes to dhz — without it the peel lights as though it were flat');
}

check(shaderErrors.length === 0, 'every shader compiles',
      shaderErrors.length ? `\n    ${shaderErrors.slice(0, 3).join('\n    ')}` : '');

check(errors.length === 0, 'no page errors', errors.length ? `\n    ${errors.slice(0, 10).join('\n    ')}` : '');

await browser.close();
server.close();
console.log(failed ? `\n${failed} check(s) failed` : '\nall checks passed');
process.exit(failed ? 1 : 0);
