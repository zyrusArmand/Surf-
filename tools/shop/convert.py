"""Bring the shop kiosk in: turn it the game's way up, decimate it, and shrink its textures.

Three things have to be true before this can replace the hut.

ORIENTATION: NONE NEEDED, which took a wrong turn to establish. Blender's glTF importer already
converts Y-up to Z-up on the way in, so a model that reads as "Z-up, 1.90 tall" in Blender is a
correct Y-up glTF. Rotating it to "fix" that laid a perfectly good building on its side. The
check that settled it was rendering the OLD hut the same way and comparing: both face -Y in
Blender, which is +Z in glTF, so the kiosk already faces where the hut faced.

WEIGHT. A million and ten thousand triangles is not a prop on a phone, it is a scan. The hut it
replaces is 92,000, which is itself heavy for what it is. Collapsed to a fraction of that: the
carved detail lives in the colour map, not in the geometry, and at the distance this is walked
up to the silhouette is what the triangles are for.

SIZE ON THE WIRE. 23.8 MB, of which nearly all is three 2048 PNGs. This is one building seen
from one side.
"""
import bpy, math, os, sys
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
SRC="/root/.claude/uploads/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/622bf2f7-shop_FULL.glb"
TARGET=int(sys.argv[-2]) if len(sys.argv)>2 else 38000
TEX=int(sys.argv[-1]) if len(sys.argv)>1 else 1024

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc=bpy.context.scene
objs=[o for o in sc.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active=objs[0]
if len(objs)>1: bpy.ops.object.join()
ob=bpy.context.active_object; ob.name="shop"

deps=bpy.context.evaluated_depsgraph_get()
m=ob.evaluated_get(deps).to_mesh()
pts=[ob.matrix_world@v.co for v in m.vertices]
n0=sum(len(p.vertices)-2 for p in m.polygons)
ob.evaluated_get(deps).to_mesh_clear()
lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
print(f"  in blender:  X {hi.x-lo.x:.2f}  Y {hi.y-lo.y:.2f}  Z(up) {hi.z-lo.z:.2f}")
print(f"  as glTF:     {hi.x-lo.x:.2f} wide, {hi.z-lo.z:.2f} tall, {hi.y-lo.y:.2f} deep")
print(f"  bounds   X {lo.x:+.2f}..{hi.x:+.2f}  Y {lo.y:+.2f}..{hi.y:+.2f}  Z {lo.z:+.2f}..{hi.z:+.2f}")

# ---- the weight ----
d=ob.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'
d.ratio=min(1.0, TARGET/n0)
deps=bpy.context.evaluated_depsgraph_get()
m2=ob.evaluated_get(deps).to_mesh()
n1=sum(len(p.vertices)-2 for p in m2.polygons)
ob.evaluated_get(deps).to_mesh_clear()
print(f"  {n0} tris -> {n1}")

# ---- the textures ----
for im in bpy.data.images:
    if not im.size[0]: continue
    if max(im.size)>TEX:
        w0,h0=im.size
        im.scale(TEX, int(TEX*h0/w0))
        print(f"  {im.name}: {w0}x{h0} -> {im.size[0]}x{im.size[1]}")
    im.file_format='JPEG'

bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.gltf(filepath=f"{OUT}/shop.glb",export_format='GLB',
                          use_selection=True,export_apply=True,
                          export_image_format='JPEG', export_jpeg_quality=82)
print(f"  wrote shop.glb  {os.path.getsize(f'{OUT}/shop.glb')/1024/1024:.2f} MB")
