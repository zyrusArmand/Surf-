"""Normalise to a unit neck ring and export at gear weight."""
import bpy, os, math
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/cloth.blend")
sc=bpy.context.scene
ob=bpy.data.objects["Bandana"]
for o in list(sc.objects):
    if o is not ob and o.type in {'MESH','ARMATURE','EMPTY'}:
        bpy.data.objects.remove(o, do_unlink=True)
# the neck ring the whole thing was built around, in pug space
NC=Vector((0.021,-0.134)); NECK_Z=0.88; NR=0.367
ob.location=(-NC.x,-NC.y,-NECK_Z)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.object.transform_apply(location=True)
ob.scale=(1.0/NR,)*3
bpy.ops.object.transform_apply(scale=True)
deps=bpy.context.evaluated_depsgraph_get()
m=ob.evaluated_get(deps).to_mesh()
n=sum(len(p.vertices)-2 for p in m.polygons)
pts=[v.co for v in m.vertices]
print(f"  normalised: X {min(p.x for p in pts):+.2f}..{max(p.x for p in pts):+.2f}  "
      f"Z {min(p.z for p in pts):+.2f}..{max(p.z for p in pts):+.2f}   {n} tris")
ob.evaluated_get(deps).to_mesh_clear()
TARGET=5000
if n>TARGET:
    d=ob.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'; d.ratio=TARGET/n
    deps=bpy.context.evaluated_depsgraph_get()
    m2=ob.evaluated_get(deps).to_mesh()
    print(f"  decimated to {sum(len(p.vertices)-2 for p in m2.polygons)} tris")
    ob.evaluated_get(deps).to_mesh_clear()
bpy.ops.export_scene.gltf(filepath=f"{OUT}/bandana.glb",export_format='GLB',
                          use_selection=True,export_apply=True)
print(f"  wrote bandana.glb  {os.path.getsize(f'{OUT}/bandana.glb')/1024:.0f} KB")
