"""Weld, decimate, re-unwrap, and bake the high-poly detail back down.

Welding alone fixed the geometry tearing and none of the smearing. Those are two
different faults with the same root: the source atlas is thousands of tiny islands.
At 1.27M triangles a triangle sits inside one island. At 75k a triangle spans many
unrelated islands and the GPU interpolates across them -- planks smear into blobs and
the carved lettering dissolves. Resolution cannot reach it; the UV layout is the
broken part, so the layout is what gets replaced.

An earlier attempt at exactly this produced ~60k microscopic islands and a nearly
black bake. The reason was ordering: Smart UV Project ran on the raw import, which is
4,906 DISCONNECTED shells, so it made one island per shard. Welding first gives it a
single watertight surface to cut, which is the whole difference.

The normal bake is the other half. It carries the 1.27M triangles of carving onto a
75k mesh as shading, so the counter keeps its relief instead of going flat.

usage: python3 bake2.py <src.glb> <dst.glb> <tris> <size>
"""
import bpy, bmesh, sys, os, math

SRC, DST = sys.argv[1], sys.argv[2]
TRIS = int(sys.argv[3]) if len(sys.argv) > 3 else 75000
SIZE = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
OUT = os.path.dirname(os.path.abspath(DST)) or "."

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

# ---- weld FIRST: everything below depends on this being one surface ----
d = max(high.dimensions)
bpy.ops.object.select_all(action='DESELECT')
high.select_set(True)
bpy.context.view_layer.objects.active = high
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=d * 0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
n0 = len(high.data.polygons)
print("  welded high:", len(high.data.vertices), "verts", n0, "faces", flush=True)

# ---- the low ----
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "LOW"
m = low.modifiers.new("dec", 'DECIMATE')
m.decimate_type = 'COLLAPSE'
m.ratio = min(1.0, TRIS / float(n0))
bpy.ops.object.modifier_apply(modifier=m.name)
print("  low:", len(low.data.polygons), "faces", flush=True)

# ---- a fresh, contiguous unwrap ----
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

# count islands, because "did the unwrap work" is the question that sank the last attempt
bm = bmesh.new(); bm.from_mesh(low.data)
uvl = bm.loops.layers.uv.active
seen = set(); islands = 0
for f in bm.faces:
    if f.index in seen:
        continue
    islands += 1
    stack = [f]; seen.add(f.index)
    while stack:
        cf = stack.pop()
        for l in cf.loops:
            uv = l[uvl].uv
            for lo in l.edge.link_loops:
                of = lo.face
                if of.index in seen:
                    continue
                if any((ol[uvl].uv - uv).length < 1e-6 for ol in of.loops):
                    seen.add(of.index); stack.append(of)
bm.free()
print("  uv islands:", islands, flush=True)

# ---- bake targets. Left UNCONNECTED during the bake, so the material being written to
# does not also feed the shader being read -- that is the "circular dependency" warning. ----
img_c = bpy.data.images.new("bakeColor", SIZE, SIZE, alpha=False)
img_n = bpy.data.images.new("bakeNormal", SIZE, SIZE, alpha=False, is_data=True)
img_n.colorspace_settings.name = 'Non-Color'

mat = bpy.data.materials.new("Shop")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
texc = nt.nodes.new("ShaderNodeTexImage"); texc.image = img_c; texc.location = (-700, 300)
texn = nt.nodes.new("ShaderNodeTexImage"); texn.image = img_n; texn.location = (-700, -250)
bsdf.inputs["Metallic"].default_value = 0.0
bsdf.inputs["Roughness"].default_value = 0.78
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
bk.margin = 16
bk.margin_type = 'ADJACENT_FACES'


def bake(kind, img, node, **kw):
    for n in nt.nodes:
        n.select = False
    node.select = True
    nt.nodes.active = node
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    print("  baking", kind, flush=True)
    bpy.ops.object.bake(type=kind, **kw)
    p = os.path.join(OUT, img.name + ".png")
    img.filepath_raw = p; img.file_format = 'PNG'; img.save()
    print("   ->", p, flush=True)


bk.use_pass_direct = False
bk.use_pass_indirect = False
bk.use_pass_color = True
bake('DIFFUSE', img_c, texc)
bake('NORMAL', img_n, texn)

# ---- only now wire them in ----
nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.location = (-380, -250)
nt.links.new(texc.outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(texn.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.data.objects.remove(high, do_unlink=True)
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB',
                          use_selection=True, export_apply=True,
                          export_image_format='JPEG')
print("  wrote", DST, round(os.path.getsize(DST) / 1e6, 2), "MB", flush=True)
