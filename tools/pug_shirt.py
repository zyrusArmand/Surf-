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
# The first version stopped at 0.495, just above the hip joint, on a literal reading of "pants
# have to fit". That is a waistcoat. A shirt is worn OVER the waistband -- the reference has the
# hem below the belly at the top of the thighs -- so it goes to 0.352 and the trousers go under
# it, which is how a shirt and trousers have always worked. That is the whole of the
# "trousers have to fit under it" requirement -- anything lower and the waistband has nowhere to
# go; anything higher is a crop top on a pug.
HEM=0.560
SLEEVE=0.78          # share of the upper arm the sleeve covers, shoulder to elbow
LIFT=0.021           # how far it stands off the fur
THICK=0.010          # cloth thickness

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
    if w.z < HEM-0.10: why['low']+=1; continue
    if w.z > 1.12: why['high']+=1; continue
    if wsum(v,HEAD)>0.52: why['head']+=1; continue
    if wsum(v,LEGS)>0.93: why['legs']+=1; continue
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

# ---- AND THE HEM IS BUILT, NOT COPIED ----
# This is the thing four rounds of threshold-tuning could not reach. Below the waist his body is
# not one volume, it is two legs -- so a shirt COPIED from his surface down there wraps each
# thigh separately and can never be a hem that hangs across both. The copy has to stop where he
# is still one shape, and the rest of the shirt has to be made.
# So the boundary loop at the waist is extruded downward in rings, each one wider and further
# out than the last: a skirt that hangs over the legs instead of around them. The rings inherit
# the vertex weights of the loop they came from, so the hem is driven by the spine and swings
# with his body rather than with either leg -- which is also what real cloth does.
bm.edges.ensure_lookup_table(); bm.verts.ensure_lookup_table()
Mw=sh.matrix_world
# Selected against the HEM PLANE, not against the mesh's lowest point. Keyed off zmin it found
# only the edges nearest the single lowest vertex -- a local patch of the ring -- and the skirt
# extruded down one side of him. The hem boundary is the one that was just cut flat; the neck
# and the two sleeve openings are the other boundaries and they are all far above it, so a
# height test separates them cleanly.
loop=[e for e in bm.edges if e.is_boundary and
      all((Mw@v.co).z < HEM+0.075 for v in e.verts)]
print('hem loop edges',len(loop))
RINGS=[(0.060,0.022),(0.062,0.030),(0.058,0.034),(0.046,0.030)]   # (drop, flare) per ring
Minv=Mw.inverted().to_3x3()
for drop,flare in RINGS:
    if not loop: break
    r=bmesh.ops.extrude_edge_only(bm,edges=loop)
    nv=[g for g in r['geom'] if isinstance(g,bmesh.types.BMVert)]
    ne=[g for g in r['geom'] if isinstance(g,bmesh.types.BMEdge)]
    for v in nv:
        w=Mw@v.co
        rad=Vector((w.x,w.y,0.0))
        out=Minv@(rad.normalized() if rad.length>1e-6 else Vector((0,1,0)))
        v.co += Minv@Vector((0,0,-drop)) + out*flare
    bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    loop=[e for e in ne if e.is_boundary]
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
# ---- IT HANGS OFF HIM ----
# A constant offset along the normal is a second skin: it follows every dip of his belly, which
# is what made the first one read as body paint with a pattern on it. Cloth does not do that. It
# clears more the further it is from where it is held up, so the lift grows toward the hem and
# the hem also swings OUT from his axis -- that flare is most of what makes a shirt look like
# fabric rather than like a decal.
zt=[ (sh.matrix_world@v.co).z for v in bm.verts ]
zlo,zhi=min(zt),max(zt)
for v in bm.verts:
    w=sh.matrix_world@v.co
    t=1.0-(w.z-zlo)/max(1e-6,zhi-zlo)          # 0 at the collar, 1 at the hem
    v.co += v.normal*(LIFT*(1.0+1.5*t*t))
    rad=Vector((w.x,w.y,0.0))
    if rad.length>1e-6:
        out=(sh.matrix_world.inverted().to_3x3()@(rad.normalized()))
        v.co += out*(0.026*t*t)
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
# Redrawn against the reference. Three things were wrong with the first one and all three are
# what made it read as clip art: it was BLUE where the reference is cream, its motifs were flat
# fills with hard edges, and it was sparse enough that the ground dominated. Hawaiian prints are
# dense -- the flowers overlap and the ground is a gap between them, not a background.
# Every motif is drawn with a SOFT edge now: a distance field blended in rather than a boolean
# mask stamped down. Hard masks alias into jagged petals the moment the cloth is seen at an
# angle, which on a shirt is always.
N=1024
CREAMBG=np.array([0.960,0.930,0.845])
GREEN  =np.array([0.215,0.430,0.185])
GREEN2 =np.array([0.330,0.560,0.250])
GREEN3 =np.array([0.160,0.340,0.170])
PINK   =np.array([0.890,0.330,0.470])
PINK2  =np.array([0.960,0.560,0.650])
RED    =np.array([0.800,0.180,0.290])
WHITE  =np.array([0.990,0.975,0.940])
YELL   =np.array([0.960,0.800,0.280])
NUT    =np.array([0.470,0.310,0.170])
img=np.tile(CREAMBG,(N,N,1))

