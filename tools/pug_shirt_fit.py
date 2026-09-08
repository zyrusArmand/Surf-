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
CLOSE_DEG=float(os.environ.get('CLOSE','62'))   # how far the panels swing toward each other
TARGET_H=COLLAR-0.360
TARGET_W=abs(B['LeftForeArm'].x-B['RightForeArm'].x)*1.08

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

for _ in range(6):
    mn,mx=wbb(sh)
    kx=TARGET_W/max(1e-6,mx.x-mn.x)
    # Depth is NOT fitted. Forcing it to a target stretched the garment front-to-back to chase a
    # number, and it never bought anything: the outward push below guarantees the girth by putting
    # the cloth on his actual skin. So the plan shape is left alone -- y follows x.
    ky=kx
    kz=TARGET_H/max(1e-6,mx.z-mn.z)
    if max(abs(kx-1),abs(ky-1),abs(kz-1))<1e-4: break
    sh.scale=(sh.scale.x*kx, sh.scale.y*ky, sh.scale.z*kz)
    C.view_layer.update()

mn,mx=wbb(sh)
sh.location=(sh.location.x-(mn.x+mx.x)*0.5,
             sh.location.y-(mn.y+mx.y)*0.5-0.10,      # his spine sits at y -0.10, not 0
             sh.location.z+(COLLAR-mx.z))
C.view_layer.update()
mn,mx=wbb(sh)
print('fitted  h %.3f  w %.3f  d %.3f   z %.3f .. %.3f'%(mx.z-mn.z,mx.x-mn.x,mx.y-mn.y,mn.z,mx.z))

# ---- AND THEN IT IS PUT *ON* HIM, NOT NEAR HIM ----
# Every fit up to here sized a BOX around a garment and a box around a dog and made the numbers
# agree. They agreed and the shirt was still inside him, because his chest is round and its
# bounding box is not: the box clears him at the corners and cuts straight through him at the
# front. Matching numbers was the wrong test -- the right one is whether any cloth is inside the
# skin, and it was.
# So the last step asks that question of every vertex and fixes the ones that fail. A ray goes out
# from his spine through the vertex; wherever it crosses his skin is where the cloth may sit, plus
# a little air. Cloth already outside is left alone -- this only ever pushes out, so the drape,
# the collar and the hanging panels keep their shape and simply stop being underneath him.
import bmesh
from mathutils.bvhtree import BVHTree

# Down to a shippable triangle count first: a million tris is unusable in the game, and it also
# makes the loop below take minutes instead of seconds. Done BEFORE the push so the push has the
# final word on the silhouette -- decimating afterwards would sink vertices back into him.
dec=sh.modifiers.new('dec','DECIMATE'); dec.ratio=50000.0/max(1,len(sh.data.polygons))
C.view_layer.objects.active=sh
bpy.ops.object.modifier_apply(modifier=dec.name)
print('shirt decimated to %d tris'%len(sh.data.polygons))

_dg=C.evaluated_depsgraph_get()
_bm=bmesh.new(); _bm.from_mesh(pug.evaluated_get(_dg).to_mesh())
_bm.transform(pug.matrix_world)
bvh=BVHTree.FromBMesh(_bm)

CLEAR=0.020          # air between skin and cloth
MAXPUSH=0.30         # never drag a vertex further than this (his legs are separate down low)
AXIS=Vector((0.0,-0.10,0.0))    # his spine
mw=sh.matrix_world.copy(); mwi=mw.inverted()
_n=len(sh.data.vertices)
_lo=_np.empty(_n*3,dtype=_np.float32); sh.data.vertices.foreach_get('co',_lo)
_lo=_lo.reshape(-1,3)
moved=0
for i in range(_n):
    p=mw@Vector((float(_lo[i,0]),float(_lo[i,1]),float(_lo[i,2])))
    dx=p.x-AXIS.x; dy=p.y-AXIS.y
    r=math.hypot(dx,dy)
    if r<1e-4: continue
    d=Vector((dx/r,dy/r,0.0))
    hit=bvh.ray_cast(Vector((AXIS.x,AXIS.y,p.z)), d)
    if hit[0] is None: continue
    want=min(hit[3]+CLEAR, r+MAXPUSH)
    if want<=r: continue
    q=mwi@Vector((AXIS.x+d.x*want, AXIS.y+d.y*want, p.z))
    _lo[i]=(q.x,q.y,q.z); moved+=1
sh.data.vertices.foreach_set('co',_lo.reshape(-1))
sh.data.update(); C.view_layer.update()
print('pushed %d of %d vertices out of his skin (%.0f%%)'%(moved,_n,100.0*moved/max(1,_n)))
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
for nm,loc,rot in [('front',(0.0,-4.1,0.86),(1.5708,0,0)),
                   ('three',(-2.5,-3.3,1.12),(1.42,0,-0.64)),
                   ('side',(-4.0,-0.9,1.00),(1.50,0,-1.36))]:
    cam.location=loc; cam.rotation_euler=Euler(rot)
    sc.render.filepath=base+'_'+nm+'.png'
    bpy.ops.render.render(write_still=True)
    print('rendered',sc.render.filepath)
