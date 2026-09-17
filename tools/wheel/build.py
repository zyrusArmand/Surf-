"""Turn wheel_FULL.glb into a shippable prize wheel whose disc can actually turn.

Same pipeline the shop needed -- weld, decimate, re-unwrap, bake the high-poly's colour and
normal down -- plus the one thing the shop did not: a CUT.

The file is a single fused shell. One node, one mesh, and its eleven "primitives" are only
65535-vertex chunks, so nothing in it says which triangles are the wheel and which are the
frame. Loose parts cannot tell them apart either: welded, the whole model is ONE part. A
prize wheel that cannot turn is a picture, so the disc is found by shape instead.

Measured, not guessed: the hub sits at x=-0.0004, z=+0.3327 with radius 0.6177, and the disc
occupies the depth band in front of y=-0.105 -- behind that is frame passing within the same
radius. Faces inside the circle AND in front of that plane are the wheel; everything else is
the frame. The radial profile backs it up: dense out to r/R=1.0, collapsing past it, with the
uprights and base showing up as a separate spike at 1.4-1.5.

The cut happens AFTER the bake, so both halves come out of one texture and one unwrap.
The disc's origin is moved onto the hub, so spinning it in the game is rotation.z and nothing
else -- no pivot arithmetic on the far side.

usage: python3 wheelbuild.py <src.glb> <dst.glb> <tris> <size>
"""
import bpy, bmesh, sys, os, math
from mathutils import Matrix, Vector

SRC, DST = sys.argv[1], sys.argv[2]
TRIS = int(sys.argv[3]) if len(sys.argv) > 3 else 50000
SIZE = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
OUT = os.path.dirname(os.path.abspath(DST)) or "."

# ---- TWO BANDS, BECAUSE THE RING IS A TORUS ----
# One depth plane cannot do this. The rim is a tube, so its REAR surface lies behind any plane
# that hugs the front of it -- cutting there left the whole back half of the ring with the
# frame, and spinning the disc tore the rim into arcs. Nothing but the rim lives out at rim
# radius (the frame's back crossbar is the one exception, and it sits at y=+0.21), so the rim
# band is claimed almost regardless of depth, while the INNER band stays tight against the
# front because the frame's centre post passes directly behind the hub.
RIM_LO = 0.66           # start of the rim band, in units of hub radius
CUT_R  = 1.30           # outer edge of the rim
RIM_Y  = 0.15           # rim is disc in front of this -- clear of the +0.21 crossbar
CUT_Y  = -0.085         # inner disc only, so the centre post stays with the frame
# ---- AND THE TICKER STAYS STILL ----
# The pointer sits on top of the rim, and the radial rule swallowed it -- a ticker that turns
# with the wheel points at nothing. It is not separable as an island (the cut leaves the disc
# in 2,104 pieces, none of them obviously it), so it is excluded by where it is: a narrow
# column at top centre. That also freezes a short arc of rim with it, which cannot be seen --
# the rim is plain blue with evenly spaced studs and reads identically at every angle. The
# prize is read off the SPOKES, and every one of those still turns.
# Measured at top centre: the rim's cross-section there sits at y -0.19..-0.18, and a SECOND
# dense cluster at y -0.12..-0.11 is the ticker mounted just behind it. So the two separate by
# depth, and the column needs no bite taken out of the rim -- which is what the first attempt
# did, leaving a notch that rotated round and read as a step in the blue.
PIN_X  = 0.180          # half width of the ticker column, in hub radii
PIN_Z  = 0.80           # ...and it only counts above this height
PIN_Y  = -0.145         # ...and only BEHIND this, which is where the rim has already ended

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in obs:
    o.select_set(True)
bpy.context.view_layer.objects.active = obs[0]
if len(obs) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
high.name = "HIGH"
# ---- FLATTEN THE OBJECT TRANSFORM FIRST ----
# The glTF importer leaves a non-identity object scale behind (the shop came in at 1.9016), so
# local and world coordinates are not the same space. Everything below -- the hub, the radial
# cut, the origin move -- measures in world and then writes into mesh data, which is local, and
# mixing the two displaces the disc by the scale factor. That is what "a plank flew off the
# model" was, and it was identical at 50k and 130k precisely because it was never decimation.
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
print("  transform applied, scale now", [round(v,4) for v in high.scale], flush=True)

d = max(high.dimensions)
bpy.ops.object.select_all(action='DESELECT')
high.select_set(True)
bpy.context.view_layer.objects.active = high
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=d * 0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
n0 = len(high.data.polygons)
print("  welded:", len(high.data.vertices), "verts", n0, "faces", flush=True)

# ---- the hub, measured off this mesh rather than pasted in ----
mw = high.matrix_world
P = [mw @ v.co for v in high.data.vertices]
xs = [p.x for p in P]; zs = [p.z for p in P]
HX = (min(xs) + max(xs)) / 2
R = (max(xs) - min(xs)) / 2
HZ = max(zs) - R
print(f"  hub x={HX:+.4f} z={HZ:+.4f} R={R:.4f}", flush=True)

# ---- low poly ----
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "LOW"
m = low.modifiers.new("dec", 'DECIMATE')
m.decimate_type = 'COLLAPSE'
m.ratio = min(1.0, TRIS / float(n0))
bpy.ops.object.modifier_apply(modifier=m.name)
print("  low:", len(low.data.polygons), "faces", flush=True)

