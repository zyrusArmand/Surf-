// Smoke test: load the game, start a run, confirm nothing throws and the run advances.
//
// Self-contained on purpose — it serves the repo root itself rather than assuming a
// python http.server is already up on :8767, because the one thing worse than a failing
// test is a test that fails because nobody started the server.
//
// Chromium is the pre-installed build at PLAYWRIGHT_BROWSERS_PATH; the bundled version
// playwright expects is NOT downloaded here, so the executable is passed explicitly.
// WebGL runs on swiftshader at about three frames a second and the game clamps dt, so waiting
// on RENDERED frames buys roughly a tenth of a second of simulation per second of wall clock —
// which is what made this suite take twenty minutes to answer questions that have nothing to do
// with pixels. It steps the simulation directly instead: __surf.tick(seconds) runs update() in a
// plain loop with no rendering, about thirty times real time, with the timestep exact and
// identical every run. Waits here are therefore in SECONDS OF SIMULATION, not wall clock.
// The few remaining waitForTimeout calls are waiting on the DOM, which does need real time.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';

const ROOT = new URL('.', import.meta.url).pathname;
// Port 0 means "whatever is free". A fixed one collided with a still-dying run from the
// previous attempt often enough to cost several whole runs to EADDRINUSE, which looks
// exactly like a broken test and is nothing of the kind.
let PORT = 0;
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
await new Promise(r => server.listen(0, '127.0.0.1', r));
PORT = server.address().port;

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

// The secret panel opens on five taps ON the version number — but the handler lives on the
// overlay and hit-tests coordinates itself, so a plain click on #ver is intercepted. The
// taps are dispatched as the pointer events the handler actually listens for.
const ver5tap = () => page.evaluate(() => {
  const r = document.getElementById('ver').getBoundingClientRect();
  for (let i = 0; i < 5; i++)
    document.getElementById('overlay').dispatchEvent(new PointerEvent('pointerdown',
      { clientX: r.left + r.width / 2, clientY: r.top + r.height / 2, bubbles: true }));
});

// ---------- the menus ----------
// Cheap, but this is the part a player meets first, and a shop that will not open is as
// broken as a wave that will not break.
let perkSeen = { found: false, filled: 0, text: '' };
for (const [openSel, panelSel, closeSel, label] of [
  ['#shopBtn', '#shop', '#shopClose', 'beach shop'],
  // Stats has no button any more: five quick taps on the version number open the secret
  // panel, which is where the dev tools live now.
  ['ver5tap', '#stats', '#stClose', 'secret stats (5 taps on the version)'],
]) {
  if (openSel === 'ver5tap') await ver5tap();
  else await page.click(openSel);
  await page.waitForTimeout(400);
  const opened = await page.$eval(panelSel, e => !e.classList.contains('hidden'));
  // While the shop is already open, check the rider cards carry their perk. Done here rather
  // than in a block of its own: a second open/close cycle left the menu in a state where the
  // next five-tap did not open the secret panel, and took a passing check down with it.
  if (panelSel === '#shop') perkSeen = await page.evaluate(() => {
    const tab = document.querySelector('[data-tab="riders"]');
    if (tab) tab.click();
    // the first rider is the free pug, which HAS no perk and correctly says so — look for a
    // card that actually claims one
    const card = [...document.querySelectorAll('.stats')].find(s => s.querySelector('.perkTx'));
    const out = card ? { found: true, filled: card.querySelectorAll('.seg i.on').length,
                         text: card.querySelector('.perkTx').textContent.trim() }
                     : { found: false, filled: 0, text: '' };
    const back = document.querySelector('[data-tab="boards"]');   // leave it as we found it
    if (back) back.click();
    return out;
  });
  await page.click(closeSel);
  await page.waitForTimeout(400);
  const closed = await page.$eval(panelSel, e => e.classList.contains('hidden'));
  check(opened && closed, `${label} opens and closes`, `opened=${opened} closed=${closed}`);
}

check(perkSeen.found && perkSeen.filled > 0 && perkSeen.text.length > 0,
      'a rider card shows its perk on a stat bar',
      `found=${perkSeen.found} filled=${perkSeen.filled} text=${JSON.stringify(perkSeen.text)}`);

await page.click('#startBtn');
await page.evaluate(() => window.__surf.tick(0.3));
const d0 = await page.textContent('#hDist');
await page.evaluate(() => window.__surf.tick(1.2));
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
// Drums off for the whole suite. They spawn into the pocket on a timer, knock you off the
// wall on contact and crack the board, and this suite spends twenty-odd minutes inside a
// barrel — so left on they eventually snap it, and every check after that is quietly
// measuring a run that has already ended: three checks in a row reported zero of everything.
// The wreckage check at the bottom places its own drums explicitly.
if (hasHook) await page.evaluate(() => window.__surf.setDebris(false));

