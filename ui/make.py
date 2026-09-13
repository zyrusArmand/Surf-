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
import sys, os, math
from PIL import Image
import numpy as np
from scipy import ndimage

U=(sys.argv[1] if len(sys.argv)>1 else '.').rstrip('/')+'/'
OUT=os.path.dirname(os.path.abspath(__file__))+'/'

# the delivered files, by what they are
LIFE_BTN='d8a10f14-image.jpg'    # SECOND LIFE (Watch Ad)
DBL_BTN ='ab09da71-image.jpg'    # DOUBLE COINS (Watch Ad)
PLANK_A ='12a5458e-image.jpg'    # "Shave 3 close calls" -- clean trough
PLANK_B ='d0465b37-image.jpg'    # "Ride a Barrel" -- round roundel, shell on the bar
PLAQUES=[('d289c307-image.jpg','plaque_life.png'),    # LIFE
         ('bfe033f1-image.jpg','plaque_share.png'),   # SHARE
         ('409ce94b-image.jpg','plaque_menu.png')]    # MAIN MENU

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

def box_xy(f,rx,ry):
    f=f.astype(np.float32)
    if rx>=1:
        c=np.cumsum(np.pad(f,((0,0),(rx+1,rx)),mode='edge'),axis=1)
        f=(c[:,2*rx+1:]-c[:,:-(2*rx+1)])/(2*rx+1)
    if ry>=1:
        c=np.cumsum(np.pad(f,((ry+1,ry),(0,0)),mode='edge'),axis=0)
        f=(c[2*ry+1:]-c[:-(2*ry+1)])/(2*ry+1)
    return f

def grain(h,w,rx,ry,seed=7):
    """Wood grain, and grain is DIRECTIONAL. fill_h reaches across each row, so the row-to-row
       variation -- which is most of the grain, because it runs the length of the board --
       survives it untouched; what it flattens is the variation ALONG each row, and a band with
       none of that is the airbrushed patch that gives the whole plank away as retouched. Noise
       smeared far along x and barely along y is what that missing half looks like."""
    g=np.random.RandomState(seed).randn(h,w).astype(np.float32)
    for _ in range(2): g=box_xy(g,rx,ry)
    sd=g.std()
    return g/sd if sd>1e-6 else g

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
tband=(yy>=34)&(yy<=114)&(xx>=374)&(xx<=994)&face
P=fill_h(P,tband)
# and the grain the fill could not carry goes back on
gt=grain(P.shape[0],P.shape[1],14,1,seed=11)*3.4
P+= (gt*tband)[...,None]
# 2. the carving in the dish -- a different drawing for every quest, never baked in
dish=rr<53
P=inpaint(P,(rr<50)&dish,rr<57)
# 2b. two dark nicks sit on the top edge above the title, which at card size read as a stray
# "- -" floating over every quest. They are above the band `face` allows -- face is eroded nine
# pixels off the outline so no fill can reach the silhouette -- so they get their own strip,
# eroded four, which is enough to stay off the edge and low enough to reach them.
nband=(yy>=24)&(yy<=42)&(xx>=690)&(xx<=880)&(blur(inside.astype(np.float32),4)>0.995)
P=fill_h(P,nband)
P+= (grain(P.shape[0],P.shape[1],14,1,seed=31)*3.0*nband)[...,None]

# 3. the count, inside the rope and never on it
cband=(yy>=129)&(yy<=178)&(xx>=560)&(xx<=800)&trough
P=fill_h(P,cband)
gc=grain(P.shape[0],P.shape[1],12,1,seed=23)*2.6
P+= (gc*cband)[...,None]

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
# It is a fifty-pixel crop of a JPEG, so it arrives genuinely soft and reads as a white blob
# rather than a shell. One unsharp pass puts the lip and the shading back.
a=a+1.15*(a-np.dstack([blur(a[:,:,c],1.6) for c in range(3)]))
out(a,al,'knob.png',80)

