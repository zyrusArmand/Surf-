from PIL import Image, ImageDraw, ImageFilter, ImageChops
import math
S=1024
# ---- background: sunset sky over sea ----
bg=Image.new('RGB',(S,S))
px=bg.load()
sky=[(0.00,(26,66,120)),(0.42,(80,140,200)),(0.60,(246,190,110)),(0.66,(250,160,90))]
sea=[(0.66,(20,120,170)),(0.80,(28,150,190)),(1.00,(16,90,150))]
def ramp(stops,t):
    for i in range(len(stops)-1):
        a,b=stops[i],stops[i+1]
        if t<=b[0]:
            k=(t-a[0])/max(1e-6,b[0]-a[0]); k=max(0,min(1,k))
            return tuple(int(a[1][j]+(b[1][j]-a[1][j])*k) for j in range(3))
    return stops[-1][1]
for y in range(S):
    t=y/S
    c=ramp(sky,t) if t<0.66 else ramp(sea,t)
    for x in range(S): px[x,y]=c
# sun on the horizon
sun=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(sun)
cx,cy,r=S*0.76,S*0.62,S*0.12
d.ellipse((cx-r,cy-r,cx+r,cy+r),fill=(255,236,170,255))
glow=sun.filter(ImageFilter.GaussianBlur(70))
bg.paste(glow,(0,0),glow); bg.paste(sun,(0,0),sun)
# horizon line and a wave band with foam
wv=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(wv)
pts=[]
for x in range(0,S+1,8):
    y=S*0.72+math.sin(x/S*math.pi*2.2+0.6)*S*0.018+math.sin(x/S*math.pi*5.1)*S*0.006
    pts.append((x,y))
poly=pts+[(S,S),(0,S)]
d.polygon(poly,fill=(40,170,205,255))
foam=[(x,y-S*0.012) for x,y in pts]+list(reversed(pts))
d.polygon(foam,fill=(235,250,255,230))
wv=wv.filter(ImageFilter.GaussianBlur(2))
bg.paste(wv,(0,0),wv)
# ---- the pug ----
pug=Image.open('pug_head.png').convert('RGBA')
# trim transparent margins then scale so the head fills ~70% width, seated low
bbox=pug.getbbox(); pug=pug.crop(bbox)
tw=int(S*0.66); pug=pug.resize((tw,int(pug.height*tw/pug.width)),Image.LANCZOS)
# soft ground shadow
sh=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(sh)
d.ellipse((S*0.18,S*0.86,S*0.82,S*1.02),fill=(0,30,60,120)); sh=sh.filter(ImageFilter.GaussianBlur(28))
bg=bg.convert('RGBA'); bg.alpha_composite(sh)
ox=int(S*0.42-pug.width*0.5); oy=int(S*0.99-pug.height)
bg.alpha_composite(pug,(ox,oy))
# ---- rounded-square mask (iOS-style superellipse approximation) ----
mask=Image.new('L',(S,S),0); ImageDraw.Draw(mask).rounded_rectangle((0,0,S-1,S-1),radius=int(S*0.225),fill=255)
icon=Image.new('RGBA',(S,S),(0,0,0,0)); icon.paste(bg,(0,0),mask)
# a faint inner vignette so edges read on white and dark
vig=Image.new('L',(S,S),0); ImageDraw.Draw(vig).rounded_rectangle((8,8,S-9,S-9),radius=int(S*0.22),outline=0,width=0)
icon.save('surf_icon_1024.png')
# square (no mask) for stores that mask themselves
sq=bg.convert('RGB'); sq.save('surf_icon_1024_square.png')
for s in (512,256,192,180,152,120,96,64,48,32):
    icon.resize((s,s),Image.LANCZOS).save(f'surf_icon_{s}.png')
sq.resize((512,512),Image.LANCZOS).save('surf_icon_512_square.png')
icon.resize((64,64),Image.LANCZOS).save('favicon.ico',sizes=[(16,16),(32,32),(48,48),(64,64)])
print('ok')
