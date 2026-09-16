"""The bandana as the photograph actually shows it: a ROLLED BAND, not an open triangle.

Two attempts modelled a square folded on its diagonal and draped -- which is one real way to
wear one, and is not the way in the reference. Look at the photo: there is a rolled band round
the neck sitting under the jaw, and two ends coming off it that cross and hang down the chest in
a narrow V. No open triangle anywhere. Modelling a bib and then trying to fit it was always
going to end at "it fits weird", because the shape was wrong before the fit was.

Built on the pug himself: the band follows his measured neck ring and the ends follow his chest
outward as it widens, so nothing is a fraction of anything.
"""
import bpy, bmesh, math, os
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
sc=bpy.context.scene
deps=bpy.context.evaluated_depsgraph_get()
pts=[]
for o in list(sc.objects):
    if o.type!='MESH': continue
    m=o.evaluated_get(deps).to_mesh()
    for v in m.vertices: pts.append(o.matrix_world @ v.co)
    o.evaluated_get(deps).to_mesh_clear()

# ---- HIS CROSS-SECTION IS NOT A CIRCLE ----
# One radius per height put the band on a hoop that stood out past his sides like a collar on a
# wire, because a pug at the neck is a good deal wider than he is deep. So the body is sampled
# per ANGLE as well as per height, and the band follows the silhouette it actually has.
NA=48
def ring_at(z, half=0.055, frac=0.88):
    r=[p for p in pts if abs(p.z-z)<half]
    if len(r)<10: return None
    ox=sum(p.x for p in r)/len(r); oy=sum(p.y for p in r)/len(r)
    ctr=Vector((ox,oy))
    prof=[0.0]*NA
    for p in r:
        dx,dy=p.x-ox,p.y-oy
        a=math.atan2(dx,-dy)%math.tau            # 0 = front, matching how the band is laid
        k=int(a/math.tau*NA)%NA
        d=math.hypot(dx,dy)
        if d>prof[k]: prof[k]=d
    # fill any empty sector from its neighbours, then smooth so the band has no corners
    for k in range(NA):
        if prof[k]<=0: prof[k]=max(prof[(k-1)%NA],prof[(k+1)%NA])
    for _ in range(3):
        prof=[(prof[(k-1)%NA]+2*prof[k]+prof[(k+1)%NA])/4 for k in range(NA)]
    rr=sorted(math.hypot(p.x-ox,p.y-oy) for p in r)
    return rr[int(len(rr)*frac)], ctr, prof

def r_at(z, ang):
    g=body(z)
    prof=g[2]
    a=ang%math.tau
    f=a/math.tau*NA
    k0=int(f)%NA; k1=(k0+1)%NA; w=f-int(f)
    return prof[k0]*(1-w)+prof[k1]*w

NECK_Z=0.88
NR,NC,_NP=ring_at(NECK_Z)
print(f"  measured neck: z={NECK_Z:.2f}  r={NR:.3f}  centre=({NC.x:+.3f},{NC.y:+.3f})")
prof={}
for i in range(50):
    z=NECK_Z-0.70*i/49
    g=ring_at(z)
    prof[round(z,3)]=g if g else (NR,NC)
def body(z):
    k=min(prof.keys(), key=lambda q: abs(q-z))
    return prof[k]

GAP=0.022
BAND_W = NR*0.40        # how wide the rolled band is
BAND_T = NR*0.20        # and how thick the roll is

def ribbon(name, path, width, thick, tw=None, seg=9, uspan=1.0):
    """A flattened strip swept along a path: cloth rolled into a band.

    UVs come from the sweep -- u along the band, v round it -- rather than from a modulo of
    world position, which was putting a seam wherever the coordinate wrapped and running the
    print at whatever scale the pug happened to be built at."""
    verts=[]; uvs=[]; rings=[]
    bm=bmesh.new()
    for i,p in enumerate(path):
        fwd=(path[min(i+1,len(path)-1)]-path[max(i-1,0)])
        if fwd.length<1e-6: fwd=Vector((0,0,-1))
        fwd.normalize()
        out=Vector((p.x-NC.x, p.y-NC.y, 0))
        if out.length<1e-6: out=Vector((0,-1,0))
        out.normalize()
        side=fwd.cross(out).normalized()
        w=width[i] if hasattr(width,'__len__') else width
        t=thick[i] if hasattr(thick,'__len__') else thick
        ring=[]
        for k in range(seg):
            a=k/seg*math.tau
            verts.append(tuple(p+side*math.cos(a)*w*0.5+out*math.sin(a)*t*0.5))
            # u runs along the band; v runs round it, kept inside the print's field so the
            # border stripes land along the folded edges where they belong
            uvs.append((0.10+0.80*uspan*(i/max(1,len(path)-1)), 0.16+0.68*(k/seg)))
            ring.append(len(verts)-1)
        rings.append(ring)
    faces=[]
    for i in range(len(rings)-1):
        for k in range(seg):
            faces.append((rings[i][k],rings[i][(k+1)%seg],rings[i+1][(k+1)%seg],rings[i+1][k]))
    faces.append(tuple(reversed(rings[0]))); faces.append(tuple(rings[-1]))
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    o=bpy.data.objects.new(name,me); sc.collection.objects.link(o)
    uv=me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices: uv.data[li].uv=uvs[me.loops[li].vertex_index]
    bm.free()
    return o

