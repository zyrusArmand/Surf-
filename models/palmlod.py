import bpy, sys, os
SRC='/home/user/Surf-/models/palm2.glb'
DST='/home/user/Surf-/models/palmlod.glb'
RATIO=float(sys.argv[-1]) if sys.argv[-1].replace('.','').isdigit() else 0.30

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
print('imported', [(o.name, len(o.data.polygons)) for o in meshes])
for o in meshes:
    bpy.context.view_layer.objects.active=o
    # ---- COLLAPSE, then put the SHADING back ----
    # At 6 per cent this shredded the fronds: seen from forty feet it read as a palm, and once
    # he could walk under one it was torn leaves and bark hanging off the trunk in flakes, with
    # white specular sparks all over the wreckage. The sparks are the tell -- they are the
    # material's sheen catching normals left pointing in random directions by the collapse.
    # Two things fix it, and both are needed: enough triangles that a frond is still a frond,
    # and normals recomputed afterwards so what survives is shaded as one surface.
    m=o.modifiers.new('dec','DECIMATE')
    m.decimate_type='COLLAPSE'
    m.ratio=RATIO
    m.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_smooth()
    o.data.materials.clear()
    print('after', o.name, len(o.data.polygons), 'uv layers', len(o.data.uv_layers))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB',
    export_image_format='NONE', export_yup=True,
    export_normals=True, export_texcoords=True,
    export_apply=True)
print('wrote', DST, os.path.getsize(DST)//1024, 'KB')
