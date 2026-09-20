"""Texel density: how many texture pixels each model puts on a square of its own surface.

"One looks sharp and one looks like mush" is a comparison, so it needs a number that can be
compared. Resolution alone cannot answer it -- a 2048 map spread over twice the surface, or
wasted on padding between thousands of tiny islands, is half the map. This walks every
triangle, adds up its area in UV space and its area in the world, and reports the ratio as
pixels per world unit. That is the quantity the eye is actually judging.
"""
import json,struct,sys,math
def load(p):
    d=open(p,'rb').read()
    n=struct.unpack('<I',d[12:16])[0]
    j=json.loads(d[20:20+n])
    off=20+n+8
    return j,d,off
def acc(j,d,bo,i):
    a=j['accessors'][i]; bv=j['bufferViews'][a['bufferView']]
    start=bo+bv.get('byteOffset',0)+a.get('byteOffset',0)
    ct=a['componentType']; ty=a['type']
    ncomp={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[ty]
    fmt={5120:'b',5121:'B',5122:'h',5123:'H',5125:'I',5126:'f'}[ct]
    sz=struct.calcsize(fmt)
    stride=bv.get('byteStride') or sz*ncomp
    out=[]
    for k in range(a['count']):
        base=start+k*stride
        out.append(struct.unpack_from('<'+fmt*ncomp,d,base))
    return out
for path in sys.argv[1:]:
    j,d,bo=load(path)
    imgs=[]
    for im in j.get('images',[]):
        bv=j['bufferViews'][im['bufferView']]
        raw=d[bo+bv.get('byteOffset',0):bo+bv.get('byteOffset',0)+bv['byteLength']]
        from PIL import Image as I; import io
        imgs.append(I.open(io.BytesIO(raw)).size)
    # base colour resolution: the map the eye reads
    res=max([w for w,h in imgs],default=0)
    a3=0.0; a2=0.0; tris=0
    for me in j['meshes']:
        for pr in me['primitives']:
            at=pr['attributes']
            if 'TEXCOORD_0' not in at: continue
            P=acc(j,d,bo,at['POSITION']); T=acc(j,d,bo,at['TEXCOORD_0'])
            idx=[v[0] for v in acc(j,d,bo,pr['indices'])]
            for t in range(0,len(idx),3):
                i0,i1,i2=idx[t],idx[t+1],idx[t+2]
                p0,p1,p2=P[i0],P[i1],P[i2]
                u0,u1,u2=T[i0],T[i1],T[i2]
                ax=(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2])
                bx=(p2[0]-p0[0],p2[1]-p0[1],p2[2]-p0[2])
                cr=(ax[1]*bx[2]-ax[2]*bx[1], ax[2]*bx[0]-ax[0]*bx[2], ax[0]*bx[1]-ax[1]*bx[0])
                a3+=0.5*math.sqrt(cr[0]**2+cr[1]**2+cr[2]**2)
                a2+=0.5*abs((u1[0]-u0[0])*(u2[1]-u0[1])-(u2[0]-u0[0])*(u1[1]-u0[1]))
                tris+=1
    dens=(math.sqrt(a2)*res/math.sqrt(a3)) if a3>0 and a2>0 else 0
    print(f"{path.split('/')[-1]:20s} tris {tris:7d}  imgs {imgs}  uvUsed {a2*100:5.1f}%  "
          f"surf {a3:8.3f}  -> {dens:7.1f} px per world unit")
