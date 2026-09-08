import bpy, math, os
from mathutils import Vector, Euler
D=bpy.data; C=bpy.context
SRC=os.environ['SRC']
SHOT=os.environ.get('SHOT','/home/user/Surf-/shirt_fit.png')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
pug=[o for o in C.scene.objects if o.type=='MESH'][0]
arm=[o for o in C.scene.objects if o.type=='ARMATURE'][0]
B={b.name:(arm.matrix_world@b.head_local) for b in arm.data.bones}

before=set(C.scene.objects)
bpy.ops.import_scene.gltf(filepath=SRC)
sh=[o for o in C.scene.objects if o not in before and o.type=='MESH'][0]
sh.name='shirt'

def wbb(o):
    pts=[o.matrix_world@Vector(c) for c in o.bound_box]
    return (Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts))),
            Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts))))

mn,mx=wbb(sh); size=mx-mn
print('shirt raw size %.3f %.3f %.3f'%tuple(size))

# ---- FITTED BY HIS OWN LANDMARKS ----
# The collar goes on his neck and the hem lands above the hip joint, so the scale is whatever
# makes the garment's own height span that gap. Picking a number and nudging it is how the last
# one took eight rounds; this is two measurements and a division.
COLLAR=B['neck'].z+0.010          # 1.052 -- where the collar sits
# ---- SIZED BY GIRTH, NOT BY ARM SPAN ----
# Matched to his arm span the shirt came out the right height and width and his belly still came
# through it: he is a stocky dog and the garment's DEPTH front-to-back is what has to clear his
# middle. Sleeves reaching past the elbow on a loose Hawaiian shirt is correct anyway; a belly
# through the buttons is not. So his own torso is measured over the band the shirt covers and
# the garment is scaled until it clears that with room to spare.
zlo,zhi=0.42,1.00
ys=[];xs=[]
for v in pug.data.vertices:
    w=pug.matrix_world@v.co
    if zlo<=w.z<=zhi: ys.append(w.y); xs.append(w.x)
TORSO_D=(max(ys)-min(ys)) if ys else 0.7
TORSO_W=(max(xs)-min(xs)) if xs else 0.7
print('his torso over the shirt band: %.3f deep, %.3f wide'%(TORSO_D,TORSO_W))
# ---- TAILORED PER AXIS, BECAUSE HE IS A BARREL ----
# No single scale fits this. Sized by height the garment is 0.56 deep against a dog 0.91 deep and
# it disappears inside him; sized by girth it is 1.33 tall against a torso 0.70 tall and it
# swallows him to below the ground. Both were tried and both are in the history above.
# He is nearly as deep as his torso is tall, which is not a proportion this shirt was cut for --
# so it gets taken in and let out on each axis separately. That is what tailoring is, and it is
# safe here in a way it would not be on him: this is a static prop, so a non-uniform scale is
# just a wider shirt rather than a sheared skin.
CLOSE_DEG=float(os.environ.get('CLOSE','30'))   # how far the panels swing toward each other
TARGET_H=COLLAR-0.360
EASE=float(os.environ.get('EASE','1.14'))       # how loose it hangs: 1.0 would be skin-tight
AXIS=Vector((0.0,-0.10,0.0))                    # his spine -- not x=0, he sits back a little

