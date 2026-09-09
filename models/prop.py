import bpy, os, sys
SRC=sys.argv[-3]
OUT=sys.argv[-2]
TARGET_TRIS=int(sys.argv[-1])
BAKE_PX=1024
FINAL_PX=512

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
hi=[o for o in bpy.data.objects if o.type=='MESH'][0]

lo=hi.copy(); lo.data=hi.data.copy(); lo.name="prop_lo"
bpy.context.collection.objects.link(lo)
bpy.context.view_layer.objects.active=lo
bpy.ops.object.select_all(action='DESELECT'); lo.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0003)
bpy.ops.object.mode_set(mode='OBJECT')
nt=sum(len(p.vertices)-2 for p in lo.data.polygons)
print("merged tris",nt,"verts",len(lo.data.vertices))

m=lo.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'
m.ratio=min(1.0,TARGET_TRIS/float(nt))
bpy.ops.object.modifier_apply(modifier=m.name)
print("lo tris",sum(len(p.vertices)-2 for p in lo.data.polygons),"verts",len(lo.data.vertices))

bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.uv.smart_project(angle_limit=1.20, island_margin=0.002)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.shade_smooth()

img=bpy.data.images.new("prop_baked", BAKE_PX, BAKE_PX)
mat=bpy.data.materials.new("prop"); mat.use_nodes=True
tree=mat.node_tree
tex=tree.nodes.new("ShaderNodeTexImage"); tex.image=img
tree.nodes.active=tex
bsdf=[n for n in tree.nodes if n.type=='BSDF_PRINCIPLED'][0]
bsdf.inputs['Metallic'].default_value=0.0
bsdf.inputs['Roughness'].default_value=0.78
lo.data.materials.clear(); lo.data.materials.append(mat)

sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=1
b=sc.render.bake
b.use_selected_to_active=True; b.cage_extrusion=0.04; b.max_ray_distance=0.16
b.use_pass_direct=False; b.use_pass_indirect=False; b.use_pass_color=True
bpy.ops.object.select_all(action='DESELECT')
hi.select_set(True); lo.select_set(True)
bpy.context.view_layer.objects.active=lo
print("baking...")
bpy.ops.object.bake(type='DIFFUSE')
img.scale(FINAL_PX,FINAL_PX)
# LINKED, not merely active -- an unlinked bake target is not part of the shader and the
# exporter writes the material it can see
tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
px=list(img.pixels[:4000]); print("bake mean %.3f"%(sum(px)/len(px)))

bpy.data.objects.remove(hi, do_unlink=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB',
                          export_image_format='JPEG', export_jpeg_quality=85,
                          export_skins=False, export_animations=False, export_yup=True)
print("wrote",OUT,os.path.getsize(OUT))
