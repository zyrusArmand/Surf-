// Screenshots of the real Unity game, without Unity: downloads the WebGL build the CI publishes
// (.github/workflows/unity-webgl.yml, release tag "unity-webgl"), serves it locally, runs it in
// headless Chromium on a phone-shaped screen and photographs the title and a ride at sunrise,
// noon and sunset. The game is driven through two hooks on the "Surf" object (Bootstrap.ShotPlay,
// Bootstrap.ShotDay); nothing else in it knows it is being watched.
//
//   node shots.mjs <outDir> [zipPath]
//
// With no zipPath the latest build is downloaded into <outDir>/build. Shots land in <outDir> as
// title.png, sunrise.png, noon.png, sunset.png and carve_sunrise.png, plus log.txt (the page's
// console). Software rendering is slow: a full set takes several minutes.
import { createRequire } from 'node:module';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { extname, join, normalize, resolve } from 'node:path';

const { chromium } = createRequire(import.meta.url)('playwright');
const OUT = resolve(process.argv[2] || 'unity-shots');
const ZIP_URL = 'https://github.com/zyrusArmand/Surf-/releases/download/unity-webgl/surf-webgl.zip';
await mkdir(OUT, { recursive: true });

// ---- the build: downloaded fresh into its own empty folder, then unpacked there ----
const buildDir = join(OUT, 'build');
await rm(buildDir, { recursive: true, force: true });
await mkdir(buildDir, { recursive: true });
let zip = process.argv[3] ? resolve(process.argv[3]) : join(OUT, 'surf-webgl.zip');
if (!process.argv[3]) execFileSync('curl', ['-sSfL', '-o', zip, ZIP_URL], { stdio: 'inherit' });
execFileSync('unzip', ['-q', '-o', zip, '-d', buildDir], { stdio: 'inherit' });
const commit = existsSync(join(buildDir, 'commit.txt')) ? (await readFile(join(buildDir, 'commit.txt'), 'utf8')).trim() : '?';
console.log('build of', commit);

// ---- served as plain files: the build unpacks its own gzip (decompressionFallback) ----
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json' };
const server = createServer(async (req, res) => {
  const p = decodeURIComponent(req.url.split('?')[0]);
  const f = join(buildDir, normalize(p === '/' ? '/index.html' : p).replace(/^(\.\.[/\\])+/, ''));
  try {
    let b = await readFile(f);
    // keep the instance the page makes, so the hooks can be called on it
    if (f.endsWith('index.html')) b = Buffer.from(b.toString().replace(/\.then\(\(unityInstance\) => \{/, '.then((unityInstance) => { window.unityInstance = unityInstance;'));
    res.writeHead(200, { 'Content-Type': MIME[extname(f)] || 'application/octet-stream' });
    res.end(b);
  } catch { res.writeHead(404).end('not found'); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/index.html`;

// ---- a phone: the WebGL template lays the canvas out full screen for a mobile browser ----
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox', '--ignore-gpu-blocklist'] });
const ctx = await browser.newContext({
  viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, hasTouch: true, isMobile: true,
  userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1',
});
const page = await ctx.newPage();
page.setDefaultTimeout(600000);
const log = [];
page.on('console', m => log.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', e => log.push(`[pageerror] ${e}`));
const send = (method, arg) => page.evaluate(([m, a]) => a === undefined ? window.unityInstance.SendMessage('Surf', m) : window.unityInstance.SendMessage('Surf', m, a), [method, arg]);
const wait = ms => page.waitForTimeout(ms);
const shot = async name => { await page.screenshot({ path: join(OUT, name + '.png') }); console.log('shot', name); };

try {
  await page.goto(url, { waitUntil: 'load' });
  await page.waitForFunction(() => !!window.unityInstance, null, { timeout: 900000 });
  await wait(25000);                                   // the world builds behind the loading screen
  await shot('title');
  await send('ShotPlay');
  for (const [name, t] of [['sunrise', 0.14], ['noon', 0.5], ['sunset', 0.88]]) {
    await send('ShotDay', t);
    await wait(9000);
    await shot(name);
  }
  // a hard carve at sunrise, held on the keyboard as a player would
  await send('ShotDay', 0.14);
  await page.keyboard.down('KeyD'); await wait(2500); await shot('carve_sunrise'); await page.keyboard.up('KeyD');
} catch (e) {
  log.push('[harness] ' + e);
  await page.screenshot({ path: join(OUT, 'failed.png') }).catch(() => {});
  process.exitCode = 1;
} finally {
  await writeFile(join(OUT, 'log.txt'), `build ${commit}\n` + log.join('\n'));
  await browser.close();
  server.close();
}
