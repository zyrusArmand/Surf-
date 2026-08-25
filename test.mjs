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
// Playwright's default is thirty seconds and that is not enough here. This runs on software GL,
// and opening the rack draws forty-six board previews in one go — seven seconds on a quiet
// machine and past thirty on a busy one, at which point the FIRST tap of the suite times out and
// nothing runs at all. Raising it weakens nothing: every check still asserts what it asserted,
// they just get long enough to be asked. Confirmed as the machine and not the page by running
// the same sequence against the previous commit, which failed exactly the same way.
page.setDefaultTimeout(120000);

// models/*.glb are OPTIONAL overrides — drop one in and it replaces the built-in shape,
// leave it out and the procedural version stands (models/README.md). Most of the table is
// absent by design, so those 404s are the documented path, not a fault. The trailing digit
// is the same arrangement one level down: every obstacle also asks for a SECOND model of
// itself — buoy2.glb and so on — so a kind can have more than one look, and all but one of
// those are missing too. Anything else that 404s is a genuinely missing asset.
const EXPECTED_404 = /\/models\/[a-z]+[0-9]*\.glb$/;

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

// ---------- the loading screen ----------
// Sixty-five megabytes of models arrive here, and they used to land in a title screen that was
// already up: the beach dressed itself in front of you, palm then chest then rider then sign.
// A sheet goes over that now until they are all in — which means the suite has to wait for it
// too, and would have failed on its first tap otherwise, since a sheet over the game is a sheet
// over the buttons.
// Sampled on the way past rather than only at the end, because the only moment a loading screen
// can be observed loading is while it is. On a local server most of this is over before the
// first sample lands, so what is asked of the samples is asked of however many there are.
const loadSeen = [];
await page.waitForFunction(() => !window.__surf || !window.__surf.loading || window.__surf.loading().gone,
                           null, { timeout: 180000 }).catch(() => {});
for (let i = 0; i < 40; i++) {
  const L = await page.evaluate(() => window.__surf && window.__surf.loading ? window.__surf.loading() : null);
  if (L) loadSeen.push(L);
  if (L && L.gone) break;
  await page.waitForTimeout(250);
}
const last = loadSeen[loadSeen.length - 1];
// Read AFTER the fade, not at the moment it starts. It stops taking taps the instant it begins
// to go — half a second of an invisible sheet eating the first tap is exactly the kind of thing
// nobody reports and everybody feels — but it is still displayed for the length of the fade, and
// asking then says it is still up when what it is doing is leaving.
await page.waitForTimeout(900);
const done = await page.evaluate(() => window.__surf && window.__surf.loading ? window.__surf.loading() : null);
check(!!done && done.gone && !done.blocking && !done.up,
      'the loading screen gets out of the way once everything is in',
      done ? `gone ${done.gone}, still displayed ${done.up}, still taking taps ${done.blocking}, ${done.pct}%` : 'no loading hook');
check(!!last && last.gone && !last.blocking,
      'and stops taking taps the moment it starts to leave, rather than when it has left',
      last ? `at the first frame of the fade: taking taps ${last.blocking}, displayed ${last.up}` : '');
// It was really counting the models, not ticking a timer. Every loadModel takes a ticket and
// every ticket comes back — including the ones that 404, which is most of the table and the one
// way a counter like this hangs for ever.
check(!!last && last.issued > 10 && last.settled === last.issued && last.waiting.length === 0,
      'and every model it was waiting on came back, the missing ones included',
      last ? `${last.settled} of ${last.issued} settled, still waiting on [${last.waiting.join(', ')}]` : 'no loading hook');
// and it was weighing BYTES. A fifteen megabyte ant and a two hundred kilobyte log are one file
// each; a bar that treats them alike sits still for most of the wait and then jumps.
check(!!last && last.bytes.total > 10e6 && last.bytes.loaded === last.bytes.total,
      'and it measured the download in bytes rather than in files',
      last ? `${(last.bytes.loaded / 1e6).toFixed(1)} of ${(last.bytes.total / 1e6).toFixed(1)}MB across ${last.issued} requests` : '');
// never backwards. Guessed weights get better as real totals arrive and that can nudge the true
// fraction down; a bar that retreats looks broken even when it is being the more honest one.
const back = loadSeen.filter((l, i) => i && l.pct < loadSeen[i - 1].pct - 0.01);
check(back.length === 0, 'and its bar never ran backwards across every reading taken of it',
      `${loadSeen.length} reading${loadSeen.length === 1 ? '' : 's'}` +
      (loadSeen.length < 3 ? ' — over a local server most of this is done before the first one lands, so this is weak evidence here and the guarantee is the clamp in paint()' : '') +
      `, worst step back ${back.length ? Math.max(...back.map((l, i) => loadSeen[i].pct - l.pct)).toFixed(1) + '%' : 'none'}`);
// and it really is a sheet over the whole game while it is up, rather than a card behind it.
// Asked structurally, by putting it back and asking the document what is on top, because the
// moment it is genuinely up is over before a check can be written against it.
const cover = await page.evaluate(() => {
  const el = document.getElementById('load');
  if (!el) return null;
  const keep = el.style.display;
  el.style.display = 'flex'; el.classList.remove('gone');
  const r = el.getBoundingClientRect(), st = getComputedStyle(el);
  const mid = document.elementFromPoint(innerWidth / 2, innerHeight / 2);
  const corner = document.elementFromPoint(6, innerHeight - 6);
  const out = { w: r.width / innerWidth, h: r.height / innerHeight, z: +st.zIndex,
                mid: !!(mid && el.contains(mid)), corner: !!(corner && el.contains(corner)),
                label: (document.getElementById('loadPct') || {}).textContent || '' };
  el.style.display = keep; el.classList.add('gone');
  return out;
});
check(cover && cover.w > 0.99 && cover.h > 0.99 && cover.z >= 120 && cover.mid && cover.corner,
      'and while it is up it covers the whole game, not just the middle of it',
      cover ? `${(cover.w * 100).toFixed(0)}% by ${(cover.h * 100).toFixed(0)}% at z ${cover.z}, ` +
              `on top at the centre ${cover.mid} and in the corner ${cover.corner}` : 'no loading screen');
// and it says what it is doing. A bar and a number say how long; they do not say what for, and
// on a phone this is a real wait.
check(!!cover && /Loading the beach/.test(cover.label) && /%/.test(cover.label),
      'and it says what it is waiting for, not just how far along it is',
      cover ? JSON.stringify(cover.label) : '');

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
// The main menu is the home dial now and its Play is #dPlay; #startBtn is the pill on the
// wipeout card ("Surf again"). Click whichever one is actually on screen.
const startRun = () => page.evaluate(() => {
  const d=document.getElementById('dPlay');
  if(d && d.offsetParent!==null){ d.click(); return 'dial'; }
  document.getElementById('startBtn').click(); return 'pill';
});
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
  // v2.88: the main menu is the home dial, so the Shop PILL is hidden there — Shop on the
  // dial is the button that opens the rack now.
  // v2.91: Board opens board school instead, so it gets its own row below.
  ['#dShop', '#shop', '#shopClose', 'shop, from the home dial'],
  ['#dBoard', '#guide', '#gClose', 'board school, from the home dial'],
  ['#dTricks', '#howto', '#howClose', 'the trick sheet, from the home dial'],
  ['#dStats', '#mystats', '#myClose', 'your surfing, from the home dial'],
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

// ---------- board school ----------
// The panel is a claim about the rack — that every board in the shop is one of twelve
// shapes — so the check is that the claim holds: no board missed, none filed twice, and
// tapping a silhouette really does swap the writing AND the board being offered.
await page.click('#dBoard');
await page.waitForTimeout(500);
const school = await page.evaluate(async () => {
  const rail = [...document.querySelectorAll('#gRail .gsil')];
  const name = () => document.querySelector('#guide .gname b').textContent;
  const pick = () => document.querySelector('#guide .card .nm').textContent;
  const before = { type: name(), board: pick() };
  // the last shape on the rail is the longboard, which shares no boards with the shortboard
  // the panel opens on — so both halves of the card have to change. Still the last one now
  // that the three that are not surfboards lead the rail rather than close it: they are the
  // three shortest things on the page and the rail is in length order.
  // an SVG <g> has no click() — the rack listens for the bubbled event instead
  rail[rail.length - 1].dispatchEvent(new MouseEvent('click', { bubbles: true }));
  await new Promise(r => setTimeout(r, 250));
  const after = { type: name(), board: pick() };
  // and the families, against the rack itself
  const ids = window.__surf ? window.__surf.boardIds() : null;
  const filed = window.__surf ? window.__surf.typeIds() : null;
  const seen = {}, dupes = [];
  for (const id of filed || []) { if (seen[id]) dupes.push(id); seen[id] = 1; }
  return {
    shapes: rail.length,
    before, after,
    missing: (ids || []).filter(id => !seen[id]),
    dupes,
    stray: (filed || []).filter(id => !(ids || []).includes(id)),
    bars: document.querySelectorAll('#guide .card .seg i.on').length,
  };
});
// The picture is lofted and photographed one board per frame after the panel is up, and a
// frame here is a third of a second — so this waits for it rather than guessing at a delay.
const shot = await page.waitForFunction(() => {
  const im = document.querySelector('#guide .card .thumb');
  return im && im.src.slice(0, 15) === 'data:image/png;' ? im.src.length : false;
}, null, { timeout: 60000 }).then(h => h.jsonValue()).catch(() => 0);
check(school.shapes === 8, 'board school draws the five surfboards and the three that are not',
      `${school.shapes} shapes`);
// Every silhouette is generated from a real board's spec and stands on the rule, so a
// shape's drawn height must BE its stated length — nothing added past the nose or tail for
// a blunt end, and nothing that moves when it is selected. One scale for all eight is the
// claim the whole rack rests on, so it is measured rather than eyeballed.
// The rack is drawn from spec sheets and the shop's boards are cut to the same numbers, so
// the two cannot be allowed to drift: every board in a family carries its family's outline
// curve exactly, and its width is its own length divided by that type's real length-to-width
// ratio. If a board is ever reshaped by hand, this is what catches it.
const cut = await page.evaluate(() => {
  const bad = [];
  for (const t of window.__surf.types()) {
    const r = t.real, ratio = r.L / r.W;
    for (const id of t.ids) {
      const s = window.__surf.boardOutline(id);
      const off = k => Math.abs(s[k] - r[k]) > 0.002;
      if (off('noseA') || off('tailA') || off('noseW') || off('tailW'))
        bad.push(`${id} outline != ${t.key}`);
      if (Math.abs(s.L / (2 * s.W) - ratio) > 0.02)
        bad.push(`${id} ${(s.L / (2 * s.W)).toFixed(2)}:1 vs ${t.key} ${ratio.toFixed(2)}:1`);
      // and the tail its type is defined by. A fish without a swallow is not a fish, which
      // is exactly what three of them were: the notch was only kept on boards that already
      // had one instead of coming from the type.
      const wantSwallow = r.swallow ? s.L * r.swallow / r.L : 0;
      if (Math.abs((s.swallow || 0) - wantSwallow) > 0.01)
        bad.push(`${id} swallow ${(s.swallow || 0).toFixed(2)} want ${wantSwallow.toFixed(2)}`);
      // and the rocker, which is as much of what makes a gun a gun as the outline is —
      // seven and a half inches of nose lift against a fish's four. It is stated in inches
      // and does NOT scale with length: a ten foot log has about the nose rocker of a six
      // foot shortboard, which is exactly why a log is flat and a shortboard is not.
      for (const [k, want] of [['rockN', r.rockN / 12], ['rockT', r.rockT / 12],
                               ['noseR', r.noseR], ['tailR', r.tailR]])
        if (Math.abs(s[k] - want) > 0.002)
          bad.push(`${id} ${k} ${s[k]} want ${want.toFixed(3)}`);
    }
  }
  return bad;
});
check(cut.length === 0, 'every board in the shop is cut to its type\'s spec sheet',
      cut.length ? cut.slice(0, 4).join('; ') : '46 boards, 8 spec sheets');
// Not the numbers that went in — the board that came OUT. Every board is lofted and its
// plan outline measured off the mesh, then held against the two widths its type's spec sheet
// publishes: twelve inches from the nose and twelve from the tail. This is the check that
// answers "is it actually the shape it says it is", as opposed to "does it carry the right
// parameters", and the two are not the same question.
const built = await page.evaluate(() => {
  const rows = [];
  for (const t of window.__surf.types()) {
    const r = t.real, f = 12 / r.L;
    for (const id of t.ids) {
      // Bins coarse enough for the sparsest mesh in the shop — the boards are lofted at
      // anywhere from 160 to 340 rings, and bins finer than the rings read as zero width.
      const p = window.__surf.boardProfile(id, 100);
      if (!p) { rows.push({ id, type: t.key, n: 0, t: 0, wantN: r.n12, wantT: r.t12 }); continue; }
      const N = p.hw.length, max = Math.max.apply(null, p.hw);
      const at = fr => { const x = fr * (N - 1), i = Math.max(0, Math.min(N - 2, Math.floor(x)));
        return p.hw[i] * (1 - (x - i)) + p.hw[i + 1] * (x - i); };
      rows.push({ id, type: t.key, wantN: r.n12, wantT: r.t12,
                  asym: window.__surf.boardOutline(id).asym,
                  n: +(at(f) / max * r.W).toFixed(2), t: +(at(1 - f) / max * r.W).toFixed(2) });
    }
  }
  return rows;
});
{
  // A board may be out by an inch and a bit. Two families genuinely are, and both were
  // written down when the curve was fitted: a fish's rails run nearly parallel from the
  // swallow tips to a foot up and this curve will not hold that, and an eFoil's outline is
  // not really a surfboard's at all. Everything else lands inside half an inch. The bin
  // width itself is worth a tenth or two on top.
  const off = r => Math.max(Math.abs(r.n - r.wantN), Math.abs(r.t - r.wantT));
  const worst = built.reduce((a, b) => off(b) > off(a) ? b : a, built[0]);
  const bad = built.filter(r => off(r) > 1.5);
  // and every board of a type must measure the SAME, because they are the same outline at
  // different lengths — this is what caught a family reading thirteen inches apart
  // — except the one board that declares itself two different boards. The Asymmetric runs a
  // longer rail on the forehand and cuts its tail forward on the backhand, so the widest
  // point of a slice comes off whichever half is wider there and it reads an inch narrower
  // at the tail than its siblings. That is the board, not a fault, and it is read off the
  // spec rather than named here so the next asymmetric shape is covered too.
  const byType = {};
  for (const r of built) if (!r.asym) (byType[r.type] ||= []).push(r);
  const ragged = Object.keys(byType).filter(k => {
    const g = byType[k];
    return Math.max(...g.map(r => r.n)) - Math.min(...g.map(r => r.n)) > 0.5 ||
           Math.max(...g.map(r => r.t)) - Math.min(...g.map(r => r.t)) > 0.5;
  });
  check(bad.length === 0 && ragged.length === 0 && built.length === 46,
        'every board, as built, measures its type\'s published nose and tail widths',
        `${built.length} boards, worst ${worst.id} ${off(worst).toFixed(2)}" ` +
        `(${worst.n}/${worst.wantN} nose, ${worst.t}/${worst.wantT} tail)` +
        (bad.length ? ` — over 1.5": ${bad.map(r => r.id).join(', ')}` : '') +
        (ragged.length ? ` — ragged families: ${ragged.join(', ')}` : ''));
}

const scale = await page.evaluate(() => {
  const types = window.__surf.types();
  return [...document.querySelectorAll('#gRail .gsil')].map(g => {
    const t = types.find(t => t.key === g.dataset.k);
    return { key: g.dataset.k, ft: t.real.L / 12,
             px: +g.querySelector('path').getBBox().height.toFixed(1) };
  });
});
const perFt = scale.map(s => s.px / s.ft);
const spread = Math.max(...perFt) - Math.min(...perFt);
check(scale.length === 8 && spread / perFt[0] < 0.01,
      'every shape measures its own length against the rule',
      scale.map(s => `${s.key} ${s.ft}ft=${s.px}`).join(' '));
check(school.before.type !== school.after.type && school.before.board !== school.after.board,
      'tapping a shape swaps both the writing and the board on offer',
      `${school.before.type}/${school.before.board} -> ${school.after.type}/${school.after.board}`);
check(school.missing.length === 0 && school.dupes.length === 0 && school.stray.length === 0,
      'every board in the rack is filed under exactly one shape',
      `missing=${JSON.stringify(school.missing)} dupes=${JSON.stringify(school.dupes)} stray=${JSON.stringify(school.stray)}`);
check(school.bars > 0 && shot > 1000,
      'the shop pick carries its picture and its stat bars',
      `bars=${school.bars} picture=${shot} bytes`);
// the matcher: two taps, one shape named
const matched = await page.evaluate(async () => {
  document.getElementById('gMatch').click();
  await new Promise(r => setTimeout(r, 120));
  document.querySelector('#qWave button').click();
  document.querySelector('#qSkill button').click();
  await new Promise(r => setTimeout(r, 200));
  return { out: document.getElementById('qOut').textContent,
           type: document.querySelector('#guide .gname b').textContent };
});
check(matched.out.includes(matched.type) && matched.type.length > 0,
      'the matcher names a shape and the panel jumps to it',
      `${matched.type} :: ${matched.out.slice(0, 60)}`);
await page.click('#gClose');
await page.waitForTimeout(300);

