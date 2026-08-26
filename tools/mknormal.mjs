// A tileable water normal map, built rather than downloaded.
// Tileable means the lattice the noise sits on WRAPS: every octave's cell count divides the
// tile, so sampling at x+N gives exactly what sampling at x gave. That is a property of how it
// is generated; it cannot be added afterwards by blending the edges.
import {deflateSync} from 'node:zlib';
import {writeFileSync} from 'node:fs';

const N=256;                      // pixels across
const OCT=[4,8,16,32,64];         // cells across, per octave — every one divides N
const AMP=[0.50,0.26,0.14,0.075,0.040];

function hash2(a,b){
  let h=a*374761393+b*668265263;
  h=(h^(h>>>13))*1274126177;
  return ((h^(h>>>16))>>>0)/4294967296;
}
// value noise whose lattice wraps at `p` cells
function pnoise(x,y,p){
  const xi=Math.floor(x), yi=Math.floor(y);
  const xf=x-xi, yf=y-yi;
  const u=xf*xf*(3-2*xf), v=yf*yf*(3-2*yf);
  const m=(k)=>((k%p)+p)%p;
  const a=hash2(m(xi),m(yi)),   b=hash2(m(xi+1),m(yi));
  const c=hash2(m(xi),m(yi+1)), d=hash2(m(xi+1),m(yi+1));
  return (a*(1-u)+b*u)*(1-v)+(c*(1-u)+d*u)*v;
}
function height(u,v){
  let h=0;
  for(let o=0;o<OCT.length;o++){
    const p=OCT[o];
    // a different phase per octave so they do not stack their peaks
    h+=AMP[o]*pnoise(u*p+o*7.3, v*p+o*11.7, p);
  }
  return h;
}
// height field first, so the normals are central differences of the SAME field that tiles
const H=new Float32Array(N*N);
for(let y=0;y<N;y++)for(let x=0;x<N;x++)H[y*N+x]=height(x/N,y/N);
const at=(x,y)=>H[(((y%N)+N)%N)*N+(((x%N)+N)%N)];

// STRENGTH is how steep the ripple reads. Too high and the map looks like hammered metal.
const STRENGTH=1.8;
const px=Buffer.alloc(N*N*3);
for(let y=0;y<N;y++)for(let x=0;x<N;x++){
  const dx=(at(x+1,y)-at(x-1,y))*STRENGTH*N/64;
  const dy=(at(x,y+1)-at(x,y-1))*STRENGTH*N/64;
  let nx=-dx, ny=-dy, nz=1;
  const l=Math.hypot(nx,ny,nz); nx/=l; ny/=l; nz/=l;
  const i=(y*N+x)*3;
  px[i]  =Math.round((nx*0.5+0.5)*255);
  px[i+1]=Math.round((ny*0.5+0.5)*255);
  px[i+2]=Math.round((nz*0.5+0.5)*255);
}
// minimal PNG: signature, IHDR, IDAT, IEND
const BPP=3, STRIDE=N*3;
const raw=Buffer.alloc((STRIDE+1)*N);
const cand=[0,1,2,3,4].map(()=>Buffer.alloc(STRIDE));
const paeth=(a,b,c)=>{const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);
  return pa<=pb&&pa<=pc?a:pb<=pc?b:c;};
for(let y=0;y<N;y++){
  const row=(i)=>px[y*STRIDE+i];
  const left=(i)=>i>=BPP?px[y*STRIDE+i-BPP]:0;
  const up=(i)=>y>0?px[(y-1)*STRIDE+i]:0;
  const ul=(i)=>(y>0&&i>=BPP)?px[(y-1)*STRIDE+i-BPP]:0;
  for(let i=0;i<STRIDE;i++){
    cand[0][i]=row(i);
    cand[1][i]=(row(i)-left(i))&255;
    cand[2][i]=(row(i)-up(i))&255;
    cand[3][i]=(row(i)-((left(i)+up(i))>>1))&255;
    cand[4][i]=(row(i)-paeth(left(i),up(i),ul(i)))&255;
  }
  let best=0,bestScore=Infinity;
  for(let f=0;f<5;f++){
    let sc=0; for(let i=0;i<STRIDE;i++){const v=cand[f][i]; sc+=v<128?v:256-v;}
    if(sc<bestScore){bestScore=sc;best=f;}
  }
  raw[y*(STRIDE+1)]=best;
  cand[best].copy(raw,y*(STRIDE+1)+1);
}
const crcT=(()=>{const t=new Int32Array(256);
  for(let n=0;n<256;n++){let c=n;for(let k=0;k<8;k++)c=c&1?0xEDB88320^(c>>>1):c>>>1;t[n]=c;}return t;})();
const crc=b=>{let c=-1;for(const v of b)c=crcT[(c^v)&255]^(c>>>8);return (c^-1)>>>0;};
const chunk=(type,data)=>{
  const len=Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td=Buffer.concat([Buffer.from(type,'ascii'),data]);
  const c=Buffer.alloc(4); c.writeUInt32BE(crc(td));
  return Buffer.concat([len,td,c]);
};
const ihdr=Buffer.alloc(13);
ihdr.writeUInt32BE(N,0); ihdr.writeUInt32BE(N,4);
ihdr[8]=8; ihdr[9]=2; ihdr[10]=0; ihdr[11]=0; ihdr[12]=0;
const png=Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),
  chunk('IHDR',ihdr), chunk('IDAT',deflateSync(raw,{level:9})), chunk('IEND',Buffer.alloc(0))]);
writeFileSync(process.argv[2]||'/home/user/Surf-/models/waternormals.png',png);
// and PROVE it tiles: the wrap columns and rows must match exactly
let worst=0;
for(let y=0;y<N;y++) worst=Math.max(worst,Math.abs(at(0,y)-at(N,y)));
for(let x=0;x<N;x++) worst=Math.max(worst,Math.abs(at(x,0)-at(x,N)));
console.log(`${N}x${N}, ${(png.length/1024).toFixed(0)}KB, worst wrap mismatch ${worst.toExponential(2)}`);