if (hasHook) {
  const ph = await page.evaluate(async () => {
    window.__surf.armSetWave();
    window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.12);
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
    window.__surf.tick(0.4);
    return { s0, s1: window.__surf.state() };
  });
  check(rode.s1.score > rode.s0.score || rode.s1.wipe, 'ride scores (or ends in a wipeout)',
        `${Math.round(rode.s0.score)} -> ${Math.round(rode.s1.score)} wipe=${rode.s1.wipe}`);

  const out = await page.evaluate(async () => {
    window.__surf.warpSetWave('EXIT');
    window.__surf.tick(0.6);
    return window.__surf.state();
  });
  check(out.swPh === -1 || out.swPh === 4, 'wave closes out and returns to idle', `swPh=${out.swPh}`);

  // From here on the wave is held open. A warped barrel lasts 6-16 SIM seconds, which at a
  // tenth of wall clock is a minute or two, and several checks below legitimately take longer
  // than that — they were quietly measuring a rider who had already been spat out, which
  // reads as a bank of zero, a jump that does nothing and a camera free to move again. The
  // clock's own behaviour is still tested, above and in its own check further down, which
  // watch swRide and swOver rather than waiting for the spit.
  await page.evaluate(() => window.__surf.holdWave(true));

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
  await page.waitForSelector('#startBtn', { timeout: 30000 });
  // The main screen is clean now: no wave test, no stats button, no purse. The wave test
  // lives behind the version 5-tap, in the secret panel, next to the other dev tools.
  const clean = await page.evaluate(() => {
    const vis = sel => { const el = document.querySelector(sel);
      return !!el && getComputedStyle(el).display !== 'none'; };
    return { wave: vis('#waveBtn'), stats: vis('#recordsBtn'), purse: vis('#ovPurse') };
  });
  check(!clean.wave && !clean.stats && !clean.purse,
        'the main screen is clean — no wave test, no stats button, no purse',
        `waveBtn=${clean.wave} statsBtn=${clean.stats} purse=${clean.purse}`);
  await ver5tap();
  await page.waitForTimeout(300);
  const secretWave = await page.evaluate(() => {
    const el = document.querySelector('#stWave');
    return !!el && getComputedStyle(el).display !== 'none';
  });
  check(secretWave, 'the wave test lives in the secret panel');
  await page.click('#stWave');
  // Two seconds of SIM time, and headless runs the sim at ~10% of wall clock.
  const fired = await page.evaluate(async () => {
    const t0 = Date.now(); let seen = null, dist = null;
    while (Date.now() - t0 < 60000) {
      const s = window.__surf.state();
      if (s.swPh !== -1) { seen = s.swPh; dist = s.dist; break; }
      window.__surf.tick(0.03);
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
    window.__surf.tick(0.12);
    const s = window.__surf.state(), out = {};
    const C = window.__surf.consts();
    // Sampled RELATIVE to the break rather than at hard-coded z. When the break moved back
    // behind the camera, "z=30" silently stopped meaning "behind the break", and the collapse
    // check would have passed on a wave that no longer collapsed anywhere in view.
    const zs = { wall: C.SW_BRK - C.SW_PEEL + 6, mid: C.SW_BRK - C.SW_PEEL * 0.55,
                 tube: 0, dead: C.SW_BRK + C.SW_COLL * 0.85 };
    for (const k of Object.keys(zs)) {
      const w = window.__surf.sampleWave(s.swX - 2, zs[k]);
      const sl = window.__surf.curlSlice(zs[k]);
      out[k] = { z: zs[k], setH: w.set, peelB: w.peelB, reach: Math.max(...sl.pts.map(p => p.f)) };
    }
    // At the CREST line, at the rider's own z. Sampling at the rider's x instead reads a
    // couple of metres down the face — a small number by design, not a collapsed wave.
    return { out, swA: s.swA, riderH: window.__surf.sampleWave(s.swX, s.pz).set };
  });
  const at = k => peel.out[k];
  check(at('wall').reach < 2 && at('tube').reach > 10,
        'lip is unbroken wall down the line and thrown at the break',
        `reach ${at('wall').reach.toFixed(2)} at z=${at('wall').z.toFixed(0)} vs ${at('tube').reach.toFixed(2)} at z=0`);
  check(at('mid').reach > at('wall').reach && at('mid').reach < at('tube').reach,
        'and pitches out progressively between the two', `${at('mid').reach.toFixed(2)} at z=${at('mid').z.toFixed(0)}`);
  check(at('dead').setH < at('tube').setH * 0.65, 'the section collapses behind the break',
        `${at('tube').setH.toFixed(2)} -> ${at('dead').setH.toFixed(2)} at z=${at('dead').z.toFixed(0)}`);
  check(at('wall').setH > at('tube').setH * 0.9, 'but stands at full height ahead of it — an unbroken wave is not a small one',
        `${at('wall').setH.toFixed(2)} vs ${at('tube').setH.toFixed(2)}`);
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
    window.__surf.tick(0.15);
    const out = { start: window.__surf.state(), at: {} };
    for (const [name, a] of [['pocket', 0], ['face', Math.PI / 2], ['roof', Math.PI], ['curtain', Math.PI * 1.5]]) {
      window.__surf.setTubeAngle(a);
      // He is eased onto the ring rather than snapped to it, and this browser runs the sim
      // at about a tenth of wall clock, so settling takes seconds of real time here.
      window.__surf.tick(0.26);
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
      window.__surf.tick(0.24);
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
  // Measured to the nearest surface, not to the sea. Off the lip's ARC he is no longer
  // necessarily on the water: since v2.56.0 the lip falls a curtain into it, and the curtain
  // is a perfectly good thing to be riding several metres above the sea. It is part of the
  // shell, so the distance to the mesh already covers it — what "mid-air" has to mean is
  // "near nothing", which is what it meant all along.
  const midAir = contact.filter(c => !c.onLip && c.toSurface > 1.2);
  check(midAir.length === 0, 'never in mid-air on the ring',
        midAir.length ? `${midAir.length} of ${contact.length} angles near NO surface`
                      + ` (worst ${Math.max(...midAir.map(c => c.toSurface)).toFixed(2)} m)`
                      : `${contact.filter(c => c.onLip).length} of ${contact.length} angles on the lip,`
                      + ` rest on the curtain or the water`);
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
      // Read it SETTLED. The claim is about where the board ends up, and a single sample
      // after a fixed wait is a frame-rate measurement in disguise — under swiftshader that
      // has come back mid-step and been reported as a lagging bank more than once.
      window.__surf.tick(0.16);
      window.__surf.riderUp();
      window.__surf.tick(0.12);
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
    for (let i = 1; i <= 8; i++) { window.__surf.setTubeAngle(i * Math.PI / 4); window.__surf.tick(0.03); }
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
    window.__surf.tick(0.15);
    const seen = [];
    for (let i = 0; i < 22; i++) {
      // keep him moving round the ring the way a held turn would
      window.__surf.setTubeAngle(window.__surf.state().swAng - 0.55);
      window.__surf.tick(0.03);
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

// ---------- the ring is a closed curve, not a curve with a cliff in it ----------
// "When turning to where the barrel breaks it shoots you more to the right and then up,
// almost like a ninety degree angle." Measured round the whole circle, that was one step of
// 5.07 m: the lip's arc ended three metres above the sea and the ring's radius jumped from
// 3.06 m straight to 8.13 m at that single angle, because there was no water in between to
// ride. The lip falls a curtain into the water now, so the radius ramps instead of jumping.
// Sampled from the ride's OWN surface function, so it cannot drift away from what is ridden.
{
  const prof = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    for (let i = 0; i < 250; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      if (s.swTubeRide && (s.swForm ?? 0) > 0.9) break;
      if (s.wipe || s.swPh !== 3) break;
    }
    return window.__surf.ringProfile();
  });
  const rows = prof.rows || [];
  let worstStep = 0, atTh = 0;
  for (let i = 0; i < rows.length; i++) {
    const step = Math.abs(rows[i].r - rows[(i + rows.length - 1) % rows.length].r);
    if (step > worstStep) { worstStep = step; atTh = rows[i].th; }
  }
  check(rows.length > 32 && worstStep < 2.5,
        'the ring has no cliff in it — going round is a ride, not a corner',
        rows.length ? `worst step ${worstStep.toFixed(2)} m at ${atTh.toFixed(2)} rad over ${rows.length} angles`
                    : (prof.error || 'no profile'));
  // And the curtain is what closed it: without a surface across that gap the step comes back.
  const openSector = rows.filter(r => !r.onLip);
  check(openSector.length > 4 && Math.max(...openSector.map(r => r.r)) > Math.min(...openSector.map(r => r.r)),
        'and the open sector is a graded ramp between the lip and the sea',
        openSector.length ? `${openSector.length} angles, ${Math.min(...openSector.map(r => r.r)).toFixed(2)}`
                          + `-${Math.max(...openSector.map(r => r.r)).toFixed(2)} m` : 'no open sector');
}

// ---------- the wave comes to you ----------
// It used to stand up in a fixed lane and leave you to go and find the pocket, so the
// biggest thing in the game could arrive and pass you by. It forms around the rider now.
{
  const formed = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.9);
    window.__surf.tick(0.25);
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
    window.__surf.tick(0.3);
    for (const h of [1.0, 4.0, 8.0]) {
      const s0 = window.__surf.state();
      const j = window.__surf.spawn('jelly', 0, -26);
      let bounced = false, arrived = false;
      // Run until the jellyfish has actually gone PAST the rider, not for a fixed number of
      // frames. A fixed count is a frame-rate test in disguise: a slow run left it still short
      // of him and reported that as "no bounce", which failed a build that was fine.
      for (let i = 0; i < 900; i++) {
        window.__surf.setRiderY(j.y + h);
        window.__surf.tick(0.03);
        const s = window.__surf.state();
        if (s.jellyHits > s0.jellyHits) { bounced = true; arrived = true; break; }
        if (s.wipe) break;
        const rel = window.__surf.obstacleZ('jelly');
        if (rel === null || rel > 4) { arrived = true; break; }      // it has been and gone
      }
      out.push({ h, bounced, arrived });
    }
    return out;
  });
  const low = heights.find(r => r.h === 1.0), mid = heights.find(r => r.h === 4.0),
        high = heights.find(r => r.h === 8.0);
  check(low?.bounced === true, 'a jellyfish you actually touch still bounces you',
        `at 1 m over it: ${low?.bounced ? 'bounced' : (low?.arrived ? 'passed clean through it' : 'NEVER REACHED HIM')}`);
  check(mid?.bounced === false && high?.bounced === false,
        'and one you fly over does not', `4 m: ${mid?.bounced ? 'BOUNCED' : 'clean'}, 8 m: ${high?.bounced ? 'BOUNCED' : 'clean'}`);
}