// ---------- the menu is a beach, and it gives everything back ----------
// It borrows the two objects the game rides — the equipped board and the rider — and stands
// them on the preview's beach. Every way out of the menu has to hand them back, or you press
// play and surf on nothing. The board school and the full-screen look go through the same
// door, so both are checked as well as play.
{
  // The beach is built by the render loop on the frame after the menu appears, and under
  // swiftshader a frame is a third of a second — so asking straight away asks too early.
  // Wait for it to come up rather than assuming it already has.
  await page.waitForFunction(() => window.__surf.menuState().up, null, { timeout: 30000 })
    .catch(() => {});
  const onMenu = await page.evaluate(() => window.__surf.menuState());
  // What the player experiences as "it lags and then loads": the menu paints the instant the
  // page is parsed, and the beach is built on the first frame that asks for it — sixty
  // thousand vertices of dune and a palm, with the page frozen for all of it. It was 1.7s.
  // Asked here, before anything has opened the shop or the viewer, because that is also
  // where the far palms get built and they are meant NOT to be built yet.
  const boot0 = await page.evaluate(() => window.__surf.boot());
  // The beach is built inside the blocking script, not on the first frame, so the first paint
  // IS the finished screen. Asked of the running game rather than of the source: the previous
  // version of this was a regex, it matched the crate ceremony's button handler, and the line
  // it was looking for had in fact been bolted onto that handler by mistake and never ran at
  // boot at all. A check that can pass while the feature does nothing is worse than none.
  check(boot0.beforeFirstFrame === true,
        'the beach is standing before the script returns, not a frame later',
        `${boot0.why || 'built at boot'}`);
  // Depth of field, on the one screen that can pay for a second pass over the scene. The
  // menu is a still life — a board and a rider under a tree, beach running away behind them
  // and sand running toward the camera in front — and photographing that with anything wider
  // than f/8 sends both soft while the subject stays sharp. The eye reads that softness as
  // DISTANCE, which is the strongest depth cue there is and the one a render gets for free
  // by not having. It focuses on the board, because the board is what the screen is for.
  const dof = await page.evaluate(async () => {
    const s = window.__surf.menuState();
    if (!s.up) return null;
    await new Promise(r => requestAnimationFrame(r));
    await new Promise(r => requestAnimationFrame(r));
    return window.__surf.fx();
  });
  check(dof && dof.depth && dof.dofLive === 1 && dof.focus > 5 && dof.focus < 80,
        'the menu is shot with a real lens, focused on the board',
        dof ? `focus ${dof.focus}ft, sharp for ${dof.range}ft either side` : 'menu not up');

  // A panel dims the beach and used to leave the home dial visible behind it, which sounds
  // harmless: what it produced was the panel's row dividers running left and right THROUGH
  // the glass buttons, so the screen showed a white line joining one button to the next.
  // Watched with an observer rather than hooked into each open and close, because there are
  // four panels and several ways into each, and a class that has to be taken off by hand in
  // eight places is a class that gets left on.
  const panel = await page.evaluate(async () => {
    const dial = () => getComputedStyle(document.getElementById('homeDial')).display;
    const before = dial();
    document.getElementById('dSettings').click();
    await new Promise(r => setTimeout(r, 120));
    const during = dial();
    document.getElementById('setClose').click();
    await new Promise(r => setTimeout(r, 120));
    return { before, during, after: dial() };
  });
  check(panel.before !== 'none' && panel.during === 'none' && panel.after !== 'none',
        'the dial gets out of the way while a panel is open, and comes back',
        JSON.stringify(panel));


  check(boot0.beach > 0 && boot0.beach < 1000,
        'the beach stands up fast enough not to read as a hang',
        `${boot0.beach}ms, of which ${boot0.sand}ms is ${boot0.sandVerts} sand vertices`);
  check(boot0.sandVerts < 70000,
        'and the sand spends its vertices across the shore, where the short waves are',
        `${boot0.sandVerts} vertices`);
  // Counted at the moment the beach finished, not now — by the time the suite reaches here
  // something may legitimately have opened a viewer and paid for them.
  check(boot0.palmsAtBoot === 0,
        'and the far palms the menu hides are not built for it',
        `${boot0.palmsAtBoot} standing when the beach was done`);
  const viaShop = await page.evaluate(async () => {
    document.getElementById('dShop').click();
    await new Promise(r => setTimeout(r, 60));
    const s = window.__surf.menuState();
    document.getElementById('shopClose').click();
    return s;
  });
  await page.waitForTimeout(400);
  check(onMenu.wanted && onMenu.up && onMenu.riderOnBeach && onMenu.boardOnBeach,
        'the main menu stands the board and the rider on a beach',
        JSON.stringify(onMenu));
  check(!viaShop.riderOnBeach && !viaShop.boardOnBeach,
        'and hands them back the moment the shop opens',
        JSON.stringify(viaShop));
  // ---- and a chest sitting on the sand in front of the tree ----
  // Placing this cost four rounds and every one failed differently: the props group carries a
  // transform of its own, the shot is turned along the beach so world +x is not screen-right,
  // the sand slopes so coming forward means coming down, and the reading was taken through
  // the wrong camera. So it is checked in the FRAME — where it lands in pixels, through the
  // camera that actually drew it — and against the ground it stands on, which are the two
  // things "it is not where I wanted it" ever means.
  {
    await page.waitForFunction(() => window.__surf.chestInfo && window.__surf.chestInfo().beach,
                               null, { timeout: 30000 }).catch(() => {});
    // ASKED WHILE THE BEACH IS UP. It comes down whenever a panel opens, and the reading then
    // comes back through the game's camera, which is out on a wave — a chest 58ft away in a
    // shot that is 21ft deep, which reads exactly like a placement bug and is not one.
    const look = async () => {
      await page.waitForFunction(() => window.__surf.menuState().up, null, { timeout: 20000 })
        .catch(() => {});
      await page.waitForTimeout(500);
      return page.evaluate(() => window.__surf.chestScreen());
    };
    const inFrame = c => c && c.cam === 'menu' && c.chest[0] > c.size[0] &&
                          c.chest[0] < c.screen[0] && c.top[1] > 0 && c.chest[1] < c.screen[1];
    const wide = await look();
    check(inFrame(wide) && wide.camDist < wide.palmDist,
          'a chest sits on the sand in front of the palm, in shot rather than behind the trunk',
          wide ? `at ${wide.chest} of ${wide.screen}, ${wide.camDist}ft out against the tree at ${wide.palmDist}ft`
               : 'no beach chest');
    // ...and the shot is composed on the BOARD. It is what the shop sells and what the Board
    // button changes, and the tree and the rider are arranged around it — so it is the thing
    // the camera is pointed at, near enough the middle of the frame to read as the subject.
    check(wide && Math.abs(wide.board[0] - wide.screen[0] / 2) < wide.screen[0] * 0.08,
          'and the board is what the shot is composed on, near the middle of the frame',
          wide ? `board at ${wide.board[0]} of ${wide.screen[0]}, ` +
                 `${(wide.board[0] - wide.screen[0] / 2).toFixed(0)}px off centre` : 'no board');
    check(wide && Math.abs(wide.bottom - wide.sand) < 0.05,
          'and it is standing ON the sand rather than sunk into it — two solid things',
          wide ? `base ${wide.bottom}, sand ${wide.sand}` : 'no beach chest');
    // It is placed off the TREE. It used to be placed off the camera — which walks ten feet
    // back on a narrow screen and stands close on a wide one — because a chest nine feet out
    // on open sand in portrait dropped clean off the bottom in landscape. Leaning on the
    // trunk it does not need that: two and a half feet from a tree that is framed on every
    // shape of screen is in frame on every shape of screen, and measuring from the lens had
    // started putting the two shapes eight feet apart in the world with only a clamp between
    // them and the chest ending up behind the trunk. So the invariant flips: what must match
    // across shapes is the distance to the TREE, not the distance to the camera.
    const foot = c => Math.hypot(c.chestWorld[0] - c.palmWorld[0], c.chestWorld[2] - c.palmWorld[2]);
    await page.setViewportSize({ width: 430, height: 932 });
    const tall = await look();
    check(inFrame(tall) && Math.abs(foot(tall) - foot(wide)) < 0.25,
          'and it holds its place at the foot of the tree when the screen changes shape',
          tall ? `portrait ${foot(tall).toFixed(2)}ft from the trunk at ${tall.chest}, ` +
                 `landscape ${foot(wide).toFixed(2)}ft at ${wide.chest}`
               : 'no beach chest');
    check(tall && foot(tall) < 3.2 && foot(tall) > 1.2,
          'and it is leaning on the trunk rather than sitting out on open sand',
          tall ? `${foot(tall).toFixed(2)}ft between the two, box ${tall.size[0]}ft across`
               : 'no beach chest');
    await page.setViewportSize({ width: 1024, height: 640 });
    await page.waitForTimeout(600);
    // and turned so the OPEN side faces you. The lid stands up on the hinge, so the highest
    // point of the mesh is the lid, and the lid belongs at the back — turned the other way it
    // stands between the camera and the treasure it exists to show.
    const lid = await page.evaluate(() => window.__surf.chestScreen());
    check(lid && lid.lidBehind > 0.15,
          'and its open lid stands at the back, so you look into it rather than at it',
          lid ? `lid ${lid.lidBehind}ft further from the lens than the box` : 'no beach chest');
  }
  // ---- and the sea stops at the sand rather than lying about on it ----
  // A beach is where the water and the sand CROSS, and with a foot and a third of swell still
  // running at the crossing they did not cross, they interleaved: over a thirty-unit band the
  // wave tops stood above the sand and the troughs fell below it, and from a camera down on
  // the beach at a grazing angle that band read as tongues of blue lying on dry sand. Hiding
  // the sea and finding the sand clean underneath is what settled that it was the water and
  // not the beach's own painting.
  //
  // The swell dies as it shoals now, and the guarantee is exact rather than approximate: at
  // the waterline the displacement is identically zero, so the sea there is a FLAT PLANE, and
  // a flat plane crosses a rising beach exactly once. Which makes the thing to check the one
  // relationship that never used to exist — that the row the swell dies at and the row the
  // water's edge falls on are the same row. Typing either in by hand is how they drift.
  {
    const sh = await page.evaluate(() => window.__surf.shoreLine());
    check(sh && Math.abs(sh.taperZ - sh.waterline) < 0.05,
          'the swell dies exactly where the water meets the sand, not somewhere near it',
          sh ? `swell flat from ${sh.taperZ}, waterline at ${sh.waterline}, over ${sh.width} units`
             : 'no beach sea');
    check(sh && sh.inshore > sh.seaY && sh.offshore < sh.seaY,
          'and the beach crosses that flat water once — dry inshore of it, drowned outside it',
          sh ? `sand ${sh.inshore} ten units in, ${sh.offshore} ten units out, water at ${sh.seaY}`
             : 'no beach sea');
    check(sh && sh.amp > 0.5,
          'and there is still a real swell out where the water is deep enough for one',
          sh ? `amplitude ${sh.amp}` : 'no beach sea');
    check(sh && sh.gameShore < 0,
          "and the game's own ocean, which has no shore in it, is left alone",
          sh ? `uShore ${sh.gameShore}` : 'no beach sea');
  }

  // ---- every rigged rider stands on the sand and faces out ----
  // Both of these were true of the pug and of nobody else, and both for the same reason: the
  // title screen's performance is written for a file that ships the whole repertoire, and the
  // two riders that do not ship it fell off the end of it. The height that stands him on the
  // sand was taken on beat ZERO, so a model without beat zero's clip skipped it and never had
  // one — the cat went into his handstand and put his face through the beach. And the turn
  // toward the camera was applied after the beats, so a model with none of those clips took
  // an early return and stood there side-on in his surfing stance.
  {
    const beach = [];
    for (const id of await page.evaluate(() => window.__surf.riderKinds())) {
      await page.evaluate(c => window.__surf.wear(c), id);
      await page.waitForTimeout(1400);
      const f = await page.evaluate(() => window.__surf.menuFace());
      const g = await page.evaluate(() => {
        const out = [];
        for (let i = 0; i < 5; i++) out.push(window.__surf.showStep(i));
        return out;
      });
      const r = await page.evaluate(() => window.__surf.rigInfo());
      beach.push({ id, off: f && f.off, ground: g[0] && g[0].ground, beats: g,
                   clips: r.clipNames || [], stand: r.stand });
    }
    check(beach.length >= 4 && beach.every(b => b.stand === true),
          'every character with a body of his own can do a handstand, whatever his file shipped',
          beach.map(b => `${b.id} ${b.clips.length} clips`).join(', '));
    check(beach.length >= 3 && beach.every(b => b.off !== null && Math.abs(b.off) < 8),
          'every rigged rider on the title screen stands square to the camera',
          beach.map(b => `${b.id} ${b.off}°`).join(', '));
    check(beach.every(b => b.ground !== null && b.ground !== undefined &&
                           b.beats.every(s => s && s.ground === b.ground)),
          'and each of them has a height on the sand that every beat of his show holds to',
          beach.map(b => `${b.id} ${b.ground === null ? 'none' : (+b.ground).toFixed(2)}`).join(', '));
    // AND EVERY ONE OF THEM DOES EVERY MOTION. They come off the same biped with the same
    // bone names, and each export shipped a different half of the repertoire — so the clips
    // are pooled and anything one of them brought, all of them can perform. The five beats of
    // the title-screen show are the test of it: a rider who cannot do a beat skips it, so
    // "every beat ran, in order" is the same statement as "he has every clip".
    check(beach.every(b => b.beats.every((s, i) => s && s.step === i)),
          'and every one of them performs the whole show, not the half his own file shipped',
          beach.map(b => `${b.id} ${b.beats.map(s => s && s.step).join('')}`).join(', '));
    await page.evaluate(() => { window.__surf.wear('pug'); });
    await page.waitForTimeout(800);
  }
  // but they ARE built the moment something that wants them is opened
  const boot1 = await page.evaluate(() => window.__surf.boot());
  check(boot1.palms >= 0, 'but are built the moment you leave the menu for something that shows them',
        `${boot1.palms}ms`);
  // He rides at pug size — under two feet — which beside a six foot board made the shot
  // read as a toy on a beach. A world unit here is a FOOT, so an average adult is 5'9" and
  // that is what he is scaled to, whatever the character and whatever is leaning on the
  // tree. Both are measured off their own boxes, not off the numbers that placed them.
  await page.waitForTimeout(400);
  const sizes = [];
  for (const id of ['pug', 'ant', 'sloth']) {
    const f = await page.evaluate(async c => {
      window.__surf.wear(c);
      await new Promise(r => setTimeout(r, 1200));
      return window.__surf.menuFigures();
    }, id).catch(() => null);
    if (f) sizes.push({ id, ...f });
  }
  // The board is not placed, it is SOLVED: its lowest point rests on the sand under it and
  // it touches the trunk without going into it. Arithmetic off half the published length
  // had it floating clear — the mesh is not the spec sheet, because rocker, rails and the
  // deck-alignment shift all move where the ends really are — and nothing at all held it
  // against the tree, so it crossed straight through the trunk. Both conditions, on boards
  // at both ends of the rack: a 4'3" skimboard, a 6' shortboard and a 10' log.
  const stood = [];
  for (const id of ['astro', 'carbonskim', 'noserider', 'bubblegum']) {
    const f = await page.evaluate(async b => { window.__surf.equip(b);
      await new Promise(r => setTimeout(r, 1200)); return window.__surf.menuFigures(); }, id);
    if (f) stood.push({ id, ...f });
  }
  check(stood.length === 4 && stood.every(f => Math.abs(f.foot - f.sand) < 0.02),
        'the board it stands there rests its foot on the sand, whatever it is',
        stood.map(f => `${f.id} ${(f.foot - f.sand).toFixed(3)}ft off`).join(', '));
  // WHICHEVER END HOLDS IT UP IS ON THE TRUNK. There are two ways a board leans on a tree
  // and the game solves for both: a short one meets the pole with its NOSE, and one longer
  // than the palm rests its BODY in the crook with the nose past the crown. Which of the two
  // happens depends on the board and on how thick the tree is, so asking only about the nose
  // failed a tree that was propping the board up perfectly well by its middle. The other
  // reading is always negative when the first is at contact — board and trunk both rise out
  // of the same sand, so below the contact point they are inside each other's clearance by
  // construction — which is why this is the nearer of the two and not the further.
  const holds = f => Math.min(Math.abs(f.gap === null ? 9 : f.gap),
                              Math.abs(f.body === null ? 9 : f.body));
  check(stood.length === 4 && stood.every(f => holds(f) < 0.30),
        'and leans on the trunk without going through it',
        stood.map(f => `${f.id} nose ${f.gap} body ${f.body} at ${f.standoff}ft out`).join(', '));
  // in FRONT of the tree, which is where a board leaning on one is in every photograph of
  // it. Smaller along the camera's forward axis is nearer the camera.
  check(stood.length === 4 && stood.every(f => f.boardF < f.trunkF - 0.4),
        'and stands in front of the tree, not behind it',
        stood.map(f => `${f.id} ${(f.trunkF - f.boardF).toFixed(2)}ft clear`).join(', '));
  // and the palm is a tree rather than the six foot shrub it was, which is what made a ten
  // foot log leaning on it tower over the whole thing
  check(stood.every(f => f.palmH > 13),
        'and the palm it leans on is a tree', `${stood[0] && stood[0].palmH}ft tall`);
  // NO TWO PALMS ARE THE SAME TREE. The model is one straight trunk with a bone chain up it,
  // and every palm in the game is that same mesh bent a different amount — so the failure
  // this guards against is the one three.js hands you for free: clones of a skinned mesh
  // share a skeleton, and a grove that shares a skeleton is one tree in five copies. Read
  // off each finished trunk's own centreline rather than off the angles that bent it.
  const leans = await page.evaluate(() => window.__surf.palmLeans());
  const bent = leans.filter(p => p.lean !== null);
  const spread = bent.length ? Math.max(...bent.map(p => p.lean)) - Math.min(...bent.map(p => p.lean)) : 0;
  check(bent.length >= 4 && spread > 8,
        'and no two palms in the game are the same tree',
        bent.map(p => `${p.seed}: ${p.lean}°`).join(', '));
  check(bent.every(p => p.lean < 60),
        'and none of them has folded over far enough to lie down',
        `steepest ${Math.max(...bent.map(p => p.lean))}°`);

  // He stands IN FRONT of the board — that is the composition now, and it is a different
  // statement from the one this used to make. It asked for daylight between the two
  // silhouettes, and daylight is exactly what a rider standing in front of a board does not
  // have. What replaces it is the reason the overlap is legible: he is NEARER THE CAMERA than
  // the board is, by enough that he occludes it rather than growing out of it. Smaller along
  // the forward axis is nearer, and one stride is about the least that reads as in-front at
  // this focal length.
  // Held across the whole rack, because the board is what moves: a ten foot log stands its
  // foot further out than a four foot skimboard does, and if he were placed off a fixed
  // offset instead of off the board's own plane, the longest board would swallow him.
  check(stood.length === 4 && stood.every(f => f.boardF - f.riderF > 1.5),
        'and the rider stands in front of the board rather than beside it',
        stood.map(f => `${f.id} ${(f.boardF - f.riderF).toFixed(2)}ft nearer the camera`).join(', '));
  // In front of it and ACROSS it, not off to one side of it and merely a step forward. The
  // depth check above passes just as happily for a rider standing a yard to the left and one
  // stride nearer, which is the old picture, so the overlap is asked for on its own.
  check(stood.length === 4 && stood.every(f => f.riderGap < 0),
        'and over its deck rather than off to the side of it',
        stood.map(f => `${f.id} ${f.riderGap}ft of overlap`).join(', '));

  // And he stands clear of the BUTTONS, which is a different question from standing clear of
  // the board and is the one a phone actually asks. The set sits left of centre so the palm
  // can hold the middle, and the home screen's glass column runs down that same left edge —
  // so every foot of daylight bought between rider and board is spent walking the rider into
  // Board, Shop and Tricks. Asked as a relationship rather than a number: his left edge is
  // right of where the buttons end, in the frame the picture is actually composed in.
  // Measured in PORTRAIT, because that is the aspect that squeezes him: the same layout on a
  // wide window has the whole beach to spread into and never gets near the glass.
  // Across the rack, because the camera SOLVES onto the group and a ten foot log pushes the
  // whole set further away than a four foot skimboard does — so where he lands in the frame
  // is a different answer per board, and the buttons do not move.
  await page.setViewportSize({ width: 460, height: 966 });
  await page.evaluate(() => window.__surf.wear('pug'));
  await page.waitForTimeout(900);
  const framed = [];
  for (const id of ['carbonskim', 'astro', 'bubblegum']) {
    const f = await page.evaluate(async b => {
      window.__surf.equip(b);
      // Read it SETTLED. The camera does not cut between boards, it eases, and halfway
      // through the ease from a six foot shortboard to a ten foot log the whole set — rider,
      // board and all — swings off the left of the screen and back. Sampled once on a timer
      // that lands in there, the reading says he is off the edge when he never is. Poll until
      // two samples a third of a second apart agree, which is the ease having stopped.
      // Settle on the BOARD, then read the rider off the same frame. Polling the rider's own
      // box never converges cleanly: the title-screen show walks him around the beach, so his
      // edge is moving under a camera that is also moving, and three matching samples in a row
      // happen by coincidence in the middle of an ease as readily as at the end of one. The
      // board is propped against the tree and does not animate at all, so the only thing that
      // moves its box is the camera — which makes it a clean read on whether the ease is done.
      // Waited OUT, not polled for. Two rounds of cleverness went into settling this by
      // watching the numbers, and both were fooled: equipping a board does not ease smoothly
      // to its answer, it steps — through a transient where the whole set swings off the left
      // of the screen and back — so samples agreeing with each other says only that a step is
      // between two jumps. Watched over ten seconds it lands within three and then holds
      // exactly. Four seconds flat, then a window to confirm it really has stopped.
      await new Promise(r => setTimeout(r, 4000));
      let m = null; const win = [];
      for (let i = 0; i < 40; i++) {
        await new Promise(r => setTimeout(r, 250));
        m = window.__surf.menuFrame();
        if (!m) continue;
        win.push(m.board.x[0]); if (win.length > 4) win.shift();
        if (win.length === 4 && Math.max(...win) - Math.min(...win) < 0.002) break;
      }
      // Both COLUMNS, by where each button sits rather than by which half of the screen it
      // is in: the dial's Play button is centred at the bottom and counting it as a right-hand
      // one puts the right edge of the band at 0.41, which is nowhere near it.
      let btn = 0, right = 1;
      for (const e of document.querySelectorAll('#homeDial .hbtn')) {
        const r = e.getBoundingClientRect();
        if (!e.offsetParent || !r.width) continue;
        const c = (r.left + r.right) / 2 / window.innerWidth;
        if (c < 0.35) btn = Math.max(btn, r.right / window.innerWidth);
        else if (c > 0.65) right = Math.min(right, r.left / window.innerWidth);
      }
      return { rider: m && m.rider, board: m && m.board, chest: m && m.chest,
               btn: +btn.toFixed(3), right: +right.toFixed(3) };
    }, id);
    if (f) framed.push({ id, ...f });
  }
  check(framed.length === 3 && framed.every(f => f.btn > 0 && f.rider.x[0] > f.btn),
        'and to the right of the buttons he shares the screen with',
        framed.map(f => `${f.id}: rider from ${f.rider.x[0]}, buttons end ${f.btn}`).join(', '));
  // Right of them, but still ON the beach with the board rather than shoved off the far side
  // of it to buy the clearance. He reads as standing beside it, so he overlaps its column.
  // Right of them, and OVER the board rather than shoved off the far side of it to buy the
  // clearance. In the picture that means the two boxes share a column: his right edge is past
  // where the board's starts, and his left edge is short of where the board's ends.
  check(framed.length === 3 &&
        framed.every(f => f.rider.x[1] > f.board.x[0] && f.rider.x[0] < f.board.x[1]),
        'and standing over the board rather than shoved past it',
        framed.map(f => `${f.id}: rider [${f.rider.x}] board [${f.board.x}]`).join(', '));
  // ---- and the whole set sits in the MIDDLE of what is left between the columns ----
  // The buttons are the frame the picture is composed inside, not furniture at the edge of
  // it. With the shot aimed where it was, the rider started at 0.26 and the chest finished
  // at 0.86 — everything crowded against the right-hand column with a hand's width of empty
  // sand down the left. Asked as the relationship it is: the midpoint of the group against
  // the midpoint of the band, both measured, neither written down. The tree is not in the
  // span on purpose — it is fourteen feet of palm and its crown runs off both sides of the
  // screen, so its box has no midpoint worth having; it is what the set is arranged around
  // and it travels with them.
  // Within a thirtieth of the screen. The default board lands on 0.499 against a band centre
  // of 0.500; the slack is for the ten and a half foot log, which is a genuinely wider subject
  // — its foot stands two feet further from the trunk than a shortboard's — and comes out at
  // 0.518. Tightening past that would mean picking a board to be right for.
  const band = f => (f.btn + f.right) / 2;
  const mid = f => (Math.min(f.rider.x[0], f.board.x[0], f.chest.x[0]) +
                    Math.max(f.rider.x[1], f.board.x[1], f.chest.x[1])) / 2;
  check(framed.length === 3 && framed.every(f => f.right > 0.65 && Math.abs(mid(f) - band(f)) < 0.03),
        'and the set is centred in the gap the buttons leave it',
        framed.map(f => `${f.id}: set at ${mid(f).toFixed(3)}, band ${f.btn}-${f.right} ` +
                        `(mid ${band(f).toFixed(3)})`).join(', '));
  await page.setViewportSize({ width: 1024, height: 640 });
  await page.waitForTimeout(600);

  // Surfaces. Vertex colours give a shape its markings; what they cannot give it is a
  // surface, and bark, glassed resin and sand all read as the same moulded plastic under a
  // directional light until something breaks the normal up finer than the mesh can. A map
  // that silently failed to attach looks exactly like one too subtle to see, so it is asked
  // about rather than eyeballed. The crown must NOT have one — a leaflet is a flat blade.
  const surf = await page.evaluate(() => window.__surf.surfaces());
  check(surf.boardUV && surf.board && surf.board.normal && surf.board.rough,
        'the board is glassed resin over cloth, not a moulded shell',
        JSON.stringify(surf.board));
  // The palm is either the procedural tree, where the trunk carries a bark relief and the
  // crown deliberately carries none — a leaflet is a flat blade and any bump on it reads as
  // dirt — or a modelled one, where the file already painted both and the split does not
  // exist. Both are real and both ship, so both are what this asks about.
  // THREE cases, not two. A modelled palm may arrive with a photograph of bark and leaves on
  // it, or — as this one did — with geometry and absolutely nothing else: no material, no
  // texture, not even a base colour. The second kind is painted on import, so what carries
  // bark-against-leaf is vertex colour, and the question becomes whether that paint says
  // anything at all. A single flat colour over the whole tree would satisfy every other check
  // here and read as a moulded toy, so the span of green across the mesh is what is asked for.
  check(surf.palmModelled
          ? (surf.palmPainted
              ? (surf.palmHue && surf.palmHue.span > 0.05 && surf.palmHue.mean > 0)
              : (surf.bark && surf.bark.normal && surf.palmMap))
          : (surf.bark && surf.bark.normal && surf.bark.rough && surf.palmGroups === 2 &&
             surf.frond && !surf.frond.normal),
        surf.palmModelled
          ? (surf.palmPainted ? 'the palm came as bare geometry and is painted bark and leaves here'
                              : 'the palm is bark and leaves off its own texture, relief and all')
          : 'the trunk is bark and the crown is not',
        JSON.stringify({modelled: surf.palmModelled, painted: surf.palmPainted,
                        hue: surf.palmHue, bark: surf.bark}));
  check(surf.sand && surf.sand.normal, 'and the sand is grains rather than a sheet');
  // A glossy surface reflects a WORLD, and what it reflects decides where the highlight is
  // and how it moves as the surface turns. One flat blue sphere is a tint, not a reflection.
  // The beach reflects its own noon — bluer above, brilliant at the horizon, hot sand under
  // it — which is a different world from the open ocean the ride reflects.
  check(surf.env && surf.menuEnv && surf.onBeach,
        'and everything glossy reflects a world with a horizon and a sun in it',
        JSON.stringify({ride: surf.env, menu: surf.menuEnv, inUse: surf.onBeach}));
  // Occlusion. A shadow map answers "is the SUN blocked" and has nothing to say about the
  // ambient half, which is why an object with a perfectly good shadow beside it can still
  // look pasted on: nothing darkens underneath it, and in a photograph it always does.
  // The palm is exempt when it is MODELLED, for the same reason it is exempt from the bark
  // relief check: the occlusion in the crown and between the bark rings is painted into the
  // file's own texture, and a baked map over the top of a photograph darkens it twice.
  check((surf.palmModelled || surf.bark.ao) && surf.sand.ao && surf.board.ao,
        'creases hold shade the light never reaches',
        JSON.stringify({bark: surf.bark.ao, modelledPalm: surf.palmModelled,
                        sand: surf.sand.ao, board: surf.board.ao}));
  check(surf.pools === 3, 'and each thing standing on the sand darkens the sand under it',
        `${surf.pools} contact pools`);
  // Where the sun is decides two different things and they were fighting. In world
  // coordinates it sat behind the set, and once the shot turned to face the water that put
  // it behind everything IN the shot — tree, board and rider all lit on the side you cannot
  // see. It comes off the camera now: over the left shoulder, so the faces in frame are the
  // lit ones. And the shadow box is the shadow's resolution, so it is cut to the group.
  check(surf.shadow && surf.shadow.front < -0.05,
        "the sun is over the camera's shoulder, so what faces you is what is lit",
        surf.shadow ? `sun-to-camera ${surf.shadow.front}` : 'no shadow rig');
  check(surf.shadow && surf.shadow.box < 30 && surf.shadow.map >= 2048,
        'and the shadow map is spread over the group rather than the county',
        surf.shadow ? `${surf.shadow.map}px over ${surf.shadow.box}ft` : 'no shadow rig');
  // Aerial perspective. Air is not clear, and a beach that holds the same saturation all the
  // way to the horizon is a texture on a plane. The group stands about 32ft out, so the haze
  // has to start well beyond it or it fogs the subject.
  check(surf.fog && surf.fog[0] > 45 && surf.fog[1] > 150,
        'and the beach behind them recedes into haze rather than staying put',
        surf.fog ? `haze from ${surf.fog[0]}ft to ${surf.fog[1]}ft` : 'no haze');

  // Midday, not dusk. The one thing that says "evening" whatever else is done is a wide warm
  // band low in the sky, so the horizon glow is the number that matters — and the sky and the
  // sea both have to be blue-dominant, which a sunset's orange horizon is not.
  const lit = stood[0] && stood[0].sky;
  const blue = c => (c & 0xff) > ((c >> 16) & 0xff);
  check(!!lit && lit.glow <= 0.25 && blue(lit.top) && blue(lit.hor) &&
        stood[0].sea && blue(stood[0].sea.deep) && blue(stood[0].sea.shal),
        'the beach is a bright tropical midday, not a sunset',
        lit ? `sky #${lit.top.toString(16)} horizon #${lit.hor.toString(16)} glow ${lit.glow}` : 'no sky');

  // the same height he rides at — one number for both, because he is the same animal
  const want = 2.20;
  check(sizes.length >= 2 && sizes.every(f => Math.abs(f.rider - want) < 0.10),
        'and stands him beside it at the height he rides at, whoever he is',
        sizes.map(f => `${f.id} ${f.rider}ft`).join(', ') + ` against ${want.toFixed(2)}ft`);

}

await startRun();
const riding = await page.evaluate(() => window.__surf.menuState());
check(!riding.up && !riding.riderOnBeach && !riding.boardOnBeach,
      'and hands them back to the run when you press play', JSON.stringify(riding));
await page.evaluate(() => window.__surf.tick(0.3));
const d0 = await page.textContent('#hDist');
await page.evaluate(() => window.__surf.tick(1.2));
const d1 = await page.textContent('#hDist');
const m = s => parseFloat(String(s).replace(/[^\d.]/g, '')) || 0;
check(m(d1) > m(d0), 'HUD distance advancing', `${d0} -> ${d1}`);

const speed = await page.textContent('#hSpeed');
check(m(speed) > 0, 'speed non-zero', speed);

