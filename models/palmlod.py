import bpy, sys, os
SRC='/home/user/Surf-/models/palm2.glb'
DST='/home/user/Surf-/models/palmlod.glb'
RATIO=float(sys.argv[-1]) if sys.argv[-1].replace('.','').isdigit() else 0.06

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
print('imported', [(o.name, len(o.data.polygons)) for o in meshes])
for o in meshes:
    bpy.context.view_layer.objects.active=o
    m=o.modifiers.new('dec','DECIMATE')
    m.decimate_type='COLLAPSE'
    m.ratio=RATIO
    m.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    o.data.materials.clear()
    print('after', o.name, len(o.data.polygons), 'uv layers', len(o.data.uv_layers))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB',
    export_image_format='NONE', export_yup=True,
    export_normals=True, export_texcoords=True,
    export_apply=True)
print('wrote', DST, os.path.getsize(DST)//1024, 'KB')
