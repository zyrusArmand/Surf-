// Packs the playable game into dist/ for RUN (rundot deploy uploads that folder).
// Only what the browser loads is copied: the page, three.js, and the art/sound folders,
// minus the Python generators and READMEs that sit beside the assets.
import { rmSync, mkdirSync, cpSync, readdirSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const OUT = 'dist';
const KEEP = new Set(['.html', '.js', '.glb', '.png', '.webp', '.jpg', '.ico',
  '.webmanifest', '.m4a', '.webm', '.mp3', '.ogg', '.wav', '.json']);
const DIRS = ['vendor', 'models', 'sounds', 'ui', 'icon', 'icons'];

rmSync(OUT, { recursive: true, force: true });
mkdirSync(OUT);
cpSync('index.html', join(OUT, 'index.html'));

let files = 1;
function copyDir(src) {
  for (const name of readdirSync(src)) {
    const from = join(src, name);
    if (statSync(from).isDirectory()) { copyDir(from); continue; }
    if (!KEEP.has(extname(name).toLowerCase())) continue;
    const to = join(OUT, from);
    mkdirSync(join(OUT, src), { recursive: true });
    cpSync(from, to);
    files++;
  }
}
DIRS.forEach(copyDir);
console.log(`dist/ ready: ${files} files`);
