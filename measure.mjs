// Measure a capture instead of squinting at it. "Flat" and "ringed" are both testable:
// flatness is local variance, rings are a periodic signal along the tube's length.
import { PNG } from 'pngjs';
import { readFileSync } from 'node:fs';
const files = process.argv.slice(2);
const lum = p => (x, y) => { const i = (p.width * y + x) << 2; return 0.2126*p.data[i] + 0.7152*p.data[i+1] + 0.0722*p.data[i+2]; };
for (const f of files) {
  const p = PNG.sync.read(readFileSync(f));
  const L = lum(p);
  // the wave region only: skip the HUD strip and the control cluster
  const y0 = Math.round(p.height*0.08), y1 = Math.round(p.height*0.74);
  let n=0, s=0, s2=0, hi=0;
  for (let y=y0; y<y1; y++) for (let x=0; x<p.width; x++) { const l=L(x,y); s+=l; s2+=l*l; n++; if (l>235) hi++; }
  const mean=s/n, sd=Math.sqrt(s2/n-mean*mean);
  // local detail: mean absolute difference against the pixel 4 to the right
  let d=0, dn=0;
  for (let y=y0; y<y1; y+=2) for (let x=0; x<p.width-4; x+=2) { d+=Math.abs(L(x,y)-L(x+4,y)); dn++; }
  console.log(`${f}\n  mean ${mean.toFixed(1)}  sd ${sd.toFixed(1)}  near-white ${(hi/n*100).toFixed(1)}%  local-detail ${(d/dn).toFixed(2)}`);
}