// Frames actually being drawn, not just state being ticked.
//
// Asked as "do three frames arrive" rather than "how many arrive in three seconds", because
// the second question is about the MACHINE. This runs on a software rasteriser drawing about
// two million triangles a frame — a second or so each — so a count over a fixed window sits
// at four or five when the box is quiet and one when it is not, and it failed twice in a row
// on a build whose own timing was unchanged. What it is for is catching a render loop that
// has stopped, and a loop that has stopped never delivers the third frame however long it is
// given.
const frames = await page.evaluate(() => new Promise(res => {
  let n = 0; const t0 = performance.now();
  const tick = () => { n++;
    if (n >= 3 || performance.now() - t0 > 15000) res({ n, ms: Math.round(performance.now() - t0) });
    else requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
}));
// WHAT it was drawing, not just how fast. This counts real animation frames, and the menu's
// beach is three times the triangles of a wave — so a run that has quietly left the title
// screen up reads as the renderer having fallen over when it is only drawing something else.
const fps = await page.evaluate(() => ({ beach: window.__surf.beachNow(),
  running: window.__surf.state().running, greet: window.__surf.greet ? window.__surf.greet().left : null }));
check(frames.n >= 3, 'frames rendering', `3 frames in ${frames.ms}ms — ${JSON.stringify(fps)}`);

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

// ---------- in the water he waves, he does not thrash ----------
// Everything moving at once — body turning on the spot, hips rising and falling, all four
// limbs at fifteen hertz — read as a seizure rather than as a man in trouble. The only
// thing that moves once he is in it is his arms, and they are up out of it. The check is
// on the joints rather than on pixels: nothing else may change at all.
{
  // Stepped with tick() and NOT with wall-clock waits, in one evaluate with no awaits in it,
  // because this check was flaky and both halves of the reason were timing:
  //   - the render loop advances the wipeout too, so an `await setTimeout` between samples
  //     let real frames slip in and the two samples came from a state the test had not set;
  //   - a wipeout lasts 3.2 s and ends in gameOver, and sampling 2.2 s in and then 1.1 s of
  //     wall clock later put the second sample on whichever side of that the machine felt
  //     like. Under load it failed as "legs still moving" (caught mid-transition) one run
  //     and "arms never moved" (caught before the splash, or after the pose was torn down)
  //     the next, which is the tell: a real regression fails the same way every time.
  // tick() steps in fixed increments regardless of how busy the box is, so this now samples
  // the same two moments every run, both of them safely inside the window.
  const drown = await page.evaluate(() => {
    window.__surf.restart();
    window.__surf.tick(1.0);
    window.__surf.wipeNow('foam');
    // how far he flies before he lands depends on where the wave had him, so step until the
    // splash rather than guessing at it
    let steps = 0;
    while (!window.__surf.state().splashed && steps++ < 40) window.__surf.tick(0.05);
    // And then WAIT for the yaw to settle before sampling. He does not snap square when he
    // lands, he eases — at about 2.2 per second, from wherever the crash left him — so a
    // sample taken shortly after the splash catches a body that is still turning and reads
    // it as thrashing. 1.9 s of that ease leaves under a fiftieth of the original angle,
    // which is the "settled" this check means. The whole thing still finishes comfortably
    // inside the 3.2 s a wipeout lasts, which is the other constraint: past that it is
    // gameOver and there is no pose left to measure.
    window.__surf.tick(1.90);
    const a = window.__surf.rigPose();
    window.__surf.tick(0.40);
    const b = window.__surf.rigPose();
    const d = {};
    for (const k in a) d[k] = Math.abs(a[k] - b[k]);
    return { d, armF: a.armF, armB: a.armB, visible: window.__surf.riderVisible(),
             splashSteps: steps, stillWiping: window.__surf.state().wipe };
  });
  // Legs and head must be dead still. The body is allowed to settle: its yaw eases back to
  // zero rather than being snapped there, so it moves a thousandth or two on the way and
  // then stops. What it may not do is keep turning.
  const still = ['legF', 'legB', 'head'].every(k => drown.d[k] < 1e-6) && drown.d.bodyY < 0.02;
  const waving = drown.d.armF + drown.d.armB > 0.01;
  const up = Math.abs(drown.armF) > 2.0 && Math.abs(drown.armB) > 2.0;
  check(still && waving && up && drown.visible && drown.stillWiping,
        'drowning moves his arms and nothing else, and they are above his head',
        `legs/head moved ${['legF','legB','head'].map(k => drown.d[k].toFixed(3)).join('/')}, ` +
        `body settled ${drown.d.bodyY.toFixed(3)}, ` +
        `arms moved ${(drown.d.armF + drown.d.armB).toFixed(3)} at ${drown.armF.toFixed(2)}/${drown.armB.toFixed(2)}, ` +
        `splash after ${drown.splashSteps} steps, still wiping: ${drown.stillWiping}`);
}

// ---------- a posed rig is off limits ----------
// The wipeout's limbs run on every frame the crash is alive, including the frames the card
// is sitting over the top of it — so a surfer looked at from that card was thrashing his
// way through his own portrait.
{
  const posed = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.tick(1.0);
    window.__surf.wipeNow('foam');
    window.__surf.tick(1.5);
    const live = window.__surf.state().wipe;
    window.__surf.showChar('pug', 0);
    await new Promise(r => setTimeout(r, 400));
    const a = window.__surf.rigPose();
    await new Promise(r => setTimeout(r, 800));
    const b = window.__surf.rigPose();
    const moved = ['armF','armB','legF','legB','head'].reduce((m,k) => Math.max(m, Math.abs(a[k]-b[k])), 0);
    document.getElementById('vClose').click();
    return { live, moved };
  });
  await page.waitForTimeout(400);
  check(posed.live && posed.moved < 1e-6,
        'a surfer held in a portrait does not move, even with a crash playing behind it',
        `crash alive=${posed.live} worst joint moved ${posed.moved}`);
}

// ---------- the wave button ----------
// It has to summon a wave from a standing start, without the 600 m the wave normally
// needs, and it has to be a one-off: the run goes back to its own rhythm afterwards.
{
  await page.evaluate(() => location.reload());
  // Waiting for #dPlay was waiting for nothing: it is in the static HTML, so the selector
  // matches while the page is still parsing, long before three.js has loaded and the game's
  // listeners are attached. The five taps below then landed on an overlay that was not
  // listening yet, the secret panel never opened, and the click on the button inside it sat
  // there for thirty seconds and took the whole run down. Wait for the game itself.
  await page.waitForFunction(() => typeof window.__surf === 'object' && !!window.__surf.state,
                             null, { timeout: 60000 });
  await page.waitForTimeout(400);
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

  // ---- and the currency wears the modelled shell ----
  // Every place a puka appears is an <i class="pk"> reading one CSS rule — the HUD, the shop's
  // prices, the buy buttons, the run card's three lines, the crate reward, the purse — so this
  // is one question asked once. The shell is a 3-D model and the icon is sixteen pixels in a
  // line of text, so it is baked straight on into a transparent PNG at load and handed to the
  // stylesheet; the drawn SVG stays as the fallback for a missing file.
  // WAITED FOR. The shell is a model like any other and arrives when it arrives, and this one
  // is heavy enough that it does not: asked at this point in the run it reported the drawn fan
  // still in place and an empty bake, both true and both temporary. What the icon looks like
  // before it lands is the fallback's job, and there is a check for that above.
  await page.waitForFunction(() => window.__surf.pukaIcon().modelled, null, { timeout: 90000 })
            .catch(() => {});
  const pk = await page.evaluate(() => window.__surf.pukaIcon());
  check(pk.modelled && pk.inUse && !pk.drawn,
        'the puka icon is the modelled shell, everywhere one appears',
        JSON.stringify({ modelled: pk.modelled, inUse: pk.inUse, drawnFallback: pk.drawn }));
  // And there is a SHELL in it. A bake that goes wrong goes blank — camera facing the wrong
  // way, model that never arrived, target cleared and never drawn into — and blank is a valid
  // PNG of the right dimensions with a plausible byte count, so every other measure of success
  // here passes while the icon quietly vanishes out of the HUD. A fan fills something like two
  // thirds of its square; nothing fills none of it, and a solid block fills all of it.
  check(pk.fill > 0.25 && pk.fill < 0.92,
        'and it is a shell rather than an empty square or a solid block',
        `${(pk.fill * 100).toFixed(0)}% of the icon is opaque`);

  // Waiting for the title screen's shot to STOP MOVING. A resize or an equip re-solves the
  // whole composition, and the re-solve does not happen once: models finish arriving, the
  // character is rebuilt, the layout runs again — so the picture goes quiet, moves, and goes
  // quiet again. A fixed wait was enough most runs and not all of them, and the runs where it
  // was not read one half of a comparison from before the last step: a camera that had
  // apparently walked a foot and a quarter backwards in the middle of a charge, and a rider
  // standing a tenth of the screen from where he stands. Three seconds of stillness in the
  // projected picture, never less than five in total, on both sides of the viewport change.
  const settleShot = async () => {
    const t0 = Date.now();
    await page.waitForFunction(() => {
      const c = window.__surf.charge();
      const w = window.__surf._settle || (window.__surf._settle = []);
      w.push(c.foot * 100 + c.dist); if (w.length > 10) w.shift();
      return w.length === 10 && Math.max(...w) - Math.min(...w) < 0.01;
    }, null, { timeout: 40000, polling: 300 }).catch(() => {});
    await page.evaluate(() => { delete window.__surf._settle; });
    if (Date.now() - t0 < 5000) await page.waitForTimeout(5000 - (Date.now() - t0));
  };

  // ---- tap him twice and he comes and has a word about it ----
  // Two taps, not one. One tap on a character is the commonest accidental tap on this screen —
  // the beach is most of it and he stands in the middle — and a title screen that sends him at
  // the camera every time a thumb lands would read as flinching.
  {
    // IN PORTRAIT, which is the shape this is composed for. He stands off to one side of the
    // set and the charge brings him to the middle, and how far that is depends on the aspect:
    // a wide window has room either side of him and the same walk covers a third as much of
    // the frame. Measured on a landscape page the travel is real and reads as nothing.
    await page.setViewportSize({ width: 460, height: 966 });
    // On a KNOWN board. The mark he runs to sits on the camera's view axis, and the camera is
    // aimed at the middle of the set — which moves with the board leaning in it. The block
    // above leaves a ten and a half foot log equipped, and on that the same walk covers half
    // as much of the frame: the check was reading a different composition from the one it was
    // written against, and the reading was right about it.
    // and on a known CHARACTER, for the same reason: the roster loop above leaves whoever it
    // finished with standing there, and they are not the same height or the same width, so
    // where his head sits and how far it travels is a different measurement per animal.
    await page.evaluate(() => { window.__surf.equip('astro'); window.__surf.wear('pug'); });
    // SETTLED, not waited out. A resize and an equip both re-solve the whole shot and it steps
    // to its answer rather than easing there — five seconds was enough most runs and not all of
    // them, and the run where it was not reported him standing on 0.45 of the width instead of
    // 0.33, which is not a rider who moved but a camera that had not arrived. Polled on where
    // he projects to, which is the thing every check below is about.
    // A LONG quiet window, and a floor under it. A resize and an equip both re-solve the whole
    // shot, and the re-solve does not happen once: models finish arriving, the character is
    // rebuilt, the layout runs again — so the picture goes quiet, moves, and goes quiet again.
    // One second of stillness was enough most runs; on the ones it was not, this block read his
    // mark from before the last step and everything after it disagreed with everything before,
    // the camera apparently having walked a foot and a quarter backwards in the middle of a
    // charge. Three seconds of stillness, and never less than five seconds in total.
    await settleShot();

    // ---- the gulls are modelled birds and their wings beat ----
  // The sky's birds were three triangles apiece: two swept shapes and a dot, hinged at the
  // shoulder. The file that replaces them brings a rig and NO clips — twenty-eight bones and
  // nothing to play on them — so the beat is driven here, and which bones to drive is measured
  // off the skeleton rather than read off names that mean nothing and would renumber on a
  // re-export.
  {
    await page.waitForFunction(() => window.__surf.birdInfo().modelled > 0, null, { timeout: 60000 })
              .catch(() => {});
    const bi = await page.evaluate(() => window.__surf.birdInfo());
    check(bi.gulls > 8 && bi.modelled === bi.gulls,
          'every bird in the sky is the modelled one, not the drawn silhouette',
          `${bi.modelled} of ${bi.gulls} across ${bi.flocks} flights`);
    // The wings MOVE. A rigged bird with nothing driving it is a bird in a fixed pose, and at
    // this range a fixed pose and a beating one are the same silhouette in a still frame — so
    // the wingtip is walked through a whole beat and asked how far it travelled, against the
    // bird's own span so the answer does not depend on how far off the flock is.
    check(bi.travel > 0.12 && bi.travel < 0.9,
          'and the wings beat rather than holding a glide',
          `the tip travels ${(bi.travel * 100).toFixed(0)}% of the span through one beat`);
    // and nobody is in step. Every bird has its own rate and its own phase, and the visible
    // form of that is the tips sitting at different heights at any one instant.
    check(bi.stagger > 0.2, 'and no two in a flight beat together',
          `${bi.stagger} between the highest and lowest tip in a flight`);
    // The V flights really are Vs: birds dropping back in PAIRS off a leader, so how far a
    // bird sits off the line of flight grows with how far back it is, and the offsets cancel.
    // Both are asked, because neither is enough on its own — four birds scattered at random
    // correlate as well as a V often enough to be useless, and can balance by luck too.
    // The floor is not 1.0 and cannot be: a rank is jittered a good fraction of its own spacing so
    // the birds are not stamped out, and that jitter is worth a few hundredths of the correlation
    // on a five bird skein. Pinned just above where a random scatter of five lands.
    const vs = (bi.flights || []).map((f, i) => ({ ...f, s: bi.shapes[i] })).filter(f => f.v);
    check(vs.length >= 2 && vs.every(f => f.s && f.s.v > 0.94 && f.s.bal !== null && f.s.bal < 0.20),
          'and the ones that fly in a V are in a V, not just called one',
          vs.map(f => `${f.n}: line ${f.s && f.s.v}, balance ${f.s && f.s.bal}`).join(', '));
    // and the sky is a MIX. Every flight used to be the same loose V of four to seven, which
    // reads as wallpaper — the eye finds the repeat at once.
    const solo = (bi.flights || []).filter(f => f.n === 1).length;
    const knot = (bi.flights || []).filter(f => !f.v && f.n > 1).length;
    check(solo >= 1 && knot >= 1 && vs.length >= 2,
          'and the sky is a mix of skeins, knots and singles rather than one shape repeated',
          `${vs.length} in V, ${knot} loose, ${solo} on their own`);
  }

  // ---- the title is a plank hanging over the beach ----
    // It was an <h1> laid over the canvas, which is the right way to draw a word and the wrong
    // way to put an object in a scene: it takes no light, it is never behind anything, and it
    // does not move when the shot does. Carved into a sign hung above the palm it is part of the
    // beach. The <h1> is still there and is only hidden — it doubles as the "Wipeout!" heading,
    // and a missing models/sign.glb has to leave the title screen with a title on it.
    {
      await page.waitForFunction(() => window.__surf.signAt().up, null, { timeout: 60000 })
                .catch(() => {});
      const sg = await page.evaluate(() => window.__surf.signAt());
      check(sg.up && sg.titleHidden, 'the title hangs over the beach as a sign, not as a word on the glass',
            JSON.stringify({ up: sg.up, wordHidden: sg.titleHidden }));
      // Across the TOP of the frame and centred on it, which is where the reference puts it. Read
      // off the plank's own box rather than a world-aligned one: a world-axis box round a turned
      // plank is a diagonal through it and reports a sign a fifth taller than the sign is.
      check(sg.up && sg.x[0] > 0.04 && sg.x[1] < 0.96 && sg.y[0] > 0.02 && sg.y[0] < 0.18 &&
            sg.y[1] < 0.34 && Math.abs(sg.cx - 0.5) < 0.04,
            'and it hangs across the top of the frame rather than half out of it',
            `x ${sg.x}, y ${sg.y}, centred on ${sg.cx}`);
      // LEVEL, and flat on. It hung a couple of degrees off square in the world, which is nothing
      // on a small prop at eye level and a great deal on a long one ten feet up in a 92° lens:
      // looking that steeply up at it, two degrees of turn became a foreshortened far end and
      // fourteen degrees of apparent roll, and the title lay downhill across the fronds. Neither
      // shows in a world-space angle — both are read in the picture, off the ends of the plank.
      // Asked as "the tilt it was ORDERED and no other" rather than as zero, because the roll is
      // a knob and a knob can be turned. It is zero today: the lettering is carved about three
      // degrees uphill of its own board, so the word and the plank cannot both be level, and
      // rolling to level the word leaves the plank lying downhill — which is what the eye picks
      // up, the plank being the long straight thing across the top of the frame.
      const want = -sg.at[4] * 180 / Math.PI;
      check(sg.up && Math.abs(sg.roll - want) < 1.5,
            'and lies at the tilt it was given and no other',
            `${sg.roll}° of slope across it against the ${want.toFixed(1)}° asked for`);
      check(sg.up && sg.keystone < 1.05 && sg.face < 3,
            'and flat on, so the far end is the same height as the near one',
            `ends differ by ${((sg.keystone - 1) * 100).toFixed(1)}%, face ${sg.face}° off parallel`);
      // and not BENT. These readings go through the fisheye the composite applies, because the
      // title screen is delivered through one and project() answers about the frame that was
      // drawn — one resample short of what anybody sees. That gap was the whole bug: measured in
      // the drawn frame the sign looked perfectly placed, and the remap then carried it up into
      // the corner of the lens where the bend is worst and served a banana. The remap is radial
      // about the middle of the frame, so it treats the two ends alike and neither roll nor
      // keystone can see it — bow is the top edge's worst departure from its own chord.
      check(sg.up && sg.bow < 0.12, 'and straight rather than bowed by the lens',
            `top edge bends ${(sg.bow * 100).toFixed(1)}% of the plank's own height`);
      // and the box being measured is the PLANK's. A plank drawn at an angle inside its own file
      // sits in a box far squarer than the plank, and every reading above is taken on that box —
      // which is square to the lens by construction, so roll and keystone read zero on it however
      // far over the plank inside is lying. The box being plank-shaped is what says they mean
      // anything: leave the file's own turn in and this drops from 2.6 to 1 towards 1 to 1.
      check(sg.up && sg.shape > 2.2, 'and the box being measured is the plank, not a box round it',
            `${sg.shape} to 1, off a ${sg.fix}° correction for the file's own turn`);
      // IN FRONT of the tree. At two feet out it hung inside the crown and the fronds ran across
      // it, hiding three of the four letters — which reads as a plank with an f on it, not as
      // anything being wrong, and is invisible in every other number here.
      check(sg.up && sg.ahead > 1.5, 'and in front of the palm rather than inside its crown',
            `${sg.ahead}ft nearer the lens than the tree`);
      // and it reads the same on every screen. Hung at a fixed height in FEET it did not: the shot
      // pulls back on a narrow frame and comes in on a squarer one, four and a half feet of travel
      // between them, so a plank that sat across the top of a tall phone went clean off the top of
      // a short one and covered the tree on one held sideways. Both its size and its place are
      // solved against the picture now, so the only way to see that is to change the picture.
      // Waited on the page AGREEING it has been resized, rather than on a fixed pause. A sleep
      // long enough on a quiet machine is not long enough on a busy one, and when it is not the
      // resize simply has not happened: both shapes came back with identical numbers, which reads
      // as "it holds everywhere" and is really "it was never asked twice". Worse, the restore does
      // not take either, and every later check in this block runs at the wrong shape — that is
      // what put the rider five feet from the lens instead of seven and failed the punch.
      const shaped = async (w, h) => {
        await page.setViewportSize({ width: w, height: h });
        await page.waitForFunction(a => {
          const f = window.__surf.menuFrame();
          return f && Math.abs(f.aspect - a) < 0.01;
        }, w / h, { timeout: 60000 }).catch(() => {});
        await page.waitForTimeout(350);
      };
      const shapes = [];
      for (const [w, h] of [[500, 600], [900, 420]]) {
        await shaped(w, h);
        shapes.push(Object.assign({ w, h }, await page.evaluate(() => window.__surf.signAt())));
      }
      await shaped(460, 966);
      check(shapes.every(s => s.up && s.y[0] > 0.02 && s.y[0] < 0.20 && Math.abs(s.cx - 0.5) < 0.05 &&
                              s.y[1] - s.y[0] < 0.28 && s.x[1] - s.x[0] > 0.12 && s.ahead > 1.5),
            'and it sits in the same place on a screen of any shape',
            shapes.map(s => `${s.w}x${s.h}: top ${s.y[0]}, centre ${s.cx}, ` +
                            `${((s.x[1] - s.x[0]) * 100).toFixed(0)}% wide by ` +
                            `${((s.y[1] - s.y[0]) * 100).toFixed(0)}% tall`).join(' | '));
      // ---- and the shot did not close in when the bend came off ----
      // The title screen used to go out through a fisheye. That bend paid for itself twice: it
      // squeezed the periphery INTO the frame, and the camera was stood back by exactly what it
      // magnified the middle, so the subject came out the size it was posed at. Take the bend away
      // and both halves go — measured, the palm's crown left the top of the frame and the rider
      // and the chest came out more than twice the size — so the standing-back is kept as its own
      // number now. Asked as a RELATIONSHIP, by turning it off and back on in whatever window this
      // is running in, because the sizes it is worth differ in every window and a number copied
      // out of one of them is a check that only holds there.
      // Asked HERE, in the sign block, because here the title screen is demonstrably up: signAt
      // has already answered about a plank hanging over it. Tried first alongside the lens checks
      // further down and it could never find a beach — those two read a high water mark kept
      // across every frame ever drawn, so they pass whether the menu is on screen or not, and the
      // "get the menu up" they are written around has quietly not been working.
      const pull = await page.evaluate(async () => {
        const at = async v => { window.__surf.menuLens(undefined, undefined, v);
                                await new Promise(r => setTimeout(r, 200));
                                const f = window.__surf.menuFrame();
                                return f && f.rider && f.chest && f.palm
                                  ? { rider: f.rider.h, chest: f.chest.h, crown: f.palm.y[0], pull: f.pull }
                                  : null; };
        const kept = await at(undefined);
        const none = await at(0);
        if (kept) await at(kept.pull);
        return { kept, none };
      });
      check(pull && pull.kept && pull.none && pull.kept.pull > 0.5 &&
            pull.kept.rider < pull.none.rider * 0.92 && pull.kept.chest < pull.none.chest * 0.92 &&
            pull.kept.crown > pull.none.crown,
            'and taking the bend off did not close the shot in — the camera still stands back',
            (pull && pull.kept && pull.none)
              ? `standing back ${pull.kept.pull}: rider ${pull.kept.rider} of the frame against ` +
                `${pull.none.rider} without it, chest ${pull.kept.chest} against ${pull.none.chest}, ` +
                `crown ${pull.kept.crown} off the top against ${pull.none.crown}`
              : 'no frame — the title screen was not up');
    }
    const at = await page.evaluate(() => {
      const f = window.__surf.menuFrame();
      return { x: (f.rider.x[0] + f.rider.x[1]) / 2, y: (f.rider.y[0] + f.rider.y[1]) / 2 };
    });
    const tap = async () => page.mouse.click(at.x * 460, at.y * 966);
    await tap();
    await page.waitForTimeout(700);
    const one = await page.evaluate(() => window.__surf.charge());
    check(!one.on, 'one tap on the rider leaves him where he is',
          `phase ${one.phase}, ${one.dist}ft from the lens`);
    // his resting bands first — see the last check in this block for why a band and not a value
    const rest = await (async () => {
      const look = [], tip = [], head = [];
      for (let i = 0; i < 9; i++) {
        const r = await page.evaluate(() => window.__surf.chargeStep(0.033, 4));
        look.push(r.look); tip.push(r.tip); head.push(r.foot);
      }
      return { look: [Math.min(...look), Math.max(...look)], tip: [Math.min(...tip), Math.max(...tip)],
               foot: +(head.reduce((a, b) => a + b, 0) / head.length).toFixed(3) };
    })();
    await tap(); await page.waitForTimeout(110); await tap();
    const two = await page.evaluate(() => window.__surf.charge());
    check(two.on && two.phase === 'in', 'and two sends him at the camera',
          `phase ${two.phase}`);
    // Walked by hand rather than by the clock. The charge is about four seconds of animation
    // and this renderer gives it a frame every second or two, so waiting it out takes minutes;
    // chargeStep drives the same code the frame loop drives, at the same clamped step.
    const seen = { in: null, hit: null, out: null }, soles = [];
    let reach = 0, punch = null;
    let last = two, done = false;
    for (let i = 0; i < 40 && !done; i++) {
      const r = await page.evaluate(() => window.__surf.chargeStep(0.033, 8));
      if (r.on) { seen[r.phase] = r; soles.push(+(r.sole - r.sand).toFixed(3)); last = r;
                  // the instant of the punch, kept whole: the frame he is furthest over the
                  // lens. Every question below is about that moment, and asking them of
                  // whatever frame the walk happened to stop on asks them of the recoil —
                  // a combo winds up and comes back, so its last frame has him on his heels
                  // and short of the middle, which is true of that frame and of nothing else.
                  if (r.phase === 'hit' && r.tip > reach) { reach = r.tip; punch = r; } }
      else done = true;
    }
    check(done && seen.in && seen.hit && seen.out,
          'and he runs in, throws the punch combo and runs back',
          `in ${seen.in && seen.in.clip}, hit ${seen.hit && seen.hit.clip}, out ${seen.out && seen.out.clip}`);
    // NEARER AND BIGGER, which is the whole of "at the camera" — a run that plays on the spot
    // is the same run the title show already had. Measured off the lens and off how much of
    // the screen he stands in, because those are the two things a player can actually see.
    check(punch && punch.dist < two.dist * 0.55 && punch.span > two.span * 2,
          'and he is close enough that it lands in your face',
          `${two.dist}ft and ${(two.span * 100).toFixed(0)}% of the screen -> ` +
          `${punch && punch.dist}ft and ${punch && (punch.span * 100).toFixed(0)}%`);
    // The beach is not level, so the height he is pinned to has to travel with him. Held to
    // the one number measured on his mark, he arrives at the lens either shin-deep in the sand
    // or walking a foot above it, and neither shows up in a check about where he got to — only
    // in a check about what is under him the whole way.
    // Measured at the LOWEST point on him, which through a forward lean is his front foot —
    // it plants and the back heel comes up, the way a body throwing a punch does. That only
    // holds because the pin is taken after he is tipped: tipping him and then standing the
    // untipped body on the sand pins a pose he is not in, and his footing wandered by an inch
    // and a half as the lean came on.
    const spread = soles.length ? Math.max(...soles) - Math.min(...soles) : 9;
    check(soles.length > 4 && spread < 0.05,
          'and his feet are on the sand the whole way in, over a beach that is not level',
          `${soles.length} frames, ${spread.toFixed(3)}ft of drift in his footing`);
    // IN THE MIDDLE of the picture, which is not the same as at the camera. This shot is not
    // aimed down its own axis — the lens looks at a point beside the board so the set sits
    // centred between the button columns — so running at the camera's position landed him off
    // to the left. He runs at a mark on the camera's view axis instead.
    // In the middle by his HEAD, not by his bounding box. Centring the box was the first go
    // and it reads wrong: an arm thrown out to one side drags the box a tenth of the width off
    // the animal, so the numbers said centred while the picture had him over on the left. What
    // an eye locks onto is his face.
    // He STARTS at the edge of the set and the run is what brings him in. That is the whole
    // shape of it: standing on the middle of the screen already, a charge at the camera is a
    // change of size and nothing else.
    // Read off where he STANDS, and read before the taps.
    // His own origin, not his head: the head swings a tenth of the width through a walk or a
    // backflip, so asked off the head "where is he on screen" answers differently depending on
    // which beat of the show the question lands in — measured, 0.33 caught standing and 0.46
    // caught mid-move, for a rider who had not moved at all.
    // And before the taps, because by the time the second one has been dispatched and the
    // answer has come back he is already running.
    check(rest.foot < 0.42 && punch && punch.foot - rest.foot > 0.12,
          'he starts off to one side and the run is what brings him to the middle',
          `standing on ${rest.foot} of the width, ${punch && punch.foot} at full stretch` + ``);
    check(punch && Math.abs(punch.head - 0.5) < 0.05,
          'and his head arrives in the middle of the frame, not his bounding box',
          `head on ${punch && punch.head} of the width (box ${punch && punch.cx}), ` +
          `from ${two.head} on his mark`);
    // LEANING OVER the lens. The camera sits about chest high on him and the punch combo
    // throws at head height, so stood upright the fists travelled past the lens and out of
    // frame — up, from down here, which is the opposite of a charge at you. Asked as how much
    // nearer his head is to the camera than his feet are, against how much nearer it is when
    // he is just standing there: it is a relationship, and it is invisible in every other
    // number here because he ends up in the same place either way.
    // Across the WHOLE punch, at its furthest, not at whatever frame the walk happened to stop
    // on. A punch combo winds up as well as landing, so the last frame of it has him back on
    // his heels — read there it says he is less tipped than when he is standing still, which is
    // true of that frame and nothing to do with whether the punch comes down at you.
    check(reach > rest.tip[1] * 1.8,
          'and tipped over it, so the punch comes down at you rather than past you',
          `head reaches ${reach.toFixed(3)}ft nearer the lens than his feet, ` +
          `against ${rest.tip[1].toFixed(3)} at his most on his mark`);
    // LOOKING AT YOU while he throws it. Folding the spine points the punch downward and does
    // nothing about where he is looking — and a body bent over you with its eyes on the middle
    // distance is not menacing, it is distracted. The head is aimed at the lens on top, solved
    // off the same head-to-muzzle line the riders are turned by. Asked as the angle between
    // where he is looking and where the camera is: sixty-odd degrees off it standing on his
    // mark, single figures when the punch lands.
    check(punch && punch.look < 12 && two.look > 30,
          'and looking down the lens while he does it',
          `${punch && punch.look}° off the camera, against ${two.look}° on his mark`);
    // and back on his mark afterwards, at the size he was, with the show running again
    const back = await page.evaluate(() => window.__surf.charge());
    check(!back.on && Math.abs(back.dist - two.dist) < 0.15 && Math.abs(back.span - two.span) < 0.02,
          'and afterwards he is back on his mark at the size he was',
          `${back.dist}ft against ${two.dist}, ${back.span} against ${two.span}, now ${back.clip}`);
    // and STOOD BACK UP, head and spine included. The fold and the head aim are written onto
    // the skeleton AFTER the mixer has had its say, so the only thing that undoes them is
    // something writing over them — and a clip that does not animate a bone does not write to
    // it. Left alone he went back to his mark with his head still screwed round at the camera
    // and his back still bent: twenty degrees of neck and half an inch of stoop that nothing
    // ever took off again.
    // Compared as BANDS, not as two readings. He is in the middle of a performance either
    // side of this, so his head and his back are moving anyway — one sample against one sample
    // is two different frames of an animation, and the difference between them says nothing.
    // The FIRST sample is thrown away. Ending the charge puts the skeleton back to its bind
    // pose, and the clip that drives it does not write until the frame after — so that one
    // frame is neither the charge nor the performance, and it reads as a rider still half
    // turned toward the camera. It is one frame and nobody sees it; it is only ever a
    // measurement problem, so it is dropped where the measurement is taken.
    const bandOf = async n => {
      const look = [], tip = [];
      for (let i = 0; i <= n; i++) {
        const r = await page.evaluate(() => window.__surf.chargeStep(0.033, 4));
        if (i === 0) continue;
        look.push(r.look); tip.push(r.tip);
      }
      return { look: [Math.min(...look), Math.max(...look)], tip: [Math.min(...tip), Math.max(...tip)] };
    };
    const settled = await bandOf(9);
    check(settled.look[0] > 30 && Math.abs(settled.tip[1] - rest.tip[1]) < 0.05 &&
          Math.abs(settled.tip[0] - rest.tip[0]) < 0.05,
          'and standing up straight again, with his head off the camera',
          `look ${settled.look.map(v => v.toFixed(0)).join('-')}° against ${rest.look.map(v => v.toFixed(0)).join('-')} before, ` +
          `tip ${settled.tip.map(v => v.toFixed(2)).join('-')} against ${rest.tip.map(v => v.toFixed(2)).join('-')}`);
    // and the page put back the way the rest of the run expects it — settled the same way on
    // the way out as on the way in. The checks below this one measure cameras, and one of them
    // asks whether the ring takes over without moving one: started mid-re-solve it reads a
    // tenth of a metre of drift that has nothing to do with the ring.
    await page.setViewportSize({ width: 1024, height: 640 });
    await settleShot();
  }

  await ver5tap();
  await page.waitForTimeout(300);
  // Both halves: the panel is actually open, and the button is in it. Asking only whether
  // the button's own display is set is no check at all — a child of a display:none panel
  // still computes its own display, so this passed every time while the panel stayed shut.
  const secretWave = await page.evaluate(() => {
    const el = document.querySelector('#stWave');
    return { open: !document.getElementById('stats').classList.contains('hidden'),
             shown: !!el && getComputedStyle(el).display !== 'none' };
  });
  check(secretWave.open && secretWave.shown, 'the wave test lives in the secret panel',
        `panel open=${secretWave.open} button shown=${secretWave.shown}`);
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
    // The SAME wave every run. Everything below this is driven by explicit ticks and is
    // deterministic; the swell is not, because it runs off the render loop's own clock, so the
    // phase the ring happens to take over at is set by how many seconds of wall time the suite
    // has spent above here. That is what the noise floor in this check's threshold was: it sat
    // between 0.015 and 0.021 and moved to 0.030 when a block upstream got ten seconds longer.
    window.__surf.waveAt(0);
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
  // 0.02 was cutting into the noise: the wave's phase at the instant the ring takes over is
  // not the same twice, and this has sat between 0.015 and 0.021 all along. Three
  // hundredths of a foot is nine millimetres — still far below anything you could see as a
  // snap, and above where the measurement itself wobbles.
  check(across.entered && (across.held || across.ended) && jumpPos < 0.03 && drift < 0.02,
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

// ---------- the landing window ----------
// v2.92: the feedback was that a flip you had plainly landed still ended the run, so the
// window opened up — 35.5 degrees of rotation to 41.3, and 44.7 of yaw to 50.4. "A little"
// is the whole claim, so this stands on both sides of it: an attitude inside the new window
// that the old one would have thrown you for survives, and one plainly short of round still
// ends the run. Both halves matter — a window that only ever passes is not a window.
{
  const land = await page.evaluate(() => {
    const drop = (flip, spin) => {
      window.__surf.restart(); window.__surf.tick(0.4);
      return window.__surf.landAt(flip, spin);
    };
    return { square: drop(0.05, 0), slack: drop(0.68, 0),
             short: drop(1.20, 0), crooked: drop(0, 1.30) };
  });
  check(!land.square.wipe && !land.slack.wipe && land.short.wipe && land.crooked.wipe,
        'a slightly crooked landing stands up and a plainly blown one still does not',
        `square=${land.square.wipe} 0.68rad=${land.slack.wipe} ` +
        `1.20rad=${land.short.kind} yaw1.30=${land.crooked.kind}`);
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

// ---------- an obstacle can come in more than one shape ----------
// Two buoys ship — a yellow danger buoy and a red channel marker — and a run that passes the
// same object twenty times reads as a corridor rather than as a sea. Every spawn picks one at
// random, so what this asks is that thirty spawns are not thirty of the same one, and that
// whichever turns up carries the ORIGINAL collision size: each model is fitted against the
// procedural template's box, because measuring the second against the first would have let
// each import quietly rescale the next.
{
  const buoys = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    for (let i = 0; i < 30; i++) window.__surf.spawn('buoy', (i % 7) - 3, -14 - i * 3);
    window.__surf.tick(0.05);
    return window.__surf.obsBounds().filter(o => o.kind === 'buoy');
  });
  const kinds = new Set(buoys.map(b => b.variant));
  check(buoys.length >= 20 && kinds.size >= 2,
        'the buoys you pass are not all the same buoy',
        `${buoys.length} spawned, ${kinds.size} different ones: ${[...kinds].join(', ')}`);
  // They are DIFFERENT SIZES on purpose — a channel marker is a small buoy and a danger buoy
  // is a big one, and fitted to the same box they were two liveries of one object. What has
  // to hold is that the hitbox went with the drawing: a buoy drawn a third bigger that still
  // stops you at the old radius is a thing you ride into before it notices you.
  const size = v => buoys.filter(b => b.variant === v);
  const tall = v => size(v).reduce((a, b) => a + b.tall, 0) / Math.max(1, size(v).length);
  const names = [...kinds];
  check(names.length < 2 || Math.abs(tall(names[0]) - tall(names[1])) > 0.8,
        'and they are not the same buoy in two liveries — one is a big one, one is a small one',
        names.map(v => `${v} ${tall(v).toFixed(2)}ft`).join(', '));
  check(buoys.every(b => Math.abs(b.r / b.tall - buoys[0].r / buoys[0].tall) < 0.06),
        'and each of them hits you at the size it is drawn',
        names.map(v => `${v} r/height ${(size(v)[0].r / size(v)[0].tall).toFixed(2)}`).join(', '));
  check(buoys.every(b => b.tall > 1.8 && b.tall < 7.6),
        'and both stand out of the water rather than looming or vanishing',
        buoys.slice(0, 4).map(b => `${b.variant}:${b.tall}`).join(', '));
}

// ---------- the octopus is an animal in the water, not a turret on it ----------
// Three separate complaints and one cause between them: it was re-aimed at the rider every
// frame. atan2 from the octopus to the player reads as an animal that TRACKS you — it swings
// round as you steer past and then spins right about the moment it is behind you, because
// that is where the bearing flips. It faces the camera from the moment it appears now and
// drifts by on the swell like everything else in the water.
{
  const oct = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    window.__surf.spawn('octopus', -1.4, -60);
    window.__surf.tick(0.05);
    const yaws = [], under = [], proud = [], tips = [];
    for (let i = 0; i < 8; i++) {
      const s = window.__surf.octoState()[0];
      if (s) { yaws.push(s.yaw); under.push(s.under); proud.push(s.hi - s.water); }
      tips.push(window.__surf.octoTips());
      window.__surf.tick(0.28);
    }
    const s0 = window.__surf.octoState()[0];
    return { yaws, under, proud, tips, arms: s0 ? s0.arms : 0, joints: s0 ? s0.joints : null };
  });
  const swing = Math.max(...oct.yaws) - Math.min(...oct.yaws);
  check(oct.yaws.length >= 6 && swing < 0.02,
        'the octopus holds the bearing it arrived on rather than turning to track you',
        `${oct.yaws.length} frames, ${swing.toFixed(4)} rad of swing`);
  // ZERO, not half a turn. The model is built looking down +z and the ride camera sits on
  // +z, so facing you is facing its own forward — the half turn that used to be here was
  // measured on a build that drew the template rather than the clone, and a template is
  // never rotated, so it faced front whatever this number said.
  check(oct.yaws.every(y => Math.abs(y) < 0.4),
        'and that bearing is toward the camera, so it comes at you face on',
        `${oct.yaws.map(y => y.toFixed(2)).join(', ')}`);
  // PART IN AND PART OUT, which is a statement about the animal and not about its origin.
  // This used to ask whether the object's origin sat at or below the surface, and that was
  // only ever a proxy — the honest question is whether the waterline crosses it, which the
  // silhouette checks below answer directly. Kept here as the coarse version of the same
  // thing: some of it under, some of it over, every frame.
  check(oct.under.every(u => u > 0.3) && oct.proud.every(p => p > 0.8),
        'and it rides IN the water rather than sitting on top of it like a beach ball',
        `${Math.min(...oct.under).toFixed(2)}ft of it under the surface at least, ` +
        `${Math.min(...oct.proud).toFixed(2)}ft of it over`);
  // ARMS THAT MOVE, AND MOVE SEPARATELY. Eight arms doing the same thing at the same time is
  // a windscreen wiper, and arms that move by an inch are a statue — the first pass was
  // measurable and not visible, which is the failure this puts a number on.
  const runs = oct.tips.filter(Boolean);
  const moved = runs.length > 1 && runs[0].map((_, a) => {
    let m = 0;
    for (let i = 1; i < runs.length; i++) {
      const p = runs[i - 1][a], q = runs[i][a];
      m = Math.max(m, Math.hypot(q[0] - p[0], q[1] - p[1], q[2] - p[2]));
    }
    return m;
  });
  const most = moved ? Math.max(...moved) : 0;
  check(moved && moved.filter(m => m > 0.12).length >= Math.ceil(moved.length * 0.6),
        'its tentacles curl and uncurl rather than hanging there',
        `${oct.arms} arms, furthest a tip travelled between frames ${most.toFixed(2)}ft`);
  check(moved && new Set(moved.map(m => m.toFixed(2))).size >= Math.ceil(moved.length * 0.5),
        'and each of them on its own clock, not eight copies of one arm',
        `${moved ? new Set(moved.map(m => m.toFixed(2))).size : 0} different amounts among ${oct.arms}`);
}

