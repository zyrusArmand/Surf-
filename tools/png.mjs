import zlib from 'node:zlib';
// Minimal PNG reader for what Playwright produces: 8-bit RGBA, non-interlaced.
// Written because there is no image library here and "is there a bright fringe at this edge"
// is a question about PIXELS — every attempt to answer it from the canvas came back black,
// the drawing buffer not being preserved.
export function readPNG(buf){
  let p=8, w=0,h=0,bd=0,ct=0, idat=[];
  while(p<buf.length){
    const len=buf.readUInt32BE(p), type=buf.toString('ascii',p+4,p+8);
    const data=buf.slice(p+8,p+8+len);
    if(type==='IHDR'){ w=data.readUInt32BE(0); h=data.readUInt32BE(4); bd=data[8]; ct=data[9]; }
    else if(type==='IDAT')idat.push(data);
    else if(type==='IEND')break;
    p+=12+len;
  }
  if(bd!==8||(ct!==6&&ct!==2))throw new Error('unexpected PNG: bd '+bd+' ct '+ct);
  const ch=ct===6?4:3;
  const raw=zlib.inflateSync(Buffer.concat(idat));
  const out=Buffer.alloc(w*h*ch);
  const stride=w*ch;
  let q=0;
  for(let y=0;y<h;y++){
    const f=raw[q++]; const line=raw.slice(q,q+stride); q+=stride;
    const cur=out.slice(y*stride,(y+1)*stride);
    const prev=y?out.slice((y-1)*stride,y*stride):Buffer.alloc(stride);
    for(let x=0;x<stride;x++){
      const a=x>=ch?cur[x-ch]:0, b=prev[x], c=x>=ch?prev[x-ch]:0, v=line[x];
      let r;
      if(f===0)r=v; else if(f===1)r=v+a; else if(f===2)r=v+b; else if(f===3)r=v+((a+b)>>1);
      else { const pp=a+b-c, pa=Math.abs(pp-a), pb=Math.abs(pp-b), pc=Math.abs(pp-c);
             r=v+((pa<=pb&&pa<=pc)?a:(pb<=pc?b:c)); }
      cur[x]=r&255;
    }
  }
  return {w,h,ch,data:out};
}
export const lum=(im,x,y)=>{ const i=(y*im.w+x)*im.ch;
  return (0.2126*im.data[i]+0.7152*im.data[i+1]+0.0722*im.data[i+2])/255; };
