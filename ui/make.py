"""Cut the run card's art out of the delivered paintings.

    python3 ui/make.py <folder of the source jpgs>

Five pictures arrive as flat images on a studio ground: two ad buttons, two quest planks and
the LIFE plaque. Three of the five are used whole -- the ground is keyed off and that is the
asset. The plank is not: the carving on the left, the words along the top and the count in the
trough are the three things that change per quest, so they are taken back out and the row
draws its own. What is left is one painting that serves all eight goals.

Nothing here is generated art. Every pixel that ships is from the delivered files; the only
work is keying the ground, borrowing the round roundel from one plank for the other, and
filling three holes back in with the wood that surrounds them.
"""
import sys, os
from PIL import Image
import numpy as np

U=(sys.argv[1] if len(sys.argv)>1 else '.').rstrip('/')+'/'
OUT=os.path.dirname(os.path.abspath(__file__))+'/'

# the delivered files, by what they are
LIFE_BTN='d8a10f14-image.jpg'    # SECOND LIFE (Watch Ad)
DBL_BTN ='ab09da71-image.jpg'    # DOUBLE COINS (Watch Ad)
PLANK_A ='12a5458e-image.jpg'    # "Shave 3 close calls" -- clean trough
PLANK_B ='d0465b37-image.jpg'    # "Ride a Barrel" -- round roundel, shell on the bar
PLAQUE  ='d289c307-image.jpg'    # LIFE

def box(f,r):
    if r<1: return f
    c=np.cumsum(np.pad(f,((0,0),(r+1,r)),mode='edge'),axis=1)
    f=(c[:,2*r+1:]-c[:,:-(2*r+1)])/(2*r+1)
    c=np.cumsum(np.pad(f,((r+1,r),(0,0)),mode='edge'),axis=0)
    return (c[2*r+1:]-c[:-(2*r+1)])/(2*r+1)
def blur(f,s):
    r=max(1,int(round(s*0.9)))
    f=f.astype(np.float32)
    for _ in range(3): f=box(f,r)
    return f

def grow(m,r):
    return blur(m.astype(np.float32),r)>0.06

def inpaint(img,mask,region=None,passes=(14,9,6,4,2.5,1.5)):
    """Push the wood in from the edge of the hole. A normalised blur -- colour and
       coverage blurred together, then divided -- fills from what is actually there and
       never drags the background in, so the plank's own silhouette survives."""
    out=img.copy()
    keep=(~mask).astype(np.float32)
    if region is not None: keep*=region.astype(np.float32)   # fill from this wood, not the next
    for s in passes:
        w=blur(keep,s)+1e-6
        for c in range(3):
            f=blur(out[:,:,c]*keep,s)/w
            out[:,:,c]=np.where(mask,f,out[:,:,c])
        keep=np.maximum(keep,blur(mask.astype(np.float32),s)*0+ (~mask)*0 + keep)
        keep=np.where(mask, np.minimum(1.0,blur(keep,s)), keep)
    return out

def fill_h(img,mask):
    """Reach ACROSS the hole, never up and down it. Both bands run the length of the
       board and are shaded top to bottom, so the colour a row wants is the colour that
       row already has either side of the words -- blurring instead flattens the shading
       and leaves the patch sitting proud of what surrounds it."""
    out=img.copy(); H,W,_=img.shape
    for y in range(H):
        mr=mask[y]
        if not mr.any(): continue
        idx=np.nonzero(~mr)[0]
        if len(idx)<2: continue
        for c in range(3):
            out[y,mr,c]=np.interp(np.nonzero(mr)[0],idx,img[y,idx,c])
    return out

def rgba_blue(path, tol=14.0, soft=14.0):
    im=Image.open(U+path).convert('RGB'); a=np.asarray(im).astype(np.float32)
    al=1.0-np.clip(((a[:,:,2]-a[:,:,0])-tol)/soft,0,1)
    return a, al


