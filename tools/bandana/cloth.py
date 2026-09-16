"""The bandana, third time: cloth again, but the RIGHT cloth, built on the pug.

Two wrong answers, and each fixed the other's fault while introducing its own:

  v10.45 draped an open triangle. It read as fabric -- soft, wide, folded -- which was right.
  But it hung 2.30 neck-radii when mid-chest is 1.45 away, so it covered his whole front and
  sat as a bib rather than something tied.

  v10.47 replaced it with a rolled band and two tubular ends. That fixed the size and the
  hugging and lost the entire point: tubes are not cloth. It read as a rope.

The photograph is both things at once, which is what a real bandana IS: a rolled band round the
neck AND a folded triangle hanging from it. So this has both, and the triangle is the short one.

Built on pug.glb throughout -- neck ring at z=0.88 r=0.40, chest bulging to 0.57 below -- and
the drape is held OUTSIDE his body by taking the larger of his own radius and its own fall, so
it never shrink-wraps into a poncho and never clips through him either.
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

NA=48
def ring_at(z, half=0.055):
    r=[p for p in pts if abs(p.z-z)<half]
    if len(r)<10: return None
    ox=sum(p.x for p in r)/len(r); oy=sum(p.y for p in r)/len(r)
    prof=[0.0]*NA
    for p in r:
        dx,dy=p.x-ox,p.y-oy
        k=int((math.atan2(dx,-dy)%math.tau)/math.tau*NA)%NA
        d=math.hypot(dx,dy)
        if d>prof[k]: prof[k]=d
    for k in range(NA):
        if prof[k]<=0: prof[k]=max(prof[(k-1)%NA],prof[(k+1)%NA])
    for _ in range(3):
        prof=[(prof[(k-1)%NA]+2*prof[k]+prof[(k+1)%NA])/4 for k in range(NA)]
    return Vector((ox,oy)), prof

NECK_Z=0.88
NC,_=ring_at(NECK_Z)
LEVELS={}
for i in range(60):
    z=NECK_Z+0.10-0.72*i/59
    g=ring_at(z)
    if g: LEVELS[round(z,3)]=g
def body(z, ang):
    k=min(LEVELS.keys(), key=lambda q: abs(q-z))
    c,prof=LEVELS[k]
    f=(ang%math.tau)/math.tau*NA
    k0=int(f)%NA; k1=(k0+1)%NA; w=f-int(f)
    return c, prof[k0]*(1-w)+prof[k1]*w
NR=body(NECK_Z,0)[1]
print(f"  neck r at the front {NR:.3f}, at the side {body(NECK_Z,math.pi/2)[1]:.3f}")

WRAP   = math.radians(282)
N, M   = 120, 54
DROP   = 0.42          # to mid-chest, measured: 1.45 neck-radii, not 2.30
GAP    = 0.024
FLARE  = 0.18
PLEATS, PL_AMP = 6, 0.10
COLL_W, COLL_T = NR*0.52, NR*0.17      # the rolled band: WIDE and flat, not a rope
TILT   = NR*0.30       # high at the back, dipping at the front

def collar_z(a):  return NECK_Z + NR*0.10 - math.cos(a)*TILT

# ---------------- the hanging triangle ----------------
verts,uvs,rows=[],[],[]
for j in range(M+1):
    t=j/M
    # ---- THE TIP DOES NOT COLLAPSE TO A POINT ----
    # A triangle that narrows to one vertex is a degenerate fan, and Solidify with an even
    # offset on a degenerate fan throws single faces out to arm's length -- the spikes came
    # from exactly four vertices at the bottom corner. Stopped at a short edge instead, which
    # at the size this is worn is a rounded point and cannot go wrong.
    cnt=max(5,N-int(round(N*t)))
    row=[]
    amp=PL_AMP*2.05*(t**0.6)*((1.0-t)**0.45)*NR
    for k in range(cnt+1):
        s=(k/cnt-0.5) if cnt else 0.0
        span=WRAP*(1.0-t)**1.05
        ang=s*span
        z=collar_z(ang)-NR*0.16-DROP*(t**1.03)
        c,br=body(z,ang)
        ripple=math.sin(s*PLEATS*(1.0-0.78*t)*math.tau)*amp
        # OUTSIDE him, or hanging free -- whichever is further out. This is what stops it
        # shrink-wrapping into a poncho where he is wide and clipping through where he is not.
        rad=max(br+GAP, NR+FLARE*(t**1.35))+ripple
        verts.append(Vector((c.x+math.sin(ang)*rad, c.y-math.cos(ang)*rad,
                             z+ripple*0.20*(1.0-t))))
        a_,b_,c_=Vector((0,0)),Vector((1,1)),Vector((1,0))
        L,R=a_.lerp(c_,t),b_.lerp(c_,t)
        uvs.append(L.lerp(R,(k/cnt) if cnt else 0.0))
        row.append(len(verts)-1)
    rows.append(row)
faces=[]
for j in range(M):
    a,b=rows[j],rows[j+1]; ia=ib=0
    while ia<len(a)-1 or ib<len(b)-1:
        if ib>=len(b)-1: faces.append((a[ia],a[ia+1],b[ib])); ia+=1
        elif ia>=len(a)-1: faces.append((a[ia],b[ib+1],b[ib])); ib+=1
        elif (ia+1)/(len(a)-1)<=(ib+1)/(len(b)-1): faces.append((a[ia],a[ia+1],b[ib])); ia+=1
        else: faces.append((a[ia],b[ib+1],b[ib])); ib+=1
me=bpy.data.meshes.new("Drape"); me.from_pydata([tuple(v) for v in verts],[],faces); me.update()
uvl=me.uv_layers.new(name="UVMap")
for poly in me.polygons:
    for li in poly.loop_indices: uvl.data[li].uv=uvs[me.loops[li].vertex_index]
drape=bpy.data.objects.new("Drape",me); sc.collection.objects.link(drape)

# ---------------- the rolled band over the top of it ----------------
CN=140; SEG=10
cv,cuv,crings=[],[],[]
for i in range(CN+1):
    a=(i/CN-0.5)*WRAP
    z=collar_z(a)
    c,br=body(z,a)
    ctr=Vector((c.x+math.sin(a)*(br+GAP+COLL_T*0.45),
                c.y-math.cos(a)*(br+GAP+COLL_T*0.45), z))
    out=Vector((math.sin(a),-math.cos(a),0))
    ring=[]
    for k in range(SEG):
        q=k/SEG*math.tau
        cv.append(tuple(ctr+Vector((0,0,1))*math.cos(q)*COLL_W*0.5+out*math.sin(q)*COLL_T*0.5))
        cuv.append((0.10+0.80*((i/CN)*2.4%1.0), 0.16+0.68*(k/SEG)))
        ring.append(len(cv)-1)
    crings.append(ring)
cf=[]
for i in range(len(crings)-1):
    for k in range(SEG):
        cf.append((crings[i][k],crings[i][(k+1)%SEG],crings[i+1][(k+1)%SEG],crings[i+1][k]))
cf.append(tuple(reversed(crings[0]))); cf.append(tuple(crings[-1]))
cm=bpy.data.meshes.new("Collar"); cm.from_pydata(cv,[],cf); cm.update()
cl=cm.uv_layers.new(name="UVMap")
for poly in cm.polygons:
    for li in poly.loop_indices: cl.data[li].uv=cuv[cm.loops[li].vertex_index]
collar=bpy.data.objects.new("Collar",cm); sc.collection.objects.link(collar)

# ---------------- the knot, in the gap at the back ----------------
c,br=body(collar_z(math.pi),math.pi)
K=Vector((c.x, c.y+(br+GAP+COLL_T*0.5), collar_z(math.pi)))
bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=1.0,location=K)
kn=bpy.context.active_object; kn.name="Knot"
kn.scale=(NR*0.24,NR*0.17,NR*0.20); bpy.ops.object.transform_apply(scale=True)
ku=kn.data.uv_layers.new(name="UVMap")
for poly in kn.data.polygons:
    for li in poly.loop_indices:
        co=kn.data.vertices[kn.data.loops[li].vertex_index].co
        ku.data[li].uv=(0.45+co.x*0.9,0.45+co.z*0.9)

# ---- SOLIDIFY THE SHEET, AND ONLY THE SHEET ----
# The drape is an open surface and needs thickness. The collar and the knot are already closed
# volumes, and solidifying a closed volume with an even offset turns its caps inside out: the
# joined mesh came back with foot-long teal spikes radiating out of the neck in every direction.
# So the modifier goes on the drape alone, and is applied before anything is joined to it.
bpy.ops.object.select_all(action='DESELECT')
drape.select_set(True); bpy.context.view_layer.objects.active=drape
m=drape.modifiers.new("Solidify",'SOLIDIFY'); m.thickness=NR*0.030; m.offset=0.0
m.use_rim=True; m.use_even_offset=False   # see the note on the tip, above
bpy.ops.object.modifier_apply(modifier="Solidify")
bpy.ops.object.select_all(action='DESELECT')
for o in (collar,kn,drape): o.select_set(True)
bpy.context.view_layer.objects.active=drape
bpy.ops.object.join()
ob=bpy.context.active_object; ob.name="Bandana"
for poly in ob.data.polygons: poly.use_smooth=True
mat=bpy.data.materials.new("BandanaCloth"); mat.use_nodes=True
nt=mat.node_tree; bsdf=nt.nodes["Principled BSDF"]
tex=nt.nodes.new("ShaderNodeTexImage")
tex.image=bpy.data.images.load(f"{OUT}/bandana_col.jpg"); tex.interpolation='Smart'
nt.links.new(tex.outputs["Color"],bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value=0.80
ob.data.materials.clear(); ob.data.materials.append(mat)
print(f"  built: {len(ob.data.vertices)} verts",flush=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/cloth.blend")
print("  saved",flush=True)