def paint(cx,cy,R,fn):
    """draw one motif with wraparound, so the tile repeats without a seam"""
    R=int(R)+3
    ys=(np.arange(cy-R,cy+R)%N); xs=(np.arange(cx-R,cx+R)%N)
    gy,gx=np.meshgrid(np.arange(-R,R),np.arange(-R,R),indexing='ij')
    sub=img[np.ix_(ys,xs)].copy()
    for a,col in fn(gx,gy):
        a=np.clip(a,0,1)[...,None]
        sub=sub*(1-a)+np.asarray(col)*a
    img[np.ix_(ys,xs)]=sub

def soft(d,w=1.6):
    """a 0..1 coverage from a signed distance -- positive inside"""
    return np.clip(d/w+0.5,0,1)

def hibiscus(S,ang,pink=True):
    ca,sa=math.cos(ang),math.sin(ang)
    A,B=(PINK,PINK2) if pink else (RED,PINK)
    def f(gx,gy):
        x= gx*ca+gy*sa; y=-gx*sa+gy*ca
        r=np.hypot(x,y); th=np.arctan2(y,x)
        # five overlapping petals, each a lobe, with a notch at the tip
        edge=S*(0.60+0.40*np.cos(5*th))*(1-0.08*np.cos(10*th))
        out=[(soft(edge-r),A)]
        out.append((soft((edge*0.97-r))*soft(r-edge*0.62)*0.34,B))     # lighter mid-petal
        # veins radiating from the throat
        vein=np.abs(np.sin(5*th*2.0))
        out.append((soft(edge*0.92-r)*np.clip((vein-0.86)*7,0,1)*0.5,B*0.85))
        out.append((soft(S*0.20-r),YELL))                              # throat
        out.append((soft(S*0.085-r),np.array([0.99,0.90,0.55])))       # stamen tip
        return out
    return f

def plumeria(S,ang):
    ca,sa=math.cos(ang),math.sin(ang)
    def f(gx,gy):
        x= gx*ca+gy*sa; y=-gx*sa+gy*ca
        r=np.hypot(x,y); th=np.arctan2(y,x)
        # five fat rounded petals that overlap like a pinwheel
        edge=S*(0.70+0.30*np.cos(5*th-0.55))
        return [(soft(edge-r),WHITE),
                (soft(S*0.34-r)*0.9,YELL),
                (soft(S*0.13-r),np.array([0.95,0.72,0.25]))]
    return f

