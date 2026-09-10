import bpy, os, sys
SRC=sys.argv[1]; OUT=sys.argv[2]; TARGET=int(sys.argv[3])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
o=[x for x in bpy.data.objects if x.type=='MESH'][0]
print("in tris", sum(len(p.vertices)-2 for p in o.data.polygons), "mats", len(o.data.materials))
bpy.context.view_layer.objects.active=o
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
nt=sum(len(p.vertices)-2 for p in o.data.polygons)
print("merged", nt)
# No textures on this one -- four flat named materials -- so there is nothing to unwrap and
# nothing to bake. Decimate keeps the material slot per face, which is the whole of what has to
# survive here.
m=o.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,TARGET/float(nt))
bpy.ops.object.modifier_apply(modifier=m.name)
print("out tris", sum(len(p.vertices)-2 for p in o.data.polygons), "verts", len(o.data.vertices))
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
try: bpy.ops.object.shade_auto_smooth(angle=0.52)
except Exception: bpy.ops.object.shade_smooth()
for mt in o.data.materials: print("  mat", mt.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', export_yup=True,
                          export_skins=False, export_animations=False)
print("wrote", OUT, os.path.getsize(OUT))
