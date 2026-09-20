"""Turn boards_FULL.glb into the wheel's rotating face.

The file is one node and one mesh, and on import it looks like 722 pieces -- which is UV-seam
splitting, not boards. Welded first it resolves into 31 real shells: a ring of 18 arranged
around a hub, plus 13 alternates fanned out either side. The ring is the wheel; the spares are
a menu the artist supplied and are dropped.

The ring is kept AS ARRANGED rather than re-laid-out. It is already a clean 20-degree
arrangement, and its own angles are what the game needs to stop the right board under the
ticker -- re-placing them would only be a chance to introduce error between the picture and
the payout. The origin is moved to the ring's centre so the whole thing parents onto the
disc's hub and inherits its spin for free.

Orientation needs no correction: the boards are flat in Blender's Y, which the y-up conversion
turns into glTF Z, and the disc turns about glTF Z. The two planes already agree.

usage: python3 boardbuild.py <src.glb> <dst.glb> <tris> <size>
"""
import bpy, bmesh, sys, os, math
from mathutils import Matrix, Vector

SRC, DST = sys.argv[1], sys.argv[2]
TRIS = int(sys.argv[3]) if len(sys.argv) > 3 else 45000
SIZE = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
OUT = os.path.dirname(os.path.abspath(DST)) or "."
RING_R = 0.42          # inside this is the ring, outside it are the spares

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in obs:
    o.select_set(True)
bpy.context.view_layer.objects.active = obs[0]
if len(obs) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
# flatten the import transform before anything is measured -- the wheel cost a whole round to
# this: a hub measured in world written into local mesh data under a non-identity scale
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

d = max(ob.dimensions)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=d * 0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')

parts = []
for o in [x for x in bpy.context.scene.objects if x.type == 'MESH']:
    P = [o.matrix_world @ v.co for v in o.data.vertices]
    if not P:
        continue
    parts.append([o, Vector((sum(p.x for p in P) / len(P), sum(p.y for p in P) / len(P),
                             sum(p.z for p in P) / len(P)))])
cx = sum(p[1].x for p in parts) / len(parts)
cz = sum(p[1].z for p in parts) / len(parts)
ring, drop = [], []
for o, c in parts:
    (ring if math.hypot(c.x - cx, c.z - cz) < RING_R else drop).append((o, c))
for o, c in drop:
    bpy.data.objects.remove(o, do_unlink=True)
cx = sum(c.x for _, c in ring) / len(ring)
cz = sum(c.z for _, c in ring) / len(ring)
ring.sort(key=lambda t: math.degrees(math.atan2(t[1].x - cx, t[1].z - cz)) % 360.0)
print(f"  ring {len(ring)} boards, centre {cx:+.4f},{cz:+.4f}", flush=True)
for k, (o, c) in enumerate(ring):
    a = math.degrees(math.atan2(c.x - cx, c.z - cz)) % 360.0
    print(f"   board{k:02d}  angle {a:6.1f}", flush=True)

bpy.ops.object.select_all(action='DESELECT')
for o, _ in ring:
    o.select_set(True)
bpy.context.view_layer.objects.active = ring[0][0]
bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
high.name = "HIGH"
# origin onto the ring's centre, so this parents straight onto the disc's hub
hub = Vector((cx, 0.0, cz))
high.data.transform(Matrix.Translation(-hub))
high.location = (0, 0, 0)
bpy.context.view_layer.update()
P = [high.matrix_world @ v.co for v in high.data.vertices]
rout = max(math.hypot(p.x, p.z) for p in P)
print(f"  ring outer radius {rout:.4f}  (board plane thickness {max(p.y for p in P)-min(p.y for p in P):.4f})", flush=True)
n0 = len(high.data.polygons)

bpy.ops.object.select_all(action='DESELECT')
high.select_set(True)
bpy.context.view_layer.objects.active = high
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "LOW"
m = low.modifiers.new("dec", 'DECIMATE')
m.decimate_type = 'COLLAPSE'
m.ratio = min(1.0, TRIS / float(n0))
bpy.ops.object.modifier_apply(modifier=m.name)
print(f"  {n0} -> {len(low.data.polygons)} faces", flush=True)

bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
while len(low.data.uv_layers) > 1:
    low.data.uv_layers.remove(low.data.uv_layers[-1])
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(89), island_margin=0.0,
                         area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
# ---- PACK THE ATLAS, OR THE RESOLUTION IS A LIE ----
# Measured against the hand-authored quest board: it puts 786 texture pixels on each world unit
# of its surface from three 1024 maps, because its UVs fill 71% of the sheet. This pipeline was
# filling 14.5% of a 2048, so the map was really a 780 -- which is the whole of why one model
# reads sharp and the other reads like mush. The legacy packer boxes each island and packs the
# boxes, wasting the corners; the newer one fits the outline and found 43%.
bpy.ops.uv.select_all(action='SELECT')
try: bpy.ops.uv.average_islands_scale()
except Exception: pass
for _kw in ({'shape_method':'CONCAVE','rotate':True,'rotate_method':'ANY',
             'margin_method':'FRACTION','margin':0.0012,'scale':True},
            {'shape_method':'CONCAVE','rotate':True,'margin':0.0012},
            {'rotate':True,'margin':0.0016}, {'margin':0.0016}):
    try: bpy.ops.uv.pack_islands(**_kw); break
    except TypeError: continue
bpy.ops.object.mode_set(mode='OBJECT')

img_c = bpy.data.images.new("bakeColor", SIZE, SIZE, alpha=False)
img_n = bpy.data.images.new("bakeNormal", SIZE, SIZE, alpha=False, is_data=True)
img_n.colorspace_settings.name = 'Non-Color'
mat = bpy.data.materials.new("Boards")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
texc = nt.nodes.new("ShaderNodeTexImage"); texc.image = img_c; texc.location = (-700, 300)
texn = nt.nodes.new("ShaderNodeTexImage"); texn.image = img_n; texn.location = (-700, -250)
bsdf.inputs["Metallic"].default_value = 0.0
bsdf.inputs["Roughness"].default_value = 0.66
low.data.materials.clear()
low.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 1
sc.cycles.use_denoising = False
bk = sc.render.bake
bk.use_selected_to_active = True
# the boards are THIN -- 0.03 against a 0.7 ring -- so the cage has to be tight or the rays
# from one board reach the one behind it
bk.cage_extrusion = 0.010
bk.max_ray_distance = 0.020
bk.margin = 6
bk.margin_type = 'ADJACENT_FACES'


def bake(kind, img, node):
    for n in nt.nodes:
        n.select = False
    node.select = True
    nt.nodes.active = node
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True); low.select_set(True)
    bpy.context.view_layer.objects.active = low
    print("  baking", kind, flush=True)
    bpy.ops.object.bake(type=kind)
    p = os.path.join(OUT, img.name + ".png")
    img.filepath_raw = p; img.file_format = 'PNG'; img.save()


bk.use_pass_direct = False
bk.use_pass_indirect = False
bk.use_pass_color = True
bake('DIFFUSE', img_c, texc)
bake('NORMAL', img_n, texn)

nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.location = (-380, -250)
nt.links.new(texc.outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(texn.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

bpy.data.objects.remove(high, do_unlink=True)
low.name = "boards"
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB',
                          use_selection=True, export_apply=False,
                          export_image_format='JPEG')
print("  wrote", DST, round(os.path.getsize(DST) / 1e6, 2), "MB", flush=True)
