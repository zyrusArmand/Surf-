import bpy, os, sys
SRC=sys.argv[1]; OUT=sys.argv[2]; TARGET=int(sys.argv[3])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

# ---- JOIN FIRST ----
# The first pack was one mesh and this script took [0] and got the whole model. jetpack2 is a
# photogrammetry scan split into thirteen primitives by the 65535-vertex limit, so [0] is a
# thirteenth of a jetpack -- and decimating a thirteenth to the whole model's budget produces a
# perfectly clean object that is one slice of the thing you wanted. Everything is joined into
# one mesh before anything else touches it.
ms=[x for x in bpy.data.objects if x.type=='MESH']
print("meshes in", len(ms), "tris in", sum(sum(len(p.vertices)-2 for p in m.data.polygons) for m in ms))
bpy.context.view_layer.objects.active=ms[0]
bpy.ops.object.select_all(action='DESELECT')
for m in ms: m.select_set(True)
if len(ms)>1: bpy.ops.object.join()
o=bpy.context.view_layer.objects.active
# the scan arrives under a node scale; bake it in so the exported units are the model's own
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
nt=sum(len(p.vertices)-2 for p in o.data.polygons)
print("merged", nt)

# No textures survive this: the pack is worn at about a foot across, thirty feet down, in fog,
# and it is painted one flat white on purpose. So there is nothing to unwrap and nothing to
# bake -- decimate straight down to the budget and drop every material for a single white one.
m=o.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,TARGET/float(nt))
bpy.ops.object.modifier_apply(modifier=m.name)
print("out tris", sum(len(p.vertices)-2 for p in o.data.polygons), "verts", len(o.data.vertices))

bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
try: bpy.ops.object.shade_auto_smooth(angle=0.52)
except Exception: bpy.ops.object.shade_smooth()

o.data.materials.clear()
w=bpy.data.materials.new("jet_white"); w.use_nodes=True
bsdf=w.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value=(0.92,0.93,0.95,1.0)
if "Roughness" in bsdf.inputs: bsdf.inputs["Roughness"].default_value=0.42
if "Metallic"  in bsdf.inputs: bsdf.inputs["Metallic"].default_value=0.0
o.data.materials.append(w)
for p in o.data.polygons: p.material_index=0

d=o.dimensions
print("dims x %.4f y %.4f z %.4f" % (d.x, d.y, d.z))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', export_yup=True,
                          export_skins=False, export_animations=False)
print("wrote", OUT, os.path.getsize(OUT))