# ---- THE FRONT IS CLOSED OVER HIM *FIRST* ----
# The garment is modelled worn OPEN -- the two front panels are spread wide, which is why his
# belly comes through them however well it is scaled. The reference has it buttoned. So the
# panels are drawn together: every vertex is rotated about the body's vertical axis toward the
# centre line by an amount that grows with how far FORWARD it sits, which closes the opening
# while leaving the back, the shoulders and the sleeves where they are.
# ORDER MATTERS: swinging the panels in SHRINKS the garment's front-to-back depth (1.00 -> 0.57
# on the first attempt), so if this ran after the sizing loop it would silently undo the girth
# fit and drop the whole shirt back inside the dog. Close it, then measure what you actually have.
import numpy as _np
_me=sh.data
_n=len(_me.vertices)
_co=_np.empty(_n*3,dtype=_np.float32); _me.vertices.foreach_get('co',_co)
_co=_co.reshape(-1,3)
_ymin,_ymax=_co[:,1].min(),_co[:,1].max()
_xmin,_xmax=_co[:,0].min(),_co[:,0].max()
# The panels swing about HIS spine, not about the mesh's origin -- which is nowhere near it.
# Rotating about (0,0) sent one whole panel across the centre line and left his right side bare.
_xp=(_xmin+_xmax)*0.5
_yp=(_ymin+_ymax)*0.5
print('garment local x %.3f..%.3f  y %.3f..%.3f  pivot %.3f %.3f'%(_xmin,_xmax,_ymin,_ymax,_xp,_yp))
# 0 at the back of the garment, 1 at the front edge of the opening
_f=_np.clip((_ymax-_co[:,1])/max(1e-6,_ymax-_ymin),0,1)**1.6
_x=_co[:,0]-_xp; _y=_co[:,1]-_yp
# NOTE THE SIGN. His front is -Y, so a POSITIVE rotation about Z carries a vertex on his +X side
# away from the front, not toward it. With the sign the other way this pried the shirt open and
# every larger angle looked worse; the histogram above is what caught it.
_ang=-_f*CLOSE_DEG*_np.pi/180.0*_np.sign(_x)
_cx,_sx=_np.cos(_ang),_np.sin(_ang)
_co[:,0]=_x*_cx - _y*_sx + _xp
_co[:,1]=_x*_sx + _y*_cx + _yp
_me.vertices.foreach_set('co',_co.reshape(-1))
_me.update()
C.view_layer.update()
print('front drawn in by %.0f degrees'%CLOSE_DEG)

def place():
    """Centre it on his spine and hang it from his collar."""
    mn,mx=wbb(sh)
    sh.location=(sh.location.x-(mn.x+mx.x)*0.5+AXIS.x,
                 sh.location.y-(mn.y+mx.y)*0.5+AXIS.y,
                 sh.location.z+(COLLAR-mx.z))
    C.view_layer.update()

for _ in range(6):                       # height first: collar to hem, nothing else
    mn,mx=wbb(sh)
    kz=TARGET_H/max(1e-6,mx.z-mn.z)
    if abs(kz-1)<1e-4: break
    sh.scale.z*=kz; C.view_layer.update()
place()

# ---- SIZED BY MEASURING THE HOLE HE GOES THROUGH ----
# Two bad assumptions lived here, and both came from sizing the garment by its BOUNDING BOX.
#
# First, the horizontal scale matched the garment's laid-open WIDTH to his arm span. On a shirt
# modelled flat and open that width is the WRAP-AROUND -- how far the cloth travels all the way
# round a body -- not a shoulder measurement. That squashed it to about a third of the girth he
# needs; three quarters of its vertices ended up buried, the push had to re-model the shirt rather
# than settle it, and cloth shoved that far comes out looking like crushed paper. It did.
#
# Second, and worse, the box does not measure the part that matters. Looked at from above this
# garment is a horseshoe: a torso tube with SLEEVES standing out either side. The box is mostly
# sleeve. The tube he actually goes through is 0.62 across and 0.92 front-to-back -- DEEPER than
# it is wide -- while he is 1.28 wide and 0.91 deep. The two cross-sections are all but at right
# angles to each other, so no uniform scale can fit this, and a mean radius reads the sleeves
# standing off to the sides as girth the body does not have. It read 0.578 against his 0.375 and
# told me to shrink a shirt that was already too narrow.
#
# So the hole gets measured directly, on both of them, the same way: fire a ray out from his spine
# and stop at the FIRST surface it crosses. On him that is his ribs -- vertex positions would have
# included his front legs. On the garment it is the inside of the tube, sleeves ignored. Sideways
# rays give the width, rays out his back give the depth, and the front is skipped because it is
# open by design.
def _ring(bv,z,a0,a1,n=9):
    rs=[]
    for i in range(n):
        a=math.radians(a0+(a1-a0)*i/max(1,n-1))
        hit=bv.ray_cast(Vector((AXIS.x,AXIS.y,z)), Vector((math.sin(a),-math.cos(a),0.0)))
        if hit[0] is not None: rs.append(hit[3])
    return sum(rs)/len(rs) if rs else 0.0

