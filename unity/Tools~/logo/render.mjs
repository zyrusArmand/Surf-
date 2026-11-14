// Renders logo.html (the carved SURF logo, an SVG) to a transparent PNG:
//   node render.mjs "$PWD/logo.html" ../../Assets/Resources/UI/logo.png
// It needs the network for its font (Lilita One, Google Fonts, SIL Open Font Licence).
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1400, height: 600 } });
await p.goto('file://' + process.argv[2]); await p.evaluate(() => document.fonts.ready); await p.waitForTimeout(800);
const ok = await p.evaluate(() => document.fonts.check('330px "Lilita One"'));
await p.locator('#s').screenshot({ path: process.argv[3], omitBackground: true });
console.log('font loaded:', ok); await b.close();