// ---------- right is right, whichever way the wave breaks ----------
// Increasing the ring angle carries the rider toward +swS in x, and the steering input was
// not multiplied by swS — so on half the waves a drag to the right sent him left.
{
  const dirs = [];
  for (const side of [1, -1]) {
    await page.evaluate(s => {
      window.__surf.armSetWave(); window.__surf.setSide(s); window.__surf.warpSetWave('RIDE');
    }, side);
    await page.evaluate(() => window.__surf.tick(0.4));
    // Parked at the bottom of the ring and left to settle first. Measured straight off the
    // warp instead, the reading is the radius easing on to the wall, which is metres of
    // sideways travel that has nothing to do with the input — it read backwards on both
    // sides, which would have condemned a correct fix.
    // Parked at the bottom and CONFIRMED parked. setTubeAngle writes the angle, but the ring
    // re-derives it from the rider's position on the frame it first takes over — so setting it
    // before the takeover has happened is silently overwritten, and the previous check leaves
    // him eight metres in the air. Measured from a starting angle near a quarter turn, holding
    // right moves him LEFT, which is arithmetic rather than a bug: past that point the circle
    // is coming back. Wait until he is on the ring at the bottom of it.
    const parked = await page.evaluate(async () => {
      for (let i = 0; i < 40; i++) {
        window.__surf.setTubeAngle(0);
        window.__surf.tick(0.04);
        const s = window.__surf.state();
        if (s.swTubeRide && Math.abs(s.swAng) < 0.2) return true;
      }
      return false;
    });
    await page.evaluate(() => window.__surf.tick(0.25));
    const before = await page.evaluate(() => window.__surf.state().px);
    void parked;
    await page.keyboard.down('ArrowRight');
    await page.evaluate(() => window.__surf.tick(0.6));
    await page.keyboard.up('ArrowRight');
    const after = await page.evaluate(() => window.__surf.state());
    dirs.push({ side, moved: +(after.px - before).toFixed(2), tube: after.swTubeRide, parked });
  }
  check(dirs.every(d => d.tube && d.parked && d.moved > 0.5), 'steering right moves the rider right on both sides of the wave',
        dirs.map(d => `swS=${d.side}: ${d.moved > 0 ? '+' : ''}${d.moved} m`
                      + (d.tube ? '' : ' (OUT OF THE TUBE)') + (d.parked ? '' : ' (NEVER PARKED)')).join(', '));
}

// ---------- crossing off the lip does not flip the board ----------
// The roll target used to switch between the ring's angle and the flat sea's carve at the
// edge of the lip's arc, and easing across that gap unwound the board through most of a
// revolution — the "whole flip" going from the top of the wave back to the bottom.
{
  const rolls = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.2);
    const out = [];
    // Step round the whole circle in small increments and watch the board's roll. Crossing
    // on to and off the lip has to be no more of a jump than any other step of the same size.
    for (let a = 0; a <= Math.PI * 2 + 0.001; a += Math.PI / 24) {
      window.__surf.setTubeAngle(a);
      // Long enough for the frame to actually run. setTubeAngle moves the angle instantly
      // and the pose follows in the next physics tick, which under swiftshader is a third
      // of a second away — at 260 ms this was reading the board mid-step and calling it a
      // lag in the bank. Measured: the same angles settle to 1.000 given a second.
      window.__surf.tick(0.09);
      // A sweep of forty-nine angles at nine hundred milliseconds is four-odd seconds of sim,
      // and the shortest wave is six — so the wave can end halfway through and every sample
      // after it reads a rider who is no longer on a ring at all. That came back as a bank of
      // zero and looked exactly like the bug this check exists to catch. Re-arm and skip.
      if (!window.__surf.state().swTubeRide) {
        window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
        window.__surf.tick(0.25);
        window.__surf.setTubeAngle(a);
        window.__surf.tick(0.09);
      }
      const u = window.__surf.riderUp();
      out.push({ a: +a.toFixed(2), onLip: u.onLip, dot: u.dot });
    }
    return out;
  });
  // Rider-up against inward is the roll, read off the rig's real world transform. It should
  // stay pinned at 1 the whole way round; a flip shows up as it falling away.
  const worst = rolls.reduce((w, r) => r.dot < w.dot ? r : w, rolls[0]);
  const crossings = rolls.filter((r, i) => i && r.onLip !== rolls[i - 1].onLip).length;
  check(worst.dot > 0.9 && crossings >= 2, 'the board stays welded to the wall across the lip\'s edge',
        `worst up·inward ${worst.dot} at ${worst.a} rad, ${crossings} lip crossings sampled`);
}

