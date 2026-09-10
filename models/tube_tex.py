# The printed cotton of the tube: a flat yellow ground, a teal band where the inner wall and the
# underside are, and the motifs scattered over the yellow. Drawn rather than photographed so the
# UV layout and the artwork are decided in the same place -- V is the way round the cross-section,
# so "teal below this line" is one comparison and cannot drift from the geometry.
from PIL import Image, ImageDraw
import math, random, sys
W,H=2048,1024
YEL=(243,190,58); TEAL=(45,180,172); TEAL_D=(30,150,146)
# v runs 0 at the outer equator, 0.25 over the top, 0.5 into the hole, 0.75 under, 1 back round
V_TEAL0, V_TEAL1 = 0.40, 0.94
im=Image.new("RGB",(W,H),YEL); d=ImageDraw.Draw(im,"RGBA")
# ---- V is measured from the BOTTOM of the image ----
# PIL writes row 0 at the top and GL samples V=0 at the bottom, so a band drawn at rows
# [0.40H, 0.94H] is V [0.06, 0.60] -- outer, over the top, into the hole. Which is exactly what
# came out: a teal tube with a yellow underside, the whole thing inside out.
def Y(v): return int((1.0-v)*H)
y1,y0=Y(V_TEAL0),Y(V_TEAL1)
d.rectangle([0,y0,W,y1],fill=TEAL)
# the moulded seam where the two colours meet, which on the real thing is a raised welt
d.rectangle([0,y0-3,W,y0+1],fill=TEAL_D); d.rectangle([0,y1-1,W,y1+3],fill=TEAL_D)
print('teal rows',y0,y1)

CORAL=(240,120,96); BLUSH=(246,176,150); SKY=(150,190,225); SAGE=(150,205,190)
SEA=(60,170,150); DEEP=(40,130,120); SAND=(250,225,190)

def sand_dollar(cx,cy,r,col):
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col+(230,))
    for k in range(5):
        a=k*2*math.pi/5-math.pi/2
        for t in range(4):
            rr=r*(0.22+t*0.17)
            px,py=cx+math.cos(a)*rr, cy+math.sin(a)*rr
            s=r*0.075
            d.ellipse([px-s,py-s,px+s,py+s],fill=(255,255,255,200))
def scallop(cx,cy,r,col):
    d.pieslice([cx-r,cy-r*1.15,cx+r,cy+r*0.95],200,340,fill=col+(230,))
    d.polygon([(cx-r*0.72,cy+r*0.30),(cx+r*0.72,cy+r*0.30),(cx,cy+r*0.78)],fill=col+(230,))
    for k in range(7):
        a=math.pi+ (k+0.5)*math.pi/7
        d.line([cx,cy+r*0.62,cx+math.cos(a)*r*0.92,cy+r*0.62+math.sin(a)*r*0.92],
               fill=(255,255,255,150),width=max(1,int(r*0.08)))
def conch(cx,cy,r,col):
    d.polygon([(cx-r*0.55,cy+r*0.85),(cx+r*0.35,cy+r*0.55),(cx+r*0.62,cy-r*0.55),
               (cx-r*0.10,cy-r*0.90),(cx-r*0.62,cy-r*0.05)],fill=col+(230,))
    d.line([cx-r*0.30,cy+r*0.55,cx+r*0.30,cy-r*0.55],fill=(255,255,255,170),
           width=max(1,int(r*0.13)))
def turtle(cx,cy,r,col):
    for sx in (-1,1):
        d.ellipse([cx+sx*r*0.55-r*0.42,cy-r*0.62,cx+sx*r*0.55+r*0.42,cy-r*0.06],fill=col+(220,))
        d.ellipse([cx+sx*r*0.52-r*0.30,cy+r*0.18,cx+sx*r*0.52+r*0.30,cy+r*0.70],fill=col+(220,))
    d.ellipse([cx-r*0.20,cy-r*1.16,cx+r*0.20,cy-r*0.70],fill=col+(230,))
    d.ellipse([cx-r*0.66,cy-r*0.82,cx+r*0.66,cy+r*0.82],fill=col+(255,))
    d.ellipse([cx-r*0.42,cy-r*0.54,cx+r*0.42,cy+r*0.54],fill=(255,255,255,120))
    for k in range(6):
        a=k*math.pi/3
        px,py=cx+math.cos(a)*r*0.34, cy+math.sin(a)*r*0.34
        s=r*0.15
        d.ellipse([px-s,py-s,px+s,py+s],fill=(255,255,255,150))
def frond(cx,cy,r,col,flip=1):
    d.line([cx-r*0.9*flip,cy+r*0.8,cx+r*0.7*flip,cy-r*0.75],fill=col+(230,),
           width=max(1,int(r*0.11)))
    for k in range(6):
        t=0.12+k*0.15
        bx,by=cx-r*0.9*flip+(r*1.6*flip)*t, cy+r*0.8-(r*1.55)*t
        L=r*(0.85-0.09*k)
        for s in (-1,1):
            d.line([bx,by,bx+s*L*0.72*flip,by-L*0.42-abs(s)*r*0.05],
                   fill=col+(215,),width=max(1,int(r*0.085)))
def board(cx,cy,r,col):
    d.polygon([(cx,cy-r),(cx+r*0.34,cy-r*0.10),(cx,cy+r),(cx-r*0.34,cy-r*0.10)],fill=col+(230,))
    d.line([cx,cy-r*0.85,cx,cy+r*0.85],fill=(255,255,255,170),width=max(1,int(r*0.10)))

MOTIF=[(sand_dollar,SKY,34),(sand_dollar,BLUSH,30),(scallop,BLUSH,34),(scallop,SKY,30),
       (conch,CORAL,32),(conch,BLUSH,28),(turtle,SEA,40),(turtle,DEEP,30),
       (frond,SKY,52),(frond,CORAL,46),(frond,SAGE,44),(board,CORAL,34),(board,SAGE,30)]
random.seed(7)
# only on the yellow, and clear of the seams -- a motif half under the welt reads as a smudge
bands=[(0.965,0.995),(0.02,0.36)]
placed=[]
for i in range(150):
    fn,col,rr=MOTIF[random.randrange(len(MOTIF))]
    r=rr*(0.72+random.random()*0.7)
    b=bands[random.randrange(len(bands))]
    v=b[0]+random.random()*(b[1]-b[0])
    cx=random.random()*W; cy=Y(v)
    if any((min(abs(cx-px),W-abs(cx-px)))**2+(cy-py)**2 < (r+pr)**2*1.15 for px,py,pr in placed):
        continue
    placed.append((cx,cy,r))
    fn(cx,cy,r,col)
    if cx<r*2: fn(cx+W,cy,r,col)          # wrap, so the print is continuous round the tube
    if cx>W-r*2: fn(cx-W,cy,r,col)
print("motifs",len(placed))
im.save(sys.argv[1] if len(sys.argv)>1 else "tube_tex.png")