# ---- the button plaques: shot on white, one to a frame, all the same size ----
# The plaque is brown and the shadow under it is neutral grey, so COLOUR is what separates
# them here -- keyed on brightness the shadow comes along with the plaque.
# ONE crop box for all of them, and no trimming to each plaque's own outline afterwards. They
# are shot in identical frames and they sit side by side in a row, so what matters is that they
# come out at the same scale in the same place -- tightened to itself, a plaque whose shadow
# reaches a few pixels further renders a few per cent smaller than the one beside it.
PBOX=(128,636,574,918)
for src,name in PLAQUES:
    a=np.asarray(Image.open(U+src).convert('RGB')).astype(np.float32)[PBOX[1]:PBOX[3],PBOX[0]:PBOX[2]]
    al=np.clip(((a[:,:,0]-a[:,:,2])-7.0)/9.0,0,1)
    im=Image.fromarray(np.dstack([np.clip(a,0,255),al*255]).astype(np.uint8),'RGBA')
    im=im.resize((260,round(im.height*260/im.width)),Image.LANCZOS)
    im.save(OUT+name,optimize=True); print(name,im.size,os.path.getsize(OUT+name)//1024,'KB')

# ---------- the three signs on the hut's back wall ----------
# Photographed blocks this time, not flat paintings: each is shot on linen, and the linen is a
# WOVEN texture rather than a flat ground, so there is no single colour to key out. What there
# is instead is that the block is wood and the cloth is not -- 75 points of red over blue on one
# and 18 on the other, with nothing in between.
for src,name in [('48d4f00d-image.jpg','sign_school.png'),
                 ('9a783959-image.jpg','sign_boards.png'),
                 ('550b0334-image.jpg','sign_riders.png')]:
    a=np.asarray(Image.open(U+src).convert('RGB')).astype(np.float32)
    H,W,_=a.shape
    # the block is WOOD and the cloth behind it is neutral linen: R-B is 75 on one and 18 on
    # the other, with nothing in between, so colour cuts it where brightness could not
    m=blur(a[:,:,0]-a[:,:,2],2.0)>38
    ys,xs=np.nonzero(m)
    # a photographed block is convex, so the outline is its own row and column extents -- that
    # closes the burnt-in engraving, which is dark enough in places to fall out of the key
    keep=np.zeros((H,W),bool)
    for y in range(ys.min(),ys.max()+1):
        r=np.nonzero(m[y])[0]
        if len(r)>8: keep[y,r.min():r.max()+1]=True
    for x in range(xs.min(),xs.max()+1):
        c=np.nonzero(keep[:,x])[0]
        if len(c)>8: keep[c.min():c.max()+1,x]=True
    al=np.clip((blur(keep.astype(np.float32),1.6)-0.42)/0.30,0,1)
    im=Image.fromarray(np.dstack([np.clip(a,0,255),al*255]).astype(np.uint8),'RGBA')
    im=im.crop(im.getbbox())
    im=im.resize((200,round(im.height*200/im.width)),Image.LANCZOS)
    im.save(OUT+name,optimize=True)
    print(name,im.size,os.path.getsize(OUT+name)//1024,'KB')


# ---------- the daily wheel, cut from one sheet of parts ----------
# The wheel arrived twice. First as a PHOTOGRAPH of the finished thing -- the whole rosette
# assembled in its ring on an easel -- which cannot be used, for four reasons and the first is
# fatal: the labels are painted on. Half the slices carry a figure that moves with the rank
# (500 shells at level one is 1,600 at the top) and one of them names whichever board is being
# lent, so the text has to be drawn at run time and painted text cannot be. Then: fourteen
# blades against six prizes, unreadable at 264px on a phone; the ring, the easel and the sign
# would all turn with the rosette if the picture were rotated as one; and the shot is off-axis,
# so the rosette is an ellipse and an ellipse spun in 2D reads as a wobbling photograph.
#
# It arrived the second time as the PARTS, straight on, on white. That is what this cuts, and
# the same argument the plank makes applies: one blade serves all six slices.
#
# ONE BLADE, TINTED SIX WAYS. Of the twelve delivered, eleven carry their own painted label
# and icon -- and the lettering on them is garbled besides ("CNEST", "SACOND LIE"), so there
# was never a version of this where the delivered text could ship. The twelfth is bare wood
# with its stringer line and nothing else. That is the one, recoloured per slice by multiplying
# its own luminance through each colour, which keeps the grain and the stringer in every copy
# and means all six are the same board in six paints rather than six boards.
SPIN_SHEET='8171fe62-image.jpg'
# measured off the sheet by connected components, not counted by eye
SPIN_PARTS={'ring':(375,84,308,311), 'easel':(408,492,243,492), 'hub':(74,779,203,203),
            'fin':(472,1071,132,235), 'sign':(16,1098,320,154)}
SPIN_BLADE=(150,38,50,161)          # row 1, column 2: the bare one
# Richer than the pastels the CSS wheel used. A painted board is a painted board; those colours
# were picked to be legible as flat CSS wedges and they read as sugar paper on timber.
SPIN_TINT=[0xF0A81E, 0x17A392, 0xE0602C, 0x2E72B8, 0xE7C04A, 0x7A5EA8]

def _sheet_rgba(x,y,w,h,pad=4,tol=234.0,soft=16.0):
    """One part off the white sheet. Keyed on how far the pixel is BELOW white rather than on
       a colour difference: the parts are wood and paint against a studio white, and two of
       them (the bare blade, the pale sign) are themselves nearly neutral, so any key that
       leans on saturation drops half of what it is supposed to keep."""
    im=Image.open(U+SPIN_SHEET).convert('RGB')
    a=np.asarray(im).astype(np.float32)[max(0,y-pad):y+h+pad, max(0,x-pad):x+w+pad]
    lum=a.mean(2)
    al=np.clip((tol-lum)/soft,0,1)
    # the studio ground carries a faint gradient; anything that faint is ground, not part
    al[al<0.10]=0.0
    return a, al

def spin_part(key,name,w,hoop=False):
    x,y,ww,hh=SPIN_PARTS[key]
    a,al=_sheet_rgba(x,y,ww,hh)
    if hoop:
        # ---- THE RING DELIVERED IS A WAGON WHEEL, AND A WAGON WHEEL HAS SPOKES ----
        # The photograph this is all drawn from has a plain hoop with the rosette filling it.
        # Twelve spokes behind six boards is two wheels arguing: the spokes cross every blade,
        # and being on top they win. So the rim is kept and the rest is thrown away, which
        # turns the part into the hoop the reference actually has -- and the hub is a separate
        # part anyway, so nothing is lost by dropping the one cast into these spokes.
        # 0.86 measured off the alpha, not guessed: mean alpha runs about 0.17 across the
        # spoke field and jumps to 0.64 at 0.88 of the radius, which is the rim starting.
        H,W=al.shape
        yy,xx=np.mgrid[0:H,0:W]
        r=np.sqrt(((yy-(H-1)/2)/(H/2))**2+((xx-(W-1)/2)/(W/2))**2)
        al=al*np.clip((r-0.855)/0.025,0,1)
    out(a,al,name,w)

# ---- THE BLADES ARE PHOTOGRAPHS AGAIN, AND THIS TIME THEY CARRY IT ----
# They were drawn for two versions, and the reason was resolution: the first parts sheet gave
# the blade at 50 by 161 pixels against about 104 device pixels on screen, and the wide
# photograph had it three times bigger but out of focus. Neither held up, so the shape, the
# dome, the stringer and the grain were generated instead -- including a real shortboard
# outline lofted off the rack's own spec sheet.
# This sheet ends that. Twelve painted boards, 140 by 348 each, straight on, no lettering, a
# real stringer inlaid down every one and the paint sitting on visible grain. Photographs beat
# anything generated when they are actually sharp, and these are: what was being worked around
# is simply gone. The generated outline goes with it -- it was a good answer to not having
# this picture.
SPIN_BOARDS='638d339a-image.jpg'
# a clean 4 x 3 grid, measured off the sheet by connected components
_BCOL=[158,308,450,601]; _BROW=[(66,352),(424,344),(768,348)]; _BW=140
# Six of the twelve, chosen to be told apart at a glance on a wheel: yellow, teal, red-orange,
# sky, sage, violet. The tan one is left out on purpose -- it is within ten points of the
# plywood it was photographed on, which is a key fighting for no reason when there are eleven
# others, and it would read as a bare slot on the wheel besides.
SPIN_PICK=[(1,0),(2,2),(1,3),(1,2),(0,3),(2,1)]   # (row, col), zero based

def spin_blades():
    im=Image.open(U+SPIN_BOARDS).convert('RGB')
    cut=[]
    for i,(r,c) in enumerate(SPIN_PICK):
        ry,rh=_BROW[r]; cx=_BCOL[c]
        crop=im.crop((cx-10,ry-10,cx+_BW+10,ry+rh+10))
        a_=np.asarray(crop).astype(np.float32)
        # the ground is pale plywood and it shades across the sheet, so each board is keyed
        # against the wood in ITS OWN corners rather than one colour for all twelve
        bg=np.median(np.vstack([a_[:8,:8].reshape(-1,3),a_[:8,-8:].reshape(-1,3),
                                a_[-8:,:8].reshape(-1,3),a_[-8:,-8:].reshape(-1,3)]),0)
        # ---- KEYED ON HUE, BECAUSE A SHADOW IS NOT A DIFFERENT COLOUR ----
        # Straight RGB distance from the ground keeps the drop shadow each board casts on the
        # plywood: a shadow is the same wood at lower brightness, so it sits tens of units away
        # from the lit ground and the key cannot tell it from paint. Every blade came out with
        # a pale crescent of shaded timber attached down one side.
        # Dividing each pixel by its own brightness throws exactly that away and leaves the
        # direction of the colour, which shading does not move. Wood and its own shadow land on
        # top of each other; paint does not land anywhere near either.
        lum=a_.mean(2)+1e-3
        dd=np.sqrt((((a_/lum[:,:,None])-(bg/bg.mean()))**2).sum(2))
        # ...and a brightness term as well, for a board dark enough that its hue stops meaning
        # anything -- the near-black one is the case, and this keeps the rule honest for it.
        dl=np.abs(lum-bg.mean())/max(1.0,bg.mean())
        al=np.clip((np.maximum(dd*3.1,dl*1.35)-0.40)/0.22,0,1)
        # LARGEST COMPONENT ONLY. The boards sit close enough together that most crops catch a
        # sliver of the one next door, and a sliver keyed in is a coloured crumb floating
        # beside the blade once it is on the wheel.
        solid=ndimage.binary_fill_holes(ndimage.binary_closing(al>0.55,np.ones((5,5))))
        lab,n=ndimage.label(solid)
        if n>1:
            sizes=ndimage.sum(np.ones_like(lab),lab,range(1,n+1))
            solid=(lab==(int(np.argmax(sizes))+1))
        # ---- AND THE EDGE COMES FROM THE SHAPE, NOT FROM THE RAMP ----
        # Keeping the soft key as the alpha left a ring of half-transparent PLYWOOD around every
        # board: the closing that fills the holes grows the mask a pixel or two, and those extra
        # pixels are wood at whatever the ramp gave them. Against a dark wheel that reads as a
        # dirty outline. Eroded back by one and re-blurred, the alpha is the board's own
        # silhouette, antialiased, and nothing outside it survives at any opacity.
        solid=ndimage.binary_erosion(solid,np.ones((3,3)))
        al=np.clip((blur(solid.astype(np.float32),0.9)-0.30)/0.45,0,1)
        cut.append((a_,al))
    # ---- ONE FRAME FOR ALL SIX, or the flower comes out lopsided ----
    # Cropping each board to its own bounding box makes every blade a different length, and six
    # petals of six lengths on one wheel is not a wheel. They are hand-cut objects photographed
    # in a grid, so they genuinely do differ -- by a few per cent, which is invisible on a shelf
    # and obvious on a rosette. Boxed together into the largest of them, each centred, so the
    # images share a frame and the wheel can place them all with one rule.
    W=max(x[1].shape[1] for x in cut); Hh=max(x[1].shape[0] for x in cut)
    for i,(a_,al) in enumerate(cut):
        h,w=al.shape
        ca=np.zeros((Hh,W,3),np.float32); cl=np.zeros((Hh,W),np.float32)
        oy,ox=(Hh-h)//2,(W-w)//2
        ca[oy:oy+h,ox:ox+w]=a_; cl[oy:oy+h,ox:ox+w]=al
        im2=Image.fromarray(np.dstack([np.clip(ca,0,255),cl*255]).astype(np.uint8),'RGBA')
        im2=im2.resize((150,round(Hh*150/W)),Image.LANCZOS)
        im2.save(OUT+'spin_blade%d.png'%(i+1),optimize=True)
        print('spin_blade%d.png'%(i+1),im2.size,os.path.getsize(OUT+'spin_blade%d.png'%(i+1))//1024,'KB')

# ---- AND THE RING IS THE WHOLE WHEEL AGAIN, SPOKES AND ALL ----
# v9.71 threw the spokes away because twelve of them crossed every blade. That was the right
# complaint about the wrong thing: the spokes were not the problem, the ORDER was -- the ring
# was being drawn after the blades and so on top of them. Behind them it is the frame the
# boards are mounted on, which is what the photograph shows, and the gaps between six petals
# get something to look through.
# Its own picture too, and not the little one off the parts sheet: this arrives 1062 across
# where that crop was 308, which is three and a half times the detail on the one part of the
# wheel that is nearly all fine lines.
SPIN_WHEEL_IMG='cea5d479-image.jpg'
def spin_ring():
    im=Image.open(U+SPIN_WHEEL_IMG).convert('RGB')
    a=np.asarray(im).astype(np.float32)
    lum=a.mean(2)
    al=np.clip((236.0-lum)/18.0,0,1)
    al[al<0.10]=0.0
    out(a,al,'spin_ring.png',640)
spin_ring()
for k,nm,w in (('hub','spin_hub.png',150),('fin','spin_fin.png',96),
               ('sign','spin_sign.png',420),('easel','spin_easel.png',300)):
    spin_part(k,nm,w)
spin_blades()