def _rings(bv,zs,spans):
    v=[_ring(bv,z,a0,a1) for z in zs for (a0,a1) in spans]
    v=[x for x in v if x>0]
    return sum(v)/len(v) if v else 0.0

def _shirt_bvh():
    # FromObject would build this in the object's LOCAL space and every distance would be wrong.
    bm=bmesh.new(); bm.from_mesh(sh.data); bm.transform(sh.matrix_world)
    t=BVHTree.FromBMesh(bm); bm.free(); return t

SIDES=[(70,110),(250,290)]      # out his flanks -> width
BACK=[(155,205)]                # out his back   -> depth (his front is where the shirt opens)

import bmesh
from mathutils.bvhtree import BVHTree

# Down to a shippable triangle count first: a million tris is unusable in the game, and it also
# makes the loop below take minutes instead of seconds. Done BEFORE the push so the push has the
# final word on the silhouette -- decimating afterwards would sink vertices back into him.
dec=sh.modifiers.new('dec','DECIMATE'); dec.ratio=70000.0/max(1,len(sh.data.polygons))
C.view_layer.objects.active=sh
bpy.ops.object.modifier_apply(modifier=dec.name)
print('shirt decimated to %d tris'%len(sh.data.polygons))

# glTF ships CUSTOM SPLIT NORMALS, which are baked to the shape as modelled. Every vertex below is
# about to move, and the stored normals would keep shading the garment as though it had not --
# lighting that disagrees with the silhouette, which looks like bad geometry and is not.
for o in (sh,):
    C.view_layer.objects.active=o
    try: bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception as e: print('  (no custom normals to clear: %s)'%e)
for p in sh.data.polygons: p.use_smooth=True

