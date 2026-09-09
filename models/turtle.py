import bpy, os, bmesh
SRC="/root/.claude/uploads/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/692e57e3-turtle_MAX.glb"
OUT="/home/user/Surf-/models/turtle.glb"
TARGET_TRIS=9000
BAKE_PX=1024
FINAL_PX=512

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for o in list(bpy.data.objects):
    if o.type=='MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)

hi=[o for o in bpy.data.objects if o.type=='MESH'][0]
print("hi tris", sum(len(p.vertices)-2 for p in hi.data.polygons), "verts", len(hi.data.vertices))

# the low-poly is a COPY, so the original survives as the bake source
lo=hi.copy(); lo.data=hi.data.copy(); lo.name="turtle_lo"
bpy.context.collection.objects.link(lo)

# ---- merge BEFORE decimating ----
# 268k verts for 306k tris is not a closed surface, it is a shattered one: near enough every
# face carrying its own copies of its corners. Collapse cannot cross a seam it thinks is a
# boundary, so on a mesh split everywhere it does not simplify the shape, it eats holes in it.
bpy.context.view_layer.objects.active=lo
bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0004)
bpy.ops.object.mode_set(mode='OBJECT')
nv=len(lo.data.vertices); nt=sum(len(p.vertices)-2 for p in lo.data.polygons)
print("merged verts", nv, "tris", nt)

ratio=min(1.0, TARGET_TRIS/float(nt))
m=lo.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'; m.ratio=ratio
bpy.ops.object.modifier_apply(modifier=m.name)
nt2=sum(len(p.vertices)-2 for p in lo.data.polygons)
print("lo tris", nt2, "verts", len(lo.data.vertices))
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
# ---- and its OWN uvs ----
# The file's UVs address a baked atlas made for the dense mesh. Simplify the mesh and the
# islands no longer line up with what they were addressing, which is the camouflage: texels
# from all over the atlas dragged across faces that used to be a hundredth of the size.
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.006)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.shade_smooth()

img=bpy.data.images.new("turtle_baked", BAKE_PX, BAKE_PX)
lomat=bpy.data.materials.new("turtle")
lomat.use_nodes=True
nt_=lomat.node_tree
tex=nt_.nodes.new("ShaderNodeTexImage"); tex.image=img
nt_.nodes.active=tex
bsdf=[n for n in nt_.nodes if n.type=='BSDF_PRINCIPLED'][0]
bsdf.inputs['Metallic'].default_value=0.0
bsdf.inputs['Roughness'].default_value=0.72
lo.data.materials.clear(); lo.data.materials.append(lomat)

sc=bpy.context.scene
sc.render.engine='CYCLES'
sc.cycles.device='CPU'
sc.cycles.samples=1
sc.render.bake.use_selected_to_active=True
sc.render.bake.cage_extrusion=0.05
sc.render.bake.max_ray_distance=0.20
sc.render.bake.use_pass_direct=False
sc.render.bake.use_pass_indirect=False
sc.render.bake.use_pass_color=True
bpy.ops.object.select_all(action='DESELECT')
hi.select_set(True); lo.select_set(True)
bpy.context.view_layer.objects.active=lo
print("baking...")
bpy.ops.object.bake(type='DIFFUSE')
print("baked")
img.scale(FINAL_PX, FINAL_PX)
# ---- and it is LINKED, which is not the same as being selected ----
# A bake target has to be the active image node and that is all it has to be, so this node sat
# unconnected through the whole bake and worked perfectly. The exporter writes the material it
# can SEE: an unlinked texture is not part of the shader, so the turtle came out of the pipe
# with a baseColorFactor of grey and no map at all -- a clean white tortoise.
nt_.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
img.filepath_raw="/tmp/claude-0/-home-user-Surf-/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/scratchpad/baked.png"
img.file_format='PNG'
img.save()
px=list(img.pixels[:4000])
print("bake mean %.3f"%(sum(px)/len(px)))

bpy.data.objects.remove(hi, do_unlink=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB',
                          export_image_format='JPEG', export_jpeg_quality=85,
                          export_skins=True, export_animations=False, export_yup=True)
print("wrote", OUT, os.path.getsize(OUT))