// ---------- a shop card photographs him standing, whatever the run did ----------
// portraitPose stands the built-in JOINTS up for the photograph, and a modelled rider has a
// skeleton of its own that knows nothing about them. The full-screen viewer had its own line
// putting that skeleton back to a standing pose and the CARDS did not — so one of the two was
// right, and the cards caught whatever the last run left him in: mid-carve, part way through
// a handstand, or wherever the wipeout stopped. Read through the same call the card makes.
{
  // A HAIR of tolerance, and it has a name: the standing pose BREATHES. R.idle carries a
  // sin(tNow) term worth three hundredths of a radian at each shoulder, so two portraits
  // taken at different moments differ by about that much and always will. What must not
  // differ is anything to do with the run — and a carve, a handstand and a wipeout between
  // the two readings moved this by no more than the breath does.
  const same = (a, b) => a && b && a.every((v, i) => Math.abs(v - b[i]) < 0.05);
  const card = await page.evaluate(() => {
    window.__surf.wear('pug');
    window.__surf.restart(); window.__surf.tick(1.0);
    const fresh = window.__surf.portraitRig();
    // a run with everything in it: a carve, the whole handstand, and a wipeout to stop on
    window.__surf.invuln(true); window.__surf.setSteer(1); window.__surf.tick(1.4);
    window.__surf.setSteer(0);
    window.__surf.hand(true); window.__surf.tick(1.8);
    window.__surf.hand(false); window.__surf.tick(0.6);
    window.__surf.wipeNow('foam'); window.__surf.tick(3);
    const after = window.__surf.portraitRig();
    // ...and put the game back. This block deliberately ends a run with a wipeout, and a
    // wipeout can leave a chest ceremony sitting over the top of everything — which a check
    // twenty lines further down then measured through, finding five buttons laid out inside
    // a hidden overlay. A block that changes global state hands it back.
    if (window.__surf.chestSkip) window.__surf.chestSkip();
    window.__surf.restart(); window.__surf.tick(0.5);
    return { fresh, after };
  });
  check(card.fresh.model === true &&
        same(card.fresh.LeftArm, card.after.LeftArm) &&
        same(card.fresh.Head, card.after.Head) &&
        same(card.fresh.Hips, card.after.Hips),
        'a shop card stands him the same way whatever the last run did to him',
        `arm ${JSON.stringify(card.fresh.LeftArm)} then ${JSON.stringify(card.after.LeftArm)}`);
  check(card.fresh.hip && card.after.hip &&
        Math.abs(card.fresh.hip[0] - card.after.hip[0]) < 0.01 &&
        Math.abs(card.fresh.hip[1] - card.after.hip[1]) < 0.01,
        'and in the same place in the frame, not shifted by where the wave left him',
        `hips ${JSON.stringify(card.fresh.hip)} then ${JSON.stringify(card.after.hip)}`);
}

// ---------- a board is wet on the wave and matt on the sand ----------
// Roughness 0.14 against a mirror-bright environment is exactly right in the water, where
// what it reflects is a moving sky over moving water and the whole point is that it reads as
// WET. Stood still on dry sand under one fixed sun it reflects a single broad soft patch of
// that environment across the deck, which reads as a smear of dirt on a white board — and on
// the metal ones, which have no diffuse colour at all, as two black holes where the reflection
// found parts of the environment with nothing in them.
{
  const fin = await page.evaluate(async () => {
    const out = {};
    window.__surf.equip('chrome');
    await new Promise(r => setTimeout(r, 300));
    window.__surf.restart(); window.__surf.tick(0.5);
    out.ride = window.__surf.boardFinish();
    // Stood up DIRECTLY rather than by clicking Menu and waiting. The beach is raised lazily
    // off the menu coming up, so a click and a pause is really a wait on a scheduler — and
    // when it had not happened yet this read the wave's own finish back as the beach's.
    document.getElementById('menuBtn').click();
    out.up = window.__surf.beachNow();
    out.beach = window.__surf.boardFinish();
    window.__surf.restart(); window.__surf.tick(0.5);
    out.back = window.__surf.boardFinish();
    return out;
  });
  check(fin.up === true && fin.beach && fin.beach.dry === true &&
        fin.beach.rough[0] >= 0.65 && fin.beach.env[1] <= 0.15,
        'a board standing on the beach is matt rather than a mirror',
        `beach up ${fin.up}, roughness ${JSON.stringify(fin.beach && fin.beach.rough)}, ` +
        `reflection ${JSON.stringify(fin.beach && fin.beach.env)}`);
  check(fin.ride && fin.ride.dry === false && fin.ride.rough[0] < 0.30 && fin.ride.env[1] > 0.30,
        'and the same board is glassed and wet the moment it is on a wave',
        `roughness ${JSON.stringify(fin.ride && fin.ride.rough)}, ` +
        `reflection ${JSON.stringify(fin.ride && fin.ride.env)}`);
  check(fin.back && fin.back.rough[0] === fin.ride.rough[0] && fin.back.env[1] === fin.ride.env[1],
        'and it is given back exactly, however many times it goes ashore and out again',
        `${JSON.stringify(fin.back && fin.back.rough)} against ${JSON.stringify(fin.ride && fin.ride.rough)}`);
  await page.evaluate(() => { window.__surf.equip('astro'); window.__surf.restart(); window.__surf.tick(0.4); });
}

// ---------- and no board on the beach can catch a highlight at all ----------
// Roughness is a dimmer on the specular lobe and never a switch, so on a traction pad at
// #05020b — a colour whose diffuse returns nothing — the sun's reflection was still the only
// thing on the pad: a white curve that slid across it as the board turned. Widening it twice
// changed nothing, because it was never too narrow. The dry board is unglossed now rather
// than rough, and the question this asks is the one roughness could not answer: is there a
// specular term on what is actually being drawn.
//
// Asked of every board through the dry call itself, not by walking the rack on the title
// screen. Equipping forty-six boards draws forty-six shop thumbnails, a thumbnail takes the
// preview scene back, and the beach coming down partway through reported the remaining
// boards glossy — a true reading of a state the player never sees, and nothing to do with
// the boards. dryAudit puts each one through the same call the sand puts it through.
{
  const spec = await page.evaluate(() => window.__surf.dryAudit());
  const bad = spec.filter(([, lit, sp]) => !(lit > 0 && sp === 0));
  check(spec.length > 4 && bad.length === 0,
        'and not one board standing on the sand has a specular term left to streak with',
        `${spec.length} boards, ${spec.reduce((a, b) => a + b[1], 0)} materials lit, ` +
        (bad.length ? `glossy still: ${JSON.stringify(bad.slice(0, 6))}` : 'none glossy'));
}

// ---------- the wind has been over the beach ----------
// A beach is not a field of grain: the wind builds dry sand into ripples — long ridges a
// hand's width apart, running across the wind and wandering rather than ruled — and it is the
// first thing in every photograph of one. The sand here had none, only isotropic grain.
//
// They live in the normal map and not the mesh, and that is not a shortcut, it is the only
// place they fit: the sand's rows are a foot and a half apart and the height function's own
// ripple term is a couple of feet from crest to crest, which is one and a half vertices per
// wave. Anything at that spacing is tessellated flat however much of it is asked for.
//
// So the check reads the baked map back and asks whether the relief is ANISOTROPIC. Ripples
// turn the normal hard one way and barely at all the other; grain turns it the same amount
// in both. A ratio near one is a beach with no wind on it, whatever the code says it baked.
{
  const sand = await page.evaluate(() => window.__surf.sandRelief());
  check(sand && sand.ratio > 2,
        'the beach is ripples rather than sandpaper — the relief runs one way',
        sand ? `${sand.along} across the ridges against ${sand.across} along them, ${sand.ratio}x`
             : 'no sand');
  // and deep enough to catch the light at all. It was baked at a whisper first — the numbers
  // that drew grain, on a relief several times bigger — and read as a clean sheet.
  check(sand && sand.scale >= 1.0,
        'and deep enough to read from across the beach',
        sand ? `normal scale ${sand.scale}` : 'no sand');
  // A hundred and fifty tiles at a grazing angle is a moire generator; four samples cannot
  // resolve the far half of the beach.
  check(sand && sand.aniso >= 8,
        'and it lies down into the distance instead of shimmering',
        sand ? `${sand.tiles[0]}x${sand.tiles[1]} tiles at ${sand.aniso}x anisotropy` : 'no sand');
  // ---- and the grains are a COLOUR, not only a shape ----
  // Everything the sand had was relief — a normal map and a shadow map over one flat colour —
  // and relief only shows where the light rakes it, so from most angles the beach was a smooth
  // tan sheet with a pattern of shading on it. Real sand is thousands of grains, quartz
  // catching the sun and heavier minerals nearly black, and that is an albedo you can see
  // straight on with no light behind it at all.
  const gr = sand && sand.grain;
  check(!!gr && gr.contrast > 12,
        'and the sand has grains you can see with no light on them at all',
        gr ? `${gr.contrast} of contrast about a mean of ${gr.mean}` : 'no colour map on the sand');
  // The SIZE of a grain is set by the camera, not by what a grain is. This lens sits a foot
  // over the sand and the near beach fills about eight feet of frame; a photograph of that
  // shows a couple of hundred grains across it — one about every centimetre. Tiled three
  // times finer than the ripples, which was the first guess, every speck lands inside a pixel
  // and the whole thing reads as noise. Half a centimetre to four is the band that reads.
  check(!!gr && gr.ft > 0.012 && gr.ft < 0.13,
        'and they are the size a grain looks from here rather than the size a grain is',
        gr ? `one texel every ${(gr.ft * 30.48).toFixed(1)}cm of beach` : 'no colour map');
  check(!!gr && gr.srgb !== false,
        'and it is treated as a colour, which is what it is',
        gr ? `sRGB ${gr.srgb}` : 'no colour map');
}

