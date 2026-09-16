"""Two outs: a hero render to look at, and a gear-weight GLB for the game."""
import bpy, os, math
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/bandana.blend")
sc=bpy.context.scene
ob=bpy.data.objects["Bandana"]
deps=bpy.context.evaluated_depsgraph_get()
def tris():
    d=bpy.context.evaluated_depsgraph_get()
    m=ob.evaluated_get(d).to_mesh()
    n=sum(len(p.vertices)-2 for p in m.polygons)
    ob.evaluated_get(d).to_mesh_clear(); return n
print("  high-poly:",tris(),"tris",flush=True)

# ---- the game one ----
# This is a neck prop on a pug on a phone, not a hero asset. Subsurf off, then collapse to a
# budget that sits alongside the hat (5,996) rather than eight times it.
TARGET=5200
for m in list(ob.modifiers):
    if m.type=='SUBSURF': m.levels=0; m.render_levels=0
cur=tris()
d=ob.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'
d.ratio=min(1.0,TARGET/max(1,cur))
print("  game:",tris(),"tris",flush=True)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.gltf(filepath=f"{OUT}/bandana.glb",export_format='GLB',
                          use_selection=True,export_apply=True)
print("  wrote bandana.glb  %.0f KB"%(os.path.getsize(f"{OUT}/bandana.glb")/1024),flush=True)
