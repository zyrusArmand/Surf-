import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Euler, Matrix

random.seed(11)
D=bpy.data; C=bpy.context
OUT=os.environ.get('OUT','/home/user/Surf-/models/shirt_hawaii.glb')
SHOT=os.environ.get('SHOT','/home/user/Surf-/shirt_preview.png')
TEX=os.environ.get('TEXOUT','/home/user/Surf-/shirt_print.png')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
pug=[o for o in C.scene.objects if o.type=='MESH'][0]
arm=[o for o in C.scene.objects if o.type=='ARMATURE'][0]
bones={b.name:(arm.matrix_world@b.head_local) for b in arm.data.bones}

# ---- WHERE A SHIRT ENDS ----
# Measured off his own skeleton rather than picked: Hips sit at z 0.438 and Spine02 at 0.597, so
# a hem at 0.495 is above the hip joint with the belly covered. That is the whole of the
# "trousers have to fit under it" requirement -- anything lower and the waistband has nowhere to
# go; anything higher is a crop top on a pug.
HEM=0.495
SLEEVE=0.62          # share of the upper arm the sleeve covers, shoulder to elbow
LIFT=0.013           # how far it stands off the fur
THICK=0.009          # cloth thickness

# ---- the shirt IS his torso, copied ----
# Built from the pug's own mesh rather than modelled beside it. Two things fall out of that for
# free and neither is easy the other way: it fits exactly, because it IS the shape it has to fit
# over, and it carries his vertex groups, so it bends when he bends instead of hovering while he
# moves inside it. A shirt modelled by hand would need weight painting to get there and would
# still be a guess at his silhouette.
bpy.ops.object.select_all(action='DESELECT')
pug.select_set(True); C.view_layer.objects.active=pug
bpy.ops.object.duplicate()
sh=C.object; sh.name='shirt'
sh.modifiers.clear()

gi={vg.name:vg.index for vg in sh.vertex_groups}
def wsum(v,names):
    t=0.0
    for g in v.groups:
        for n in names:
            if gi.get(n)==g.group: t+=g.weight
    return t
# ---- WHAT IS SHIRT, BY WEIGHT AND BY HEIGHT ----
# The first pass excluded anything weighted to Hips, on the reasoning that Hips is the pelvis.
# It is not: on this rig Hips is the ROOT of the spine and it carries most of the belly, so that
# test deleted the whole lower torso and left a collar round his neck. Height does the job Hips
# was wrongly asked to do -- the hem is a stated z -- and weight is left to the three things a
# height cannot separate: the head (which is above the shoulders AND in front of them), the legs
# (which start below the hem but reach up past it), and the forearms.
HEAD=['Head','head_end','headfront','neck']
LEGS=['LeftUpLeg','RightUpLeg','LeftLeg','RightLeg','LeftFoot','RightFoot',
      'LeftToeBase','RightToeBase']
FORE=['LeftForeArm','RightForeArm','LeftHand','RightHand']

keep=set(); why={'low':0,'high':0,'head':0,'legs':0,'fore':0,'kept':0}
# Counted per reason, because "the shirt came out as a collar" does not say WHICH test threw the
# body away, and two rounds have now been spent guessing at that instead of asking.
for v in sh.data.vertices:
    w=(sh.matrix_world@v.co)
    if w.z < HEM-0.14: why['low']+=1; continue
    if w.z > 1.12: why['high']+=1; continue
    if wsum(v,HEAD)>0.52: why['head']+=1; continue
    if wsum(v,LEGS)>0.88: why['legs']+=1; continue
    if wsum(v,FORE)>0.55: why['fore']+=1; continue
    keep.add(v.index); why['kept']+=1
