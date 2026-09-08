# ============================================================================
# A LOFTED HAWAIIAN SHIRT FOR THE PUG
#
# The previous one was carved out of a copy of his body, and that could never be
# this: a garment needs topology a body has not got. A hem ring that spans both
# legs, a collar that folds outward, a placket down the front, cuffs -- none of
# those exist anywhere in his mesh to be selected out of it.
#
# So the shirt is BUILT. Rings are sampled from his cross-sections by casting
# rays at his surface, which keeps the fit honest, and then joined into clean
# quads. Everything a shirt has that a dog has not is added as its own piece.
# Weights come from his nearest vertex, so it still bends when he bends.
# ============================================================================
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Euler, kdtree
from mathutils.bvhtree import BVHTree

D=bpy.data; C=bpy.context
OUT=os.environ.get('OUT','/home/user/Surf-/models/shirt_hawaii.glb')
SHOT=os.environ.get('SHOT','/home/user/Surf-/shirt_preview.png')
TEX=os.environ.get('TEXOUT','/home/user/Surf-/shirt_print.png')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
pug=[o for o in C.scene.objects if o.type=='MESH'][0]
arm=[o for o in C.scene.objects if o.type=='ARMATURE'][0]
B={b.name:(arm.matrix_world@b.head_local) for b in arm.data.bones}
M=pug.matrix_world

# ---- his surface, to measure against, IN WORLD UNITS ----
# BVHTree.FromObject builds the tree in the object's LOCAL space, and this pug carries a 0.01
# scale in his world matrix -- so a tree built that way is a hundred times the size of the rays
# fired at it. Every cast missed, every radius fell back to the 0.05 minimum, and the shirt came
# out entirely inside the dog. Built from world-space polygons instead, so the rays and the
# surface are in the same units.
dg=C.evaluated_depsgraph_get()
_ev=pug.evaluated_get(dg); _me=_ev.to_mesh()
_wv=[pug.matrix_world@v.co for v in _me.vertices]
_wp=[list(p.vertices) for p in _me.polygons]
bvh=BVHTree.FromPolygons(_wv,_wp)
_ev.to_mesh_clear()

# and his vertices, to take weights from
kd=kdtree.KDTree(len(pug.data.vertices))
for i,v in enumerate(pug.data.vertices): kd.insert(M@v.co,i)
kd.balance()

AXIS_Y=-0.10                 # his body's centre line in plan; the spine bones sit near -0.11
NSEG=32                      # segments round the shirt
COLLAR_Z=1.030
HEM_Z=0.320
CLEAR=0.028                  # how far the cloth stands off the fur

def surface_r(z,th,lo=0.05):
    """how far his surface is from the body axis at this height and bearing"""
    o=Vector((0.0,AXIS_Y,z)); d=Vector((math.cos(th),math.sin(th),0.0))
    hit=bvh.ray_cast(o+d*lo, d, 3.0)
    if hit[0] is None:
        hit=bvh.ray_cast(o+d*0.001, d, 3.0)
    return (hit[0]-o).length if hit[0] is not None else lo

# What the casts actually return, before anything is built on them. "The shirt came out inside
# the dog" has several possible causes and the radii tell you which in one line.
print('BVH polys',len(_wp))
print('arm world scale',[round(v,4) for v in arm.matrix_world.to_scale()])
for z in (1.00,0.86,0.66,0.50,0.38):
    rs=[surface_r(z,i/8*math.tau) for i in range(8)]
    print('  z %.2f  r  '%z+' '.join('%.3f'%r for r in rs))

# ---- the body of the shirt ----
# Ring heights are closer together at the top, where the shape changes fastest (shoulder into
# chest), and open out toward the hem where it is just falling.
HS=[1.030,1.000,0.965,0.925,0.880,0.830,0.775,0.715,0.655,0.600,0.545,0.490,0.435,0.380,0.320]
rings=[]
for z in HS:
    t=max(0.0,(0.62-z)/(0.62-HEM_Z))          # 0 at the waist, 1 at the hem
    row=[]
    for i in range(NSEG):
        th=i/NSEG*math.tau
        # widest of a small arc, so a ring below the crotch spans BOTH legs instead of dipping
        # into the gap between them -- that gap is why a copied surface could never make a hem
        r=max(surface_r(z,th+dth) for dth in (-0.22,-0.11,0.0,0.11,0.22))
        r=r+CLEAR+0.042*t*t                    # and it flares as it falls
        row.append(Vector((math.cos(th)*r, AXIS_Y+math.sin(th)*r, z)))
    rings.append(row)

