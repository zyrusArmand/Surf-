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

const NU=220, NR=128;                // the board's own grid — see nu/nr in the spec
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
const DECK=0x2b313b, RAIL=0x8ea0b4, STRINGER=0x7cffc4, SW=0, RAILAT=0.94, VIV=1.02;
function art(c,u,v,top){
  const pz=u-0.5, px=Math.sqrt(Math.max(0,u*(1-u)))*v;
  const r=Math.min(1,Math.sqrt(px*px+pz*pz)*2);
  const th=Math.atan2(px,pz), TAU=Math.PI*2;
  const SEG=14;
  const seg=(th/TAU+0.5)*SEG, si=Math.floor(seg), sf=seg-si;

  // 1. HULL PLATES
  _mix(c,0x2f3742,1);
  _mix(c,0x3d4757,(si%2)?0.55:0.0);
  _mix(c,0x50423a,_hash(si*3.3,1.7)>0.66?0.40:0);
  _mix(c,0x1b2028,Math.max(0,(r-0.62))*1.1);          // the hull falls away toward the rim

  // 2. RADIAL SEAMS, deep, so it reads as plates bolted together
  const seam=Math.min(sf,1-sf)*SEG;
  if(r>0.30)_mix(c,0x0b0e14,Math.max(0,1-seam*1.25));

  // 3. THE BRIGHT COLLAR the plasma sits in
  if(r>0.46&&r<0.545){
    const t=(r-0.46)/0.085;
    _mix(c,0xaebccc,Math.sin(Math.PI*t)*0.95);
    if(Math.abs(sf-0.5)<0.14)_mix(c,0x1b2028,0.75);   // a lug per plate
  }

  // 4. CIRCUITRY, dashed rather than ruled
  if(r>0.56&&r<0.90){
    const rings=[[0.604,30,0.52],[0.678,44,0.40],[0.752,26,0.58],[0.830,52,0.34]];
    for(const [at,n,duty] of rings){
      const d=Math.abs(r-at);
      if(d<0.012&&(((th/TAU+0.5)*n)%1)<duty)_mix(c,0x46eaff,(1-d/0.012)*0.95);
    }
    // a stub of trace running out from each plate, at its own radius
    const tr=0.58+_hash(si*7.1,3.3)*0.26;
    if(Math.abs(r-tr)<0.075&&Math.abs(sf-0.5)<0.055)_mix(c,0x46eaff,0.85);
  }

  // 5. THE PLASMA, a narrow ring and not a disc
  if(r>0.305&&r<0.455){
    const t=(r-0.305)/0.150;
    const arc=0.48+0.52*Math.sin(th*6.0+_fbm(r*7,th*2.6)*7.0);
    const k=Math.sin(Math.PI*t);
    _mix(c,0x5a3cff,k*0.92);
    _mix(c,0xbca4ff,k*arc*0.80);
    _mix(c,0xf4efff,Math.pow(k,2.4)*arc*0.70);
  }

  // 6. THE BEZEL and THE CORE
  if(r<0.315){
    _mix(c,0xa8b8ca,1);
    for(const at of [0.300,0.246,0.196]){
      const d=Math.abs(r-at);
      if(d<0.017)_mix(c,0x232a34,(1-d/0.017)*0.92);
    }
    if(r<0.160){ _mix(c,0xeaf1fa,1);
      const d=Math.abs(r-0.132); if(d<0.014)_mix(c,0x9fb0c4,(1-d/0.014)*0.8); }
  }

  // 7. THE RIM
  if(r>0.90){
    const t=(r-0.90)/0.10;
    _mix(c,0x93a5ba,t*0.95);
    _mix(c,0xe4eef8,Math.pow(t,3)*0.85);
  }
  if(!top)_mix(c,0x11161e,0.45);
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