// ---------- and the wind has PILED it, not just combed it ----------
// Ripples are the fine relief; dunes are the coarse one, and the beach had neither in the
// picture. The dune terms were in the height function all along and switched off where it
// counts: they are multiplied by how DRY the sand is, that ramped from -46, and the title
// screen's set stands at -50 — so the whole of the visible beach sat in the wet flats where
// every dune is multiplied by nearly nothing.
//
// Getting it back took three wrong turns and the numbers caught all three. Too much relief
// and the hollows go below the waterline, where the sea is a flat sheet a foot up that simply
// covers them and the bottom of the screen turns blue. Made one-sided so it can only pile up,
// it climbed nine feet across the view — this camera stands under four feet over the sand, so
// that is a wall in front of the thing the screen is about. So the check is BOTH bounds:
// enough relief to see, and never a hollow the sea can get into.
{
  // WITH THE TITLE SCREEN UP, because the menu raises the sea a foot to drown the flats and
  // that raised sea is the one the hollows have to clear. Sampled with the beach down it read
  // the waterline as zero and passed on a bar that was not the real one.
  const sp = await page.evaluate(async () => {
    for (let i = 0; i < 12 && !window.__surf.beachNow(); i++) {
      document.getElementById('menuBtn').click();
      await new Promise(r => setTimeout(r, 120));
    }
    return Object.assign({ up: window.__surf.beachNow() }, window.__surf.sandProfile());
  });
  const ys = (sp && sp.line || []).map(p2 => p2.y);
  const relief = ys.length ? Math.max(...ys) - Math.min(...ys) : 0;
  check(sp && sp.nonFinite === 0,
        'the beach is a surface — every vertex of it is a number',
        sp ? `${sp.verts} vertices, ${sp.nonFinite} not finite` : 'no sand');
  check(relief > 0.5,
        'and the wind has piled it into dunes rather than combing it flat',
        `${relief.toFixed(2)}ft between the highest and lowest of it along the view`);
  check(sp && sp.up === true && sp.seaY > 0.5 && ys.every(y => y > sp.seaY),
        'and none of it dips under the sea that would then be drawn over it',
        sp ? `beach up ${sp.up}, lowest ${Math.min(...ys)} against a waterline at ${sp.seaY}`
           : 'no sand');

  // ---- and it is DUNES, not a ramp, in the strip the lens actually sees ----
  // The check above passed, every run, on a beach with no dunes in it whatsoever, because it
  // asks about HEIGHT: this beach climbs three feet from the camera to the set no matter what
  // is done to it, so "more than half a foot between the highest and lowest" is satisfied by
  // a perfectly smooth slope. It was, and the title screen was a sheet of paper.
  //
  // A dune is where the ground TURNS OVER, so the measure is the second difference along the
  // view — zero on anything smooth however tall or however tilted, large exactly where there
  // is a crest — and it is checked as a RATIO against that same measure taken on the beach's
  // own level curve, which has no dunes in it by construction. That makes it a comparison
  // rather than a threshold somebody has to re-guess the next time the beach is reshaped.
  const sv = await page.evaluate(() => window.__surf.sandView());
  const peak = sv ? Math.max(...sv.bump) : 0;
  check(sv && peak / Math.max(0.005, sv.flatBump) > 20,
        'and the near beach turns over into ridges rather than running away as one smooth ramp',
        sv ? `${peak.toFixed(3)}ft of turn-over per step against ${sv.flatBump.toFixed(3)} on the ` +
             `bare profile — ${Math.round(peak / Math.max(0.005, sv.flatBump))}x` : 'no sand');
  // The other half of the same lever, and the one that broke first: the eye height was
  // measured at the SET, twenty units down a sloping beach from where the lens stands, so the
  // first dune big enough to see put the camera under the surface and the screen filled with
  // the back of the beach and the sea showing through it.
  check(sv && sv.clear > 0.5,
        'and the lens stands over the sand rather than inside it, whatever the dunes do',
        sv ? `${sv.clear}ft of daylight under the camera` : 'no sand');
  // against the waterline the check above already established is a real one — sandView is read
  // a frame later and the sea is not always standing at its menu height by then, and a hollow
  // measured against a sea at zero is not measured against anything.
  const sea = Math.max(sv ? sv.seaY : 0, sp ? sp.seaY : 0);
  check(sv && sea > 0.5 && sv.lo > sea,
        'and the deepest hollow in that strip still clears the water',
        sv ? `lowest ${sv.lo} against a waterline at ${sea}` : 'no sand');

  // ---- and no two kickers are sitting at the same depth ----
  // A ramp used to be a ramp: same height out of the water every time, so the jump it gave was
  // a fact you learned once. Each one now floats somewhere between riding high and half-under,
  // and the two things that follow are the two a player can actually see — less of it above the
  // surface, and less of a throw. What is checked is that the two agree: the share of its own
  // height a ramp has been dropped by, and the share of the launch it keeps, must add to one.
  // A depth that did not reach the physics would look like variety and play like nothing.
  {
    const rr = await page.evaluate(() => {
      window.__surf.restart(); window.__surf.tick(0.2);
      for (let i = 0; i < 14; i++) window.__surf.spawn('ramp', (i % 5) * 2 - 4, -14 - i * 6);
      window.__surf.tick(0.1);
      return window.__surf.obsObjects().filter(o => o.userData.ramp).map(o => ({
        h: o.userData.rampH, sink: o.userData.sink, lift: o.userData.lift }));
    });
    // FOUR depths, not a continuum. Rolling it continuously gave every kicker its own private
    // number, which is the same as giving none of them a size — two ramps a few per cent apart
    // are indistinguishable, and there is no distribution to learn from single samples. What is
    // asked for is that the set of depths is small enough to recognise and that its members are
    // far enough apart to tell apart, which is a statement about the STEPS between them.
    const steps = [...new Set(rr.map(r => +r.sink.toFixed(3)))].sort((a, b) => a - b);
    check(rr.length >= 8 && steps.length >= 3 && steps.length <= 4,
          'kickers come in three or four depths rather than every depth',
          `${rr.length} ramps across ${steps.length}: ${steps.map(v => v.toFixed(2) + 'ft').join(', ')}`);
    check(steps.length >= 3 && steps.every((v, i) => i === 0 || v - steps[i - 1] > 0.15),
          'and no two of those depths are near enough to read as the same ramp',
          steps.slice(1).map((v, i) => `+${(v - steps[i]).toFixed(2)}ft`).join(', '));
    check(rr.length >= 8 && rr.every(r => Math.abs((r.sink / r.h) + r.lift - 1) < 0.02),
          'and the jump each one gives is the share of it that is out of the water',
          rr.slice(0, 4).map(r => `${(r.sink / r.h * 100).toFixed(0)}% under / ` +
                                  `${(r.lift * 100).toFixed(0)}% of the launch`).join(', '));
  }

  // ---- and the depth is FELT, which is a different claim from the depth existing ----
  // It did not used to be. The launch carries a floor so that hitting a kicker is never a
  // damp squib, and as a single number that floor was higher than the curve gives at any
  // ordinary speed — so it won every time, every ramp in the game launched you at exactly
  // 17, and four carefully spaced depths produced one jump. The numbers above all passed
  // throughout: the depth reached userData, userData reached the launch expression, and the
  // launch expression was then thrown away by a Math.max. Asked here at the far end, off the
  // lip, where the player is: what does each of the four ACTUALLY throw you at.
  {
    const at = async eff => {
      const rs = await page.evaluate(e => {
        window.__surf.restart(); window.__surf.tick(0.2);
        for (let i = 0; i < 40; i++) window.__surf.spawn('ramp', (i % 5) * 2 - 4, -14 - i * 6);
        window.__surf.tick(0.1);
        const by = {};
        for (const r of window.__surf.rampLaunch(e)) by[r.tier] = r;
        return Object.keys(by).sort().map(k => by[k]);
      }, eff);
      return rs;
    };
    // At a speed near the start of a run, which is where the floor used to flatten everything,
    // and again well up into a fast one, where the curve is doing the work instead. Both, since
    // a fix that only holds in one of the two regimes is the same bug in the other.
    const slow = await at(16), fast = await at(34);
    const ordered = r => r.length >= 3 && r.every((v, i) => i === 0 || v.vy < r[i - 1].vy - 0.8);
    check(ordered(slow) && ordered(fast),
          'a deeper kicker throws you less hard, at a crawl and at full speed alike',
          `slow ${slow.map(r => r.vy).join(' / ')} — fast ${fast.map(r => r.vy).join(' / ')}`);
    // And by enough to be a difference rather than a rounding. Ballistic flight puts both the
    // height and the distance in proportion to this, so a third off the launch is a third off
    // the jump in every direction — which is the whole of what the depth is for.
    const ratio = r => r[0].vy / r[r.length - 1].vy;
    check(slow.length >= 3 && fast.length >= 3 && ratio(slow) > 1.4 && ratio(fast) > 1.4,
          'and the shallowest throws you half again as far as the deepest',
          `slow ${ratio(slow).toFixed(2)}x, fast ${ratio(fast).toFixed(2)}x`);
  }

  // ---- and somebody is DRIVING the jet ski ----
  // The built-in ski was modelled with a little goggled figure on it. The loader that swaps a
  // modelled hull in cleared every child of the group first, which is right for the hull and
  // wrong for the rider, so a driverless jet ski has been crossing the lane. He is one of the
  // roster's own animals now rather than a person who wandered in from another game.
  {
    const crew = await page.evaluate(() => {
      window.__surf.restart(); window.__surf.tick(0.3);
      window.__surf.skiNow(true, 1); window.__surf.tick(0.05);
      const c = window.__surf.skiCrew();
      // and the lane left as it was found. A ski parked beside the rider throws a wake at
      // him, and the checks further down this page ride the board through their own ticks —
      // one of them measures his crouch against the deck and reads a different number
      // entirely with a wave going under it.
      window.__surf.restart();
      return c;
    });
    check(crew.rider && crew.pug && crew.onScreen && !crew.figure,
          'the jet ski crosses your line with a pug driving it',
          JSON.stringify({ pug: crew.pug, figure: crew.figure, on: crew.onScreen }));
    // Sized against the BOAT, and measured off what is drawn rather than off a bounding box.
    // This model's vertices are in metres and its joints in centimetres under an armature
    // scaled to a hundredth — legal, and both halves land correctly on screen — so every box
    // taken off its geometry reports it a hundred times its size. Scaled off one of those he
    // was placed perfectly, reported at two thirds the length of the ski, and drawn as a
    // speck: the numbers agreed with each other and disagreed with the picture the whole way.
    const share = crew.skiLong ? crew.tall / crew.skiLong : 0;
    check(share > 0.2 && share < 0.55,
          'and he is a rider on it rather than a speck or a giant',
          `${crew.tall}ft against ${crew.skiLong}ft of boat — ${(share * 100).toFixed(0)}%`);
    // ON the hull: his soles above its keel and below the top of its console, and over its
    // centreline rather than out on the water beside it.
    check(crew.feet > crew.hullFloor && crew.feet < crew.hullTop && crew.offAxis < 0.6,
          'and standing on the deck rather than in the hull or beside it',
          `soles ${crew.feet} between keel ${crew.hullFloor} and top ${crew.hullTop}, ` +
          `${crew.offAxis}ft off the centreline`);
    // Facing the way it drives. Solved off the rig by turning him a known amount and seeing
    // which way his head went, because these exports arrive mirrored and a reflection in the
    // chain flips the sense of a yaw — subtracting the measured angle, which is right for
    // every unmirrored model here, left him a hundred and thirteen degrees off. Side-on at
    // forty metres he is a pug-shaped blob either way, which is why this is a number.
    check(Math.abs(crew.face) < 12,
          'and looking where it is going, not out to sea',
          `${crew.face}° off the bow`);
  }

  // ---- and the sand you can get close to is MODELLED, not painted ----
  // The beach mesh is two feet a quad: it can carry a dune and it cannot carry sand. A small
  // modelled piece of ground is scattered over it — turned, resized and sunk to meet the beach
  // at every one of a hundred-odd places — and what makes that read as a beach rather than as
  // tiles is that each piece meets the ground it is lying on. So that is what is measured: how
  // far the underside of a piece sits from the beach beneath it, over the whole field.
  const st = await page.evaluate(() => window.__surf.sandTiles());
  check(st && st.tile && st.on && st.count > 40,
        'the sand on the preview beach is a modelled piece laid down over and over',
        st ? `${st.count} pieces of ${st.trisEach} triangles, ${st.tris} in all, ${st.draws} draw call`
           : 'no sand tiles');
  // Every piece is placed against the height function rather than dropped at a fixed level, so
  // this is the number that says the placement is still doing its job. Loose, on purpose: the
  // pieces are deliberately lifted clear of the dunes they lie across and pressed flat where
  // anything is standing, so it is a foot or so and not zero — what it must not be is metres,
  // which is a field floating over the beach with daylight under it.
  check(st && st.on && Math.abs(st.sit.worst) < 2.5 && st.sit.mean < 1.2,
        'and every one of them meets the beach it is lying on rather than floating over it',
        st ? `worst ${st.sit.worst}ft, mean ${st.sit.mean}ft between a piece and the ground` : 'no sand tiles');
  // One object, however many pieces: an InstancedMesh, so the cost is triangles and not calls.
  check(st && st.on && st.draws === 1 && st.tris < 900000,
        'and the whole field is one object rather than a hundred of them',
        st ? `${st.tris} triangles in ${st.draws} draw call` : 'no sand tiles');

  // ---- and the lens over all of it is WIDE and STRAIGHT ----
  // It used to be a fisheye: a wide rectilinear shot with the composite remapping angle to radius
  // on the way out, so the periphery was squeezed in rather than stretched and straight lines bowed
  // around the middle. The bend is off now, by choice. It was never free — it bent the title along
  // with everything else, and a plank hung across the top of the frame is the one thing on this
  // screen with a long straight edge to lose.
  //
  // Taking it off is two changes, not one, and the second is easy to forget: the bend paid for
  // itself twice, squeezing the edges into frame AND standing the camera back by exactly what it
  // magnified the middle. Remove it and the shot closes in — measured, the palm's crown left the
  // top of the frame and the rider and the chest came out half again the size. So the standing-back
  // is its own number now, and these ask for the picture the bend used to produce rather than for
  // the formula that used to produce it.
  // WITH THE TITLE SCREEN UP, and put up here rather than assumed: the checks above leave the page
  // wherever they finished, and this belongs to one screen.
  const lens = await page.evaluate(async () => {
    // beachNow(), not "does the menu want to be up": the click sets the screen and the beach is
    // built on the frame AFTER it, and menuLens draws the frame it then reports on — asked in
    // between, it finds nothing to draw and reports the game's last frame instead.
    for (let i = 0; i < 12 && !window.__surf.beachNow(); i++) {
      document.getElementById('menuBtn').click();
      await new Promise(r => setTimeout(r, 150));
    }
    await new Promise(r => setTimeout(r, 150));
    return window.__surf.menuLens();
  });
  // The post chain is still there — bloom and the rest still run — and no bend reaches it. Asked
  // of what the composite was actually HANDED on the frame it just drew, not of what the title
  // screen intends to hand it, and as a high water mark across every frame so far, because one
  // bent frame is one too many and any later frame would erase a plain reading of it.
  check(lens && lens.post && lens.everApplied === 0,
        'the title screen is drawn straight — the post chain runs, and nothing bends it',
        lens ? `post chain ${lens.post}, worst bend across ${lens.frames} frames ${lens.everApplied}`
             : 'no lens');
  check(lens && lens.cornerDeg > 85,
        'and the lens is still wide, which is what puts the whole beach in a phone',
        lens ? `${lens.fov}° down the frame, ${lens.acrossDeg}° across it, ${lens.cornerDeg}° corner to corner`
             : 'no lens');

}

// ---------- the grip pad lies ON the deck ----------
// A white curve kept appearing across the black pad on the title screen, and moving as the
// board swayed. It was taken for a reflection twice and it never was one: the pad is a
// separate skin laid a hair above the deck, and it had a dome of ITS OWN — (1 - 0.64v²)
// across its own width, against the deck's (1 - s²) across the board's. Two different curves
// cannot stay a hair apart. Near the pad's edges its profile fell faster than the deck it was
// lying on, by several times the six thousandths it had been lifted, so the white deck came
// up THROUGH the black pad. It tracked the deck's curvature, which is why it moved.
//
// The pad is written in the deck's own coordinate now, so the two are parallel by
// construction — and this is the measurement that says so: a ray dropped from every sampled
// pad vertex onto the deck below it, on every board in the rack. Positive everywhere means
// the pad is on top; a narrow spread means it is lying flat rather than crossing.
{
  const pads = await page.evaluate(() => window.__surf.boardIds()
    .map(id => [id, window.__surf.padGap(id)]).filter(r => r[1]));
  const through = pads.filter(([, g]) => g.min <= 0.002);
  const domed = pads.filter(([, g]) => g.max - g.min > 0.01);
  check(pads.length > 20 && through.length === 0,
        'every grip pad in the rack sits on top of the deck rather than through it',
        `${pads.length} padded boards, ` +
        (through.length ? `deck poking through: ${JSON.stringify(through.slice(0, 4))}`
                        : `thinnest gap ${Math.min(...pads.map(p => p[1].min)).toFixed(4)}ft`));
  check(pads.length > 20 && domed.length === 0,
        'and lies flat on it, the same curve rather than one of its own',
        domed.length ? `bowed: ${JSON.stringify(domed.slice(0, 4))}`
                     : `worst spread ${Math.max(...pads.map(p => p[1].max - p[1].min)).toFixed(4)}ft`);
}

// ---------- a ramp that came out of a file is still a ramp ----------
// The built-in kicker is a curve the ride knows by heart — its deck is H*t^P and the rider's
// feet are put on that formula. Four numbers describe it, and none of them were carried onto
// a model: the spec that models are rebuilt from did not copy them, and a fitted model's
// userData is REPLACED rather than merged. So the first ramp anyone ever dropped in was a
// ramp with no slope on it, and `undefined` does not announce itself — it spreads. t came out
// NaN, the deck height came out NaN, the rider's y came out NaN, and the first thing to
// actually complain was a foam ring several systems away failing to upload a matrix, on a
// frame with nothing to do with ramps. The gate that should have stopped it, `t<0||t>1`, is
// false for NaN in both halves.
//
// It had never run because ramp.glb was one of the documented absences until a file arrived.
{
  const ramp = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.tick(1.0);
    window.__surf.spawn('ramp', 0, -14);
    window.__surf.tick(0.2);
    const surf = window.__surf.rampSurface();
    // and ride it: step forward until he is over it, watching for anything that stops
    // being a number
    let finite = true, lifted = 0;
    const y0 = window.__surf.state().py;
    for (let i = 0; i < 60; i++) {
      window.__surf.tick(0.05);
      const s = window.__surf.state();
      if (!isFinite(s.py) || !isFinite(s.px) || !isFinite(s.pz)) { finite = false; break; }
      lifted = Math.max(lifted, s.py - y0);
    }
    return { surf, finite, lifted, tpl: window.__surf.obsTemplate('ramp') };
  });
  const modelled = !!(ramp.tpl && ramp.tpl.variant);
  const rows = ((ramp.surf && ramp.surf.rows) || []).filter(r => r.mesh !== null && r.t < 0.95);
  const worst = rows.length ? Math.max(...rows.map(r => Math.abs(r.mesh - r.ride))) : 99;
  check(ramp.finite, 'riding a ramp leaves every number a number',
        ramp.finite ? 'x, y and z all finite across the whole climb'
                    : 'the rider went NaN on the way up');
  // Two and a half inches, because a ramp sitting on the swell is TILTED — a twentieth of a
  // radian of it, which is a couple of inches at the ends of a five-foot kicker — and the
  // rays read the tilted mesh while the ride reads the untilted profile. Tighter than that
  // is measuring the swell. Loose enough to still catch the thing this is for: a deck the
  // ride is not following at all, which was five inches out and climbing the wrong curve.
  check(rows.length >= 4 && worst < 0.21,
        'and his feet are on the deck that is actually there, not the one it was assumed to have',
        `${rows.length} points along it, worst ${worst.toFixed(2)}ft between mesh and ride` +
        (modelled ? ' (modelled ramp)' : ' (built-in ramp)'));
  check(ramp.lifted > 0.4, 'and it still lifts him',
        `${ramp.lifted.toFixed(2)}ft gained`);
  await page.evaluate(() => { window.__surf.restart(); window.__surf.tick(0.4); });
}

// ---------- the octopus faces you, flexes, throws, and arrives one at a time ----------
// Four complaints and four different causes, which is why they are four checks.
//
// Facing was a number that could not be wrong out loud: half a turn was measured on a build
// that was drawing the TEMPLATE rather than the clone, and a template is never rotated, so it
// faced front whatever the number said. With the clone drawn properly the same number turned
// every octopus round to show you its back. The model is built looking down +z and the ride
// camera sits on +z, so facing you is zero.
{
  const oct = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    window.__surf.spawn('octopus', 0.5, -45); window.__surf.tick(0.02);
    const a = window.__surf.octoFlex(), yaw = window.__surf.octoState()[0];
    window.__surf.tick(0.45);
    const b = window.__surf.octoFlex();
    let most = 0, moved = 0;
    if (a && b && a.length === b.length) for (let i = 0; i < a.length; i += 3) {
      const d = Math.hypot(a[i] - b[i], a[i + 1] - b[i + 1], a[i + 2] - b[i + 2]);
      if (d > most) most = d;
      if (d > 0.05) moved++;
    }
    return { yaw: yaw && yaw.yaw, samples: a ? a.length / 3 : 0, moved, most: +most.toFixed(2) };
  });
  check(oct.yaw !== undefined && Math.abs(oct.yaw) < 0.4,
        'an octopus comes at you face on rather than showing you its back',
        `yaw ${oct.yaw}`);
  // Its SKIN, not its bones. Every reading before this one was of bone positions, and bones
  // moving is not the same statement as an animal moving — that is exactly the shape the
  // clone bug took, where the code turned one skeleton and the picture came from another.
  check(oct.samples > 20 && oct.moved > oct.samples * 0.6 && oct.most > 0.15,
        'and its arms actually move the skin, not just the skeleton',
        `${oct.moved} of ${oct.samples} sampled vertices moved, furthest ${oct.most}ft in half a second`);
  // AND IT STILL LOOKS LIKE AN OCTOPUS while it does it. Two ways it stopped: floated so low
  // that every arm was under the surface and all you could see was a purple hump, and — at
  // the amplitudes it took to make the arms visibly move — swung far enough that they folded
  // over the body and the whole animal read as lying on its side. So the silhouette gets its
  // own checks: the mantle stands out of the water, and the fan of arms keeps its height.
  const shape = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    window.__surf.spawn('octopus', -1.0, -22); window.__surf.freeze(true);
    const out = [];
    for (let i = 0; i < 6; i++) { window.__surf.tick(0.35);
      const s = window.__surf.octoState()[0];
      if (s) out.push({ proud: +(s.hi - s.water).toFixed(2), tall: +(s.hi - s.lo).toFixed(2) }); }
    window.__surf.freeze(false);
    return out;
  });
  check(shape.length >= 5 && shape.every(f => f.proud > 0.8),
        'and it stands out of the water rather than showing you a purple hump',
        `mantle ${Math.min(...shape.map(f => f.proud)).toFixed(2)}ft clear at its lowest`);
  check(shape.length >= 5 && shape.every(f => f.tall > 2.4),
        'and its arms stay fanned out rather than folding over it',
        `${Math.min(...shape.map(f => f.tall)).toFixed(2)}ft from lowest arm to top at its flattest`);
  // It THROWS. Measured over forty spawns this was one starfish, because an octopus crosses
  // the throwing window in about a second and a half at speed and its first timer ran to
  // nearly two — most of them drifted past having never thrown anything at all.
  const thrown = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    const before = window.__surf.starsUp().thrown;
    let flying = 0;
    for (let i = 0; i < 40; i++) {
      if (i % 6 === 0) window.__surf.spawn('octopus', (i % 5) - 2, -40);
      window.__surf.tick(0.4);
      flying = Math.max(flying, window.__surf.starsUp().flying);
    }
    return { thrown: window.__surf.starsUp().thrown - before, flying };
  });
  check(thrown.thrown >= 4 && thrown.flying >= 1,
        'and the ones you pass actually throw something at you',
        `${thrown.thrown} starfish let go of, ${thrown.flying} in the air at once`);
  // ONE AT A TIME. The weight arrived at 24 against a field of 86 the moment the counter
  // passed 780 m, so the first octopus and the next eight turned up together — nothing, and
  // then a wall of them. Obstacles are held in spawn order, so "no two adjacent" is the
  // question, and there have to be at least two other things between one and the next.
  const spread = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true);
    window.__surf.setDist(2600); window.__surf.tick(30);
    const list = window.__surf.obsBounds().map(o => o.kind);
    let worst = 0, run = 0;
    for (let i = 1; i < list.length; i++) {
      if (list[i] === 'octopus' && list[i - 1] === 'octopus') { run++; worst = Math.max(worst, run); }
      else run = 0;
    }
    return { list, worst, n: list.filter(k => k === 'octopus').length };
  });
  check(spread.list.length > 6 && spread.worst === 0,
        'and they arrive spread through the field rather than in a wall of them',
        `${spread.n} of ${spread.list.length} in the water, ${spread.worst} back to back`);
}