bm=bmesh.new()
vgrid=[[bm.verts.new(p) for p in row] for row in rings]
bm.verts.ensure_lookup_table()
for r in range(len(vgrid)-1):
    for i in range(NSEG):
        j=(i+1)%NSEG
        bm.faces.new((vgrid[r][i],vgrid[r][j],vgrid[r+1][j],vgrid[r+1][i]))

# ---- the collar ----
# Two rings above the neckline: one standing up and out, one folded back down over it. That fold
# is the whole reason a collar reads as a collar rather than as a hoop of cloth.
top=vgrid[0]
def ring_from(src,dr,dz,scale=1.0):
    out=[]
    for i,v in enumerate(src):
        p=v.co.copy()
        rad=Vector((p.x,p.y-AXIS_Y,0.0))
        n=rad.normalized() if rad.length>1e-6 else Vector((0,1,0))
        out.append(bm.verts.new(Vector((p.x+n.x*dr, p.y+n.y*dr, p.z+dz))))
    return out
c1=ring_from(top, 0.012, 0.055)
c2=ring_from(top, 0.075, 0.020)
for a,b in ((top,c1),(c1,c2)):
    for i in range(NSEG):
        j=(i+1)%NSEG
        bm.faces.new((a[i],a[j],b[j],b[i]))

# ---- the placket, and its buttons ----
# A raised band down the centre front. In the reference it is the strongest vertical in the whole
# garment and it is most of what says "shirt" rather than "smock".
FRONT=int(NSEG*0.75)                    # -Y is the front, which is index 3/4 of the way round
def placket():
    idx=[(FRONT-1)%NSEG,FRONT%NSEG,(FRONT+1)%NSEG]
    left=[]; right=[]
    for r in range(len(vgrid)):
        row=vgrid[r]
        a=row[idx[0]].co; b=row[idx[2]].co
        na=Vector((a.x,a.y-AXIS_Y,0)).normalized()
        nb=Vector((b.x,b.y-AXIS_Y,0)).normalized()
        left.append(bm.verts.new(a+na*0.010))
        right.append(bm.verts.new(b+nb*0.010))
    for r in range(len(vgrid)-1):
        bm.faces.new((left[r],right[r],right[r+1],left[r+1]))
placket()

