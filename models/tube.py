# The swim tube. Built vertex by vertex rather than with the torus primitive, because the whole
# job is that the UV runs a known way round the cross-section: V=0 at the outer equator, 0.25 over
# the top, 0.5 into the hole, 0.75 underneath. The texture is painted against exactly that, so
# "teal below this line" is one number in two files and cannot drift.
import bpy, bmesh, math, os, sys
OUT=sys.argv[1] if len(sys.argv)>1 else "/home/user/Surf-/models/tube.glb"
TEX=os.path.join(os.path.dirname(os.path.abspath(__file__)),"tube_tex.png")

R=1.00          # major radius
# Set from the photograph rather than by eye: the hole is about 0.30 of the outer diameter, and
# (R-r)/(R+r)=0.30 puts r at 0.54 of R. At 0.365 the hole was 0.46 of it -- a life ring, not a
# pool tube -- and the whole thing was too thin to sit in.
r=0.520         # minor radius
NU=160          # around the ring
NV=64           # around the tube
SQUASH=0.88     # a tube sits on the water slightly squashed, never a perfect circle
RIBS=28         # the moulded segments
RIB_A=0.013     # ...and how proud they stand

bpy.ops.wm.read_factory_settings(use_empty=True)
me=bpy.data.meshes.new("tube"); ob=bpy.data.objects.new("tube",me)
bpy.context.collection.objects.link(ob)
bm=bmesh.new(); uvl=bm.loops.layers.uv.new("UVMap")
grid=[]
for i in range(NU):
    u=i/NU; au=u*2*math.pi
    # the seam ribs, strongest on the outer wall and fading over the top -- which is where the
    # moulding actually shows on one of these
    rib_w=1.0
    row=[]
    for j in range(NV):
        v=j/NV; av=v*2*math.pi
        # the moulding shows on the OUTER wall and the underside and fades over the top, which
        # is where it is on the real thing; applied all the way round it reads as corrugation
        rr=r*(1.0+RIB_A*math.cos(au*RIBS)*(0.35+0.65*max(0.0,math.cos(av))))
        rad=R+rr*math.cos(av)
        z=rr*math.sin(av)*SQUASH
        row.append(bm.verts.new((rad*math.cos(au), rad*math.sin(au), z)))
    grid.append(row)
bm.verts.ensure_lookup_table()
for i in range(NU):
    for j in range(NV):
        a=grid[i][j]; b=grid[(i+1)%NU][j]; c=grid[(i+1)%NU][(j+1)%NV]; d=grid[i][(j+1)%NV]
        f=bm.faces.new((a,b,c,d))
        # U repeats twice round the ring so the print is denser and the wrap is invisible
        u0,u1=(i/NU)*2.0,((i+1)/NU)*2.0
        v0,v1=j/NV,(j+1)/NV
        for lp,uv in zip(f.loops,[(u0,v0),(u1,v0),(u1,v1),(u0,v1)]):
            lp[uvl].uv=uv
bm.normal_update(); bm.to_mesh(me); bm.free()

# ---- the two grab handles ----
# Flattened rings standing proud of the top surface, one either side. Placed from the tube's own
# geometry -- top of the cross-section at that angle -- rather than at a guessed height.
hmat_i=1
for k,ang in enumerate([math.radians(118), math.radians(298)]):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.255, minor_radius=0.052,
                                     major_segments=56, minor_segments=16,
                                     location=(0,0,0))
    h=bpy.context.active_object; h.name="handle%d"%k
    h.scale=(1.45,1.0,0.58)
    bpy.ops.object.transform_apply(scale=True)
    h.rotation_euler=(0,0,ang)
    h.location=(R*math.cos(ang), R*math.sin(ang), r*SQUASH*0.93)
    bpy.ops.object.transform_apply(rotation=True, location=True)
    h.select_set(True)

# join the handles into the tube so it is one object with two material slots
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.object.join()

# ---- materials ----
img=bpy.data.images.load(TEX)
m1=bpy.data.materials.new("tube_print"); m1.use_nodes=True
nt=m1.node_tree; bsdf=[n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'][0]
tex=nt.nodes.new("ShaderNodeTexImage"); tex.image=img
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=0.42     # vinyl: soft sheen, not plastic gloss
bsdf.inputs['Metallic'].default_value=0.0
m2=bpy.data.materials.new("tube_teal"); m2.use_nodes=True
b2=[n for n in m2.node_tree.nodes if n.type=='BSDF_PRINCIPLED'][0]
b2.inputs['Base Color'].default_value=(0.055,0.44,0.42,1)
b2.inputs['Roughness'].default_value=0.38
ob.data.materials.clear(); ob.data.materials.append(m1); ob.data.materials.append(m2)
# the ring is slot 0, the handles slot 1 -- the ring's faces are the first NU*NV of them
ring=NU*NV
for i,p in enumerate(ob.data.polygons): p.material_index=0 if i<ring else 1

bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
try: bpy.ops.object.shade_auto_smooth(angle=0.60)
except Exception: bpy.ops.object.shade_smooth()
print("tris", sum(len(p.vertices)-2 for p in ob.data.polygons), "verts", len(ob.data.vertices))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', export_yup=True,
                          export_image_format='JPEG', export_jpeg_quality=90,
                          export_skins=False, export_animations=False)
print("wrote", OUT, os.path.getsize(OUT))