def out2(a,al,name,box_=None,w=None):
    im=Image.fromarray(np.dstack([np.clip(a,0,255),np.clip(al,0,1)*255]).astype(np.uint8),'RGBA')
    im=im.crop(box_) if box_ else im
    im=im.crop(im.getbbox())
    if w: im=im.resize((w,round(im.height*w/im.width)),Image.LANCZOS)
    im.save(OUT+name,optimize=True); print(name,im.size,os.path.getsize(OUT+name)//1024,'KB')

def out(a,al,name,w):
    im=Image.fromarray(np.dstack([np.clip(a,0,255),np.clip(al,0,1)*255]).astype(np.uint8),'RGBA')
    im=im.crop(im.getbbox())
    im=im.resize((w,round(im.height*w/im.width)),Image.LANCZOS)
    im.save(OUT+name,optimize=True); print(name,im.size,os.path.getsize(OUT+name)//1024,'KB')


# ---------- the two ad buttons: the art is the whole button, lettering and all ----------
a,al=rgba_blue(LIFE_BTN); out2(a,al,'btn_life.png',(0,60,497,331),420)
a,al=rgba_blue(DBL_BTN); out2(a,al,'btn_dbl.png',(30,118,534,381),420)


# ---------- the quest plank ----------
A,AL=rgba_blue(PLANK_A)          # clean trough
B,BL=rgba_blue(PLANK_B)          # circular roundel
Bp=np.zeros_like(A); n=min(A.shape[0],B.shape[0]-2); Bp[:n]=B[2:2+n]
Blp=np.zeros(A.shape[:2],np.float32);         Blp[:n]=BL[2:2+n]

P=A.copy(); PL=AL.copy()
# the left end comes from B, whose roundel is a clean circle rather than a spilling fin
P[14:206,148:382]=Bp[14:206,148:382]; PL[14:206,148:382]=Blp[14:206,148:382]

lum=P.sum(2)/3
H,W=lum.shape
yy,xx=np.mgrid[0:H,0:W]
CX,CY=266.5,113.5
rr=np.sqrt((xx-CX)**2+(yy-CY)**2)
inside=PL>0.5

# Everything that has to come off this plank is LETTERING, and lettering is the glyph
# plus the shadow painted under it. Masking the bright half alone leaves the dark half
# behind as a perfectly legible ghost, and chasing both halves by threshold leaves crumbs.
# The bands the words sit in are flat wood, so the whole band goes and is filled back.
# Each hole is filled from its OWN material: the plank's outline and the trough's rope are
# far bigger departures than any letter, and a fill that can see either drags it inward.
core=blur(inside.astype(np.float32),9)>0.995

def pill(x0,x1,y0,y1):
    cy=(y0+y1)/2.0; r=(y1-y0)/2.0
    cx=np.clip(xx,x0+r,x1-r)
    return ((xx-cx)**2+(yy-cy)**2)<=r*r

trough=pill(402,960,127,180)
face=core&(~pill(392,970,116,190))&(rr>59)

# 1. the title -- the row prints its own words
P=fill_h(P,(yy>=34)&(yy<=114)&(xx>=374)&(xx<=994)&face)
# 2. the carving in the dish -- a different drawing for every quest, never baked in
dish=rr<53
P=inpaint(P,(rr<50)&dish,rr<57)
# 3. the count, inside the rope and never on it
P=fill_h(P,(yy>=129)&(yy<=178)&(xx>=560)&(xx<=800)&trough)

img=Image.fromarray(np.dstack([np.clip(P,0,255),PL*255]).astype(np.uint8),'RGBA').crop((158,16,1002,204))
img=img.resize((760,round(img.height*760/img.width)),Image.LANCZOS)
img.save(OUT+'plank.png',optimize=True)
print('plank.png',img.size,os.path.getsize(OUT+'plank.png')//1024,'KB')

# ---- the shell that rides the bar ----
a=np.asarray(Image.open(U+PLANK_B).convert('RGB')).astype(np.float32)[92:172,679:737]
pale=((a[:,:,0]>140)&(a[:,:,1]>136)&(a[:,:,2]>120)).astype(np.float32)
al=np.clip((blur(pale,1.6)-0.30)/0.34,0,1)
# keep only the bead itself -- the crop catches a crumb of the rope either side of it
seed=np.zeros_like(al); seed[al.shape[0]//2,al.shape[1]//2]=1
for _ in range(140): seed=np.minimum(np.clip(blur(seed,1.2)*6,0,1),(al>0.25).astype(np.float32))
al*=np.clip(blur(seed,1.0)*3,0,1)
out(a,al,'knob.png',80)

# ---- the LIFE plaque: shot on white, so brightness is what separates it ----
a=np.asarray(Image.open(U+PLAQUE).convert('RGB')).astype(np.float32)[600:990,90:610]
# the plaque is brown and its shadow is neutral grey, so colour separates them where
# brightness alone would keep the shadow
al=np.clip(((a[:,:,0]-a[:,:,2])-7.0)/9.0,0,1)
out(a,al,'plaque_life.png',260)
