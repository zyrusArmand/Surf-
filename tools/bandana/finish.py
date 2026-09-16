"""Thickness, a hem, the knot at the back, and the print. Then a game-weight export."""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix
OUT = os.path.dirname(os.path.abspath(__file__))
R_NECK, THICK = 1.00, 0.020
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/bandana_draped.blend")
sc = bpy.context.scene
ob = bpy.data.objects["Bandana"]


def tube(name, pts, radii, seg=14):
    """A swept tube along a polyline -- the knot and its tails are all made of these."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        fwd = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)])
        if fwd.length < 1e-6: fwd = Vector((0, 0, 1))
        fwd.normalize()
        up = Vector((0, 0, 1)) if abs(fwd.z) < 0.9 else Vector((1, 0, 0))
        sx = fwd.cross(up).normalized(); sy = fwd.cross(sx).normalized()
        ring = []
        for k in range(seg):
            a = k / seg * math.tau
            # flattened: cloth pulled into a knot is a ribbon, not a rope
            ring.append(bm.verts.new(p + sx*math.cos(a)*radii[i] + sy*math.sin(a)*radii[i]*0.62))
        rings.append(ring)
    bm.verts.ensure_lookup_table()
    for i in range(len(rings)-1):
        for k in range(seg):
            bm.faces.new([rings[i][k], rings[i][(k+1) % seg],
                          rings[i+1][(k+1) % seg], rings[i+1][k]])
    for r, rev in ((rings[0], True), (rings[-1], False)):
        f = bm.faces.new(list(reversed(r)) if rev else r)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    return o


# ---- THE KNOT, at the back, where the wrap leaves a gap ----
# A lump with two ends coming out of it. The first attempt swept two crossed loops to suggest an
# over-under, and at this size that read as two horns sticking out sideways rather than as a
# knot -- the loops were wider than the lump they were meant to be tied into. A knot seen small
# is a silhouette, so it is a silhouette: a flattened ball of gathered cloth with two tails
# falling from under it.
BACK = Vector((0, R_NECK * 1.00, -0.02))
parts = []
bpy.ops.mesh.primitive_uv_sphere_add(segments=22, ring_count=14, radius=1.0, location=BACK)
lump = bpy.context.active_object; lump.name = "Knot"
lump.scale = (0.175, 0.125, 0.145)
bpy.ops.object.transform_apply(scale=True)
# pinched across the middle, the way cloth gathers rather than a ball
for v in lump.data.vertices:
    d = (v.co - BACK)
    v.co = BACK + Vector((d.x, d.y * (1.0 - 0.34 * math.exp(-(d.x / 0.085) ** 2)), d.z))
parts.append(lump)
for sgn in (1, -1):
    pts, radii = [], []
    for i in range(14):
        u = i / 13
        # down and a little out, with one soft kink so they are not two straight pins
        pts.append(BACK + Vector((sgn * (0.055 + u * 0.135 + math.sin(u * 2.6) * 0.030),
                                  -0.010 - u * 0.055,
                                  -0.075 - u * 0.50)))
        radii.append(0.049 * (1.0 - 0.70 * u ** 1.2))
    parts.append(tube(f"Tail{sgn}", pts, radii))

# the knot parts carry the print too, taken from the middle of the field
for p in parts:
    uv = p.data.uv_layers.new(name="UVMap")
    for poly in p.data.polygons:
        for li in poly.loop_indices:
            co = p.data.vertices[p.data.loops[li].vertex_index].co
            uv.data[li].uv = (0.42 + co.x * 0.30, 0.46 + co.z * 0.30)

# ---- one object ----
bpy.ops.object.select_all(action='DESELECT')
for p in parts: p.select_set(True)
ob.select_set(True)
bpy.context.view_layer.objects.active = ob
bpy.ops.object.join()

# ---- cloth has two sides and an edge ----
m = ob.modifiers.new("Solidify", 'SOLIDIFY')
m.thickness = THICK; m.offset = 0.0; m.use_rim = True; m.use_even_offset = True
b = ob.modifiers.new("Bevel", 'BEVEL')
b.width = THICK * 0.42; b.segments = 2; b.limit_method = 'ANGLE'; b.angle_limit = math.radians(38)
sub = ob.modifiers.new("Subsurf", 'SUBSURF')
sub.levels = 0; sub.render_levels = 1
for poly in ob.data.polygons: poly.use_smooth = True

# ---- the print ----
mat = bpy.data.materials.new("BandanaCloth"); mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
tex = nt.nodes.new("ShaderNodeTexImage")
# 1024 JPEG, not a 2048 PNG: this is a neck prop on a phone, and the PNG alone was
# 600 KB of the 667 KB export.
img = bpy.data.images.load(f"{OUT}/bandana_col.jpg")
tex.image = img; tex.interpolation = 'Smart'
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.78
try: bsdf.inputs["Sheen Weight"].default_value = 0.35
except Exception: pass
try: bsdf.inputs["Specular IOR Level"].default_value = 0.30
except Exception: pass
# a faint bump off the print itself, so the weave catches the light
bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.16
nt.links.new(tex.outputs["Color"], bump.inputs["Height"])
nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
ob.data.materials.clear(); ob.data.materials.append(mat)

deps = bpy.context.evaluated_depsgraph_get()
mesh = ob.evaluated_get(deps).to_mesh()
print(f"  finished: {sum(len(p.vertices)-2 for p in mesh.polygons)} tris", flush=True)
ob.evaluated_get(deps).to_mesh_clear()
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/bandana.blend")
print("  saved bandana.blend", flush=True)
