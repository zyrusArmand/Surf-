"""Bring the shop kiosk in: turn it the game's way up, weld it, decimate it, shrink its textures.

SUPERSEDED BY bake.py, which is what actually ships. Keeping the source atlas -- which is what
this script does -- smears the walls no matter how many triangles or texels it is given, for
the reason written up in bake.py. This is kept for the orientation and budget findings below,
which still hold, and because it is the cheap path for any future model whose UVs are sane.

Four things have to be true before this can replace the hut.

WELD FIRST, AND THIS IS THE ONE THAT MATTERED. The source is auto-unwrapped, so its atlas is
thousands of tiny islands, and glTF splits a vertex at every UV seam -- the mesh arrives as
4,906 DISCONNECTED shells, not one surface. Collapse-decimating confetti shrinks each shard on
its own and pulls gaps open between them, and that is what "the shop looks low quality" was:
cracks and flat shards across the counter, and real daylight through the thatch that read as a
broken transparent material. Welding by distance first (741,020 verts -> 630,508, 4,906 shells
-> 1) makes it one watertight surface, and because Blender keeps UVs per face corner the seams
survive in UV space while the geometry stops being confetti. Every build shipped before this
skipped the weld, and no amount of triangles or texture resolution could have fixed them --
this was never a resolution problem, which is where two passes were spent before measuring.

ORIENTATION: NONE NEEDED, which took a wrong turn to establish. Blender's glTF importer already
converts Y-up to Z-up on the way in, so a model that reads as "Z-up, 1.90 tall" in Blender is a
correct Y-up glTF. Rotating it to "fix" that laid a perfectly good building on its side. The
check that settled it was rendering the OLD hut the same way and comparing: both face -Y in
Blender, which is +Z in glTF, so the kiosk already faces where the hut faced.

WEIGHT, AND HOW FAR IS TOO FAR. 1,273,776 triangles is not a prop on a phone, it is a scan.
The first pass took the second building down to 38,000 -- the number that had been right for
the flat kiosk before it -- and that was too far: thatch and a carved counter are silhouette
all the way down, and the render came back visibly shattered into flat facets. 130,000 looks
right and weighs 5.65 MB, which is not a number to put on a phone either. 80,000 is where the
faceting stops being visible and the file lands at 3.7 MB. There is no Draco in the vendored
GLTFLoader, so that is the whole of the geometry budget.

SIZE ON THE WIRE. 23.8 MB, of which nearly all is three 2048 PNGs. This is one building seen
from one side.
"""
import bpy, math, os, sys
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
SRC="/root/.claude/uploads/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/2e063e41-surfshop4_MAX.glb"
TARGET=int(sys.argv[-2]) if len(sys.argv)>2 else 80000
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

# ---- one surface, before anything is collapsed ----
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=max(hi-lo)*0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
print(f"  welded -> {len(ob.data.vertices)} verts")

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