def frond(S,ang):
    ca,sa=math.cos(ang),math.sin(ang)
    def f(gx,gy):
        x= gx*ca+gy*sa; y=-gx*sa+gy*ca
        # a long blade with feathered edges -- the serration is what says palm
        along=np.clip(x/(S*1.75)+0.5,0,1)
        halfw=S*0.30*np.sin(np.pi*np.clip(along,0,1))**0.75
        serr=1-0.30*np.abs(np.sin(y*0+x*math.pi/(S*0.14)))
        d=halfw*serr-np.abs(y)
        rib=soft(S*0.030-np.abs(y))*soft(halfw-np.abs(y))
        return [(soft(d),GREEN),(rib,GREEN3),
                (soft(d)*np.clip((y/(halfw+1e-6)+1)*0.5,0,1)*0.22,GREEN2)]
    return f

def monstera(S,ang):
    ca,sa=math.cos(ang),math.sin(ang)
    def f(gx,gy):
        x= gx*ca+gy*sa; y=-gx*sa+gy*ca
        r=np.hypot(x,y*1.45); th=np.arctan2(y*1.45,x)
        blade=S*(0.92-0.22*np.cos(2*th))-r
        cut=(np.abs(np.sin(y*math.pi/(S*0.40)))<0.26)&(np.abs(x)>S*0.14)
        a=soft(blade); a=np.where(cut,0,a)
        return [(a,GREEN2),(soft(S*0.035-np.abs(x))*a,GREEN3)]
    return f

def coconut(S):
    def f(gx,gy):
        r=np.hypot(gx,gy)
        return [(soft(S-r),NUT),(soft(S*0.72-r)*0.4,NUT*0.72),
                (soft(S*0.16-np.hypot(gx-S*0.28,gy+S*0.20))*0.8,NUT*0.55)]
    return f

# Layered back to front the way the reference is: foliage first as a bed, then the blooms on
# top of it. Densities chosen so the cream shows through as gaps rather than as background.
rnd=random.Random(5)
S=N//5
for _ in range(46):
    paint(rnd.randrange(N),rnd.randrange(N),S*1.05,frond(S*0.58,rnd.uniform(0,math.pi*2)))
for _ in range(26):
    paint(rnd.randrange(N),rnd.randrange(N),S*0.80,monstera(S*0.60,rnd.uniform(0,math.pi*2)))
for _ in range(38):
    paint(rnd.randrange(N),rnd.randrange(N),S*0.72,
          hibiscus(S*0.56,rnd.uniform(0,math.pi*2),rnd.random()<0.6))
for _ in range(13):
    paint(rnd.randrange(N),rnd.randrange(N),S*0.52,plumeria(S*0.38,rnd.uniform(0,math.pi*2)))
for _ in range(12):
    paint(rnd.randrange(N),rnd.randrange(N),S*0.18,coconut(S*0.13))

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
mp.inputs['Scale'].default_value=(4.2,4.2,4.2)
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
    sc.camera=cam
    for pos,en in (((-2.4,-3.0,3.0),700),((2.6,-2.4,1.8),380),((0,2.6,2.2),260)):
        l=D.lights.new('l','AREA'); l.energy=en; l.size=3.0
        ob=D.objects.new('l',l); sc.collection.objects.link(ob); ob.location=pos
        ob.rotation_euler=(Vector((0,0,0.9))-Vector(pos)).to_track_quat('-Z','Y').to_euler()
    w=D.worlds.new('w'); sc.world=w; w.use_nodes=True
    w.node_tree.nodes['Background'].inputs[0].default_value=(0.90,0.92,0.95,1)
    w.node_tree.nodes['Background'].inputs[1].default_value=0.7
    sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=44
    sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
    sc.render.resolution_x=680; sc.render.resolution_y=860
    base=os.path.splitext(SHOT)[0]
    for nm,loc,rot in [('front',(0.0,-4.3,0.86),(1.5708,0,0)),
                       ('three',(-2.6,-3.5,1.15),(1.42,0,-0.64)),
                       ('side',(-4.2,-0.9,1.00),(1.50,0,-1.36))]:
        cam.location=loc; cam.rotation_euler=Euler(rot)
        sc.render.filepath=base+'_'+nm+'.png'
        bpy.ops.render.render(write_still=True)
        print('rendered',sc.render.filepath)