print('vertex filter',why)
zs=sorted((sh.matrix_world@sh.data.vertices[i].co).z for i in keep)
print('kept z %.3f .. %.3f'%(zs[0],zs[-1]))
# and the height histogram of what survived, which says where the hem actually is
zs=sorted((sh.matrix_world@sh.data.vertices[i].co).z for i in keep)
if zs: print('kept z %.3f .. %.3f  median %.3f'%(zs[0],zs[-1],zs[len(zs)//2]))

bm=bmesh.new(); bm.from_mesh(sh.data); bm.verts.ensure_lookup_table()
# ---- a face belongs if MOST of it does ----
# Requiring every vertex of a face to pass left the lower belly empty even though its vertices
# were kept: down there the weight tests alternate vertex to vertex, so no face had all of them
# and none survived. The mesh ended at 0.567 against a hem asked for at 0.495, the bisect had
# nothing to cut, and the hem followed the triangulation. A majority test fills the patchy
# region and lets the plane do what it was there for.
drop=[f for f in bm.faces
      if sum(1 for v in f.verts if v.index in keep)*2 < len(f.verts)]
bmesh.ops.delete(bm,geom=drop,context='FACES')
bm.verts.ensure_lookup_table()
loose=[v for v in bm.verts if not v.link_faces]
if loose: bmesh.ops.delete(bm,geom=loose,context='VERTS')

# ---- and the hem is a CUT, not a ragged edge ----
# Dropping whole faces by vertex test leaves the boundary following the triangulation, which on
# a 9000-poly body is a zigzag you can see. Bisecting gives a straight hem at a stated height.
M=sh.matrix_world
def bisect(co,no,clear_outer=True):
    geom=bm.verts[:]+bm.edges[:]+bm.faces[:]
    bmesh.ops.bisect_plane(bm,geom=geom,dist=1e-5,plane_co=co,plane_no=no,
                           clear_outer=clear_outer,clear_inner=not clear_outer)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
Mi=M.inverted()
bisect(Mi@Vector((0,0,HEM)), (Mi.to_3x3()@Vector((0,0,-1))).normalized(), True)

# short sleeves: cut square across each arm, so far along it from the shoulder
for side in ('Left','Right'):
    a=bones[side+'Arm']; e=bones[side+'ForeArm']
    d=(e-a); L=d.length; d=d.normalized()
    cut=a+d*(L*SLEEVE)
    bisect(Mi@cut, (Mi.to_3x3()@d).normalized(), True)

bm.to_mesh(sh.data); bm.free()
sh.data.update()

# ---- stood off the fur, then given thickness ----
# Pushed along each vertex's own normal rather than scaled: scaling a torso from its centre moves
# the shoulders further than the belly and the shirt ends up baggy at the top and tight at the
# bottom. Along the normal, every part of it clears by the same amount.
me=sh.data
me.calc_normals_split() if hasattr(me,'calc_normals_split') else None
nrm={}
bm=bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
for v in bm.verts: v.co += v.normal*LIFT
bm.to_mesh(me); bm.free()
sd=sh.modifiers.new('s','SOLIDIFY'); sd.thickness=THICK; sd.offset=1.0
C.view_layer.objects.active=sh
bpy.ops.object.modifier_apply(modifier=sd.name)

# ---- its own UVs ----
# The copy arrives wearing the pug's UVs, which map into his fur atlas -- correct for fur and
# meaningless for a repeating print. Unwrapped fresh so the pattern lies on the cloth.
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
bpy.ops.object.mode_set(mode='OBJECT')

# ================= THE PRINT =================
# Drawn here rather than fetched: four colours, hibiscus and monstera, and it TILES -- every
# motif is stamped with wraparound so the pattern can repeat across the cloth without a seam.
N=1024
BG   =np.array([0.055,0.180,0.365])     # deep blue ground
LEAF =np.array([0.110,0.360,0.560])     # a lighter blue for the foliage
LEAF2=np.array([0.180,0.520,0.470])
PINK =np.array([0.960,0.400,0.600])
PINK2=np.array([0.870,0.250,0.470])
YELL =np.array([0.980,0.780,0.220])
CREAM=np.array([1.000,0.930,0.720])
img=np.tile(BG,(N,N,1))

yy,xx=np.mgrid[0:N,0:N]
def stamp(mask,col):
    img[mask]=col

def wrapped(cx,cy,R,fn):
    """apply fn over a window round (cx,cy), wrapping at the edges so the tile has no seam"""
    R=int(R)+2
    ys=(np.arange(cy-R,cy+R)%N); xs=(np.arange(cx-R,cx+R)%N)
    gy,gx=np.meshgrid(np.arange(-R,R),np.arange(-R,R),indexing='ij')
    m,c=fn(gx,gy)
    sub=img[np.ix_(ys,xs)]
    for k in range(len(m)):
        sub[m[k]]=c[k]
    img[np.ix_(ys,xs)]=sub

def hibiscus(size,pinkish=True):
    def f(gx,gy):
        r=np.hypot(gx,gy); th=np.arctan2(gy,gx)
        petal=size*(0.62+0.38*np.cos(5*th))
        body=r<petal
        edge=(r<petal)&(r>petal-size*0.10)
        core=r<size*0.20
        dot =r<size*0.085
        a,b=(PINK,PINK2) if pinkish else (YELL,np.array([0.93,0.62,0.15]))
        return [body,edge,core,dot],[a,b,CREAM if pinkish else PINK,YELL if pinkish else PINK2]
    return f

def monstera(size,ang):
    ca,sa=math.cos(ang),math.sin(ang)
    def f(gx,gy):
        x= gx*ca+gy*sa; y=-gx*sa+gy*ca
        r=np.hypot(x,y*1.55)
        blade=r<size*(0.95-0.25*np.cos(2*np.arctan2(y*1.55,x)))
        # the splits that make a monstera a monstera
        cut=(np.abs(np.sin(y*math.pi/(size*0.42)))<0.30)&(np.abs(x)>size*0.16)
        rib=(np.abs(x)<size*0.045)
        c1=LEAF if (int(ang*7)%3) else YELL*0.92
        return [blade&~cut, blade&rib],[c1,LEAF2]
    return f

# a jittered grid, so it reads as a print rather than as wallpaper
S=N//6
for iy in range(6):
    for ix in range(6):
        cx=int(ix*S+S*0.5+random.uniform(-0.22,0.22)*S)
        cy=int(iy*S+S*0.5+random.uniform(-0.22,0.22)*S)
        wrapped(cx,cy,S*0.46,monstera(S*0.40,random.uniform(0,math.pi)))
for iy in range(6):
    for ix in range(6):
        cx=int(ix*S+random.uniform(-0.18,0.18)*S)
        cy=int(iy*S+random.uniform(-0.18,0.18)*S)
        wrapped(cx,cy,S*0.44,hibiscus(S*0.40,(ix+iy)%2==0))

pix=np.dstack([img,np.ones((N,N))]).astype(np.float32)
tex=D.images.new('hawaii',N,N,alpha=False)
tex.pixels=pix.ravel()
tex.filepath_raw=TEX; tex.file_format='PNG'; tex.save()
print('print written',TEX)

mat=D.materials.new('hawaii'); mat.use_nodes=True
nt=mat.node_tree; bsdf=nt.nodes['Principled BSDF']
ti=nt.nodes.new('ShaderNodeTexImage'); ti.image=tex; ti.location=(-420,240)
mp=nt.nodes.new('ShaderNodeMapping'); mp.location=(-620,240)
tc=nt.nodes.new('ShaderNodeTexCoord'); tc.location=(-820,240)
mp.inputs['Scale'].default_value=(5.0,5.0,5.0)
nt.links.new(tc.outputs['UV'],mp.inputs['Vector'])
nt.links.new(mp.outputs['Vector'],ti.inputs['Vector'])
nt.links.new(ti.outputs['Color'],bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=0.78
if 'Specular IOR Level' in bsdf.inputs: bsdf.inputs['Specular IOR Level'].default_value=0.35
sh.data.materials.clear(); sh.data.materials.append(mat)

# the armature modifier back, so it exports as a skinned mesh bound to his own bones
am=sh.modifiers.new('Armature','ARMATURE'); am.object=arm
sh.parent=arm

me=sh.data; me.calc_loop_triangles()
print('shirt tris',len(me.loop_triangles),'verts',len(me.vertices))
bb=[sh.matrix_world@Vector(c) for c in sh.bound_box]
print('shirt z %.3f .. %.3f  (hips %.3f, spine02 %.3f, neck %.3f)'%(
    min(v.z for v in bb),max(v.z for v in bb),bones['Hips'].z,bones['Spine02'].z,bones['neck'].z))

bpy.ops.object.select_all(action='DESELECT')
sh.select_set(True); arm.select_set(True); C.view_layer.objects.active=sh
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=True,
                          export_apply=False, export_yup=True, export_skins=True)
print('wrote',OUT,os.path.getsize(OUT))

# ---------- preview: him wearing it ----------
if os.environ.get('RENDER'):
    sc=C.scene
    cam_d=D.cameras.new('c'); cam=D.objects.new('cam',cam_d); sc.collection.objects.link(cam)
    cam_d.lens=52
    cam.location=(-2.6,-3.5,1.15); cam.rotation_euler=Euler((1.42,0,-0.64))
    sc.camera=cam
    for pos,en in (((-2.4,-3.0,3.0),700),((2.6,-2.4,1.8),380),((0,2.6,2.2),260)):
        l=D.lights.new('l','AREA'); l.energy=en; l.size=3.0
        ob=D.objects.new('l',l); sc.collection.objects.link(ob); ob.location=pos
        ob.rotation_euler=(Vector((0,0,0.9))-Vector(pos)).to_track_quat('-Z','Y').to_euler()
    w=D.worlds.new('w'); sc.world=w; w.use_nodes=True
    w.node_tree.nodes['Background'].inputs[0].default_value=(0.90,0.92,0.95,1)
    w.node_tree.nodes['Background'].inputs[1].default_value=0.7
    sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=48
    sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
    sc.render.resolution_x=740; sc.render.resolution_y=900
    sc.render.filepath=SHOT
    bpy.ops.render.render(write_still=True)
    print('rendered',SHOT)