// ---------- the board lies down the line, and you can still throw a trick ----------
// Two things the ring was getting wrong at once. The board was yawed AND pitched by how fast
// he was going round, which stood it up across the barrel — and the wall is a cylinder about
// z, so the only heading that lies flat on it is along the tube. And every trick calls
// doJump when you are not airborne, while the ring pinned airborne to false every frame, so
// nothing ever left the water and no trick could start.
{
  await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.25);
  });
  await page.keyboard.down('ArrowRight');
  await page.evaluate(() => window.__surf.tick(0.4));
  const flat = await page.evaluate(() => {
    const s = window.__surf.state();
    return { yaw: Math.abs(s.boardYaw), pitch: Math.abs(s.boardPitch), tube: s.swTubeRide };
  });
  check(flat.tube && flat.yaw < 0.2 && flat.pitch < 0.2, 'the board lies down the line while going round the ring',
        `yaw ${flat.yaw.toFixed(2)}, pitch ${flat.pitch.toFixed(2)} rad`);
  // Jump: in the tube that is a push off the wall toward the axis, and it is what makes the
  // trick buttons live in there.
  const air = await page.evaluate(async () => {
    const before = window.__surf.state();
    window.__surf.jump();
    window.__surf.tick(0.09);
    const mid = window.__surf.state();
    return { wasAir: before.swPipeAir, air: mid.swPipeAir, airborne: mid.airborne,
             ready: mid.trickReady, tube: mid.swTubeRide };
  });
  await page.keyboard.up('ArrowRight');
  check(!air.wasAir && air.air && air.airborne && air.ready && air.tube,
        'jumping in the tube takes you off the wall, so tricks are live in there',
        `pipe air ${air.air}, airborne ${air.airborne}, trickReady ${air.ready}, in the tube ${air.tube}`
        + (air.wasAir ? ' — WAS ALREADY IN THE AIR' : ''));
  // And it puts you back on the wall rather than out of the wave.
  const landed = await page.evaluate(async () => {
    for (let i = 0; i < 200; i++) {
      const s = window.__surf.state();
      if (!s.swPipeAir) return { back: true, tube: s.swTubeRide, wipe: s.wipe };
      window.__surf.tick(0.03);
    }
    return { back: false };
  });
  check(landed.back && landed.tube && !landed.wipe, 'and it drops you back on the ring, still in the wave',
        landed.back ? 'landed on the wall' : 'never came back down');
}

// ---------- the shot holds still, aim included ----------
// Position was locked and the AIM was not: it tracked a fifth of the climb, and a fifth of
// seven metres is the whole picture drifting every time he goes round. The water clamp was
// also being re-applied every frame against a swell that moves, which fed that movement into
// a camera that is supposed to be nailed down.
{
  const cam = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.25);
    const out = [];
    for (const a of [0, 1.6, 3.1, 4.7]) {
      window.__surf.setTubeAngle(a);
      window.__surf.tick(0.09);
      out.push(window.__surf.cam());
    }
    return out;
  });
  const span = k => Math.max(...cam.map(c => c[k])) - Math.min(...cam.map(c => c[k]));
  const moved = Math.max(span('x'), span('y'), span('z'));
  const turned = Math.max(span('dx'), span('dy'), span('dz'));
  check(moved < 0.05 && turned < 0.02, 'the barrel camera holds still, aim included, all the way round',
        `moved ${moved.toFixed(3)} m, aim shifted ${turned.toFixed(4)}`);

  // And from the very first frame of the ride, which is where the last drift lived: the aim
  // point's DEPTH rode swCam as it ramped 0 -> 1, swinging the whole picture round over the
  // first second. Sampling only after everything has settled would never have caught it.
  const entry = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.07);
    const a = window.__surf.cam();
    window.__surf.tick(0.6);
    return { a, b: window.__surf.cam() };
  });
  const drift = Math.max(...['x','y','z','dx','dy','dz'].map(k => Math.abs(entry.a[k] - entry.b[k])));
  check(drift < 0.02, 'and from the first frame of the ride, not just once it has settled',
        `${drift.toFixed(4)} between entry and six seconds later`);

}

// ---------- a trick in the tube is a hop, not a launch ----------
// The pop off the wall was carrying three metres toward the axis of a tube six and a half
// across — from the bottom of the ring that is straight up, and it read as being fired out of
// the wave rather than doing a trick in it.
{
  const hop = await page.evaluate(async () => {
    window.__surf.setTubeAngle(0);
    window.__surf.tick(0.12);
    const r0 = window.__surf.state().swRad;
    window.__surf.jump();
    let peak = r0;
    for (let i = 0; i < 120; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      peak = Math.min(peak, s.swRad);
      if (!s.swPipeAir && i > 3) break;
    }
    return { off: +(r0 - peak).toFixed(2), tube: window.__surf.state().swTubeRide };
  });
  check(hop.tube && hop.off > 0.4 && hop.off < 2.6, 'jumping in the tube lifts you off the wall without firing you across it',
        `${hop.off} m off the wall`);
}

// ---------- keep circling and the wave waits ----------
// A fixed 6-16 s clock was spitting the rider out mid-loop. The clock stops while he is
// actually going round; park and it runs again.
{
  // Racing the wall clock would prove nothing here: swiftshader advances the sim at about a
  // tenth of real time, so a wave that lasts 6-16 SIM seconds outlives any reasonable wait
  // whether the fix is in or not. Measure the clock itself instead — swOver counts the time
  // spent going round, swRide is what the spit is compared against.
  await page.evaluate(() => { window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE'); });
  await page.evaluate(() => window.__surf.tick(0.15));
  // Steered for real, not driven through setTubeAngle — that hook zeroes the angular
  // velocity, which is the very thing the clock watches.
  await page.keyboard.down('ArrowRight');
  const held = await page.evaluate(async () => {
    window.__surf.tick(0.2);           // let the turn spin up
    const a = window.__surf.state();
    window.__surf.tick(3.0);          // ~3 s of sim
    const b = window.__surf.state();
    return { ranOn: +(b.swRide - a.swRide).toFixed(2), circled: +(b.swOver - a.swOver).toFixed(2),
             ride: +b.swRide.toFixed(1), max: +b.swRideMax.toFixed(1), ph: b.swPh };
  });
  await page.keyboard.up('ArrowRight');
  check(held.circled > 1.0 && held.ranOn < 0.4 && held.ph === 3,
        'the clock stops while you are going round, so the wave does not spit you out mid-loop',
        `${held.circled}s circling advanced the spit clock by ${held.ranOn}s (${held.ride}/${held.max})`);
  // And the other half of the rule: stop, and the wave closes on you as it always did.
  const parked = await page.evaluate(async () => {
    window.__surf.tick(0.8);           // let the turn wind down
    const a = window.__surf.state().swRide;
    window.__surf.tick(1.5);
    const s = window.__surf.state();
    return { ranOn: +(s.swRide - a).toFixed(2), ph: s.swPh };
  });
  check(parked.ph !== 3 || parked.ranOn > 0.4, 'and it still runs when you stop',
        parked.ph !== 3 ? 'closed out while parked' : `clock advanced ${parked.ranOn}s`);
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
        window.__surf.tick(0.03);
        if (window.__surf.state().swCut > 0.97) break;
      }
      return window.__surf.state();
    };
    await settle();
    const out = [];
    for (const a of [0, 1.2, 2.4, 3.6, 4.8]) {
      window.__surf.setTubeAngle(a);
      window.__surf.tick(0.035);
      const s = window.__surf.state();
      out.push({ a, cut: +s.swCut.toFixed(2), tube: s.swTubeRide });
    }
    return out;
  });
  const worst = cut.reduce((w, r) => r.cut < w.cut ? r : w, cut[0]);
  check(cut.every(r => r.tube && r.cut > 0.9), 'the near wall is cut away all the way round the ring',
        `worst cut ${worst.cut} at ${worst.a} rad`);
}

