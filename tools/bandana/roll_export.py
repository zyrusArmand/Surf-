"""Normalise and export. Everything was built in the PUG's own space against his measured neck;
the game needs it as a unit ring at the origin, because that is what the fitting code assumes --
scale is then simply the neck radius and nothing has to be divided by anything.

The collar's tilt is baked into the mesh now rather than applied at runtime, so the in-game
tilt goes back to zero: a lean that belongs to the object should live in the object.
"""
import bpy, os, math
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/roll.blend")
sc=bpy.context.scene
ob=bpy.data.objects["Bandana"]
for o in list(sc.objects):
    if o is not ob and o.type in {'MESH','ARMATURE','EMPTY'}:
        bpy.data.objects.remove(o, do_unlink=True)
NC=Vector((0.021,-0.134)); NECK_Z=0.88; NR=0.402
ob.location=(-NC.x, -NC.y, -NECK_Z)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.object.transform_apply(location=True)
ob.scale=(1.0/NR,)*3
bpy.ops.object.transform_apply(scale=True)
deps=bpy.context.evaluated_depsgraph_get()
m=ob.evaluated_get(deps).to_mesh()
pts=[v.co for v in m.vertices]
lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
n=sum(len(p.vertices)-2 for p in m.polygons)
ob.evaluated_get(deps).to_mesh_clear()
print(f"  normalised: X {lo.x:+.2f}..{hi.x:+.2f}  Y {lo.y:+.2f}..{hi.y:+.2f}  Z {lo.z:+.2f}..{hi.z:+.2f}")
print(f"  {n} tris")
TARGET=4200
if n>TARGET:
    d=ob.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'; d.ratio=TARGET/n
    deps=bpy.context.evaluated_depsgraph_get()
    m2=ob.evaluated_get(deps).to_mesh()
    print(f"  decimated to {sum(len(p.vertices)-2 for p in m2.polygons)} tris")
    ob.evaluated_get(deps).to_mesh_clear()
bpy.ops.export_scene.gltf(filepath=f"{OUT}/bandana.glb",export_format='GLB',
                          use_selection=True,export_apply=True)
print(f"  wrote bandana.glb  {os.path.getsize(f'{OUT}/bandana.glb')/1024:.0f} KB")
