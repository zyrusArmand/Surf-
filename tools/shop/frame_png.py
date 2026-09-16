"""Render the niche flat, square on, with alpha -- so it can be the frame round a shop tile.

Orthographic and dead front-on, because this becomes a 2D asset: any perspective baked into it
would fight the grid it is tiled into. Lit softly from the front so the carving still reads,
rather than with the raking key a hero render wants -- a frame that is half in shadow looks
broken when it is repeated twelve times down a list.
"""
import bpy, math, os, sys
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
SRC="/root/.claude/uploads/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/bb091353-Shopframe.glb"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=64
sc.cycles.use_denoising=True
sc.render.film_transparent=True
sc.render.image_settings.file_format='PNG'
sc.render.image_settings.color_mode='RGBA'
deps=bpy.context.evaluated_depsgraph_get()
pts=[]
for o in sc.objects:
    if o.type!='MESH': continue
    m=o.evaluated_get(deps).to_mesh()
    for v in m.vertices: pts.append(o.matrix_world@v.co)
    o.evaluated_get(deps).to_mesh_clear()
lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
W,H=hi.x-lo.x, hi.z-lo.z
ctr=(lo+hi)/2
print(f"  niche {W:.3f} x {H:.3f}   aspect {W/H:.4f}")
PX=560
sc.render.resolution_x=PX; sc.render.resolution_y=int(PX*H/W)
w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True
bg=[x for x in w.node_tree.nodes if x.type=="BACKGROUND"][0]
bg.inputs[0].default_value=(1,1,1,1); bg.inputs[1].default_value=0.85
for rot,en in [((math.radians(62),0,math.radians(22)),2.2),
               ((math.radians(96),0,math.radians(-40)),1.1),
               ((math.radians(30),0,math.radians(180)),0.8)]:
    d=bpy.data.lights.new("L",'SUN'); d.energy=en; d.angle=math.radians(22)
    o=bpy.data.objects.new("L",d); o.rotation_euler=rot; sc.collection.objects.link(o)
cam_d=bpy.data.cameras.new("C"); cam_d.type='ORTHO'
cam_d.ortho_scale=max(W,H)*1.005
cam=bpy.data.objects.new("C",cam_d); sc.collection.objects.link(cam); sc.camera=cam
cam.location=(ctr.x, ctr.y-4.0, ctr.z)
cam.rotation_euler=(math.radians(90),0,0)
sc.render.filepath=f"{OUT}/shelf_frame.png"
bpy.ops.render.render(write_still=True)
print(f"  wrote shelf_frame.png  {os.path.getsize(f'{OUT}/shelf_frame.png')/1024:.0f} KB")