# ---- unwrap ----
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
while len(low.data.uv_layers) > 1:
    low.data.uv_layers.remove(low.data.uv_layers[-1])
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(78), island_margin=0.002,
                         area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
bpy.ops.object.mode_set(mode='OBJECT')

# ---- bake ----
img_c = bpy.data.images.new("bakeColor", SIZE, SIZE, alpha=False)
img_n = bpy.data.images.new("bakeNormal", SIZE, SIZE, alpha=False, is_data=True)
img_n.colorspace_settings.name = 'Non-Color'
mat = bpy.data.materials.new("Wheel")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
texc = nt.nodes.new("ShaderNodeTexImage"); texc.image = img_c; texc.location = (-700, 300)
texn = nt.nodes.new("ShaderNodeTexImage"); texn.image = img_n; texn.location = (-700, -250)
bsdf.inputs["Metallic"].default_value = 0.0
bsdf.inputs["Roughness"].default_value = 0.74
low.data.materials.clear()
low.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 1
sc.cycles.use_denoising = False
bk = sc.render.bake
bk.use_selected_to_active = True
bk.cage_extrusion = d * 0.03
bk.max_ray_distance = d * 0.06
bk.margin = 12
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

# ---- THE CUT, after the bake so both halves share one unwrap and one texture ----
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
bm = bmesh.new(); bm.from_mesh(low.data)
bm.faces.ensure_lookup_table()
nd = 0
for f in bm.faces:
    c = f.calc_center_median()
    r = math.hypot(c.x - HX, c.z - HZ) / R
    if (abs(c.x - HX) <= R * PIN_X and (c.z - HZ) >= R * PIN_Z and c.y >= PIN_Y):
        f.select = False                   # the ticker -- behind the rim, so the rim keeps all of itself
    elif r > CUT_R:
        f.select = False
    elif r >= RIM_LO:
        f.select = (c.y <= RIM_Y)          # the whole rim tube, front and back
    else:
        f.select = (c.y <= CUT_Y)          # inner disc only
    if f.select:
        nd += 1
print(f"  disc faces {nd} / {len(bm.faces)}", flush=True)
bm.to_mesh(low.data); bm.free()
low.data.update()

bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']
# the separated piece is the new object; the original keeps the rest
disc = [o for o in parts if o.name != "LOW"][0]
frame = [o for o in parts if o.name == "LOW"][0]
disc.name = "disc"; frame.name = "frame"
print(f"  disc {len(disc.data.polygons)} faces, frame {len(frame.data.polygons)} faces", flush=True)

# ---- AND THE TICKER, WHICH PREDICATES COULD NOT FIND ----
# A depth rule failed: the two dense clusters at top centre turned out to be the rim's own
# front and back shell (it is hollow), and the ticker is a few hundred faces lost in the noise
# between them. Widening the column from 0.095 to 0.180 moved 429 faces and left it turning.
# So it is caught as a SHAPE instead: after the cut the disc falls into islands, and any small
# island sitting in the column at top centre is the ticker rather than wheel. Size matters --
# the rim and spokes are large islands and are never taken by this.
bpy.ops.object.select_all(action='DESELECT')
disc.select_set(True)
bpy.context.view_layer.objects.active = disc
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
shards = [o for o in bpy.context.scene.objects
          if o.type == 'MESH' and o.name.startswith("disc")]
moved = 0
keep = []
for o in shards:
    P = [o.matrix_world @ v.co for v in o.data.vertices]
    if not P:
        continue
    cxx = sum(p.x for p in P) / len(P)
    czz = sum(p.z for p in P) / len(P)
    if (len(o.data.polygons) < 2500 and abs(cxx - HX) <= R * PIN_X
            and (czz - HZ) >= R * PIN_Z):
        o.name = "frameBit"
        moved += len(o.data.polygons)
    else:
        keep.append(o)
print(f"  ticker islands moved to frame: {moved} faces", flush=True)
bpy.ops.object.select_all(action='DESELECT')
for o in keep:
    o.select_set(True)
bpy.context.view_layer.objects.active = keep[0]
bpy.ops.object.join()
disc = bpy.context.view_layer.objects.active
disc.name = "disc"
# the ticker pieces rejoin the frame, so the file still holds exactly two objects
bits = [o for o in bpy.context.scene.objects if o.name.startswith("frameBit")]
if bits:
    bpy.ops.object.select_all(action='DESELECT')
    for o in bits:
        o.select_set(True)
    frame.select_set(True)
    bpy.context.view_layer.objects.active = frame
    bpy.ops.object.join()
    frame = bpy.context.view_layer.objects.active
    frame.name = "frame"
print(f"  final: disc {len(disc.data.polygons)}  frame {len(frame.data.polygons)}", flush=True)

# ---- put the disc's ORIGIN on the hub, so the game spins it with rotation and nothing else ----
hub = Vector((HX, 0.0, HZ))
disc.data.transform(Matrix.Translation(-hub))
disc.location = hub
print(f"  disc origin -> {tuple(round(v,4) for v in hub)}", flush=True)

bpy.ops.object.select_all(action='DESELECT')
disc.select_set(True); frame.select_set(True)
bpy.context.view_layer.objects.active = frame
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB',
                          use_selection=True, export_apply=False,
                          export_image_format='JPEG')
print("  wrote", DST, round(os.path.getsize(DST) / 1e6, 2), "MB", flush=True)
