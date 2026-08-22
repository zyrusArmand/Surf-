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
  // the last shape on the rail is the SUP, which shares no boards with the shortboard the
  // panel opens on — so both halves of the card have to change
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
  check(stood.length === 4 && stood.every(f => f.gap !== null && f.gap >= -0.01 && f.gap < 0.30),
        'and leans on the trunk without going through it',
        stood.map(f => `${f.id} gap ${f.gap} at ${f.standoff}ft out`).join(', '));
  // in FRONT of the tree, which is where a board leaning on one is in every photograph of
  // it. Smaller along the camera's forward axis is nearer the camera.
  check(stood.length === 4 && stood.every(f => f.boardF < f.trunkF - 0.4),
        'and stands in front of the tree, not behind it',
        stood.map(f => `${f.id} ${(f.trunkF - f.boardF).toFixed(2)}ft clear`).join(', '));
  // and the palm is a tree rather than the six foot shrub it was, which is what made a ten
  // foot log leaning on it tower over the whole thing
  check(stood.every(f => f.palmH > 13),
        'and the palm it leans on is a tree', `${stood[0] && stood[0].palmH}ft tall`);

  // He stands CLEAR of the board. A board leaning across the frame sweeps over the ground
  // it stands on, so a fixed distance from its foot put his shoulder through the middle of
  // it — and by how much depended on which board, which is why it looked fine on one and
  // wrong on the next. Measured between the two silhouettes in the picture the camera sees.
  // Clear, but standing WITH it rather than a yard down the beach from it — the pair reads
  // as a pair, and the camera centres on the two of them.
  check(stood.length === 4 && stood.every(f => f.riderGap >= 0.12 && f.riderGap < 0.6),
        'and the rider stands clear of it rather than through it',
        stood.map(f => `${f.id} ${f.riderGap}ft between them`).join(', '));

  // Surfaces. Vertex colours give a shape its markings; what they cannot give it is a
  // surface, and bark, glassed resin and sand all read as the same moulded plastic under a
  // directional light until something breaks the normal up finer than the mesh can. A map
  // that silently failed to attach looks exactly like one too subtle to see, so it is asked
  // about rather than eyeballed. The crown must NOT have one — a leaflet is a flat blade.
  const surf = await page.evaluate(() => window.__surf.surfaces());
  check(surf.boardUV && surf.board && surf.board.normal && surf.board.rough,
        'the board is glassed resin over cloth, not a moulded shell',
        JSON.stringify(surf.board));
  check(surf.bark && surf.bark.normal && surf.bark.rough && surf.palmGroups === 2 &&
        surf.frond && !surf.frond.normal,
        'the trunk is bark and the crown is not', JSON.stringify({bark: surf.bark, frond: surf.frond}));
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
  check(surf.bark.ao && surf.sand.ao && surf.board.ao,
        'creases hold shade the light never reaches',
        JSON.stringify({bark: surf.bark.ao, sand: surf.sand.ao, board: surf.board.ao}));
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
  for (let i = 0; i < 30; i++) {
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
  check(n > 10 && worstTail < 0.50,
        'so the tail stays down in it and the fins stay under',
        `worst tail stood ${worstTail.toFixed(2)} ft above the water beneath it over ${n} samples`);
  check(n > 10 && wettest < -0.05,
        'and the hull floats down IN the surface rather than perched on it',
        `midships underside sat ${wettest.toFixed(2)} ft below the waterline at its shallowest`);
}

// ---------- a half-turn leaves the board turned round ----------
// Landing a 180 used to snap the board back to nose-first under a rider who was now facing
// the other way. Riding switch means the whole thing came round: fins forward, paint where
// the spin left it. Both the rider AND the board carry the half turn, and both give it back.
{
  const spun = await page.evaluate(() => { window.__surf.landAt(0, Math.PI); return window.__surf.stance(); });
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
    if (await page.evaluate(() => { const c = document.getElementById('overlay').className;
                                    return c.includes('over') && !c.includes('hidden'); })) break;
  }
  const row = await page.evaluate(() => {
    // the dial is display:none behind the card, so its SIZE comes from the style rather
    // than from a box it does not currently have
    const dial = getComputedStyle(document.getElementById('dBoard'));
    const ids = ['reviveBtn', 'againBtn', 'shopBtn', 'menuBtn', 'shareBtn'];
    const ov = document.getElementById('overlay').className;
    return { dial: [Math.round(parseFloat(dial.width)), Math.round(parseFloat(dial.height))],
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
        `${on.map(b => b.id).join(', ')} at ${on[0].w}x${on[0].h} against the dial's ${row.dial.join('x')}`);
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
    const settle = async () => { for (let i = 0; i < 40; i++) window.__surf.tick(0.05); };
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
  check(sc.buoy.y > 2 && sc.buoy.y < 6 && sc.log.x > 3 && sc.log.x < 8,
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
  // and clamping the last frame leaves him on his feet. It is scrubbed instead: forward to
  // the inversion, parked there while the button is down, forward again to dismount.
  //
  // A TICK after the scrub, and it matters: the clip supplies the limbs but the GAME turns
  // him over, and that half only happens inside the frame. Without it this reads the pose
  // with the clip applied and the turn missing, which is the rider standing up.
  const up = await page.evaluate(() => {
    window.__surf.standAt(1.8); window.__surf.tick(0.1); return window.__surf.rigInfo(); });
  check(up.upY < -0.5, 'and pressing HAND actually turns him upside down',
        `his own up-axis points ${up.upY} through the deck`);
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
    window.__surf.standAt(null); window.__surf.tick(4); return window.__surf.rigInfo(); });
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
  const show = await page.evaluate(() => {
    const out = [];
    for (let i = 0; i < 5; i++) out.push(window.__surf.showStep(i));
    out.push(window.__surf.showStep(0));
    return out;
  });
  check(show.every(Boolean) && show.slice(0, 5).every((s2, i) => s2.step === i) && show[0].left > 0.5,
        'and on the title screen he runs through his whole repertoire',
        show.every(Boolean) ? `${show.length - 1} beats, first runs ${show[0].left}s` : 'no show');
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