// ---------- the tow plane is the modelled one ----------
// It flies past towing a rope and it is the same aircraft the set wave is thrown by, so one
// model covers both. The propeller is the thing to be careful of: the engine-failure sequence
// spins that one part by name, and a single-mesh import has none — so the original's parts
// are HIDDEN rather than thrown away and the propeller keeps turning inside the model.
{
  const pl = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.6);
    const a = window.__surf.planeAt(-40, 0, 10);
    window.__surf.tick(0.5);
    return a;
  });
  check(pl.modelled === true && pl.meshes[0] === 1 && pl.meshes[1] > 5,
        'the plane that flies past is the modelled one, with the built-in parts kept and hidden',
        `${pl.meshes[0]} of ${pl.meshes[1]} meshes drawn`);
  check(pl.size[0] > 3 && pl.size[0] < 12 && pl.size[2] > 2 && pl.size[2] < 12 &&
        pl.size[0] > pl.size[1] * 1.5,
        'and it is an aeroplane-shaped thing — wider than it is tall, and about as long',
        `${pl.size.join(' x ')}ft`);
}

// ---------- a rigged obstacle is drawn where it IS ----------
// Object3D.clone() cannot clone a rigged model. A SkinnedMesh is drawn from its SKELETON's
// world matrices, and clone() copies the mesh while leaving it pointing at the ORIGINAL's
// bones — so every octopus in the water was skinned by the template's skeleton, which sits at
// the origin and is never animated. They were all drawn in the same place, on top of the
// rider, however far out they had been spawned, and their arms never moved because the code
// was turning each clone's own bones while the picture came from bones nothing had touched.
//
// It is the trap this project keeps walking into: a reading that agrees with itself and
// disagrees with the screen. position.z said a hundred and seventy feet out and was right;
// the shader drew it at your feet. So the check is on where the VERTICES land, run through
// the same skinning maths the shader runs.
{
  const drawn = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true); window.__surf.tick(0.5);
    window.__surf.spawn('octopus', -1.0, -120);
    window.__surf.spawn('octopus', 2.0, -30);
    window.__surf.spawn('log', 3.2, -60);
    window.__surf.tick(0.05);
    return window.__surf.drawnAt();
  });
  const off = drawn.map(o => Math.abs(o.drawnZ - o.z));
  check(drawn.length >= 3 && off.every(d => d < 4),
        'a spawned obstacle is drawn where it was spawned, rig or no rig',
        drawn.map(o => `${o.kind} at ${o.z} drawn at ${o.drawnZ}`).join(', '));
  const octs = drawn.filter(o => o.kind === 'octopus');
  check(octs.length === 2 && Math.abs(octs[0].drawnZ - octs[1].drawnZ) > 60,
        'and two of the same rigged thing are drawn in two different places',
        octs.map(o => o.drawnZ).join(' vs '));
}

// ---------- the treasure chest ----------
// Its LID FACES UP. This one went wrong three times, and never in a way that a number could
// catch: the lid is modelled already open, so the bounding box is nearly a cube, no axis
// tapers, and all six faces read flat — every symmetry test says the chest is fine while it
// lies on its back with the gems pointed at the camera. What does catch it is that an open
// chest is a box with a hole in it. Fire rays at all six faces and five of them stop on the
// shell; the sixth falls through to the inside of the base. The deep one has to be +y.
{
  const open = await page.evaluate(() => window.__surf.chestOpen());
  check(open && open.open === 'y+',
        'the chest opens upward — the lid side is the one you can see into',
        `deepest face ${open && open.open}, ${JSON.stringify(open)}`);
  check(open && open['y+'] > open['y-'] * 4,
        'and it is a floor underneath rather than a second opening',
        `top ${open && open['y+']}, bottom ${open && open['y-']}`);
}
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
    text: document.querySelector('#crateReward').textContent,
    hint: document.querySelector('#crateHint').textContent,
    // the run card is built the moment the run ends and hidden while the chest has the
    // screen — if it is not still waiting here, the ceremony has nothing to hand you on to
    cardWaiting: document.querySelector('#overlay').classList.contains('hidden'),
  }));
  check(opened.reward, 'tapping it open pays out',
        `reward: ${opened.text.trim().slice(0, 60)}`);
  check(opened.cardWaiting && /carry on/i.test(opened.hint),
        'and says how to carry on rather than offering its own way out of the game',
        `hint "${opened.hint}", run card hidden: ${opened.cardWaiting}`);
  // The chest is a step between going under and the run card, so the tap that ends it has
  // to LAND you on that card — not on the menu, and not on a blank screen. It used to carry
  // its own Again/Shop/Menu, which meant the run you had just finished was built and then
  // never shown, because the chest offered a way out before you ever got to it.
  await page.evaluate(() => document.querySelector('#crateOv')
    .dispatchEvent(new PointerEvent('pointerdown', { bubbles: true })));
  const after = await page.evaluate(() => ({
    crate: document.querySelector('#crateOv').classList.contains('hidden'),
    card: !document.querySelector('#overlay').classList.contains('hidden'),
    over: document.querySelector('#overlay').className,
    again: !!document.querySelector('#againBtn'),
  }));
  check(after.crate && after.card && /\bover\b/.test(after.over) && after.again,
        'and the next tap carries on to the run card, which is where the ways on live',
        `crate away: ${after.crate}, card up: ${after.card} (${after.over})`);

  // Nothing of the menu may be on screen while a run is live, and for one version all of it
  // was. beginRun hides the overlay and then, one line later, sweeps away any chest ceremony
  // that might still be up — and that sweep put the overlay straight back, because the
  // reveal had been written into the teardown to serve its other caller. Every run started
  // with the title, the dial and Play laid over a live game, the HUD and the trick bar
  // showing through them.
  //
  // Nothing caught it because nothing asserted the plainest thing in the game: a run and a
  // menu are not both on screen. Checked down both routes into beginRun, because it was the
  // second one that broke and the first one that everything else exercises.
  const live = await page.evaluate(() => {
    const look = () => ({
      menu: !document.querySelector('#overlay').classList.contains('hidden'),
      crate: !document.querySelector('#crateOv').classList.contains('hidden'),
      dial: document.querySelector('#dPlay').getClientRects().length > 0,
      running: window.__surf.state().running,
    });
    document.getElementById('againBtn').click();         // straight on out of a ceremony
    const afterChest = look();
    window.__surf.restart();                             // and the ordinary way in
    const afterPlain = look();
    return { afterChest, afterPlain };
  });
  for (const [how, r] of [['after a chest', live.afterChest], ['the ordinary way', live.afterPlain]])
    check(!r.menu && !r.crate && !r.dial && r.running,
          `and a run started ${how} has none of the menu left on screen`,
          `menu up: ${r.menu}, chest up: ${r.crate}, dial drawn: ${r.dial}, running: ${r.running}`);
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

// ---------- the board floats in the water, not on top of it ----------
// v3.4: the hull pitch was geared at 1.5x the slope of the water under it, so on a face
// measuring 0.18 rad the board came out at 0.27 — and a board is nearly six feet long, so
// the far end stood a third of a foot clear of the surface it is supposed to be resting on.
// Riding down a face that end is the TAIL, which is where the fins are. It rode with its
// back end and all three fins in the air. A hull cannot tilt past the water it lies on:
// the tail is allowed to sit a little proud of the water beneath IT, and no more.
{
  await page.evaluate(() => { window.__surf.equip('astro'); document.getElementById('startBtn').click(); });
  await page.evaluate(() => window.__surf.tick(4));
  let worstTail = -99, wettest = 99, gear = 0, gn = 0, n = 0;
  const tails = [];
  // SIXTY samples, not thirty. A run starts on a random phase of the swell and picks up
  // speed through it, so thirty readings put the ninetieth percentile anywhere between 0.35
  // and 0.60 across runs with nothing changing — which is a check that fails one run in
  // three and teaches you to ignore it. Twice the samples is the honest fix for a question
  // that is statistical in the first place: does the tail ride high, not did it ever.
  for (let i = 0; i < 60; i++) {
    await page.evaluate(() => window.__surf.tick(0.4));
    const m = await page.evaluate(() => {
      const h = window.__surf.hullY(), b = window.__surf.buoy();
      if (!b || window.__surf.state().airborne) return null;
      // where the tail's underside actually is, the water directly under it, and the slope
      // of that water — the hull samples are taken 2.55 ft either side of the middle
      return { tail: h.py - Math.sin(h.pitch) * h.z1 + h.hullLo * Math.cos(h.pitch),
               hT: b.hT, mid: h.py + h.hullLo - b.waterY,
               pitch: h.pitch, slope: Math.atan2(b.hN - b.hT, 5.10) };
    });
    if (!m) continue;
    n++;
    worstTail = Math.max(worstTail, m.tail - m.hT);
    tails.push(m.tail - m.hT);
    wettest = Math.min(wettest, m.mid);
    // Nobody is leaning in a headless run, so all the tilt there is comes from the water.
    if (Math.abs(m.slope) > 0.08) { gear += m.pitch / m.slope; gn++; }
  }
  const geared = gn ? gear / gn : 0;
  check(gn > 5 && geared < 1.20,
        'the hull lies along the water rather than tilting past it',
        `pitch averaged ${geared.toFixed(2)}x the slope of the water under it over ${gn} samples`);
  // Half a foot covers the tail rocker and the 0.36 ft of board that overhangs the aft hull
  // sample. The old 1.5x gearing put it two thirds of a foot up with the fins in daylight.
  //
  // Taken at the NINETIETH PERCENTILE rather than at the single worst sample. One reading at
  // the top of a swell is not the fins in daylight; the tail sitting proud most of the time
  // is, and that is what this asks.
  const sorted = tails.slice().sort((a, b) => a - b);
  const p90 = sorted.length ? sorted[Math.floor(sorted.length * 0.90)] : 99;
  check(n > 10 && p90 < 0.50,
        'so the tail stays down in it and the fins stay under',
        `tail stood ${p90.toFixed(2)} ft above the water beneath it nine samples in ten, ` +
        `worst of ${n} was ${worstTail.toFixed(2)}`);
  check(n > 10 && wettest < -0.05,
        'and the hull floats down IN the surface rather than perched on it',
        `midships underside sat ${wettest.toFixed(2)} ft below the waterline at its shallowest`);
}

// ---------- a half-turn leaves the board turned round ----------
// Landing a 180 used to snap the board back to nose-first under a rider who was now facing
// the other way. Riding switch means the whole thing came round: fins forward, paint where
// the spin left it. Both the rider AND the board carry the half turn, and both give it back.
{
  // FROM A KNOWN STANCE. landAt toggles, so the pair of readings below only mean what they
  // say if the rider starts nose-first — and whether he does depended on everything the suite
  // had done before this point. A restart is the one thing that settles it.
  const spun = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.tick(0.4);
    window.__surf.landAt(0, Math.PI); return window.__surf.stance(); });
  const half = x => Math.abs(Math.abs(((x + Math.PI) % (Math.PI * 2)) - Math.PI) - Math.PI) < 0.02;
  check(spun.sw && half(spun.board),
        'a landed 180 leaves the board itself backwards, fins and paint included',
        `switch=${spun.sw}, board yaw ${spun.board}`);
  const back = await page.evaluate(() => { window.__surf.landAt(0, Math.PI); return window.__surf.stance(); });
  check(!back.sw && Math.abs(back.board) < 0.02,
        'and a second one turns it back',
        `switch=${back.sw}, board yaw ${back.board}`);
}

// ---------- the run card's controls are the buttons you already know ----------
// Four stacked pills took half the card and read as a form. The card carries the home
// dial's own buttons instead — same class, same glass, same size — in a single row, shop
// included. If one of them ever falls out of the row or off the class, this says so.
{
  await page.evaluate(() => { window.__surf.wipeNow('foam'); });
  for (let i = 0; i < 40; i++) {
    await page.evaluate(() => { try { window.__surf.tick(0.25); } catch (e) {} });
    await page.waitForTimeout(200);
    // A CHEST caught earlier in the suite comes up first and HIDES the run card behind it —
    // the ceremony is a step on the way, not a card of its own — so this waited out its forty
    // tries and then measured five buttons laid out inside a hidden overlay. Stepped past
    // through the game's own carry-on rather than by clicking the overlay, because which
    // element carries that handler is not a thing a check should have to know.
    await page.evaluate(() => { if (window.__surf.chestSkip) window.__surf.chestSkip(); });
    // Waits for the thing being MEASURED, not for a class name that is one step before it.
    // The overlay can carry the right classes a frame before its contents have a box, and a
    // check that stops at the class then measures five buttons at zero by zero.
    if (await page.evaluate(() => { const c = document.getElementById('overlay').className;
          if (!(c.includes('over') && !c.includes('hidden'))) return false;
          return document.getElementById('againBtn').getBoundingClientRect().width > 1; })) break;
  }
  const row = await page.evaluate(() => {
    // the dial is display:none behind the card, so its SIZE comes from the style rather
    // than from a box it does not currently have
    const dial = getComputedStyle(document.getElementById('dBoard'));
    const ids = ['reviveBtn', 'againBtn', 'shopBtn', 'menuBtn', 'shareBtn'];
    const ov = document.getElementById('overlay').className;
    return { dial: [Math.round(parseFloat(dial.width)), Math.round(parseFloat(dial.height))],
             ovCls: ov,
             card: ov.includes('over') && !ov.includes('hidden'),
             btns: ids.map(id => { const e = document.getElementById(id), b = e.getBoundingClientRect();
               return { id, cls: e.className, w: Math.round(b.width), h: Math.round(b.height),
                        top: Math.round(b.top), left: Math.round(b.left),
                        shown: getComputedStyle(e).display !== 'none' }; }) };
  });
  const on = row.btns.filter(b => b.shown);
  const sameSize = on.every(b => b.w === row.dial[0] && b.h === row.dial[1]);
  const sameRow = on.every(b => Math.abs(b.top - on[0].top) <= 1);
  const inOrder = on.every((b, i) => i === 0 || b.left > on[i - 1].left);
  check(row.card && on.length >= 4 && sameSize && sameRow && inOrder &&
        on.every(b => /\bhbtn\b/.test(b.cls) && /\bglass\b/.test(b.cls)),
        'the run card carries the dial buttons in one row, shop among them',
        `${on.map(b => b.id).join(', ')} at ${on[0].w}x${on[0].h} against the dial's ` +
        `${row.dial.join('x')}, card up: ${row.card}, overlay "${row.ovCls}"`);
  check(on.some(b => b.id === 'shopBtn') && on.some(b => b.id === 'againBtn'),
        'and both the ways on are there: surf again and the shop');
  // the old pill row must be gone from the card, or the card is twice as tall as it needs
  const visiblePills = await page.evaluate(() =>
    [...document.querySelectorAll('#ovBtns .go')].filter(e => e.getClientRects().length > 0).length);
  check(visiblePills === 0, 'and none of the old pills are left on it', `${visiblePills} still showing`);
}

// ---------- the page boots on the menu, not on a half-dressed one ----------
// The markup starts in whatever state it is written in, and the line that sorts it out is
// eight thousand lines into a 700KB file — which on a phone is a real wait. It used to boot
// with the HUD across the top and the run card's orange Play pill lying across the middle
// of the menu, on top of the dial's own Play. Both of those are decided in the markup now,
// so the first paint is the menu. Checked on the SOURCE, because by the time a browser has
// finished with it the script has been and gone.
{
  const src = await (await fetch(`http://127.0.0.1:${PORT}/index.html`)).text();
  const head = src.slice(0, src.indexOf('<div id="homeDial"'));
  check(/<div id="overlay" class="[^"]*\bmain\b/.test(head),
        'the page boots on the main menu, so the run card\'s pill is never on it');
  check(/<div id="hud" style="display:\s*none/.test(head),
        'and boots with no HUD, which is what a menu has');
  // AND NOTHING LAID OVER IT. The overlay carried a radial scrim nearly half black through
  // the middle of the frame, and on the title screen that is over the beach, the palm, the
  // rider and the sea — all of which are the picture rather than a backdrop for text. It read
  // as a dark sheet across the game. The run CARD keeps it, because that is a sheet of text
  // over a frozen wipeout and needs the separation.
  const scrim = await page.evaluate(() => {
    const o = document.getElementById('overlay'), was = o.className;
    o.className = 'main';
    const menu = getComputedStyle(o).backgroundImage;
    o.className = 'over';
    const card = getComputedStyle(o).backgroundImage;
    o.className = was;
    return { menu, card };
  });
  check(scrim.menu === 'none' && /gradient/.test(scrim.card),
        'and with nothing laid over the beach, though the run card keeps its own',
        `menu "${scrim.menu}", card "${scrim.card.slice(0, 40)}"`);
  // and the menu's furniture waits for the beach behind it, or the first thing you get is a
  // title and six buttons on an empty gradient — a separate, broken-looking screen that then
  // turns into the real one
  check(/<body class="[^"]*\bboot\b/.test(head) &&
        /body\.boot #overlay[^}]*opacity:\s*0/.test(src),
        'and the title and the dial wait for the scene rather than arriving without it');
  // The beach is built inside the blocking script, not on the first frame, so the first paint
  // IS the finished screen. Asked of the running game, not of the source: the previous
  // version of this check was a regex, it matched the crate ceremony's button handler, and
  // the line it was checking for had in fact been bolted onto that handler by mistake and
  // never ran at boot at all. A check that can pass while the feature does nothing is worse
  // than no check.

  // and the script must still be able to turn it back on when a run starts
  check(/hud\.style\.display\s*=\s*''/.test(src), 'and the run still turns the HUD on');
}

// ---------- the composite, and what it is for ----------
// Every screen frame goes through a render target now, and two things about that are worth
// pinning. One: the target has to be MULTISAMPLED, because the default framebuffer's own
// antialiasing does not follow you into a target, and trading the palm's edges for the sun's
// glow is not a trade worth making. Two: the sun has to be genuinely brighter than white, or
// the filmic curve maps its 1.0 down to 0.8 and it never crosses the bloom threshold at all
// — a disc darker than paper, which is exactly what it was.
{
  const fx = await page.evaluate(() => window.__surf.fx());
  check(fx.on && fx.samples >= 4,
        'the frame is composited, and not at the cost of the antialiasing',
        `${fx.samples}x multisampled`);
  check(fx.sunToneMapped === false && fx.sunHot > 1.0,
        'and the sun is over white, so the lens has something to bleed',
        `${fx.sunHot}x white, tone-mapped ${fx.sunToneMapped}`);
  // The threshold decides whether this reads as light or as a filter: the sky fills most of
  // a frame at around 0.7, and anything near that blooms the sky into itself and turns the
  // sunset to milk. It has to sit above what a tone-mapped white surface comes out at.
  // The first pass at these was a filter, not a light: the sky sits around 0.7 linear and
  // lit sand is not far behind it, so anything near that blooms the frame into itself and
  // everything wears a white sheen. Only what is genuinely AT white gets to bleed.
  check(fx.thresh >= 0.93 && fx.amount <= 0.40,
        'and it catches only what is at white, not the whole frame',
        `threshold ${fx.thresh}, amount ${fx.amount}`);
  // pow(1/2.2) and sRGB agree in the midtones and part company down low, where the
  // approximation lifts every shadow by about a tenth — which is what "faded" means.
  const fxSrc = await readFile(join(ROOT, 'index.html'), 'utf8');
  check(/1\.055\*pow\(c,vec3\(1\.0\/2\.4\)\)/.test(fxSrc) && !/1\.0\/2\.2/.test(fxSrc),
        'and the frame is encoded with the real sRGB curve rather than a gamma that lifts the darks');
  // The glitter track has to run from the sun you can SEE. The directional light that shades
  // the water is a constant set at load; the sun crosses the sky over a run, and a track
  // keyed to the light would have sat in the wrong place all day.
  const glit = await page.evaluate(() => {
    // on a fresh run, because the sun's height is a function of how far you have got and
    // whatever the suite left behind may not be moving at all
    window.__surf.restart(); window.__surf.invuln(true);
    window.__surf.tick(6);
    const a = window.__surf.fx().glitter;
    window.__surf.tick(90);
    return { a, b: window.__surf.fx().glitter };
  });
  check(glit.a.y !== glit.b.y,
        'and the water glitters at the sun in the picture, not at a constant',
        `sun height ${glit.a.y} -> ${glit.b.y} over the run`);
}

// ---------- the light belongs to the sky it is under ----------
// The scene lights were nailed down on purpose once: moving or warming them repaints the
// ocean, the animals, the board and the pug. What it cost was that the rider is lit from the
// same angle at dusk as at noon, in the same white, under a sky that has gone orange —
// nothing in the frame agreeing with anything else, which is most of why a run read flatter
// than the menu. The direction follows the disc now and the colour follows part of the way;
// only part, because a full sunset key would swamp forty-six paint schemes.
{
  const lit = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true);
    window.__surf.tick(8);
    const a = window.__surf.key();
    window.__surf.tick(150);
    return { a, b: window.__surf.key() };
  });
  const moved = Math.max(...[0, 1, 2].map(i => Math.abs(lit.a.dir[i] - lit.b.dir[i])));
  check(moved > 0.05, 'the key light crosses the sky with the sun rather than standing still',
        `${JSON.stringify(lit.a.dir)} -> ${JSON.stringify(lit.b.dir)}`);
  check(lit.a.col !== lit.b.col || lit.a.ground !== lit.b.ground,
        'and warms with it, along with what bounces back up off the water',
        `key #${lit.b.col.toString(16)}, bounce #${lit.b.ground.toString(16)}`);
  // never from below, whatever the disc does — a light under the chin is a torch, and the
  // sun going down is carried by intensity instead
  check(lit.a.dir[1] > 0.1 && lit.b.dir[1] > 0.1,
        'and never gets under it', `height ${lit.a.dir[1]} -> ${lit.b.dir[1]}`);
}

// ---------- spray is water, not beads ----------
// Six-by-five spheres in flat white is what a droplet is NOT: water off a rail is torn, half
// air, and has no edge. A hard little ball reads as a bead and a hundred of them read as
// polystyrene. And a sprite has no idea where the sea is, so it cut through the surface with
// a hard ellipse the moment it landed — the giveaway in every frame of this game. The proper
// fix is a depth buffer; the one intersection that matters is with a surface whose height is
// already known to the millimetre, so it is done against that for nothing.
{
  const sp = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true);
    // stepped finely, or every drop in the air was thrown on the same frame and they are
    // all necessarily at the same point in the same life
    window.__surf.tick(2);
    for (let i = 0; i < 14; i++) window.__surf.tick(0.04);
    const a = window.__surf.spray();
    for (let i = 0; i < 6; i++) window.__surf.tick(0.04);
    return { a, b: window.__surf.spray() };
  });
  check(sp.a.sprite && sp.a.soft, 'a drop is a soft sprite rather than a little sphere',
        JSON.stringify({sprite: sp.a.sprite, soft: sp.a.soft}));
  check(sp.a.own, 'and each carries its own opacity, because each is at its own point in its own life');
  const fade = await page.evaluate(() => window.__surf.sprayProbe());
  check(fade && fade.near[0] > fade.near[1] + 0.3,
        'and one about to land is dimmer than one still in the air, so none of them cut the surface',
        fade ? `surface fade ${fade.near[0]} at 3ft up against ${fade.near[1]} at the waterline` : 'no probe');

  // Riding along is not an event. Two drops used to be thrown every time the board fell
  // faster than 5.5 through the chop, and on an ordinary swell at speed that is most
  // frames — so a permanent scatter of small bright dots lay in the wake behind him and
  // read as glitter rather than as spray, which is what it had been taken for three times
  // running. Everything that throws water because something HAPPENED still does; this
  // measures the one case where nothing has.
  const idle = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.invuln(true);
    window.__surf.tick(6);                          // a good long stretch of open water
    return { live: window.__surf.spray().live, mph: window.__surf.state().speed };
  });
  // Bounded at a couple rather than at zero, and NOT because zero was too strict on the
  // board. Something else in the sea throws the odd drop — a creature surfacing, once every
  // seven seconds or so on a random roll — and pinning this at zero made the check turn on
  // that coin flip: it passed on the run it shipped on and failed on the next one for a
  // reason that had nothing to do with the board. Before the fix this read thirty-three, so
  // a bound of two still catches the regression by a mile without being decided by luck.
  check(idle.live <= 2, 'and riding open water throws next to none, because nothing has happened',
        `${idle.live} drop(s) still in the air after six seconds at ${Math.round(idle.mph)}`);
}