# ---- sleeves ----
# Built along the arm's own axis rather than sliced out of the torso, so they are round, they
# taper, and they end in a cuff.
def sleeve(side):
    a=B[side+'Arm']; e=B[side+'ForeArm']
    d=(e-a).normalized(); L=(e-a).length
    up=Vector((0,0,1)); u=d.cross(up).normalized(); w=u.cross(d).normalized()
    prev=None; first=None
    for k,(t,scale) in enumerate([(0.02,1.00),(0.30,0.98),(0.58,0.98),(0.80,1.06),(0.86,1.02)]):
        c=a+d*(L*t)
        row=[]
        for i in range(NSEG//2):
            th=i/(NSEG//2)*math.tau
            dirv=(u*math.cos(th)+w*math.sin(th))
            hit=bvh.ray_cast(c+dirv*0.02, dirv, 1.0)
            r=((hit[0]-c).length if hit[0] is not None else 0.11)
            r=(r+CLEAR*0.85)*scale
            row.append(bm.verts.new(c+dirv*r))
        if prev:
            n=len(row)
            for i in range(n):
                j=(i+1)%n
                bm.faces.new((prev[i],prev[j],row[j],row[i]))
        prev=row
        if first is None: first=row
sleeve('Left'); sleeve('Right')

bm.normal_update()
me=D.meshes.new('shirt'); bm.to_mesh(me); bm.free()
sh=D.objects.new('shirt',me); C.collection.objects.link(sh)

# thickness, and a bevel so the hem and cuffs are edges rather than paper
sd=sh.modifiers.new('s','SOLIDIFY'); sd.thickness=0.011; sd.offset=0.0
C.view_layer.objects.active=sh
bpy.ops.object.modifier_apply(modifier=sd.name)
bpy.ops.object.shade_smooth()

# ---- weights, taken from whoever he is nearest ----
# The shirt is new geometry, so it has no weights of its own. Each vertex borrows the groups of
# the closest vertex on him -- which is the right answer for cloth lying on a body, and it means
# the sleeves follow the arms and the hem follows the spine without any of it being painted.
for g in pug.vertex_groups: sh.vertex_groups.new(name=g.name)
gname={g.index:g.name for g in pug.vertex_groups}
for v in sh.data.vertices:
    co=sh.matrix_world@v.co
    _,idx,_=kd.find(co)
    for g in pug.data.vertices[idx].groups:
        sh.vertex_groups[gname[g.group]].add([v.index], g.weight, 'REPLACE')
am=sh.modifiers.new('Armature','ARMATURE'); am.object=arm
# ---- PARENTED WITHOUT INHERITING HIS SCALE ----
# The armature carries the same 0.01 in its world matrix that the mesh does. Parenting to it
# without cancelling that shrinks the shirt a hundredfold the instant the line runs -- which is
# what put it inside the dog even after the ray casts were returning correct radii. The measured
# radii were right and the object was still wrong, one line later, for the same reason.
sh.parent=arm
sh.matrix_parent_inverse=arm.matrix_world.inverted()

# ---- UVs: round the shirt, not scattered over islands ----
# Cylindrical, so the print runs continuously at ONE scale. Smart-project gave every island its
# own orientation and the pattern came out at a different size on every panel, which is a large
# part of why it read as noise rather than as fabric.
me=sh.data
if not me.uv_layers: me.uv_layers.new(name='UVMap')
uv=me.uv_layers.active.data
for poly in me.polygons:
    for li in poly.loop_indices:
        vi=me.loops[li].vertex_index
        p=me.vertices[vi].co
        th=math.atan2(p.y-AXIS_Y,p.x)
        # ---- and the two axes are scaled to the CLOTH, not to 0..1 ----
        # The shirt is about 2.5ft round and 0.77ft tall, so its circumference is roughly 3.2
        # times its height. Mapping both axes to similar ranges stretches every motif sideways
        # by that factor, which is the smearing -- flowers pulled into streaks. Matching the
        # ratio makes a texel square and a hibiscus round.
        uv[li].uv=((th/math.tau)%1.0*3.2, (p.z-HEM_Z)/(COLLAR_Z-HEM_Z)*1.0)
# seam fix: any face straddling the wrap gets pulled back to one side
for poly in me.polygons:
    us=[uv[li].uv[0] for li in poly.loop_indices]
    if max(us)-min(us)>1.6:
        for li in poly.loop_indices:
            if uv[li].uv[0]<1.6: uv[li].uv[0]+=3.2

print('shirt verts',len(me.vertices),'polys',len(me.polygons))
bb=[sh.matrix_world@Vector(c) for c in sh.bound_box]
print('shirt z %.3f .. %.3f  (hips %.3f neck %.3f)'%(
    min(v.z for v in bb),max(v.z for v in bb),B['Hips'].z,B['neck'].z))

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


# ---------- material ----------
mat=D.materials.new('hawaii'); mat.use_nodes=True
nt=mat.node_tree; bsdf=nt.nodes['Principled BSDF']
ti=nt.nodes.new('ShaderNodeTexImage'); ti.image=tex; ti.location=(-420,240)
nt.links.new(ti.outputs['Color'],bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=0.80
if 'Specular IOR Level' in bsdf.inputs: bsdf.inputs['Specular IOR Level'].default_value=0.30
sh.data.materials.clear(); sh.data.materials.append(mat)

bpy.ops.object.select_all(action='DESELECT')
sh.select_set(True); arm.select_set(True); C.view_layer.objects.active=sh
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=True,
                          export_apply=False, export_yup=True, export_skins=True)
print('wrote',OUT,os.path.getsize(OUT))

if os.environ.get('RENDER'):
    sc=C.scene
    cam_d=D.cameras.new('c'); cam=D.objects.new('cam',cam_d); sc.collection.objects.link(cam)
    cam_d.lens=52; sc.camera=cam
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
