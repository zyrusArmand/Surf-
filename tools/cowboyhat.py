"""
Cowboy hat — a cattleman crease, built the way a hat is built.

The shape is NOT assembled from primitives. It is one surface of revolution swept
from a hand-authored profile (bmesh.ops.spin, which is extrude+rotate), then pushed
into a hat by two deformations that are what actually make it read as a cowboy hat
rather than a generic cone:

  1. the CATTLEMAN CREASE  - a centre groove front-to-back along the crown top, plus
     the two finger dents pressed into the sides near the front
  2. the BRIM ROLL         - sides curled hard up, front dipped down, back lifted.
     A flat brim is a boater, not a stetson. The roll is modulated by angle, which
     is why it cannot be done in the profile.

Then: Solidify (felt has thickness), Bevel (a perfectly sharp edge is the giveaway
of a cheap model - real edges catch light), Subdivision, and a fine Displace for the
felt nap. Band and buckle are separate objects, extruded and beveled.

Units are metres, real hat sizes: 57cm hat band, 13.5cm crown, 10cm brim.
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, Euler

OUT = "models"
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- dimensions
A        = 0.100     # head oval, front-back semi-axis  (57cm circumference-ish)
B        = 0.0875    # head oval, side-to-side semi-axis
CROWN_H  = 0.135     # crown height
THETA    = 256       # segments around  (high poly, as asked)

# ---------------------------------------------------------------- the profile
# (radial factor on the head oval, z in metres).  Brim is authored FLAT here --
# its roll is angle-dependent, so it has to be applied after the sweep.
PROFILE = [
    # crown dome, apex first
    (0.000, 0.1350), (0.150, 0.1349), (0.300, 0.1344), (0.440, 0.1332),
    (0.570, 0.1312), (0.685, 0.1283), (0.780, 0.1245), (0.855, 0.1198),
    (0.910, 0.1143), (0.950, 0.1078), (0.975, 0.1002), (0.990, 0.0915),
    # crown wall, very slightly barrelled and flaring at the base
    (0.997, 0.0800), (1.000, 0.0670), (1.001, 0.0540), (1.004, 0.0410),
    (1.010, 0.0295), (1.019, 0.0195), (1.031, 0.0115), (1.045, 0.0058),
    (1.058, 0.0022),
    # brim, flat for now, dished very slightly
    (1.105, 0.0000), (1.200, -0.0022), (1.340, -0.0040), (1.500, -0.0050),
    (1.670, -0.0052), (1.840, -0.0046), (2.000, -0.0034), (2.140, -0.0020),
    (2.260, -0.0008), (2.350, 0.0000), (2.410, 0.0004), (2.440, 0.0005),
]


def catmull(pts, n):
    """Resample a polyline through a Catmull-Rom spline. Hand-placed points give
    the shape; the spline gives the density, so the silhouette stays smooth
    instead of faceting at every authored point."""
    p = [pts[0]] + list(pts) + [pts[-1]]
    out, segs = [], len(p) - 3
    for i in range(n):
        u = i / (n - 1) * segs
        k = min(int(u), segs - 1)
        t = u - k
        p0, p1, p2, p3 = p[k], p[k + 1], p[k + 2], p[k + 3]
        t2, t3 = t * t, t * t * t
        c0, c1 = -0.5 * t3 + t2 - 0.5 * t, 1.5 * t3 - 2.5 * t2 + 1.0
        c2, c3 = -1.5 * t3 + 2.0 * t2 + 0.5 * t, 0.5 * t3 - 0.5 * t2
        out.append((p0[0] * c0 + p1[0] * c1 + p2[0] * c2 + p3[0] * c3,
                    p0[1] * c0 + p1[1] * c1 + p2[1] * c2 + p3[1] * c3))
    return out


prof = catmull(PROFILE, 150)

# ---------------------------------------------------- sweep it (extrude+rotate)
bm = bmesh.new()
verts = [bm.verts.new((r * A, 0.0, z)) for r, z in prof]
edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]

# spin IS extrude-and-rotate: it walks the edge chain around Z, extruding a ring
# of faces at every step. One call, and the topology comes out as clean quads.
bmesh.ops.spin(bm, geom=edges + verts, axis=(0, 0, 1), cent=(0, 0, 0),
               dvec=(0, 0, 0), angle=2 * math.pi, steps=THETA, use_duplicate=False)
# Weld the seam. 1e-6 was too tight: spin accumulates rotation error over 256 steps, so
# the last ring lands a hair off the first and the seam stays OPEN -- which showed up in
# the render as a wedge bitten out of the brim. Smallest real feature here is the 1.1 mm
# bevel, so 0.05 mm is safe and comfortably clears the error.
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=5e-5)

# A surface of revolution swept from an OPEN profile (apex on the axis out to the brim
# edge) must have exactly one boundary loop: the brim rim. More than that means the seam
# did not close. This is checkable, so it gets checked rather than eyeballed in a render.
_bnd = [e for e in bm.edges if len(e.link_faces) < 2]
print(f"  boundary edges: {len(_bnd)}  (expect ~{THETA} = the brim rim alone)")
if len(_bnd) > THETA * 1.2:
    print("  !! SEAM OPEN -- the sweep did not close")

def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


# ---- THE HEAD IS AN OVAL. THE BRIM IS NOT. ----
# Scaling the whole hat by the head oval was wrong, and it is what produced the wedge that
# looked bitten out of the brim: measured, the rim came out 244 mm across the front and only
# 206 mm at the sides, so from any three-quarter view the outline jumped between the two and
# read as a notch. The mesh was closed the whole time -- zero boundary edges after modifiers
# -- so it was never a hole in the geometry. It was the silhouette.
# A real hat is oval where it grips the head and very nearly round at the brim edge: the brim
# stands off the crown by roughly the same distance the whole way round.
bm.verts.ensure_lookup_table()
RAD = [math.hypot(v.co.x, v.co.y) / A for v in bm.verts]   # circular radius, before squashing


def ovality(R):
    # B/A at the head opening, relaxing to almost round by the brim edge
    return (B / A) + (1.0 - B / A) * 0.85 * smoothstep(1.06, 2.20, R)


for i, v in enumerate(bm.verts):
    v.co.y *= ovality(RAD[i])

me = bpy.data.meshes.new("HatFelt")
bm.to_mesh(me)
bm.free()
hat = bpy.data.objects.new("CowboyHat", me)
scene.collection.objects.link(hat)

# ------------------------------------------------- deform 1: cattleman crease
CREASE_D, CREASE_W = 0.0235, 0.027      # centre groove: depth, half-width
DENT_D,   DENT_R   = 0.0165, 0.050      # side finger dents: depth, radius

# the two front dents, one each side, pressed inward at the front quarters
dents = [Vector((0.052,  B * 1.02, CROWN_H * 0.80)),
         Vector((0.052, -B * 1.02, CROWN_H * 0.80))]

for v in me.vertices:
    co = v.co
    R = RAD[v.index]                            # normalised radius: 1.0 = crown wall
    if R < 1.07 and co.z > CROWN_H * 0.40:
        # centre groove, strongest at the apex, faded out by 40% height
        ramp = smoothstep(CROWN_H * 0.40, CROWN_H * 0.92, co.z)
        co.z -= CREASE_D * math.exp(-(co.y / CREASE_W) ** 2) * ramp
        # ...and the crown squeezes in a little where it is creased, as felt does
        co.y *= 1.0 - 0.055 * math.exp(-(co.y / (CREASE_W * 2.2)) ** 2) * ramp
    if R < 1.10 and co.z > CROWN_H * 0.30:
        # side dents, pushed along the inward horizontal normal
        for d in dents:
            f = max(0.0, 1.0 - ((co - d).length / DENT_R) ** 2)
            if f > 0.0:
                n = Vector((co.x / (A * A), co.y / (B * B), 0.0))
                if n.length > 1e-9:
                    co -= n.normalized() * (DENT_D * f * f)

# -------------------------------------------------- deform 2: the brim roll
ROLL, DIP, BACK = 0.062, 0.016, 0.012

for v in me.vertices:
    co = v.co
    R = RAD[v.index]
    if R <= 1.10:
        continue
    t = smoothstep(1.10, 2.44, R) ** 1.7        # nothing at the crown, all at the edge
    th = math.atan2(co.y, co.x)                 # +x is the front of the hat
    side = math.sin(th) ** 2                    # 1 at the sides, 0 front and back
    front = max(0.0, math.cos(th)) ** 2
    back = max(0.0, -math.cos(th)) ** 2
    co.z += (ROLL * side - DIP * front + BACK * back) * t
    # the brim narrows very slightly where it curls hardest, like real felt
    k = 1.0 - 0.018 * side * t
    co.x *= k
    co.y *= k

# a low-frequency wobble so it reads hand-blocked rather than machined
import random
random.seed(7)
ph = [random.uniform(0, 6.283) for _ in range(4)]
for v in me.vertices:
    co = v.co
    R = RAD[v.index]
    th = math.atan2(co.y, co.x)
    w = (math.sin(2 * th + ph[0]) * 0.0022 + math.sin(3 * th + ph[1]) * 0.0014
         + math.sin(5 * th + ph[2]) * 0.0007)
    co.z += w * smoothstep(0.9, 2.2, R)

me.update()

# ------------------------------------------------------------ the modifier stack
def add(o, kind, name, **kw):
    m = o.modifiers.new(name, kind)
    for k, val in kw.items():
        setattr(m, k, val)
    return m

# felt has thickness. A single-sided surface reads as paper at every silhouette.
add(hat, 'SOLIDIFY', 'Felt', thickness=0.0032, offset=0.0,
    use_rim=True, use_rim_only=False)
# a perfectly sharp edge is the single biggest tell of a cheap model
add(hat, 'BEVEL', 'Edges', width=0.0011, segments=3, limit_method='ANGLE',
    angle_limit=math.radians(32), harden_normals=False)
add(hat, 'SUBSURF', 'Smooth', levels=1, render_levels=2)

# the felt nap: a fine, shallow grain. Without it the surface is glass.
tex = bpy.data.textures.new("FeltNap", 'CLOUDS')
tex.noise_scale = 0.011
tex.noise_depth = 3
add(hat, 'DISPLACE', 'Nap', texture=tex, strength=0.0011, mid_level=0.5)

for p in me.polygons:
    p.use_smooth = True

# ------------------------------------------------------- hat band + buckle
# Built by extruding a ring outward, not by dropping in a torus primitive.
bmb = bmesh.new()
bw_lo, bw_hi = 0.0085, 0.0300          # band sits at the base of the crown
ring = []
for i in range(THETA):
    a = 2 * math.pi * i / THETA
    ring.append(bmb.verts.new((math.cos(a) * A * 1.062, math.sin(a) * B * 1.062, bw_lo)))
bmb.verts.ensure_lookup_table()
eds = [bmb.edges.new((ring[i], ring[(i + 1) % THETA])) for i in range(THETA)]
# extrude the ring upward to give the band its height
up = bmesh.ops.extrude_edge_only(bmb, edges=eds)
for el in up['geom']:
    if isinstance(el, bmesh.types.BMVert):
        el.co.z = bw_hi
        el.co.x *= 1.004
        el.co.y *= 1.004
meb = bpy.data.meshes.new("BandMesh")
bmb.to_mesh(meb)
bmb.free()
band = bpy.data.objects.new("HatBand", meb)
scene.collection.objects.link(band)
add(band, 'SOLIDIFY', 'Leather', thickness=0.0022, offset=1.0)
add(band, 'BEVEL', 'Edges', width=0.0007, segments=2, limit_method='ANGLE',
    angle_limit=math.radians(30))
add(band, 'SUBSURF', 'Smooth', levels=1, render_levels=2)
for p in meb.polygons:
    p.use_smooth = True

# buckle: a small plate at the front, inset and extruded so it has a real rim
bmk = bmesh.new()
bmesh.ops.create_cube(bmk, size=1.0)
for v in bmk.verts:
    v.co.x *= 0.0042
    v.co.y *= 0.021
    v.co.z *= 0.0145
res = bmesh.ops.inset_region(bmk, faces=[f for f in bmk.faces if f.normal.x > 0.5],
                             thickness=0.0038, depth=0.0)
bmesh.ops.translate(bmk, verts=list({v for f in res['faces'] for v in f.verts}),
                    vec=(-0.0028, 0, 0))            # press the middle in: a real buckle
bmesh.ops.bevel(bmk, geom=list(bmk.verts) + list(bmk.edges), offset=0.0009,
                segments=3, affect='EDGES')
mek = bpy.data.meshes.new("BuckleMesh")
bmk.to_mesh(mek)
bmk.free()
buckle = bpy.data.objects.new("Buckle", mek)
scene.collection.objects.link(buckle)
buckle.location = (A * 1.072, 0.0, (bw_lo + bw_hi) * 0.5)
add(buckle, 'SUBSURF', 'Smooth', levels=1, render_levels=2)
for p in mek.polygons:
    p.use_smooth = True

# ------------------------------------------------------------------ materials
def pbr(name, base, rough, metal=0.0, sheen=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    for key, val in (("Sheen Weight", sheen), ("Sheen Roughness", 0.35)):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = val
    # roughness variation: a uniform roughness is what makes CG look like plastic
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 260.0
    nz.inputs["Detail"].default_value = 6.0
    rmp = nt.nodes.new("ShaderNodeMapRange")
    rmp.inputs["To Min"].default_value = max(0.0, rough - 0.14)
    rmp.inputs["To Max"].default_value = min(1.0, rough + 0.14)
    nt.links.new(nz.outputs["Fac"], rmp.inputs["Value"])
    nt.links.new(rmp.outputs["Result"], bsdf.inputs["Roughness"])
    # and a faint bump, so the felt catches light at grazing angles
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.22
    nt.links.new(nz.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m


hat.data.materials.append(pbr("Felt", (0.086, 0.051, 0.031), 0.80, 0.0, 0.55))
band.data.materials.append(pbr("Leather", (0.045, 0.028, 0.019), 0.52))
buckle.data.materials.append(pbr("Brass", (0.68, 0.52, 0.21), 0.26, 1.0))

# ----------------------------------------------------------------- report + save
deps = bpy.context.evaluated_depsgraph_get()
tot = 0
for o in (hat, band, buckle):
    ev = o.evaluated_get(deps)
    m = ev.to_mesh()
    n = sum(len(p.vertices) - 2 for p in m.polygons)
    print(f"  {o.name:12s} {len(m.vertices):8d} verts  {n:8d} tris (viewport subdiv)")
    tot += n
    ev.to_mesh_clear()
print(f"  {'TOTAL':12s} {'':8s}        {tot:8d} tris")

bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/cowboyhat.blend")
print("saved", f"{OUT}/cowboyhat.blend")
