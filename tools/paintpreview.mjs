// usage: node tools/paintpreview.mjs [out.png]
//
// A flat preview of a board's deck paint, rasterised straight from the same arithmetic the
// game runs per vertex — so an artwork pass costs a second instead of an eight-minute headless
// render of the whole game. It is NOT the game's renderer: no lighting, no specular, no mesh.
// It answers the one question those renders were being spent on, which is what the PATTERN
// does, and it answers it at the vertex density the board is actually built at.
//
// It paid for itself on the first board that had a drawing on it. Four headless renders went
// into finding out that fine straw noise reads as wood grain, that a saucer and a beam drawn
// per-vertex read as a splodge and a staircase, and that a hide sampled at equal frequency on
// u and v reads as stripes -- all of which this shows in a second. The one thing it cannot
// answer is what the LIGHTING does, so the last look is still taken in the game.
//
// It is a COPY of the game's helpers rather than an import, because the game is one HTML file
// with no module boundary to import from. Keep the block below in step with index.html: if a
// preview stops matching what the board looks like, that is the first place to check.
import { PNG } from 'pngjs';
import { writeFileSync } from 'node:fs';

const NU=260, NR=70;                 // the board's own grid — see nu/nr in the spec
const W=560, H=560;    // square, because the board this previews is

// ---- the game's helpers, verbatim ----
const _hash=(x,y)=>{const n=Math.sin(x*127.1+y*311.7)*43758.5453;return n-Math.floor(n);};
function _noise(x,y){
  const xi=Math.floor(x),yi=Math.floor(y),xf=x-xi,yf=y-yi;
  const a=xf*xf*(3-2*xf), b=yf*yf*(3-2*yf);
  return (_hash(xi,yi)*(1-a)+_hash(xi+1,yi)*a)*(1-b)
       + (_hash(xi,yi+1)*(1-a)+_hash(xi+1,yi+1)*a)*b;
}
function _fbm(x,y){let s=0,m=0.5,f=1;for(let i=0;i<4;i++){s+=_noise(x*f,y*f)*m;f*=2.07;m*=0.5;}return s;}
const _sm=(a,b,x)=>{const t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t);};
const vivid=(hex,boost)=>{
  let r=((hex>>16)&255)/255, g=((hex>>8)&255)/255, b=(hex&255)/255;
  const k=0.30*(boost===undefined?1:boost), avg=(r+g+b)/3;
  // ...and into linear, the way THREE.Color.convertSRGBToLinear does, because the game's
  // vivid() ends with exactly that and a preview that skips it shows every dark colour two
  // stops lighter than the board actually is
  const lin=x=>x<0.04045?x/12.92:Math.pow((x+0.055)/1.055,2.4);
  return [lin(Math.min(1,Math.max(0,avg+(r-avg)*(1+k)))),
          lin(Math.min(1,Math.max(0,avg+(g-avg)*(1+k)))),
          lin(Math.min(1,Math.max(0,avg+(b-avg)*(1+k))))];
};
const _mix=(c,hex,k)=>{ const t=vivid(hex,1.25);
  c[0]+=(t[0]-c[0])*k; c[1]+=(t[1]-c[1])*k; c[2]+=(t[2]-c[2])*k; return c; };