// Placed after every other check that needs a live run. This one can snap the board and
// end the ride, and when it ran earlier the blocks after it were quietly measuring a
// game that was already over — the wave clock reported zero seconds of everything.
// ---------- the ring taking over does not move the camera at all ----------
// Every version of the barrel camera before this one cut to a chosen placement — pulled back
// to 25.5, re-centred on the tube's axis, aimed at a settled value — and every one of them is
// a snap at the instant the ring takes charge, which is what kept getting reported. There is
// no barrel camera now: the ride camera stops following and holds exactly where it already
// was. Measured ACROSS the takeover, which is the frame the snap lived in.
{
  const across = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.tick(0.4);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    // Getting to the takeover is slow under swiftshader: the swell runs, then the lip has to
    // form (swForm climbs at 0.26/s of SIMULATED time) before the ring will engage at all.
    // Measured at 18.2 s of wall clock on a good run, so the old 24 s bound failed on a slow
    // one and reported it as the tube letting go at phase 2 — which is SWELL, i.e. it had
    // never taken over in the first place. Budget 80 s and say so when it never gets there.
    let before = window.__surf.cam(), entered = false, enteredAt = 'never';
    for (let i = 0; i < 400; i++) {
      window.__surf.tick(0.03);
      if (window.__surf.state().swTubeRide) { entered = true; enteredAt = `phase ${window.__surf.state().swPh} after ${i * 0.2}s`; break; }
      before = window.__surf.cam();          // the last frame before the ring had it
    }
    // The aim SETTLES onto the tube's axis while the barrel forms — that is the centred
    // barrel view, by design since v2.62 — and is frozen at swForm 0.99. So the hold is
    // measured from the formed frame on; what must never move at any point is the position.
    for (let i = 0; i < 200 && entered; i++) {
      if ((window.__surf.state().swForm ?? 0) >= 0.99) break;
      window.__surf.tick(0.05);
      if (!window.__surf.state().swTubeRide) break;
    }
    const after = window.__surf.cam();
    // Sampled only while the ring is actually in charge. If the tube lets go the ride camera
    // is supposed to resume, so measuring across that and calling it camera drift blames the
    // wrong thing — it is reported separately instead.
    let held = true, settled = after, why = '';
    for (let i = 0; i < 20; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      if (!s.swTubeRide) {
        held = false;
        // Ending the run is a legitimate way to leave the tube and is not a camera fault;
        // letting go while still riding it would be.
        why = s.wipe ? `run ended as ${s.lastWipe && s.lastWipe.kind}` : `tube dropped at phase ${s.swPh}`;
        break;
      }
      settled = window.__surf.cam();
    }
    const s2 = window.__surf.state();
    if (!entered) why = `the ring never took over — still at phase ${s2.swPh}`;
    return { before, after, settled, held, why, entered, enteredAt, ended: !!s2.wipe };
  });
  // Position across the whole takeover AND formation: zero. Aim: free to settle during
  // formation (that is the centred view arriving), frozen once formed.
  const jumpPos = Math.max(...['x','y','z'].map(k => Math.abs(across.before[k] - across.after[k])));
  const drift = Math.max(...['x','y','z','dx','dy','dz'].map(k => Math.abs(across.after[k] - across.settled[k])));
  check(across.entered && (across.held || across.ended) && jumpPos < 0.02 && drift < 0.02,
        'the ring taking over does not move the camera',
        `position ${jumpPos.toFixed(3)} across takeover and formation, ${drift.toFixed(4)} once formed`
        + (across.held ? '' : ` (${across.why}; took over at ${across.enteredAt})`));
}

// ---------- the barrel builds, it does not arrive finished ----------
// The tube used to exist the moment the ride phase began: you reached the wave and there was
// already a completed barrel waiting for you, which is not how any wave has ever worked. The
// lip grows out of the crest and arches over across about four seconds, and the ring only
// takes charge once there is a bore worth riding round.
{
  const grew = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.tick(0.3);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    const seen = [];
    // Four seconds of SIM to build, which is forty of wall clock here. Do not tighten.
    for (let i = 0; i < 320; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      if (s.swPh === 3) seen.push({ form: s.swForm, r: s.ring.r, tube: s.swTubeRide });
      if (seen.length && seen[seen.length - 1].form >= 0.999) break;
    }
    return seen;
  });
  const first = grew[0] || {}, last = grew[grew.length - 1] || {};
  const roseFirst = grew.findIndex(g => g.tube);
  check(grew.length > 3 && first.form < 0.25 && first.r < 2.0 && last.form > 0.95 && last.r > 4.5,
        'the barrel builds around you instead of arriving finished',
        `bore ${(first.r ?? 0).toFixed(2)} m at the start of the ride, ${(last.r ?? 0).toFixed(2)} m once it is over you`);
  check(roseFirst > 0, 'and the ring only takes charge once there is a tube to ride',
        roseFirst > 0 ? `${roseFirst} samples of open face first` : 'the ring had him immediately');
}

// ---------- a trick in the tube turns about the board ----------
// player is the parent and rig the child, so a trick rotation is applied in WORLD axes to a
// board the ring has already banked. Banked ninety degrees the board's transverse axis IS
// world y, so a spin came out as a flip. A pure spin turns the board about its own up, which
// means its up must not move at all — that is what this measures, at the bank where the two
// axes have fully swapped.
{
  const spun = await page.evaluate(async () => {
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.25);
    window.__surf.setTubeAngle(Math.PI / 2);               // on the wall, banked right over
    window.__surf.tick(0.15);
    const before = window.__surf.riderUp().dot;
    window.__surf.trick('spin');
    let worst = before;
    for (let i = 0; i < 25; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      if (!s.swTubeRide || s.wipe) break;
      worst = Math.min(worst, window.__surf.riderUp().dot);
    }
    return { before, worst };
  });
  check(spun.worst > 0.85, 'a spin in the tube turns the board about its own axis, not the world\'s',
        `board stayed on the wall through it — worst up·inward ${spun.worst.toFixed(3)}`);
}