# ---- the band round the neck ----
band=[]
CN=120
for i in range(CN+1):
    a=(i/CN)*math.tau
    # ---- AND IT TILTS ----
    # A horizontal ring on a dog reads as a bar laid across his chest, which is what the last
    # one looked like. A collar sits HIGH AT THE BACK and dips at the front, following the line
    # the neck actually runs at, and that tilt is most of what makes it read as worn.
    z=NECK_Z + NR*0.12 - math.cos(a)*NR*0.30
    c=body(z)[1]
    rr=r_at(z,a)
    band.append(Vector((c.x+math.sin(a)*(rr+GAP+BAND_T*0.4),
                        c.y-math.cos(a)*(rr+GAP+BAND_T*0.4), z)))
parts=[ribbon("Band", band, BAND_W, BAND_T, uspan=3.0)]

# ---- the two ends, crossing at the front and hanging down the chest ----
for sgn in (1,-1):
    path,ws=[],[]
    S=22
    for i in range(S):
        u=i/(S-1)
        z=NECK_Z-NR*0.30-u*0.36
        c=body(z)[1]
        # from just off the centre-front, crossing close under the band rather than out by
        # his shoulders -- in the photograph the two ends stay together down the chest
        ang=sgn*(0.34 - 0.62*u)
        rad=r_at(z,ang)+GAP+BAND_T*0.35+u*0.012
        path.append(Vector((c.x+math.sin(ang)*rad, c.y-math.cos(ang)*rad, z)))
        ws.append(BAND_W*(1.02-0.42*u**1.4))
    parts.append(ribbon(f"End{sgn}", path, ws, BAND_T*0.82, uspan=1.4))

# ---- the knot, at the back ----
c=body(NECK_Z)[1]
K=Vector((c.x, c.y+(r_at(NECK_Z,math.pi)+GAP+BAND_T*0.5), NECK_Z+NR*0.42))
bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=1.0,location=K)
kn=bpy.context.active_object; kn.name="Knot"
kn.scale=(NR*0.26,NR*0.19,NR*0.21); bpy.ops.object.transform_apply(scale=True)
uv=kn.data.uv_layers.new(name="UVMap")
for poly in kn.data.polygons:
    for li in poly.loop_indices:
        co=kn.data.vertices[kn.data.loops[li].vertex_index].co
        uv.data[li].uv=(0.45+co.x*0.9,0.45+co.z*0.9)
parts.append(kn)

bpy.ops.object.select_all(action='DESELECT')
for p in parts: p.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
ob=bpy.context.active_object; ob.name="Bandana"
b=ob.modifiers.new("Bevel",'BEVEL'); b.width=NR*0.012; b.segments=2
b.limit_method='ANGLE'; b.angle_limit=math.radians(40)
sub=ob.modifiers.new("Subsurf",'SUBSURF'); sub.levels=1; sub.render_levels=1
for poly in ob.data.polygons: poly.use_smooth=True
mat=bpy.data.materials.new("BandanaCloth"); mat.use_nodes=True
nt=mat.node_tree; bsdf=nt.nodes["Principled BSDF"]
tex=nt.nodes.new("ShaderNodeTexImage")
tex.image=bpy.data.images.load(f"{OUT}/bandana_col.jpg"); tex.interpolation='Smart'
nt.links.new(tex.outputs["Color"],bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value=0.80
ob.data.materials.clear(); ob.data.materials.append(mat)
print(f"  built: {len(ob.data.vertices)} verts",flush=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/roll.blend")
print("  saved roll.blend",flush=True)