// ---------- and he rides like one ----------
// Every photograph of a surfer trimming has the knees loaded and the chest out over the
// front foot. This one stood to attention: the rig only bent under g-force, so cruising
// straight he was a man standing upright on a bus. The catch is that deepening the bend in
// this rig swings the hips through the leg chain as well — drop the pelvis on top of it, as
// looks obvious, and his feet go a quarter of a foot INTO the deck, which from directly
// behind you cannot see has happened. Measured in the board's own frame, because the sea
// heaves and the hull pitches and a world height answers a different question every frame.
{
  const feet = await page.evaluate(async () => {
    window.__surf.restart(); window.__surf.invuln(true);
    // Two seconds was not enough, and what was left in it was the SWELL. The crouch eases in
    // over about a second, but the wave goes on heaving the hull under it the whole time, and
    // the residual this reads is that — so the answer depended on where the swell happened to
    // be when the run restarted, which depends on every test above this one. Measured across
    // five different starting phases it swung 0.020 to 0.047 against a limit of 0.06, and the
    // check passed or failed on nothing to do with the stance. At six seconds it converges:
    // 0.011 to 0.020 across the same five. Not longer than that — twelve seconds of run is
    // long enough to meet something, and one of those phases threw 0.117 off a wave event.
    const settle = async () => { for (let i = 0; i < 120; i++) window.__surf.tick(0.05); };
    const was = window.__surf.crouch();
    window.__surf.crouch(0, 0); await settle();
    const flat = window.__surf.footProbe();
    window.__surf.crouch(was.c, was.h); await settle();
    return { flat, crouched: window.__surf.footProbe(), depth: was.c };
  });
  check(feet.depth > 0.15, 'he rides in a stance rather than standing to attention',
        `${feet.depth} rad of knee on top of whatever the wave asks for`);
  const moved = Math.max(Math.abs(feet.crouched.front - feet.flat.front),
                         Math.abs(feet.crouched.back - feet.flat.back));
  check(moved < 0.06, 'and the stance keeps his feet on the deck rather than through it',
        `worst foot moved ${moved.toFixed(3)}ft against the board`);

  // Every arm pose in animateRig had the sign of rotation.z backwards, and had had it from
  // the beginning. The front shoulder socket sits at negative x and a positive z swings the
  // arm toward positive x, so "both paws out wide" swung both arms IN, across the chest,
  // where the torso swallowed them whole. Nobody caught it because the camera sits dead
  // astern of a rider standing side-on: the arms pointed at the lens and away from it, so
  // out and folded looked the same — a paw-shaped lump on his own belly, either way.
  const arms = await page.evaluate(() => window.__surf.armProbe());
  check(arms.front < -arms.half && arms.back > arms.half,
        'and his paws are outside his body rather than folded across his own chest',
        `front ${arms.front}, back ${arms.back}, against a half-width of ${arms.half}`);
  check(Math.sign(arms.front) === Math.sign(arms.sockF),
        'and each paw is on the side its own shoulder is',
        `front paw ${arms.front} off a socket at ${arms.sockF}`);

  // A paw is the rounded END of an arm. It should be a shade thicker than the wrist it caps
  // and no more — much past that and it stops being a paw and becomes a ball tied to a
  // string, which is what a balloon animal is made of.
  //
  // This was invisible in the code and obvious in a render. `armLen` is applied as a scale
  // on the shoulder JOINT, so it multiplied everything hanging under it — including the paw
  // mesh. The arm TUBE hangs off nothing: it is swept in the rig's own space from the joint
  // positions, and its radius comes straight off armProf untouched. So one end of the same
  // limb was scaled and the other was not, and nobody noticed because every character was
  // wrong by a different factor. The pug has the longest arms in the roster at 2.10 and his
  // paws came out 2.49x the wrist they sat on.
  //
  // Checked on EVERY character, because the factor is armLen and armLen is per-character:
  // the same bug reads as balloon hands at one end of the roster and pinheads at the other.
  const paws = await page.evaluate(() => {
    const out = {};
    for (const id of window.__surf.charIds()) out[id] = window.__surf.pawProbe(id);
    return out;
  });
  // ratio is the paw's THINNEST axis against the wrist, so a frog's splayed paddle and a
  // penguin's flipper — both deliberately wide — are judged on how fat they are, not how
  // broad. Before the fix the pug measured 2.21 here and the short-armed animals sat under
  // half. Bounded both ways: one-sided, this check would have passed the bug it was written
  // for in half the roster.
  let worstHigh = { id: '-', r: 0 }, worstLow = { id: '-', r: 99 };
  for (const [id, p] of Object.entries(paws)) {
    for (const k of ['front', 'back', 'footF', 'footB']) {
      if (!p[k]) continue;
      if (p[k].ratio > worstHigh.r) worstHigh = { id: `${id} ${k}`, r: p[k].ratio };
      if (p[k].ratio < worstLow.r) worstLow = { id: `${id} ${k}`, r: p[k].ratio };
    }
  }
  // There was a check here that the shoulder swell stayed inside the skin of the body it
  // was stuck on. The swell is gone — a separate mass parked on a torso has its own closed
  // silhouette however carefully it is sized, and the limb sweep does that job properly by
  // growing out of the trunk instead — so the check goes with it rather than sitting here
  // measuring nothing and reporting undefined. What it was really guarding is worth keeping
  // in words: when a part is scaled off one thing and attached to another, nothing compares
  // the two, and reshaping either one silently pushes the part through the surface.
  check(worstHigh.r <= 1.75,
        'no paw is a balloon tied to the end of a limb',
        `fattest is ${worstHigh.id} at ${worstHigh.r}x the wrist it caps`);
  check(worstLow.r >= 0.45,
        'and none of them has shrunk to a pinhead on a full-thickness wrist',
        `thinnest is ${worstLow.id} at ${worstLow.r}x`);
}

// ---------- a rider is an animal, on a board that is a board ----------
// A world unit here is a FOOT — that was settled when the boards were cut to their published
// spec sheets, and everything else in the water turns out to agree with it: the buoy measures
// about three feet, the log five, the ramp five by two and a half. Each is roughly what the
// real thing measures. The RIDER was the one object that did not: one foot eleven, standing
// on a six foot board, which is why the board read as a barge under him. Normalised rather
// than multiplied, so every character comes out the same height — they all stand on the same
// boards and pass the same buoys.
//
// Two foot two, walked down from five foot six a screenshot at a time — 5.60, 4.60, 4.15,
// 3.70, 3.35, 2.95, 2.20. It has only ever come down, and the last step was measured off the
// v2.60 screenshots rather than guessed: the rider there is about a third of the board. He is a pug, drawn from a
// reference of one standing on a longboard at about half its length; at an adult human's
// height he covered the deck end to end. But height was only ever half of it — see the width
// check below, which is the half that actually made him look too big for the board.
{
  // measured on a freshly built character, because a pose changes a person's box — the menu
  // holds him in a portrait stance and he stands a few inches taller in it, as anyone does
  const sc = await page.evaluate(async () => {
    window.__surf.wear('pug');
    await new Promise(r => setTimeout(r, 250));
    return window.__surf.scaleProbe();
  });
  check(Math.abs(sc.riderFt - 2.20) < 0.05 && Math.abs(sc.boardFt - sc.boardSpec) < 0.4,
        'the rider stands about a third of the length of the board he is on',
        `${sc.riderFt}ft on a ${sc.boardFt}ft board`);
  // and he FITS it. Height was never the thing that made him look too big for the board: he
  // measured 4.60 tall against a 5.83 board and still swamped it, because the body was 1.40
  // across and 1.67 through on a deck 1.42 wide — broader through the ribs than the board and
  // half again as thick as he was wide. Nothing on two legs is built like that.
  check(sc.bodyW < sc.board.x && sc.bodyD < sc.bodyW,
        'and his body fits between the rails, and is broader than it is thick',
        `${sc.bodyW}ft wide by ${sc.bodyD}ft thick, on a deck ${sc.board.x}ft across`);
  // and the props he rides past were right all along — this is the check that says the
  // rider was the odd one out rather than everything else being small
  // The buoy is a NAVIGATION buoy now, deliberately much bigger than the little striped
  // float it replaced — that is the point of it, and of the small one beside it. What still
  // has to hold is that it is furniture in the water rather than scenery on the horizon: a
  // thing you steer round, taller than the rider and shorter than the wave.
  check(sc.buoy.y > 2 && sc.buoy.y < 8 && sc.log.x > 3 && sc.log.x < 8,
        'and the buoys and logs he passes were already at that scale',
        `buoy ${sc.buoy.y}ft tall, log ${sc.log.x}ft long`);
  const tall = await page.evaluate(async () => {
    const out = {};
    for (const c of ['pug', 'ant', 'sloth', 'crab']) {
      try { window.__surf.wear(c); } catch (e) { continue; }
      await new Promise(r => setTimeout(r, 200));
      out[c] = window.__surf.scaleProbe().riderFt;
    }
    return out;
  });
  const hs = Object.values(tall);
  check(hs.length >= 3 && Math.max(...hs) - Math.min(...hs) < 0.05,
        'and every character is the same person, whichever animal he is',
        JSON.stringify(tall));
}

// ---------- haptics ----------
// The Settings switch was turning nothing on and off for the reporter, and the reason is
// that navigator.vibrate was the only thing being called: iOS Safari has never implemented
// it, so on an iPhone the whole feature was a no-op. Where the API exists it is still used.
{
  const vib = await page.evaluate(() => {
    const calls = [];
    try { Object.defineProperty(navigator, 'vibrate',
            { configurable: true, value: p => { calls.push(p); return true; } }); }
    catch (e) { return null; }
    document.getElementById('dSettings').click();     // a menu tap goes through buzz()
    document.getElementById('setClose').click();
    return calls;
  });
  check(vib && vib.length > 0, 'where the vibration API exists, that is what buzzes',
        `${vib ? vib.length : 0} call(s)`);
}

// ---------- the crash camera does not fall into the whirlpool ----------
// waveH carries the whirlpool's dip and a whirlpool is ten metres deep, so anything using
// the sea under the RIDER as a floor for the camera follows the bowl down — and ends up
// under the surrounding water, looking out through the back of the swirl. Two lines were
// doing it: the close-in framing beat and the wide hold after it. He is the thing to watch,
// going round it. (The splash and the landing still use the real surface: that is where he
// actually hits.)
{
  const dive = await page.evaluate(async () => {
    window.__surf.restart();
    window.__surf.invuln(false);      // earlier checks leave it on, and it cannot drown him
    window.__surf.tick(6);
    window.__surf.whirlNow();
    const seen = [];
    for (let i = 0; i < 16; i++) {
      window.__surf.tick(0.25);
      const s = window.__surf.camVsSea();
      if (window.__surf.state().wipe && s.dip < -1) seen.push(s.over);
    }
    return seen;
  });
  const worst = dive.length ? Math.min(...dive) : null;
  check(dive.length >= 4 && worst > 0,
        'the crash camera stays above the sea around a whirlpool rather than dropping into it',
        `closest it came was ${worst}ft over, across ${dive.length} frames in the bowl`);
}

// ---------- nothing on a face may share a surface with the skull ----------
// Z-fighting on something that MOVES does not read as z-fighting. It reads as the feature
// crawling around the face, which is what the ant's eyes and smile were doing: the eye's
// DEPTH had been scaled with the head, which pulled it inside, so what showed was whichever
// sliver the curve of the skull left over — and that sliver moves as the head does. The size
// follows the head; the depth is measured against the front of the face and stays put.
{
  const face = await page.evaluate(async () => {
    window.__surf.wear('ant');
    await new Promise(r => setTimeout(r, 300));
    return window.__surf.faceProbe();
  });
  check(face.parts.length >= 5 && face.parts.every(p => p.off),
        'every part of a face is pushed in front of the skull in the depth test',
        `${face.parts.filter(p => p.off).length}/${face.parts.length} offset`);
  const src2 = await readFile(join(ROOT, 'index.html'), 'utf8');
  // the 'wide' eye specifically: x and y follow the head, z is a bare constant measured
  // against the front of the face. (The alien's eyes DO scale in z, on purpose.)
  check(/w\.position\.set\(sx\*0\.125\*D\[0\],0\.10\*D\[1\],-0\.30\)/.test(src2),
        'and its depth is not scaled with the head, which is what buried it');
  // and the ramp's lit bar sits ON its deck rather than half inside it — a bright cyan bar
  // fighting the deck for the same depth is the top of the ramp flickering light blue
  const ramp = await page.evaluate(() => window.__surf.rampProbe());
  check(ramp && ramp.lipLow > ramp.deck && ramp.off,
        'and the ramp\'s lit bar rests on its deck instead of inside it',
        ramp ? `bar bottom ${ramp.lipLow} against a deck at ${ramp.deck}` : 'no ramp');
}

// ---------- a rig with the joints in it ----------
// Drawn against a humanoid biped rig and a shelf of plush-toy characters, and the gap was
// never the shading — it was that half the joints existed as transforms and none of them as
// shapes. The head was lifted above the shoulder line by 0.30 to 0.72 of its own RADIUS, so
// on every character in the roster the bottom half of the skull was inside the chest: no
// neck, no throat, no chin, head and body one blob with ears on it. The arms grew straight
// out of a smooth torso with no shoulder over the socket. Both are geometry now, and this is
// the check that the head actually clears what it is supposed to be sitting on.
{
  const necks = await page.evaluate(async () => {
    const out = {};
    for (const c of ['pug', 'cat', 'panda', 'corgi', 'penguin']) {
      try { window.__surf.wear(c); } catch (e) { continue; }
      await new Promise(r => setTimeout(r, 180));
      out[c] = window.__surf.neckProbe();
    }
    return out;
  });
  // Not "the head clears the shoulder line" — it does not, and it should not. A skull sits
  // INTO its shoulders by a little; what it may not do is sit into them by half of itself.
  // The old lift was 0.30 to 0.72 of a radius, which puts the bottom of the head a good 0.44
  // of a radius under the line on a short-necked animal; it is within a twentieth of it now.
  // Measured against the head's own radius so it means the same thing on a cat and a panda.
  // Bounded BOTH ways, and that is not pedantry — the first two versions of this check were
  // green while reporting a corgi whose head floated three and a half radii above its
  // shoulders, because they only ever asserted a floor. A one-sided check cannot tell a fixed
  // rig from a broken probe. The head must sit near the line: not half-buried under it, which
  // is what the old rig did at -0.44 radii, and not hovering over it either.
  const rows = Object.entries(necks);
  check(rows.length >= 4 && rows.every(([, n]) =>
          Math.abs(n.gap) < 0.25 * n.headR && n.neck > 0.02),
        'every head sits ON its shoulders — not buried in them, not floating over them',
        rows.map(([k, n]) => `${k} ${(n.gap / n.headR).toFixed(2)} radii, neck ${n.neck}`).join(', '));

  // and the pupil still shows on a head the house proportions made a third bigger
  const eyes = await page.evaluate(async () => {
    window.__surf.wear('ant');
    await new Promise(r => setTimeout(r, 200));
    return window.__surf.eyeProbe();
  });
  check(eyes && eyes.proud > 0.012,
        'and a wide eye keeps its pupil in front of its white, whatever size the head is',
        eyes ? `pupil stands ${eyes.proud} proud on a ${eyes.headR} skull` : 'no wide eye found');
}

// ---------- the pug is a pug ----------
// Drawn from a reference, and every one of the three faults below was found by looking at a
// render rather than by reasoning about the numbers — which is exactly why they are worth a
// check. An ear parked on the side of a round head clears the silhouette and stands up as a
// dark blade: that is a horn, not an ear, and it happened at every position and angle tried
// until the ear became a CAP of a sphere concentric with the skull. Black carried across the
// bridge at eye height is a pair of sunglasses. And an eye that does not clear the skull is a
// bead down a socket, which is what the old 7 cm bead on the side of the head was.
{
  const f = await page.evaluate(async () => {
    window.__surf.wear('pug');
    await new Promise(r => setTimeout(r, 300));
    return window.__surf.pugFace();
  });
  check(f.ear > f.skull && f.ear < f.skull + 0.09,
        'his ears lie on the skull instead of standing off it as horns',
        `ear reaches ${f.ear} on a ${f.skull} skull`);
  check(f.eyeY !== null && f.maskTop < f.eyeY,
        'and the mask goes up BETWEEN his eyes rather than across them',
        `black on the bridge tops out at ${f.maskTop}, eyes at ${f.eyeY}`);
  check(f.eyeOut > f.skull + 0.03,
        'and his eyes stand proud of the skull rather than sunk into it',
        `eye reaches ${f.eyeOut} on a ${f.skull} skull`);
}