// ---------- a blown landing in the tube ends the run ----------
// It used to score nothing and let you carry on, which is neither of the two honest rules.
{
  const blown = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.tick(0.2);
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.25);
    window.__surf.setTubeAngle(0);
    window.__surf.tick(0.12);
    // Held all the way down, so he is still turning when he arrives back at the wall.
    window.__surf.trick('flip');
    for (let i = 0; i < 200; i++) {
      window.__surf.tick(0.03);
      const s = window.__surf.state();
      if (s.wipe) return { ended: true, kind: s.lastWipe && s.lastWipe.kind, tube: s.lastWipe && s.lastWipe.tube };
      if (!s.swPipeAir && i > 6) return { ended: false, landed: true };
    }
    return { ended: false };
  });
  // Landing it squarely is a legitimate outcome of this: the point is that the tube stops
  // being a place where the question is not asked at all.
  check(blown.ended ? (blown.kind === 'land' || blown.kind === 'under') && blown.tube : blown.landed,
        'a trick in the tube is landed or blown, the same as anywhere else',
        blown.ended ? `blown, ended as ${blown.kind}` : 'landed it square');
}

// ---------- nothing is put in the tube to crash into ----------
// Wreckage went in as the one thing that still bit in there, and it was never a hazard you
// could play against: the ring owns your position while you are going round it, the drum
// arrives out of a wall you cannot see through, and the whole event is over in a frame. It
// was reported twice as a wipeout out of nowhere. A blown trick is the risk in there now.
{
  const drums = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.setDebris(true);   // let the game place its own
    window.__surf.tick(0.2);
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    let worst = null;
    for (let i = 0; i < 150; i++) {
      window.__surf.tick(0.03);
      const z = window.__surf.obstacleZ('debris');
      if (z !== null) worst = z;
      if (window.__surf.state().wipe) return { spawned: true, wiped: true };
    }
    return { spawned: worst !== null, worst };
  });
  check(!drums.spawned, 'the wave puts nothing in the tube to crash into',
        drums.spawned ? `a drum turned up at ${drums.worst}` : 'a barrel is water, all the way round');
}

// ---------- the shot does not pull back when you die ----------
// Last of the live checks, because it ends the run: anything after it would be
// measuring a dead game.
{
  // Crash in the barrel and the shot stays put. It used to cut to a wide chase on the crash,
  // which is the "camera zooms out when you die".
  const dead = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.tick(0.2);
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    window.__surf.tick(0.25);
    const before = window.__surf.cam();
    const tube = window.__surf.state().swTubeRide;
    const w = window.__surf.wipeNow('foam');
    window.__surf.tick(0.4);
    return { before, after: window.__surf.cam(), wiped: w.wiped, held: w.held, tube };
  });
  const jumped = Math.max(...['x','y','z','dx','dy','dz'].map(k => Math.abs(dead.before[k] - dead.after[k])));
  check(dead.wiped && dead.held && jumped < 0.02, 'and it does not pull back when the run ends in the barrel',
        `${jumped.toFixed(4)} of camera movement across the wipeout`
        + (dead.held ? '' : dead.tube ? ' — NO LOCK CAPTURED' : ' — WAS NOT IN THE TUBE'));
}

// ---------- and a crash off the top of the wave FALLS down it ----------
// "When I crashed or under rotated, character flew to right bottom when he should just fall
// from top of wave to bottom, kind of like real gravity." Two of the three launch terms were
// wrong for a crash on the ring: vx is the LANE velocity, which the ring overwrites every
// frame while it goes on integrating the steering underneath — metres a second of something
// he is not doing — and the vy floor threw him upward off a wall he was already twelve metres
// up. Plus a random sideways kick on top. He now leaves with the ring's own tangential motion
// and gravity has the rest.
{
  const c = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    for (let i = 0; i < 60; i++) { window.__surf.tick(1);
      const s = window.__surf.state(); if (s.swTubeRide && (s.swForm ?? 0) > 0.97) break; }
    for (let i = 0; i < 40; i++) { window.__surf.setTubeAngle(3.0); window.__surf.tick(0.05); }
    const a = window.__surf.state();
    if (!a.swTubeRide) return { skipped: true };
    window.__surf.invuln(false); window.__surf.wipeNow('foam');
    let low = a.py, side = 0;
    for (let i = 0; i < 16; i++) { window.__surf.tick(0.1); const s = window.__surf.state();
      side = Math.max(side, Math.abs(s.px - a.px)); low = Math.min(low, s.py); }
    return { fromY: a.py, fell: a.py - low, side };
  });
  check(!c.skipped && c.fell > 5 && c.side < c.fell * 0.35,
        'a crash off the top of the wave falls down it instead of flying sideways',
        c.skipped ? 'never got on the ring'
                  : `fell ${c.fell.toFixed(1)} m from ${c.fromY.toFixed(1)}, drifted ${c.side.toFixed(2)} m sideways`);
}

// ---------- the curtain lands on water, and the water reacts ----------
// The impact line — where the falling curtain meets the sea — is the one place on the wave
// where water is hitting water, and it was drawn as clean glass meeting painted foam. Three
// things now live there: churn baked into the shell (aFall), spray off the line, and the
// sea's own land band from v2.50. The churn is GATED on the peel rather than scaled by it —
// scaled, it read as nothing precisely where the line is on screen, which is the mid-peel.
{
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  check(/aFall/.test(src) && /churn=smoothstep/.test(src) && /fall\.push\(fv\*gate\)/.test(src),
        'the curtain carries churn at its foot, gated on the peel');
  const spray = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    let mid = null;
    for (let i = 0; i < 60; i++) { window.__surf.tick(1);
      const s = window.__surf.state();
      if (mid === null && (s.swForm ?? 0) > 0.30 && (s.swForm ?? 0) < 0.50) mid = s.footSplash;
      if (s.swTubeRide && (s.swForm ?? 0) > 0.97) break; }
    const c0 = window.__surf.state().footSplash;
    window.__surf.tick(4);
    const c1 = window.__surf.state().footSplash;
    const f = window.__surf.foot(0), st = window.__surf.state();
    return { mid, spawned: c1 - c0, off: Math.abs(f.x - st.swX), y: f.y };
  });
  check(spray.mid === 0, 'no spray before there is a curtain to land', `${spray.mid} splashes mid-formation`);
  check(spray.spawned > 4 && spray.spawned < 200,
        'the impact line spits, at a rate a phone can afford',
        `${spray.spawned} splashes in 4 s of simulation`);
  check(spray.off > 4 && spray.off < 16 && spray.y > -2 && spray.y < 3,
        'and the spray lands where the curtain does — out in front, at sea level',
        `${spray.off.toFixed(1)} m out from the crest, y ${spray.y.toFixed(2)}`);
}