# Decimating a million triangles leaves slivers and coincident vertices behind. Corrective Smooth
# divides by edge length, so a zero-length edge sends a vertex to infinity -- which it did, and the
# tell was a bounding box of 4e11 rather than anything visible in a render.
_bc=bmesh.new(); _bc.from_mesh(sh.data)
bmesh.ops.remove_doubles(_bc, verts=_bc.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(_bc, dist=1e-6, edges=_bc.edges[:])
_bc.to_mesh(sh.data); _bc.free()
sh.data.update()
print('cleaned to %d tris / %d verts'%(len(sh.data.polygons),len(sh.data.vertices)))

if os.environ.get('DUMP'):
    # Snapshot before anything touches the shape, so a broken garment can be blamed on the right
    # step instead of the last one.
    for o in C.scene.objects: o.select_set(o is sh)
    C.view_layer.objects.active=sh
    bpy.ops.export_scene.gltf(filepath=os.environ['DUMP'], use_selection=True, export_format='GLB',
                              export_draco_mesh_compression_enable=False)
    print('dumped mid-pipeline to '+os.environ['DUMP'])

_dg=C.evaluated_depsgraph_get()
_bm=bmesh.new(); _bm.from_mesh(pug.evaluated_get(_dg).to_mesh())
_bm.transform(pug.matrix_world)
bvh=BVHTree.FromBMesh(_bm)

ZS=[0.58,0.68,0.78,0.88]
_pw=_rings(bvh,ZS,SIDES); _pd=_rings(bvh,ZS,BACK)
print('his chest: %.3f half-width, %.3f half-depth'%(_pw,_pd))
for _ in range(5):
    _sb=_shirt_bvh()
    _sw=_rings(_sb,ZS,SIDES); _sd=_rings(_sb,ZS,BACK)
    kx=(_pw*EASE)/max(1e-6,_sw); ky=(_pd*EASE)/max(1e-6,_sd)
    print('  hole: shirt %.3f x %.3f -> x%.2f y%.2f'%(_sw,_sd,kx,ky))
    if max(abs(kx-1),abs(ky-1))<0.01: break
    sh.scale.x*=kx; sh.scale.y*=ky
    C.view_layer.update(); place()

mn,mx=wbb(sh)
print('fitted  h %.3f  w %.3f  d %.3f   z %.3f .. %.3f'%(mx.z-mn.z,mx.x-mn.x,mx.y-mn.y,mn.z,mx.z))

# ---- AND THEN IT IS SETTLED ONTO HIM ----
# Sized right, only a little cloth is left inside him -- seams, the underarm, wherever he is
# lumpier than the garment. Those get moved to the NEAREST point on his skin plus a little air.
# This used to fire a horizontal ray out from his spine instead, and that is a cylinder, not a dog:
# everywhere his body is not vertical -- the slope of his chest, the shoulders, the tuck under the
# hem -- a horizontal ray shoves cloth sideways rather than outward, which read as a stiff barrel
# with a flat skirt hem. Nearest-point-on-surface follows the body it is actually lying on.

CLEAR=float(os.environ.get('CLEAR','0.022'))    # air between skin and cloth

def _inside(tag):
    """How much cloth is still buried. The only honest measure of whether this worked."""
    n=0; mwl=sh.matrix_world
    for v in sh.data.vertices:
        p=mwl@v.co
        loc,nrm,idx,dist=bvh.find_nearest(p)
        if loc is not None and (p-loc).dot(nrm)<0.0: n+=1
    print('  %-14s %d of %d vertices inside him (%.0f%%)'%(tag,n,len(sh.data.vertices),
                                                           100.0*n/max(1,len(sh.data.vertices))))
    return n

_inside('as sized:')

# There WAS a step here that took the slack out of the garment -- drawing any cloth standing too
# far off his skin back in, to stop the side profile reading as a boxy slab. It is gone, because it
# was destroying the shirt: it snapped each vertex onto the offset surface individually, and since
# the nearest-surface normal swings wildly around his neck, neighbouring vertices were thrown in
# different directions and the collar came apart into foil. It cost four wrong diagnoses -- I
# blamed the decimate, z-fighting between the garment's two cloth layers, and the size of the
# push's step -- because an early A/B test of this step ran while a second bug was shredding BOTH
# arms of the comparison, so it looked innocent. Rendering the finished shirt with no dog in
# frame, and bisecting the two stages against that, settled it in a minute.
# The boxy side profile is the lesser problem and it stays.

# Shrinkwrap in OUTSIDE mode moves only the cloth that is buried and leaves the rest alone, and
# Corrective Smooth then undoes the distortion that causes by comparing against the garment's
# ORIGINAL shape -- so the cloth ends up lying on him while still looking like the shirt it was.
# This replaces a hand-rolled push/relax loop that did the same job badly: it drove every moved
# vertex exactly onto the offset surface, so the garment took on his lumps and came out looking
# like crushed paper. These two are the same idea done properly, in C, and they are also 30x faster.
# Cloth that is buried gets moved out to his skin -- but NEVER FAR IN ONE STEP, and that cap is the
# whole trick. Blender's Shrinkwrap in OUTSIDE mode does this in C and did it beautifully across
# the chest and back, then tore the collar into confetti, because "nearest point on his skin" is
# discontinuous in a concavity: the hollow where his neck meets his chest is nearest to his jowl
# for one vertex and to his sternum for the vertex beside it, so the triangle between them is
# stretched across the gap and bursts. The modifier offers no way to limit that jump.
# Capping the step per pass bounds how far two neighbours can diverge, so the cloth walks out over
# several passes instead of teleporting, and a light smoothing between passes heals what strain is
# left. It ends on a push, so nothing is smoothed back under his skin afterwards.
#
# AND IT MOVES THE CLOTH AS CLOTH, NOT AS LOOSE VERTICES.
# This garment is a solid: every part of it is two surfaces, an outside and a lining, about 5mm
# apart. Moving each vertex to exactly CLEAR from his skin puts BOTH of those surfaces on the same
# offset surface, and two coincident surfaces z-fight -- which renders as a spray of shards and
# holes that looks exactly like shattered geometry. I read it as shattered geometry and spent
# three fixes on tearing that was never happening: capping the step, binding the smooth, blaming
# the decimate. What gave it away was turning the push OFF and seeing the same shards, there
# because the buried shirt was poking through his skin in slivers.
# So the displacement is computed as a FIELD OVER SPACE rather than per vertex: each vertex moves
# by the largest displacement any cloth near it needs. The lining and the outer face sit within
# that radius of each other, so they move together and stay 5mm apart.
CAP=float(os.environ.get('CAP','0.05'))
PASSES=int(os.environ.get('PASSES','7'))
BLUR=int(os.environ.get('BLUR','90'))    # how far strain is spread along the cloth
_mwl=sh.matrix_world; _mwli=_mwl.inverted()
_n=len(sh.data.vertices)
_ea=_np.empty(len(sh.data.edges),dtype=_np.int64); _eb=_np.empty_like(_ea)
for i,e in enumerate(sh.data.edges): _ea[i],_eb[i]=e.vertices[0],e.vertices[1]
_deg=_np.bincount(_np.concatenate([_ea,_eb]),minlength=_n).astype(_np.float64)
_deg[_deg==0]=1.0

def _blur(a,iters):
    """Average a per-vertex quantity along the cloth itself."""
    for _ in range(iters):
        acc=_np.zeros_like(a)
        _np.add.at(acc,_ea,a[_eb]); _np.add.at(acc,_eb,a[_ea])
        a=0.5*a+0.5*(acc/(_deg[:,None] if a.ndim>1 else _deg))
    return a

# Blurring the displacement is what stops the cloth crumpling, but it also carries the big push
# needed at his widest point out over the whole garment, so the shirt inflates -- 1.56 wide against
# a dog 1.28 wide -- and any single bad direction gets smeared into a flag of cloth standing off
# his shoulder. Both are the same failure: cloth travelling further than a fitting should move it.
# So nothing is ever allowed more than MAXTOT from where it started.
MAXTOT=float(os.environ.get('MAXTOT','0.085'))
_P0=[_mwl@v.co for v in sh.data.vertices]

for _k in range(PASSES):
    P=[_mwl@v.co for v in sh.data.vertices]
    need=_np.zeros(_n); dirs=_np.zeros((_n,3))
    for i,p in enumerate(P):
        loc,nrm,idx,dist=bvh.find_nearest(p)
        if loc is None: continue
        dirs[i]=(nrm.x,nrm.y,nrm.z)
        s=(p-loc).dot(nrm)
        if s<CLEAR: need[i]=CLEAR-s
    if not need.any(): break
    # BOTH the amount and the DIRECTION get averaged along the cloth before anything moves. The
    # direction is the half that matters: "nearest point on his skin" points one way for a vertex
    # beside his neck and quite another for the vertex next to it, and following that field
    # literally is what crumples cloth. Averaging it turns a field with cliffs in it into one a
    # sheet can actually follow.
    need=_blur(need,BLUR)
    dirs=_blur(dirs,BLUR)
    _ln=_np.linalg.norm(dirs,axis=1); _ln[_ln<1e-9]=1.0
    dirs/=_ln[:,None]
    step=_np.minimum(need,CAP)
    hit=int((step>1e-6).sum())
    for i,p in enumerate(P):
        if step[i]<=1e-6: continue
        q=Vector((p.x+dirs[i,0]*step[i], p.y+dirs[i,1]*step[i], p.z+dirs[i,2]*step[i]))
        off=q-_P0[i]
        if off.length>MAXTOT: q=_P0[i]+off.normalized()*MAXTOT
        sh.data.vertices[i].co=_mwli@q
    sh.data.update()
    print('    pass %d: %d vertices moved'%(_k+1,hit))
C.view_layer.update()
moved=_inside('settled:')
mw=sh.matrix_world.copy()
_n=len(sh.data.vertices)
_lo=_np.empty(_n*3,dtype=_np.float32); sh.data.vertices.foreach_get('co',_lo)
_lo=_lo.reshape(-1,3)
C.view_layer.update()
mn,mx=wbb(sh)
print('worn    h %.3f  w %.3f  d %.3f   z %.3f .. %.3f'%(mx.z-mn.z,mx.x-mn.x,mx.y-mn.y,mn.z,mx.z))

# ---- DOES IT ACTUALLY COVER HIS CHEST? ----
# Renders answered this for 90s a time and I still read them wrong twice. The question is only
# ever "which way round him is there cloth", so it gets asked in degrees: 0 is straight out his
# front, +/-180 is his back. Anything with no cloth near 0 is a shirt hanging open.
_w=_np.array([ (mw@Vector((float(a),float(b),float(c)))) [:] for a,b,c in _lo ],dtype=_np.float64)
_band=(_w[:,2]>0.55)&(_w[:,2]<0.85)
_th=_np.degrees(_np.arctan2(_w[_band,0], -(_w[_band,1]-AXIS.y)))
_hist=_np.histogram(_th,bins=24,range=(-180,180))[0]
print('chest cloth by angle (0=front, each cell 15deg):')
print('  '+' '.join(('%d'%min(9,v//60)) if v else '.' for v in _hist))
_front=_np.abs(_th)<20
print('  cloth within 20deg of dead front: %d verts'%int(_front.sum()))

OUT=os.environ.get('OUT')
if OUT:
    # 4096 maps on a garment this size are 14 MB of download for detail no phone will ever resolve.
    TEX=int(os.environ.get('TEX','1024'))
    for im in D.images:
        if im.size[0]>TEX or im.size[1]>TEX:
            k=TEX/float(max(im.size))
            im.scale(max(4,int(im.size[0]*k)), max(4,int(im.size[1]*k)))
            print('  %s -> %dx%d'%(im.name,im.size[0],im.size[1]))
    for o in list(C.scene.objects): o.select_set(o is sh)
    C.view_layer.objects.active=sh
    bpy.ops.export_scene.gltf(filepath=OUT, use_selection=True, export_format='GLB',
                              export_draco_mesh_compression_enable=False,   # no DRACOLoader in the game
                              export_image_format='JPEG')
    print('exported %s (%.2f MB)'%(OUT, os.path.getsize(OUT)/1048576.0))

if os.environ.get('NORENDER'):
    raise SystemExit(0)

sc=C.scene
cam_d=D.cameras.new('c'); cam=D.objects.new('cam',cam_d); sc.collection.objects.link(cam)
cam_d.lens=54; sc.camera=cam
for pos,en in (((-2.4,-3.0,3.0),700),((2.6,-2.4,1.8),380),((0,2.6,2.2),260)):
    l=D.lights.new('l','AREA'); l.energy=en; l.size=3.0
    ob=D.objects.new('l',l); sc.collection.objects.link(ob); ob.location=pos
    ob.rotation_euler=(Vector((0,0,0.9))-Vector(pos)).to_track_quat('-Z','Y').to_euler()
w=D.worlds.new('w'); sc.world=w; w.use_nodes=True
w.node_tree.nodes['Background'].inputs[0].default_value=(0.90,0.92,0.95,1)
w.node_tree.nodes['Background'].inputs[1].default_value=0.7
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=40
sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
sc.render.resolution_x=680; sc.render.resolution_y=860
base=os.path.splitext(SHOT)[0]
for nm,loc,rot,lens in [('front',(0.0,-4.1,0.86),(1.5708,0,0),54),
                        ('three',(-2.5,-3.3,1.12),(1.42,0,-0.64),54),
                        ('side',(-4.0,-0.9,1.00),(1.50,0,-1.36),54),
                        ('collar',(0.0,-1.55,0.95),(1.5708,0,0),85)]:
    cam.location=loc; cam.rotation_euler=Euler(rot); cam_d.lens=lens
    sc.render.filepath=base+'_'+nm+'.png'
    bpy.ops.render.render(write_still=True)
    print('rendered',sc.render.filepath)
