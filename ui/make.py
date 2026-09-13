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

# The shortboard's own spec sheet, lifted from B_TYPES in index.html. Not approximated: the
# wheel's boards are the same shape the rack draws, off the same four numbers a shaper quotes.
SPIN_SPEC=dict(L=72.0, W=18.75, noseA=1.063, tailA=3.200, noseW=0.03, tailW=0.590)

def board_hw():
    """The game's own outline, ported. ONE curve from nose to tail, trimmed at each end to the
       width the spec quotes there rather than having a nose bolted onto it -- index.html sets
       out at length why the second way leaves a neck and a shoulder at each tip."""
    sp=SPIN_SPEC
    a_,b_=sp['noseA'],sp['tailA']; up=a_/(a_+b_)
    peak=up**a_*(1-up)**b_
    beta=lambda u:(max(0.0,u)**a_)*(max(0.0,1-u)**b_)/peak
    def inv(t,lo,hi):
        for _ in range(44):
            m=(lo+hi)*0.5
            if beta(m)<t: lo=m
            else: hi=m
        return (lo+hi)*0.5
    uN=inv(sp['noseW'],0,up) if sp['noseW']>0 else 0.0
    uT=inv(sp['tailW'],1,up) if sp['tailW']>0 else 1.0
    L,W=sp['L'],sp['W']
    capN=W*sp['noseW']; capT=W*sp['tailW']
    def cap(d,r): return 1.0 if d>=r else math.sqrt(max(0.0,1-(1-d/r)**2))
    def hw(u):
        e=(cap(u*L,capN) if capN>0 else 1.0)*(cap((1-u)*L,capT) if capT>0 else 1.0)
        return W*beta(uN+(uT-uN)*u)*e
    return hw, L/(2.0*max(hw(x/400.0) for x in range(401)))


def spin_blades():
    """The blades are DRAWN, and every other part of this wheel is cut from the photographs.

       Not a preference -- it is what the sources will carry. The parts sheet gives the blade at
       50 by 161 pixels against about 104 device pixels on screen, so every JPEG block arrives
       at double size; the original photograph has it three times larger and out of focus,
       because the shot is focused on the middle of the rosette and the blades fan away from it.
       Upscaling the sharper of two blurs is still a blur.

       AND THE DELIVERED BLADES ARE NOT SURFBOARDS. They are symmetric pointed lenses -- the
       same at both ends, no nose, no tail, widest dead centre. Drawing them faithfully was
       drawing a leaf. A board has a pointed nose, a blunter tail, its wide point about halfway,
       a stringer, a deck pad over the back foot and fins under it, and it is those that say
       surfboard at a glance rather than the colour. So the outline comes off the shortboard's
       own spec sheet through the same curve the rack is lofted from.
    """
    hw,aspect=board_hw()
    W=264; H=int(round(W*aspect))
    yy,xx=np.mgrid[0:H,0:W].astype(np.float32)
    u=yy/(H-1.0)                                    # 0 nose, 1 tail
    us=np.linspace(0,1,H).astype(np.float32)
    hwv=np.array([hw(float(t)) for t in us],np.float32)
    hwv=hwv/hwv.max()*(W/2.0-3.0)
    half=hwv[:,None]*np.ones((1,W),np.float32)
    dx=xx-(W-1)/2.0
    d=half-np.abs(dx)
    al=np.clip(d/1.6,0,1)
    n=np.clip(dx/np.maximum(1.0,half),-1,1)
    dome=np.sqrt(np.clip(1.0-n*n,0,1))
    shade=0.58+0.42*np.power(dome,0.62)
    g=grain(H,W,1,26,seed=11)
    shade=shade*(1.0+0.075*(g-g.mean())/max(1e-4,g.std()))
    st=np.abs(dx)
    shade=shade*(1.0-0.30*np.exp(-(st/1.9)**2))
    shade=shade*(1.0+0.16*np.exp(-((st-3.4)/2.2)**2))
    shade=shade*(1.0-0.34*np.clip(1.0-d/7.0,0,1))
    # ---- the deck pad, over the back foot ----
    # Where a real one goes and the size a real one is: the back third, inside the rail, with
    # a rounded end. At a hundred pixels across it is one dark shape near the tail -- and one
    # dark shape near the tail is most of the difference between a board and an ellipse.
    # ARCHED at the front and inside the rail, which is what one looks like -- the first cut
    # ran a straight line clean across the board at a fixed height and covered the back forty
    # per cent, so it read as the board having been dipped in something rather than as a pad.
    arch=0.660+0.055*np.power(np.abs(n),1.6)          # the front edge curves back at the rails
    pad=np.clip((u-arch)/0.030,0,1)*np.clip((0.925-u)/0.030,0,1)
    pad=pad*np.clip((half*0.60-np.abs(dx))/2.4,0,1)
    # ---- and the fins under the tail ----
    fin=np.zeros((H,W),np.float32)
    for fx,fu,fs in ((-0.58,0.905,0.85),(0.58,0.905,0.85),(0.0,0.945,0.70)):
        cx=(W-1)/2.0+fx*half[int(0.905*(H-1)),0]
        fw=8.0*fs; fh=26.0*fs
        t=np.clip((u-fu)*(H-1)/fh,0,1)
        w=fw*(1.0-t)                                  # a fin tapers to its tip
        fin=np.maximum(fin, np.clip((w-np.abs(xx-cx))/1.4,0,1)*np.clip(t*6,0,1))
    fin=fin*al
    shade=np.clip(shade,0,1.35)[:,:,None]
    for i,c in enumerate(SPIN_TINT):
        col=np.array([(c>>16)&255,(c>>8)&255,c&255],np.float32)
        dark=col*0.40
        light=np.minimum(255.0,col*1.06+26.0)
        rgb=dark+(light-dark)*shade
        # the pad is a dark grippy slab, near-neutral whatever the board is painted
        rgb=rgb*(1.0-0.52*pad[:,:,None])+np.array([40.,35.,31.],np.float32)*0.52*pad[:,:,None]
        # the fins are darker again and sit under the tail
        rgb=rgb*(1.0-0.78*fin[:,:,None])+np.array([24.,22.,26.],np.float32)*0.78*fin[:,:,None]
        out(rgb, np.maximum(al,fin), 'spin_blade%d.png'%(i+1), 150)

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