// ---- the board, exactly as index.html has it ----
const DECK=0x140f22, RAIL=0x2bff9b, STRINGER=0x7cffc4, SW=0, RAILAT=0.82, VIV=1.05;
function art(c,u,v,top){
  const a=Math.abs(v);
  _mix(c,0x0b0716,0.55*_sm(0.46,0.00,u));
  _mix(c,0x1d5c4a,0.05+0.20*_sm(0.24,1.00,u));
  _mix(c,0x9dffd8,0.55*_sm(0.80,0.97,a));
  // ---- ISOTROPIC IN THE WORLD, NOT IN uv ----
  // The board is nine foot two long and a foot wide, so equal noise frequency on u and v makes
  // every feature nine times longer than it is wide: that is the streaking down the deck. The
  // ratio is the board's own aspect.
  // ---- SAMPLED WHERE THE VERTEX ACTUALLY IS ----
  // u and v are POLAR on a round board: v runs the full -1 to 1 across a half-width that
  // shrinks to nothing at both poles, so noise sampled in uv comes out as spokes converging on
  // the nose and the tail. It is the nine-foot-plank mistake again in polar dress. The outline
  // is known right here -- the half-width is L*sqrt(u(1-u)) -- so where the vertex actually
  // sits on the disc is two lines of arithmetic, and a blotch sampled there is round.
  const pz=u-0.5, px=Math.sqrt(Math.max(0,u*(1-u)))*v;
  const n=_fbm(px*9.0+2.0,pz*9.0+7.0)+(_fbm(px*26.0,pz*26.0)-0.5)*0.12;
  const blot=_sm(0.520,0.600,n);
  // the markings GLOW, and harder the closer they are to the tail
  _mix(c,0x5bff92,blot*0.95);
  _mix(c,0xe8fff0,blot*_sm(0.30,1.00,u)*0.55);
  _mix(c,0x093a2a,blot*(1-blot)*1.2);
  // ---- AND THE RINGS ----
  // A stringer down the middle of a disc reads as a surfboard part bolted onto a saucer, so
  // there is none. Concentric ribs instead, which is what the underside of the thing that
  // dropped this looks like, and the one graphic that only works on a round board.
  const rr=Math.sqrt(px*px+pz*pz)*2;      // 0 in the middle, 1 at the rim
  for(const at of [0.40,0.66,0.87]){
  const d=Math.abs(rr-at);
  if(d<0.038)_mix(c,0xbdffe4,(1-d/0.038)*0.55);
  }
}
function deckBase(u,v){
  const c=vivid(DECK,VIV), s=Math.abs(v);
  if(s<SW){ const t=vivid(STRINGER,1.3); return [t[0],t[1],t[2]]; }
  if(s>RAILAT)_mix(c,RAIL,_sm(RAILAT,1.0,s));
  return c;
}
// the plan view of a longboard, so the pattern is judged on the shape it lands on
// the plan outline the board actually has: u^noseA (1-u)^tailA over its own peak. At a=b=0.5
// that is a semicircle, which is what makes Cattle Class round.
const NOSE_A=0.5, TAIL_A=0.5;
const halfW=u=>{ const t=Math.min(1,Math.max(0,u));
  const up=NOSE_A/(NOSE_A+TAIL_A), pk=Math.pow(up,NOSE_A)*Math.pow(1-up,TAIL_A);
  return Math.pow(t,NOSE_A)*Math.pow(1-t,TAIL_A)/pk*0.99; };

const png=new PNG({width:W,height:H});
for(let py=0;py<H;py++){
  const u=py/(H-1);
  const hw=halfW(u);
  for(let px=0;px<W;px++){
    const x=(px/(W-1))*2-1;
    let i=(py*W+px)*4;
    if(Math.abs(x)>hw||hw<=0.01){ png.data[i]=22;png.data[i+1]=26;png.data[i+2]=32;png.data[i+3]=255; continue; }
    // snap onto the vertex grid, because that is the resolution the paint actually has
    const uq=Math.round(u*NU)/NU, vq=Math.round((x/hw)*(NR/2))/(NR/2);
    const c=deckBase(uq,vq);
    art(c,uq,vq,true);
    // back out of linear, roughly, so the PNG looks like the screen does
    for(let k=0;k<3;k++)png.data[i+k]=Math.round(255*Math.pow(Math.min(1,Math.max(0,c[k])),1/2.2));
    png.data[i+3]=255;
  }
}
writeFileSync(process.argv[2]||'_paint.png', PNG.sync.write(png));
console.log('wrote', process.argv[2]||'_paint.png');