// ---------- forming does not count against the ride ----------
// swRideMax was tuned when the barrel arrived finished; once it started FORMING during the
// ride phase, the ~4 s of formation quietly ate most of a 6-16 s window and short draws
// closed out two seconds after the tube finished forming — "the wave shows up and goes,
// then another one shows up". The spit clock must not run until the barrel exists.
{
  const clock = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    let during = null;
    for (let i = 0; i < 80; i++) { window.__surf.tick(0.5);
      const s = window.__surf.state();
      if (during === null && s.swTubeRide && (s.swForm ?? 0) > 0.5 && (s.swForm ?? 0) < 0.9)
        during = s.swRide;
      if ((s.swForm ?? 0) >= 0.97) break; }
    window.__surf.tick(2);                       // formed and parked: now it may run
    const after = window.__surf.state();
    return { during, after: after.swRide, max: after.swRideMax };
  });
  check(clock.during !== null && clock.during < 0.2,
        'the spit clock waits for the barrel to form',
        `${clock.during === null ? 'never sampled mid-formation' : clock.during.toFixed(2) + ' s on the clock at mid-formation'}`);
  check(clock.after > 1 && clock.max >= 10,
        'and runs once it has formed, against a window worth having',
        `${clock.after.toFixed(1)} s after 2 s parked, window ${clock.max.toFixed(1)} s`);
}

// ---------- the barrel does not let go, and the shot is frozen from its first frame ----------
// Steering hard away from the wave for the whole approach is the escape attempt: before the
// ring engages, the lane steering could carry him out through the mouth and leave him
// watching his own barrel from the flat. A daylight-side tether holds him in the bore. And
// the centred aim now arrives on the CHASE camera before the lock — so from the first frame
// the barrel owns the shot, nothing moves, aim included.
{
  const esc = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    window.__surf.setSteer(-window.__surf.state().swS * 0.95);
    let camAtLock = null;
    for (let i = 0; i < 200; i++) { window.__surf.tick(0.1);
      const s = window.__surf.state();
      if (camAtLock === null && s.swTubeRide) camAtLock = window.__surf.cam();
      if (s.swTubeRide && (s.swForm ?? 0) > 0.99) break; }
    window.__surf.setSteer(0);
    window.__surf.tick(2);
    const c2 = window.__surf.cam(), st = window.__surf.state();
    const drift = camAtLock ? Math.max(...['x','y','z','dx','dy','dz'].map(k => Math.abs(camAtLock[k] - c2[k]))) : 1e9;
    return { inTube: st.swTubeRide, off: Math.abs(st.px - st.ring.cx), r: st.ring.r, drift };
  });
  check(esc.inTube && esc.off < esc.r + 1.5,
        'steering away for the whole approach still ends inside the barrel',
        `${esc.off.toFixed(2)} m off the axis against a bore of ${esc.r.toFixed(2)} m`);
  check(esc.drift < 0.005,
        'and the shot is frozen from the first barrel frame, aim included',
        `${esc.drift.toFixed(4)} of drift between the first locked frame and two seconds on`);
}

// ---------- the foam bank: a body of whitewater, and only where the lip has landed ----------
// A volumetric mass on the impact line, in the shell's own frame. It must not exist before
// the curtain reaches the water — the first version hung at the tip mid-formation, a cloud
// sitting on nothing at the mouth.
{
  const bank = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    let mid = null;
    for (let i = 0; i < 100; i++) { window.__surf.tick(0.5);
      const s = window.__surf.state();
      if (mid === null && (s.swForm ?? 0) > 0.3 && (s.swForm ?? 0) < 0.6) mid = s.foamFade;
      if ((s.swForm ?? 0) >= 0.99) break; }
    window.__surf.tick(1);
    return { mid, full: window.__surf.state().foamFade };
  });
  check(bank.mid !== null && bank.mid < 0.05 && bank.full > 0.5,
        'the foam bank arrives with the landing, not before it',
        `fade ${bank.mid} mid-formation, ${bank.full} once the lip is down`);
}

// ---------- the foam is rideable, and riding it is a circle ----------
// "Make it so the player surfs and moves around and up them, so he can go in perfect circle
// movement." The puffs sit on the lip's own circle; where they have climbed, the ring's
// surface blends from the ray-marched water to that circle — so at full coverage the orbit
// is genuinely circular. History of this radius: a 5.07 m cliff at one angle, then a
// 4.9-7.5 m bulge through the open sector, now a circle to within centimetres.
{
  const circ = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('SWELL', 4.6);
    for (let i = 0; i < 80; i++) { window.__surf.tick(0.5);
      const s = window.__surf.state(); if (s.swTubeRide && (s.swForm ?? 0) > 0.99) break; }
    window.__surf.tick(2);
    const prof = window.__surf.ringProfile();
    const rs = (prof.rows || []).map(r => r.r);
    window.__surf.setSteer(0.9);
    const seen = [];
    for (let i = 0; i < 60; i++) { window.__surf.tick(0.15);
      const s = window.__surf.state(); if (!s.swTubeRide) break; seen.push(s.swRad); }
    window.__surf.setSteer(0);
    return { spanProf: rs.length ? Math.max(...rs) - Math.min(...rs) : 1e9,
             spanRide: seen.length > 30 ? Math.max(...seen) - Math.min(...seen) : 1e9,
             n: seen.length };
  });
  check(circ.spanProf < 0.6, 'the formed ring is a circle, foam included',
        `radius varies ${circ.spanProf.toFixed(2)} m round the whole ring`);
  check(circ.spanRide < 0.6, 'and a held turn rides that circle',
        `radius band ${circ.spanRide.toFixed(2)} m over ${circ.n} moving samples`);
}

