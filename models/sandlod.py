# The beach's scanned sand piece, collapsed for the fork's island.
#
# sandFieldBuild instances the full 8,000-triangle piece about 336 times over the menu beach.
# The island is more than twice that area, so the same field at the same density is upwards of
# three million triangles -- on a screen that is also drawing the ride. This is the same piece
# at a seventh of the count: the DUNE-scale relief is in the shape and survives a collapse, and
# the grain-scale relief was never in the geometry at all, it is in the normal map, which is
# shared with the beach and unchanged.
#
# Materials are cleared and no images are written: the tiles wear SAND_MAT in the game, so
# there is one sand texture and the island can never drift away from the beach.
import bpy, sys, os
SRC='/home/user/Surf-/models/sand.glb'
DST='/home/user/Surf-/models/sandlod.glb'
RATIO=float(sys.argv[-1]) if sys.argv[-1].replace('.','').isdigit() else 0.15
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for o in [o for o in bpy.context.scene.objects if o.type=='MESH']:
    bpy.context.view_layer.objects.active=o
    m=o.modifiers.new('dec','DECIMATE'); m.decimate_type='COLLAPSE'
    m.ratio=RATIO; m.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    o.data.materials.clear()
    print('after',o.name,len(o.data.polygons),'tris, uv layers',len(o.data.uv_layers))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_image_format='NONE',
    export_yup=True, export_normals=True, export_texcoords=True, export_apply=True)
print('wrote',DST,os.path.getsize(DST)//1024,'KB')