// ---------- nothing shows through a panel, and nothing is a pill any more ----------
// A panel dims the beach and used to leave the home dial visible behind it, which sounds
// harmless: what it actually produced was the panel's row dividers running left and right
// THROUGH the glass buttons, so the screen showed a white line joining one button to the
// next. Watched with an observer rather than hooked into each open and close, because there
// are four panels and several ways into each, and a class that has to be taken off by hand
// in eight places is a class that gets left on.
{
  // The chest carries no way out of the game at all any more — no button, and no handler
  // left behind reaching for one that is gone. There is exactly one set of ways on out of a
  // finished run and it lives on the run card.
  const crateEmpty = await page.evaluate(() => ({
    btns: document.querySelectorAll('#crateOv button').length,
    ids: ['crateAgain', 'crateShop', 'crateMenu', 'crateBtns'].filter(i => document.getElementById(i)),
  }));
  check(crateEmpty.btns === 0 && crateEmpty.ids.length === 0,
        'and the chest carries no menu of its own — it is a step, not a way out',
        `${crateEmpty.btns} button(s), leftovers: ${crateEmpty.ids.join(',') || 'none'}`);
  // the boot paint is a colour with nothing to say, not a soft-focus photograph of a beach
  const pSrc = await readFile(join(ROOT, 'index.html'), 'utf8');
  check(/html,body\s*\{[^}]*background:#[0-9a-f]{6};/i.test(pSrc) &&
        !/background:linear-gradient\(180deg,#1668c4/.test(pSrc),
        'and the page boots on one flat colour rather than a blurred beach');
}

// ---------- the imported rider ----------
// models/pug.glb is a rigged export with a handstand clip in it, and it replaces Astro's
// procedural body. Almost everything about wiring one of those up fails SILENTLY: the mesh
// still draws, the bones still turn, the numbers all read correctly, and the picture is
// wrong. Each of these is a specific silence that cost real time to find.
{
  // AFTER a tick, every time. Nothing about the rider is placed or posed until a frame runs
  // — the stance, the ground contact and the exit from the title-screen show all happen
  // inside update() — so reading straight off a restart reads whatever the menu left behind.
  const rig = await page.evaluate(() => {
    window.__surf.restart(); window.__surf.tick(0.4); return window.__surf.rigInfo(); });
  check(rig.model === true && rig.bones >= 20 && rig.stand === true,
        'the rigged rider loads with its skeleton and its handstand clip',
        `model ${rig.model}, ${rig.bones} bones, clip ${rig.stand}`);
  // He is fitted to the box the built-in rider occupied, by his FEET rather than his middle:
  // a model whose legs are a different fraction of his height ends up planted through the
  // deck or hovering over it.
  // Against the built-in rider's own SOLES, not against the bottom of his bounding box. The
  // box bottom is whatever hangs lowest on him — a tail, a dropped paw — and sits a fifth of
  // a foot under the deck, so standing an imported rider on it buries his feet by exactly
  // that. Which is what it did.
  // ...after a tick. The placement happens inside the frame, so asking straight off a restart
  // asks where he was before anything put him anywhere.
  const g0 = await page.evaluate(() => { window.__surf.tick(0.4); return window.__surf.deckGap(); });
  check(g0 && Math.abs(g0.gap) < 0.05,
        'and his feet land on the deck the built-in rider stands on',
        `${g0 ? g0.gap.toFixed(3) : '?'}ft between his soles and the ones already down there`);
  // THE SKIN FOLLOWS THE BONES. In this build of three.js that is a flag on the material,
  // and the game replaces the loader's material with its own — which does not set it. Every
  // other reading stays right when it is missing: the bones turn, the skeleton reports a
  // body upside down, three.js's own applyBoneTransform puts the vertices where a handstand
  // puts them. Only the picture disagrees, and only if you look at it.
  const kick = await page.evaluate(() => window.__surf.boneKick('LeftArm', 'x', 1.2));
  const moved = kick && kick.after && Math.max(...kick.before.map((v, i) => Math.abs(v - kick.after[i])));
  check(moved > 0.05,
        'and the skin actually deforms with them, rather than drawing its bind pose forever',
        `worst corner of the body moved ${moved ? moved.toFixed(3) : 'n/a'} by a 1.2rad shoulder`);
  // Every rider in this game is BUILT facing -z; this one was exported facing +z, and
  // fitToBox may have turned it a further quarter for its own reasons. So the model measures
  // its own muzzle — off the `headfront` bone the rig ships — and turns itself onto -z. A
  // hard-coded half turn was tried first and was wrong twice over, leaving him side-on to
  // the wave. Read in the rider group's frame, before the handstand: the surf stance and the
  // turn both sit outside it, and the question here is only whether the model arrived
  // pointing the same way the built-in body does.
  // ...and then a further RIDER_YAW on top, so he stands square across the board with his
  // chest down the line instead of three-quarters away from the camera. Measured against the
  // direction that yaw intends rather than against a bare -z, so the check still fails if the
  // auto-turn breaks — it just does not insist he face somewhere no surfer faces.
  const face = rig.faceDir, YAW = -0.42;
  const off = face && Math.abs(Math.atan2(-face[0], -face[2]) - YAW);
  check(off !== null && off < 0.22,
        'and the imported body arrives square across the board, facing down the line',
        face ? `muzzle points ${JSON.stringify(face)}, ` +
               `${Math.round((off || 0) * 180 / Math.PI)}° off the stance it should hold` : 'no bones');
  // The clip is a round TRIP — stand, kick up, hold, come down, stand — so playing it once
  // and clamping the last frame leaves him on his feet. It is driven instead: forward to the
  // frame the HAND LANDS on, parked there while the button is down, and rewound to come back
  // off it. Pressed as a BUTTON here rather than scrubbed to a frame, because the button is
  // the path the game actually runs and the scrub skips the whole state machine.
  const up = await page.evaluate(() => {
    window.__surf.hand(true); window.__surf.tick(1.6); return window.__surf.rigInfo(); });
  check(up.upY < -0.15, 'and pressing HAND actually turns him over onto his hands',
        `his own up-axis points ${up.upY} through the deck, planting at ${up.plantT}s`);
  // ...and stops where the hand LANDS rather than carrying on to a full vertical. The pose to
  // hold is the frame the palm takes his weight, because that is the frame there is something
  // under it; past that the animator stretches him out with the hand well clear, which on a
  // floor reads as a handstand and on a surfboard is a rider balancing on nothing.
  const held = await page.evaluate(() => {
    const out = [];
    for (let i = 0; i < 8; i++) { window.__surf.tick(0.15);
      const r = window.__surf.rigInfo(), g = window.__surf.deckGap();
      out.push({ up: r.upY, phase: r.phase, gap: g && g.gap, t: r.ct }); }
    return out;
  });
  check(held.every(h => h.phase === 'hold'),
        'and stays there for as long as the button is down',
        `${held.filter(h => h.phase === 'hold').length}/8 frames held`);
  check(held.every(h => h.gap !== null && Math.abs(h.gap) < 0.06),
        'with his hand ON the board the whole time he is up there',
        `worst ${Math.max(...held.map(h => Math.abs(h.gap))).toFixed(3)}ft off the deck`);
  check(Math.max(...held.map(h => h.up)) - Math.min(...held.map(h => h.up)) < 0.35,
        'and the body parked rather than swinging through the pose',
        `${(Math.max(...held.map(h => h.up)) - Math.min(...held.map(h => h.up))).toFixed(2)} of up-axis across the hold`);
  // SIDE ON, and ON the board. Two more things the clip does that belong on a floor and not
  // on a surfboard: it turns him through eighty degrees on the way over, which swings him
  // from side-on to showing you his back, and it throws the whole body out over the planted
  // hand — with the hips held where they started, the palm landed a third of a foot OUTSIDE
  // the rail. Touching the deck by every number and touching nothing at all in the picture.
  //
  // The body angle is read in WORLD space on purpose. The turn is taken back off the rider
  // GROUP, and faceDir is measured inside that group — so it comes back identical either way
  // and says nothing at all about which way he is really pointing.
  const plant = await page.evaluate(() => {
    const out = { ride: null, hold: [], after: null };
    window.__surf.hand(false); window.__surf.restart(); window.__surf.tick(1.0);
    out.who = window.__surf.rigInfo().who;
    out.ride = { deg: window.__surf.rigInfo().bodyDeg, x: window.__surf.deckGap().x };
    window.__surf.hand(true); window.__surf.tick(1.8);
    for (let i = 0; i < 4; i++) { window.__surf.tick(0.2);
      out.hold.push({ deg: window.__surf.rigInfo().bodyDeg, g: window.__surf.deckGap() }); }
    window.__surf.hand(false); window.__surf.tick(3);
    const ri = window.__surf.rigInfo();
    out.after = { deg: ri.bodyDeg, x: window.__surf.deckGap().x,
                  xFix: ri.xFix, yawFix: ri.yawFix };
    return out;
  });
  const swing = Math.max(...plant.hold.map(h => Math.abs(h.deg - plant.ride.deg)));
  check(plant.ride.deg !== null && swing < 12,
        'he stays side on through it rather than turning his back to you',
        `${plant.who} riding at ${plant.ride.deg}°, hold at ` +
        `${plant.hold.map(h => h.deg).join(', ')} — worst ${swing.toFixed(1)}° off`);
  check(plant.hold.every(h => Math.abs(h.g.x) < 0.20 && Math.abs(h.g.gap) < 0.06),
        'and the hand he is on is planted over the stringer, not off the rail',
        plant.hold.map(h => `${h.g.x.toFixed(2)}ft across a ${h.g.rail}ft half-width`).join(', '));
  // Asked of the OFFSETS, not of where his foot ended up. The handstand turns him and slides
  // him across the board, and both are meant to come off when he lands — but the reading was
  // his foot's position three seconds later against its position before, and in three seconds
  // of riding a foot moves on its own. The reference wandered between 0.17 and 0.33 across
  // runs and the check finally caught its own noise rather than a bug.
  check(Math.abs(plant.after.deg - plant.ride.deg) < 12 &&
        Math.abs(plant.after.xFix) < 0.005 && Math.abs(plant.after.yawFix) < 0.005,
        'and both are given back when he comes down, rather than left on him',
        `${plant.after.deg}° against ${plant.ride.deg}°, ` +
        `slide ${plant.after.xFix} and turn ${plant.after.yawFix} left on him`);
  // ...over his own board, and ON it. The clip was animated on a floor: it travels a third
  // of a body-width sideways and puts his head straight through the deck, because nothing in
  // it knows the board is there. So the clip's root is dropped and the game turns him over
  // itself, then measures the posed body and stands it on the deck.
  const drift = up.skin && rig.skin &&
    Math.max(Math.abs(up.skin.x[0] - rig.skin.x[0]), Math.abs(up.skin.x[1] - rig.skin.x[1]));
  const gUp = await page.evaluate(() => window.__surf.deckGap());
  check(drift !== null && drift < 0.8 && gUp && Math.abs(gUp.gap) < 0.10,
        'and stays on the board doing it, rather than through the deck or off the rail',
        `moved ${drift ? drift.toFixed(2) : '?'}ft across, hands ` +
        `${gUp ? gUp.gap.toFixed(3) : '?'}ft off the deck`);
  // ...and he comes back down off it, all the way, rather than riding out the run on his
  // hands: let go and the clip runs on to its dismount and the turn unwinds with it.
  const back = await page.evaluate(() => {
    window.__surf.hand(false); window.__surf.tick(4); return window.__surf.rigInfo(); });
  check(back.upY > 0.7 && back.phase === 'idle',
        'and he comes back down on his feet when it is released',
        `up-axis ${back.upY}, phase "${back.phase}"`);
  // ---- the face: the file's if it brought one, ours if it did not ----
  // Both paths are real and both ship. A model exported as bare geometry — what an AI
  // generator hands you by default, UVs and no texture — gets one clean coat in his roster
  // colour and eyes built as geometry, because vertex colour cannot draw a white sclera with
  // a pupil on it at these counts. A model that arrives textured gets left completely alone:
  // no coat written over it, no eyes bolted on over the ones it already has.
  const f = await page.evaluate(() => window.__surf.faceStats());
  const own = !!(f && f.authored), made = !!(f && f.head > 0 && f.eyeBalls === 4);
  check(own || made,
        'he has a face — his own if the file brought one, ours if it did not',
        own ? 'the export is textured, so the game left it alone'
            : f ? `built: ${f.head} head verts, ${f.eyeBalls || 0} eye parts` : 'no face at all');

  // ---- the title screen show ----
  // The file ships a performance, so he performs it rather than standing there. The sit and
  // the lie-down are the get-up clip run BACKWARDS — there is no sit clip in the file, and a
  // six-second get-up reversed is exactly the descent, on the animator's own timing.
  const moves = await page.evaluate(() => window.__surf.showMoves()) || [];
  const show = await page.evaluate(n => {
    const out = [];
    for (let i = 0; i < n; i++) out.push(window.__surf.showStep(i));
    out.push(window.__surf.showStep(0));
    return out;
  }, moves.length);
  check(moves.length >= 8 && show.every(Boolean) &&
        show.slice(0, moves.length).every((s2, i) => s2.step === i) && show[0].left > 0.5,
        'and on the title screen he runs through his whole repertoire',
        show.every(Boolean) ? `${moves.length} moves, first runs ${show[0].left}s` : 'no show');
  // ---- and a breath between them ----
  // Back to back the performance reads as a fidget: the last frame of one move is a third of a
  // second from the first frame of the next, all the way round nine of them, and nothing in it
  // ever settles. Every beat already carried a hold of its own, tuned to what that move needs
  // after it lands — a handstand wants longer on its feet than a walk does — so the rest is
  // added to all of them and the shape of that survives.
  // Asked as the STILL stretch inside each beat, which is the beat's length less how long its
  // clip is actually moving. Neither of those alone says anything: a long beat can be a long
  // clip, and a long clip can end the moment it stops.
  const rests = show.slice(0, moves.length).filter(s2 => s2 && s2.rest !== null).map(s2 => s2.rest);
  check(rests.length >= 8 && rests.every(v => v >= 0.9),
        'and holds still for a moment between them rather than running them together',
        rests.map((v, i) => `${show[i].clip}: ${v}s`).slice(0, 4).join(', ') +
        `, worst ${Math.min(...rests)}s`);

  // ---- the wave, which is OFF, and the pose behind it, which still has to be right ----
  // The greeting on the title screen was removed by request. The pose was not: the scan that
  // picks the arm with room to swing, the rocking, and the countdown are all still here, still
  // reachable, and still checked — because what was wanted was the greeting gone from the
  // opening screen, not the ability to make a rider wave. So this asks two separate things:
  // that nothing greets you when the game opens, and that if the pose IS asked for it is still
  // the good one rather than the version that swung the paw in behind his head.
  // ---- he waves when the game opens ----
  // There is no wave in any of these files — a chat, a walk, a run, a stomp, a backflip, a
  // handstand, a get-up and a lie-down, and no greeting among them — so it is posed rather
  // than played: the standing idle underneath and one arm lifted and rocking over the top.
  // The pose was walked against the SCREEN and not the skeleton, because a higher arm
  // measures better and looks worse: on a character this stocky the paw swings in behind the
  // head and vanishes. So what is checked is what the camera can see — the paw clear of the
  // head sideways and above it — at BOTH ends of the swing, not just at the middle.
  {
    const wave = await page.evaluate(async () => {
      // WITH THE TITLE SCREEN UP. The pose is measured against the camera that draws it and
      // on top of the standing idle, and asking for it mid-ride reads the wave through the
      // surfing stance and the game's own camera — which put the paw below his head and
      // looked exactly like a wave that does not work.
      for (let i = 0; i < 12 && !window.__surf.beachNow(); i++) {
        document.getElementById('menuBtn').click();
        await new Promise(r => setTimeout(r, 120));
      }
      window.__surf.greet(5);
      const at = t => window.__surf.wavePose(t);
      // a quarter period either side of centre is the widest the swing goes
      return { up: window.__surf.beachNow(),
               mid: at(0), a: at(Math.PI / 2 / 6.6), b: at(-Math.PI / 2 / 6.6),
               full: window.__surf.greet().full };
    });
    const poses = [wave.mid, wave.a, wave.b];
    // In HEAD-LENGTHS, and measured in the WORLD. Counting pixels passed on a phone held
    // upright and failed in a browser window, where the same pose is drawn smaller. Dividing
    // by the head's own size on screen fixed that and left a subtler version of it: the ratio
    // is independent of resolution and NOT of angle. Drop the camera to the sand and look up
    // and the head's length foreshortens while the arm's reach does not, so the number moves
    // without the pose moving at all — which is exactly what happened the next time the shot
    // was reframed. How far the paw is from the head against the size of the head, in feet,
    // is a fact about the POSE and survives any lens; it reads the same at 393x852 and at
    // 1024x640 to two decimal places.
    //
    // Calibrated against both ends: the pose that was kept measures 0.89 to 0.99 heads out
    // across the swing, and the poses rejected for putting the paw behind his head measured
    // about half that. The bar sits between them rather than just under the good one.
    check(wave.up === true &&
          poses.every(p2 => p2 && p2.up > 0.1 && p2.outW > 0.6 && p2.upW > 0.15),
          'the character waves when the game opens, with the paw clear of his own head',
          `beach up ${wave.up}, ` +
          poses.map(p2 => `${p2.outW} heads out / ${p2.upW} up`).join(', '));
    check(wave.full === 0,
          'and nothing greets you when the game opens — the wave is off',
          `${wave.full}s of greeting on load`);
    // and then hands over to the show and does not come back. It is counted down once per
    // LOAD rather than once per character or once per visit to the title screen, so neither
    // switching riders nor coming back from a run should get you greeted again.
    const after = await page.evaluate(async () => {
      window.__surf.greet(5);
      for (let i = 0; i < 130; i++) window.__surf.showTick ? 0 : 0;
      window.__surf.showTrace(6, 1 / 30);          // six seconds of show, which spends it
      const spent = window.__surf.greet().left;
      window.__surf.restart(); window.__surf.tick(0.5);
      document.getElementById('menuBtn').click();
      await new Promise(r => setTimeout(r, 400));
      return { spent, back: window.__surf.greet().left };
    });
    check(after.spent === 0 && after.back === 0,
          'and only when it opens — coming back from a run does not get you greeted again',
          `${after.spent}s left after the first five, ${after.back}s after a run and back`);
  }

  // ---- and it is a performance rather than a slideshow ----
  // Each beat used to stop every other clip and start the next at full weight, so between two
  // frames the whole skeleton jumped from the last pose of one animation to the first pose of
  // another. That is what read as a refresh — he blinked into a new pose facing a new way.
  // The numbers said it plainly: at a beat change the hips moved a third of a foot and the
  // body turned forty-one degrees IN ONE FRAME, against seven thousandths of a foot and two
  // degrees at the fastest moment inside a clip.
  //
  // So the test is a RATIO, not a threshold. The join has to be quieter than the animation it
  // joins: whatever the animator drew is allowed to be the fastest thing on screen, and the
  // seam between two of his drawings is not. A fixed number would have to be re-guessed every
  // time a clip with more movement in it is added.
  const flow = await page.evaluate(() => window.__surf.showTrace(80));
  const cutM = flow.atCut.move.moved, flowM = flow.inFlow.move.moved;
  const cutT = flow.atCut.turn.turned, flowT = flow.inFlow.turn.turned;
  check(flow.order.length > 6 && cutM < flowM && cutT < flowT,
        'and the joins between his moves are quieter than the moves themselves',
        `worst join ${cutM}ft / ${cutT}deg against ${flowM}ft / ${flowT}deg inside a clip` +
        ` — ${(flowM / Math.max(1e-4, cutM)).toFixed(0)}x and ${(flowT / Math.max(1e-4, cutT)).toFixed(0)}x`);
  // A get-up begins on the floor and a chat begins on both feet. Played in file order that
  // meant standing one second and flat out the next with nothing in between, so every move
  // now says what it starts in and what it leaves him in, and the show only picks one that
  // begins where the last ended. Walked for eighty seconds rather than reasoned about.
  const illegal = flow.order.filter((m, i) => i && flow.order[i - 1].to !== m.from);
  check(flow.order.length > 6 && illegal.length === 0,
        'and every move begins in the posture the last one left him in',
        `${flow.order.length} moves walked` +
        (illegal.length ? `, illegal: ${JSON.stringify(illegal.slice(0, 3))}`
                        : ', never standing up without getting up'));
  // He is on the SAND through all of it. Every clip was animated on a floor at the animator's
  // origin, which is not where this beach is, so the height that stands him on it is measured
  // once and then held — lying down drops his body without dropping him through the ground.
  check(show[0].ground !== null && show[0].ground !== undefined &&
        show.every(s2 => s2.ground === show[0].ground),
        'and stays on the sand doing it, lying down included',
        `ground held at ${show[0].ground === null ? 'none' : (+show[0].ground).toFixed(3)}`);

  // A CARD IS A PORTRAIT: it has to look the same every time it is opened. The title-screen
  // show drives all twenty-four bones, and the standing pose writes about a dozen — so
  // restoring only what the pose writes left the rest of whatever clip had been running
  // underneath, and he turned up mid-something-different on every open. The card resets the
  // whole skeleton to its bind transforms first, then stands him up.
  const card = await page.evaluate(() => {
    const shots = [];
    for (const step of [1, 2, 4]) {
      window.__surf.showStep(step);
      window.__surf.showChar('pug', 0);
      shots.push(window.__surf.rigInfo().LeftArm);
    }
    window.__surf.closeCard();          // or every later reading is taken inside the shop
    window.__surf.restart(); window.__surf.tick(0.4);
    return shots;
  });
  const spread = card[0] && Math.max(...card[0].map((_, i) =>
    Math.max(...card.map(c => Math.abs(c[i] - card[0][i])))));
  check(card.every(Boolean) && spread < 0.05,
        'and his shop card stands him the same way every time it is opened',
        `worst difference across three openings: ${spread === undefined ? '?' : spread.toFixed(4)}`);

  // He turns to face the viewer on the title screen and turns BACK the moment a run starts.
  // The stance is square across the board, which is right on a wave and wrong when he is
  // being looked at; the risk is only ever that he keeps the title screen's turn onto it.
  const turn = await page.evaluate(() => {
    const a = window.__surf.stance ? null : null;
    window.__surf.restart(); window.__surf.tick(0.4);
    return window.__surf.stance ? window.__surf.stance() : null;
  });
  check(!turn || Math.abs((turn.rider !== undefined ? turn.rider : 0) - (-1.2)) < 0.25,
        'and rides square across the board rather than keeping the title screen\'s turn',
        turn ? `stance ${turn.rider}` : 'no stance probe');
  // TWO SOLID OBJECTS DO NOT OVERLAP, and the only honest way to ask is to put the sole and
  // the deck in the same frame — the board's own, because the sea heaves and the hull pitches
  // and a world height answers a different question every frame. The built-in rider's soles
  // ARE the deck for this purpose: he has always looked right standing on it. (The board's
  // bounding-box top is not — that is the nose rocker, a third of a foot above anything
  // underfoot, and measuring against it says every rider in the game is buried.)
  const sole = await page.evaluate(() => {
    window.__surf.wear('pug'); window.__surf.restart(); window.__surf.tick(0.6);
    const out = []; for (let i = 0; i < 6; i++) { window.__surf.tick(0.4); out.push(window.__surf.deckGap()); }
    return out;
  });
  const worst = sole.filter(Boolean).reduce((a, g) => Math.abs(g.gap) > Math.abs(a) ? g.gap : a, 0);
  check(sole[0] && Math.abs(worst) < 0.05,
        'his soles rest on the deck rather than inside it — they are two solid things',
        `worst gap ${worst.toFixed(3)}ft against the built-in rider's own soles`);
  // AGAINST THE DECK UNDER HIS FEET, not against the crown of the board. A deck is domed: it
  // is highest down the stringer and falls away to both rails, and his feet are a third of a
  // foot outboard of the stringer. Stood a hair above the crown, his paws floated over the
  // part of the board they were actually above by three times the clearance intended — small
  // in feet, and a dog visibly standing on nothing in a picture. The number that catches a
  // regression is not the gap, which read a perfect 0.015 throughout: it is that the height
  // being measured against is BELOW the crown, which only a height map can produce.
  // Asked of the DECK rather than of where his feet happen to land, because how much dome
  // there is under a paw depends on the board: it is a third of an inch on the foil and
  // almost nothing on the shortboard. What has to be true of every board is that the height
  // map knows the deck falls away from the stringer at all — one number for the whole board
  // cannot produce that, so this is the reading that catches a silent revert.
  const prof = await page.evaluate(() => window.__surf.deckProfile());
  const fall = prof.at[0] - prof.at[prof.at.length - 1];
  check(fall > 0.01 && prof.at.every((y, i) => i === 0 || y <= prof.at[i - 1] + 0.001),
        'and against the part of the board they are over, not the crown of it',
        `deck across the board ${JSON.stringify(prof.at)} — ${(fall * 12).toFixed(1)} inches ` +
        `of dome from stringer to rail, crown ${prof.crown}`);
  // The built-in rig is HIDDEN under him, not thrown away. Every other character in the
  // roster is built out of the same joints, so emptying the group to make room meant the
  // sloth's owner got an invisible rider on coming back to Astro — and, worse, the detached
  // joints kept whatever world matrix they last held, so the checks that walk the built-in
  // rig started disagreeing with themselves between runs. pawProbe builds another character
  // and puts Astro back, which is exactly the round trip that used to lose him.
  const swap = await page.evaluate(() => {
    const before = window.__surf.rigInfo().modelOn;
    window.__surf.pawProbe('sloth');
    const r = window.__surf.rigInfo();
    return { before, on: r.modelOn, kids: r.kids };
  });
  check(swap.before === true && swap.on === true && swap.kids >= 2,
        'and the built-in body waits under him, so the rest of the roster still has one',
        `model shown ${swap.before} -> ${swap.on}, ${swap.kids} bodies in the group`);
  // EVERY character with a body of its own rides it, and stands on the deck in it. Walked
  // off the game's own list rather than a copy of it here, because a copy goes stale the
  // next time a model is dropped in — which is the one moment this check exists for.
  const kinds = await page.evaluate(() => window.__surf.riderKinds());
  const rode = await page.evaluate(async ids => {
    const out = {};
    for (const id of ids) {
      window.__surf.wear(id); window.__surf.restart(); window.__surf.tick(0.8);
      for (let i = 0; i < 3; i++) window.__surf.tick(0.4);
      const r = window.__surf.rigInfo(), g = window.__surf.deckGap();
      out[id] = { on: r.modelOn, who: r.who, bones: r.bones || 0, gap: g ? g.gap : null };
    }
    window.__surf.wear('pug'); window.__surf.restart(); window.__surf.tick(0.4);
    return out;
  }, kinds);
  check(kinds.length >= 3 && kinds.every(id => rode[id].on && rode[id].who === id && rode[id].bones > 0),
        'and each character with a body of his own is riding it, on his own skeleton',
        kinds.map(id => `${id}: ${rode[id].bones} bones`).join(', '));
  check(kinds.every(id => rode[id].gap !== null && Math.abs(rode[id].gap) < 0.05),
        'and every one of them stands ON the board rather than through it',
        kinds.map(id => `${id} ${rode[id].gap === null ? 'no reading' : rode[id].gap.toFixed(3)}`).join(', '));
}

// ---------- the wipeout card fits the phone it is on ----------
// v2.93: it did not. The buttons finished 16px above the version number on a big phone and
// 130px BELOW the bottom of the glass on a small one, where "Surf again" simply was not
// reachable. Three real sizes, each in its own page rather than resizing the one the suite
// has been riding all this time: the card must end clear of the version and nothing may
// hang off the bottom.
// The page the suite has been riding has held a WebGL context and a running render loop
// for twenty minutes, and a second one alongside it starved so badly under swiftshader
// that it never finished loading at all — a 30s navigation timeout, not a layout fault.
// Everything that needs that page is done by here, so it goes before the phones come up.
await page.close();
for (const [width, height, label] of [[430, 932, 'large phone'],
                                      [393, 852, 'phone'],
                                      [375, 667, 'small phone']]) {
  const p2 = await browser.newPage({ viewport: { width, height } });
  const oops = [];
  p2.on('pageerror', e => oops.push(e.message));
  await p2.goto(`http://127.0.0.1:${PORT}/index.html#debug`,
                { waitUntil: 'domcontentloaded', timeout: 120000 });
  await p2.waitForTimeout(5000);
  await p2.evaluate(() => document.getElementById('dPlay').click());
  await p2.waitForTimeout(1200);
  await p2.evaluate(() => { window.__surf.tick(2.0); window.__surf.wipeNow('foam'); });
  // the card arrives after the wipeout plays out, which is real seconds, not sim ones
  for (let i = 0; i < 40; i++) {
    await p2.evaluate(() => { try { window.__surf.tick(0.25); } catch (e) {} });
    await p2.waitForTimeout(250);
    const cls = await p2.evaluate(() => document.getElementById('overlay').className);
    if (cls.includes('over') && !cls.includes('hidden')) break;
  }
  const fit = await p2.evaluate(() => {
    const ov = document.getElementById('overlay');
    const box = e => e.getBoundingClientRect();
    return { card: ov.className.includes('over'),
             btns: Math.round(box(document.getElementById('ovBtns')).bottom),
             ver: Math.round(box(document.getElementById('ver')).top),
             over: ov.scrollHeight - ov.clientHeight, H: innerHeight };
  });
  const gap = fit.ver - fit.btns;
  check(fit.card && gap >= 18 && fit.btns <= fit.H && fit.over === 0,
        `the wipeout card fits a ${label} and clears the version`,
        `${width}x${height}: buttons end ${fit.btns}, version at ${fit.ver}, ` +
        `gap ${gap}, overflow ${fit.over}`);
  errors.push(...oops);
  await p2.close();
}

// ---------- and on a browser with no vibration API at all, which is every iPhone ----------
// iOS has had a real Taptic tap since 17.4 when you flip an <input type="checkbox" switch>,
// so that is the buzzer. Two things kill it silently and both are checked: the control has
// to be IN the document, and it has to be RENDERED — display:none, visibility:hidden or a
// detached node and the system plays nothing at all.
{
  const ctx = await browser.newContext({ viewport: { width: 393, height: 852 } });
  await ctx.addInitScript(() => {
    try { Object.defineProperty(navigator, 'vibrate', { get: () => undefined }); } catch (e) {}
  });
  const p3 = await ctx.newPage();
  const oops = [];
  p3.on('pageerror', e => oops.push(e.message));
  await p3.goto(`http://127.0.0.1:${PORT}/index.html#debug`,
                { waitUntil: 'domcontentloaded', timeout: 120000 });
  await p3.waitForFunction(() => typeof window.__surf === 'object' && !!window.__surf.state,
                           null, { timeout: 120000 });
  const h = await p3.evaluate(() => window.__surf.haptics());
  check(!h.vibrate && h.isSwitch && h.attached && h.rendered,
        'and where it does not, the buzzer is a real switch the system can feel',
        JSON.stringify(h));
  const flip = await p3.evaluate(() => {
    const before = window.__surf.haptics().checked;
    document.getElementById('dSettings').click();
    return { before, after: window.__surf.haptics().checked };
  });
  check(flip.before !== flip.after, 'and a tap actually flips it',
        `${flip.before} -> ${flip.after}`);

  // "It says On and I feel nothing" has four causes on an iPhone and they are
  // indistinguishable from the outside: no vibrate API and no switch either (iOS 17.3 and
  // older), switch haptics present but the phone's own System Haptics or Low Power Mode
  // silencing them, the toggle simply off, or a genuine bug in here. A web page can see
  // exactly one of those four, so the panel has to say which of the paths it is ON and
  // hand back the rest — otherwise the only way to tell a platform limit from a bug is to
  // own both phones.
  const diag = await p3.evaluate(() => {
    document.getElementById('dSettings').click();
    const note = document.getElementById('setBuzzNote');
    const test = document.getElementById('setBuzzTest');
    return { path: window.__surf.haptics().path,
             note: note ? note.textContent.trim() : null,
             hasTest: !!test };
  });
  check(diag.path === 'tap' && diag.note && /Safari|iOS/i.test(diag.note),
        'and the panel says WHY, rather than leaving a silent toggle to be guessed at',
        `path "${diag.path}", note: ${(diag.note || '(none)').slice(0, 72)}…`);
  // Test has to buzz even with the toggle off: "can this phone do it at all" is a different
  // question from "do I want it while I surf", and you cannot answer the first with the
  // second turned off.
  const tested = await p3.evaluate(() => {
    const before = window.__surf.haptics().checked;
    document.getElementById('setBuzz').click();               // turn the toggle OFF
    const off = window.__surf.haptics().on;
    const mid = window.__surf.haptics().checked;
    document.getElementById('setBuzzTest').click();
    const after = window.__surf.haptics().checked;
    document.getElementById('setBuzz').click();               // and back on
    return { before, off, moved: mid !== after, on: window.__surf.haptics().on };
  });
  check(diag.hasTest && tested.off === false && tested.moved && tested.on === true,
        'and Test fires the tap even with the toggle off, which is the whole point of it',
        `toggle went off: ${tested.off === false}, switch moved: ${tested.moved}, back on: ${tested.on}`);
  errors.push(...oops);
  await ctx.close();
}

check(errors.length === 0, 'no page errors', errors.length ? `\n    ${errors.slice(0, 10).join('\n    ')}` : '');

await browser.close();
server.close();
console.log(failed ? `\n${failed} check(s) failed` : '\nall checks passed');
process.exit(failed ? 1 : 0);