// ---------- the treasure chest ----------
// Floats two metres off the water so it takes a jump to catch; banks for the end of the
// run; and the end-of-run ceremony is tap-tap-tap until it bursts and pays out.
{
  const caught = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    // riding flat THROUGH it must not collect it — the chest is a jump, not a drive-through
    window.__surf.plantCrate();
    window.__surf.tick(1.2);
    const flat = window.__surf.state().crateHeld;
    // now jump for it
    let held = false, peak = -9, dbg = null;
    for (let tries = 0; tries < 8 && !held; tries++) {
      window.__surf.plantCrate();
      window.__surf.tick(0.35);
      const jumped = window.__surf.jump();
      for (let i = 0; i < 30; i++) { window.__surf.tick(0.05);
        const st = window.__surf.state();
        const w = window.__surf.sampleWave(st.px, st.pz);
        peak = Math.max(peak, st.py - w.sea);
        if (st.crateHeld) { held = true; break; } }
      if (!held && dbg === null) {
        const st = window.__surf.state();
        dbg = { jumped, on: st.crateOn, ph: st.swPh, wipe: st.wipe, air: st.airborne };
      }
    }
    return { flat, held, peak, dbg };
  });
  // v2.84: the jump requirement is gone. Riding straight through a chest has to take it —
  // that was the whole complaint, and the old test asserted the opposite. The old second
  // check ("jumping catches it") is dropped rather than kept: once the flat pass collects,
  // crateHeld is already true when the jump loop starts, so it could no longer fail.
  check(caught.flat, 'riding through the chest collects it, no jump needed',
        caught.flat ? '' : `not collected — ${JSON.stringify(caught.dbg)}`);

  const coins0 = await page.evaluate(() => {
    const t = document.querySelector('#ovCoins'); return t ? +t.textContent : 0; });
  const shown = await page.evaluate(async () => {
    window.__surf.invuln(false);
    window.__surf.wipeNow('foam');
    window.__surf.tick(5);
    const ov = document.querySelector('#crateOv');
    return !!ov && !ov.classList.contains('hidden');
  });
  check(shown, 'the chest ceremony comes up when the run ends');
  // The chest is 3D now; the whole overlay is the tap target, and the words wait ~0.75 s
  // for the lid to swing before they appear.
  await page.evaluate(() => { const b = document.querySelector('#crateOv');
    for (let i = 0; i < 15; i++) b.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true })); });
  // the lid animation runs on real frames and the reveal on a timer, and under swiftshader
  // both can be slow — poll rather than guess
  await page.waitForFunction(() =>
    !document.querySelector('#crateReward').classList.contains('hidden'),
    null, { timeout: 15000 }).catch(() => {});
  const opened = await page.evaluate(() => ({
    reward: !document.querySelector('#crateReward').classList.contains('hidden'),
    btns: !document.querySelector('#crateBtns').classList.contains('hidden'),
    text: document.querySelector('#crateReward').textContent,
  }));
  check(opened.reward && opened.btns, 'tapping it open pays out and offers the three ways on',
        `reward: ${opened.text.trim().slice(0, 60)}`);
  await page.click('#crateMenu');
  const closed = await page.evaluate(() =>
    document.querySelector('#crateOv').classList.contains('hidden'));
  check(closed, 'and Main menu puts the ceremony away');
}

// ---------- the feature batch: haptics, glitter, goals, perks, share, tube chest ----------
{
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  check(/navigator\.vibrate/.test(src) && /CLOSE CALL/.test(src)
        && /RIDER_PERK\[rider\]/.test(src) && /id="shareBtn"/.test(src)
        && !/step\(0\.982,gh\)/.test(src),
        'haptics, close calls, rider perks and share are wired — and the sparkles are gone');

  // goals: three of them exist, and the run card shows them
  const goalsShown = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.tick(1);
    window.__surf.wipeNow('foam'); window.__surf.tick(5);
    const txt = document.getElementById('ovText').textContent;
    return { onCard: /Goals/.test(txt), rows: (txt.match(/\u2726|\u2736|✦/g) || []).length };
  });
  check(goalsShown.onCard && goalsShown.rows === 3,
        'three session goals stand on the run card', `${goalsShown.rows} rows`);

  // the chest in the barrel: hangs low over the pocket, a pipe jump reaches it
  const tube = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(1);
    window.__surf.armSetWave(); window.__surf.warpSetWave('RIDE');
    for (let i = 0; i < 40; i++) { window.__surf.tick(0.2);
      if (window.__surf.state().swTubeRide) break; }
    if (!window.__surf.state().swTubeRide) return { skipped: true };
    let held = false, before = null, planted = null, dbg = null;
    for (let tries = 0; tries < 4 && !held; tries++) {
      for (let i = 0; i < 30; i++) { window.__surf.setTubeAngle(0); window.__surf.tick(0.05); }
      planted = window.__surf.tubeChest();
      if (before === null) before = window.__surf.state().crateHeld;
      window.__surf.jump();
      for (let i = 0; i < 40; i++) { window.__surf.tick(0.05);
        if (window.__surf.state().crateHeld) { held = true; break; } }
      if (!held && dbg === null) { const st = window.__surf.state();
        dbg = { planted, tube: st.swTubeRide, on: st.crateOn, pipe: st.swPipeAir,
                px: +st.px.toFixed(1), c: +st.tubeC.toFixed(1) }; }
    }
    return { skipped: false, before, held, dbg };
  });
  // v2.85: no jump requirement anywhere, the barrel included — so `before` (not yet held the
  // instant it is planted) is no longer part of the claim. What still has to be true is that
  // a chest planted in the pocket is reachable at all from the ring.
  check(!tube.skipped && tube.held,
        'the barrel chest is caught off the ring, jump or no jump',
        tube.skipped ? 'never got on the ring'
                     : `held=${tube.held}` + (tube.held ? '' : ` ${JSON.stringify(tube.dbg)}`));
}

// ---------- the ruler reads the same water the game draws ----------
// sampleWave is what every "how far off the surface is he" check in this file measures
// against, and it was sampling at tNow while the ocean is drawn from waveClock — a clock that
// advances at 1.0+0.55*sin+0.28*sin and so drifts away without bound. Readings were against
// water from some other moment, out by as much as 1.4 m for reasons that had nothing to do
// with the rider.
{
  const src = await readFile(join(ROOT, 'index.html'), 'utf8');
  const hook = src.slice(src.indexOf('sampleWave:(x,z)=>'), src.indexOf('sampleWave:(x,z)=>') + 220);
  check(/sea:waveH\(x,z\+dist\*WAVE_DRIFT,waveClock\)/.test(hook),
        'the test hook samples the sea on the clock the ocean is drawn from');
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
  // The near wall is thinned by depth alone. Boring the hole toward the rider instead put a
  // patch of see-through water travelling round the tube with the board, which is the one
  // thing water never does — so nothing in this fade may depend on where he is.
  const curlFrag = src.slice(src.indexOf('float dcam=length(cameraPosition-vW);') - 1400,
                             src.indexOf('float dcam=length(cameraPosition-vW);') + 400);
  // And it is a fixed clearance in front of the lens, not "everything nearer than the rider":
  // that reached however far away he was and deleted the whole tube around the camera, roof
  // included, leaving open sky across the top of the screen.
  check(!/uRiderP/.test(src) && !/uRiderD/.test(src)
        && /a\*=1\.0-\(1\.0-smoothstep\(1\.30,3\.10,dcam\)\)\*uCut/.test(curlFrag),
        'the near wall is cut to a fixed clearance, with nothing tracking the rider',
        'no rider position in the curl shader');
  // The aeroplane is PLACED before it is shown. The FLY phase moves it, and that first move
  // is a frame away — so it used to be drawn for one frame wherever it had been left, which
  // after the banner tow is close overhead at 2.6 scale. That is the plane flashing across
  // the screen the instant you press play.
  const startWave = src.slice(src.indexOf('function startSetWave()'),
                              src.indexOf('function startSetWave()') + 1600);
  check(startWave.indexOf('plane.position.set(') < startWave.indexOf('plane.visible=true'),
        'the aeroplane is put where it belongs before it is made visible',
        'no one-frame flash on starting a wave');
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
