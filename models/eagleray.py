"""
SMOOTH-SKINNED EAGLE RAY -- built, not scanned.

    python3 models/eagleray.py models/eagleray.glb [texture px]

Everything here comes off the character sheet: a top, bottom, side and back view plus a
three-quarter reference. There is no photogrammetry to clean up, so this is the other half of
models/README.md -- a shape described in numbers rather than one imported and simplified.

WHY IT IS A LOFT AND NOT A SCULPT
A ray is one surface. Wing, body, head and tail are not parts stuck together, they are one
membrane that is thick down the middle and thins to an edge all the way round, so the honest way
to describe it is a closed tube swept nose to tail whose cross-section is an ellipse of varying
width and height. That gets the whole animal -- including the pelvic lobes and the whip -- out of
two profile curves and a mound, with no seams anywhere and no boolean anything.

    ring angle a in [0,2pi)    u = -cos(a)          -1 at the left edge, +1 at the right
    x = u * w(t)                                    w is the planform half-width
    z = (a<pi ? topH : botH) * sin(a)               two half-heights, so the back domes and the
                                                    belly stays flat
    y = -t * BODY                                   t=0 at the snout, t=1 at the back of the disc

sin(a) and sqrt(1-u^2) are the same number, which is why this closes cleanly: the thickness
already goes to nought at u=+-1 with a vertical tangent, so top and bottom meet in a rounded rim
rather than a crease, and no special case is needed at the wing edge.

THE UV SEAM COSTS NOTHING
UVs in Blender live on LOOPS, not on vertices, so the tube is built with no duplicated column at
a=0: the wrap-around faces simply get u=1 on the corners that belong to column zero. That means
one continuous vertex ring, plain smooth shading, and no shading seam down the left wing -- which
is what a duplicated seam column would have cost, and is the usual reason a lofted model has a
visible line down it.

THE BAKE
There is no Cycles in this environment and no EGL for Eevee, so the bake is done here rather than
asked for: real ambient occlusion, ray cast against the finished mesh with Blender's own BVH, a
cosine-weighted hemisphere per vertex and a short throw so it darkens the places that actually
close up -- under the wing roots, the gill creases, the hollow between the pelvic lobes, the
sockets round the eyes. Because the mesh IS a grid, the per-vertex result is already a rectangular
array in UV space, so it resizes into the texture exactly instead of being rasterised.

The colour is painted the same way -- per texel, from the same parametrisation that built the
vertex under it -- so the mouth line, the gill slits and the tail bands land where the geometry
says they should rather than where an unwrap happened to put them.
"""
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from PIL import Image, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "models/eagleray.glb"
TEX = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
BAKE = 2048                      # painted at this, shipped at TEX

# ---------------------------------------------------------------- proportions
# Measured off the sheet's TOP VIEW in pixels and divided through by the half-span, so every
# number below is "how many half-spans": nose to the back of the disc came out 295px against a
# 217px half-span, and the widest point sat 150px back of the nose.
HALF_SPAN = 1.00
BODY      = 1.36                 # nose to the back of the disc
TAIL_END  = 3.10                 # in the same t units -- the whip is a shade over 2x the disc
TIP_T     = 0.508                # where the wing is widest, as a fraction of the disc

# planform: half-width against t. The leading edge sweeps out and back; the trailing edge is
# concave -- it falls away fast off the tip and then flattens into the pelvic lobes, which is the
# difference between an eagle ray and a kite.
# Traced off the sheet at twenty stations rather than invented: nose (272,103), left tip
# (56,258), back of the disc (272,400), so the half-span is 217px, the disc is 297px long and the
# widest point falls at (258-103)/297 = 0.52. The first pass put that peak at 0.705 -- a wing
# swept half again as far back as the drawing -- and the silhouette came out a lampshade: a
# straight leading edge running all the way from the snout to the tip, with no head in it. The
# flare between 0.19 and 0.33 is what makes an eagle ray: the head stays narrow and then the
# wing leaves it almost sideways.
PLAN = [
    # ...and the snout is BLUNT. The sheet's is a duck's bill seen from above -- rounded, and
    # wide enough to carry a mouth across it. Started at 0.022 it was a spike, which both lost
    # the bill and gave the bake a ring too small to fire a ray out of.
    (0.000, 0.060), (0.012, 0.084), (0.032, 0.114), (0.058, 0.142),
    (0.090, 0.178), (0.125, 0.212), (0.160, 0.256), (0.193, 0.314),
    (0.228, 0.424), (0.261, 0.562), (0.295, 0.662), (0.329, 0.747),
    (0.363, 0.816), (0.397, 0.876), (0.430, 0.926), (0.464, 0.968),
    # ...and the tip is a POINT. Sampled every 0.03 the peak came out a parabola and the wing
    # ended in a thumb; the sheet's tip is a raked point, so the three stations either side of
    # it are close together and steep.
    (0.495, 0.975), (0.512, 0.995), (0.522, 1.000), (0.534, 0.995),
    (0.552, 0.975), (0.575, 0.947), (0.600, 0.908),
    (0.634, 0.831), (0.668, 0.748), (0.700, 0.656), (0.735, 0.563),
    (0.770, 0.469), (0.803, 0.379), (0.837, 0.306), (0.871, 0.251),
    (0.900, 0.214), (0.930, 0.192), (0.955, 0.172), (0.975, 0.141),
    (0.990, 0.114), (1.000, 0.088),
    # ...and then it is a tail. Not a separate object: the whip is the same loft carrying on,
    # which is why there is no join to hide at the root.
    (1.008, 0.068), (1.025, 0.052), (1.055, 0.046), (1.100, 0.042),
    (1.200, 0.039), (1.400, 0.031), (1.700, 0.024), (2.100, 0.017),
    (2.500, 0.011), (2.850, 0.006), (3.100, 0.000),
]

# how thick the membrane is on its own, before the body mound: a wing is a skin, not a slab
MEM = [
    (0.000, 0.012), (0.060, 0.020), (0.150, 0.026), (0.300, 0.028),
    (0.500, 0.026), (0.700, 0.022), (0.860, 0.019), (0.960, 0.019),
    (1.000, 0.020), (1.040, 0.030), (1.150, 0.030), (1.600, 0.026),
    (2.200, 0.019), (2.700, 0.011), (3.100, 0.000),
]

# the body itself -- the mound down the middle that the wings hang off. Peaks at the shoulders,
# a third of the way back, which is where the side view is deepest.
# ---- and it is TWICE what the first pass had ----
# Read off the SIDE view rather than guessed: the body is 62px deep against a 285px disc, which
# is 0.218 of the disc length and 0.296 in half-spans. The first table topped out at 0.160 of
# that -- barely half -- and the three-quarter render came back a flat plate with a silhouette
# instead of a body. A ray is a thin animal at the edges and a fat one down the middle, and it
# is the middle that makes it read as alive rather than as a kite.
MOUND = [
    (0.000, 0.030), (0.030, 0.078), (0.065, 0.112), (0.100, 0.140),
    (0.140, 0.166), (0.190, 0.196), (0.250, 0.222), (0.300, 0.230),
    (0.360, 0.226), (0.440, 0.208), (0.530, 0.182), (0.630, 0.150),
    (0.730, 0.118), (0.820, 0.090), (0.890, 0.068), (0.945, 0.050),
    (1.000, 0.036), (1.050, 0.020), (1.180, 0.000), (3.100, 0.000),
]
MOUND_W  = 0.255                 # how far out from the spine the mound reaches
TOP_BIAS = 0.635                 # of the mound, how much goes above the mid-surface
DIHEDRAL = 0.150                 # how far the wing tips sit above the middle
DIHED_P  = 2.05

EYE_X, EYE_T, EYE_R = 0.1520, 0.1220, 0.0505


def spline(tab):
    """A monotone-ish cubic through the table, so the profiles are smooth without overshooting
    into a bulge the sheet does not have. scipy's PCHIP is exactly that; np.interp is the
    fallback and only costs a little faceting."""
    xs = np.array([p[0] for p in tab], dtype=np.float64)
    ys = np.array([p[1] for p in tab], dtype=np.float64)
    try:
        from scipy.interpolate import PchipInterpolator
        f = PchipInterpolator(xs, ys, extrapolate=True)
        return lambda t: np.clip(f(np.clip(t, xs[0], xs[-1])), 0.0, None)
    except Exception:
        return lambda t: np.interp(np.clip(t, xs[0], xs[-1]), xs, ys)


W_OF     = spline(PLAN)
MEM_OF   = spline(MEM)
MOUND_OF = spline(MOUND)


def arch(t):
    """The mid-surface is not a flat plane. In the side view the snout rides low, the back lifts
    over the shoulders and the tail leaves slightly below the line of the body."""
    t = np.asarray(t, dtype=np.float64)
    a = -0.055 * np.exp(-((t - 0.02) / 0.16) ** 2)        # nose down
    a += 0.022 * np.exp(-((t - 0.40) / 0.34) ** 2)        # shoulders up
    a -= 0.030 * np.clip((t - 0.95) / 0.5, 0, 1)          # the tail sets off downward
    a -= 0.055 * np.clip((t - 1.40) / 1.7, 0, 1) ** 1.4   # ...and keeps drooping, gently
    return a


def ring_t():
    """Rings are spaced by where the shape is BUSY, not evenly: the head and the tail root carry
    all the curvature and the wing is nearly flat, so even spacing spends most of its budget on
    the part that needs it least."""
    fine = np.linspace(0.0, TAIL_END, 6000)
    d = (1.0
         + 3.4 * np.exp(-((fine - 0.06) / 0.11) ** 2)     # snout
         + 2.6 * np.exp(-((fine - 0.16) / 0.13) ** 2)     # the head and the eye bumps
         + 2.2 * np.exp(-((fine - 0.25) / 0.09) ** 2)     # where the wing leaves the head
         + 1.5 * np.exp(-((fine - 0.52) / 0.12) ** 2)     # the wing tip
         + 3.0 * np.exp(-((fine - 1.00) / 0.10) ** 2)     # the tail root
         + 0.9 * np.exp(-((fine - 1.25) / 0.30) ** 2))
    d = np.where(fine > 1.30, d * 0.42, d)                # the whip is a cone; it needs nothing
    c = np.concatenate([[0.0], np.cumsum(d[1:] * np.diff(fine))])
    c /= c[-1]
    return np.interp(np.linspace(0, 1, NT + 1), c, fine)


NT    = 176                      # rings nose to tail tip
NRING = 128                      # points around each ring


def surface(tt, aa):
    """The whole animal, as one function. tt and aa broadcast together."""
    u  = -np.cos(aa)
    sn = np.sin(aa)
    w  = W_OF(tt)
    x  = u * w

    mound = MOUND_OF(tt) * np.exp(-(x / MOUND_W) ** 2)
    mem   = MEM_OF(tt)

    # the two bumps the eyes sit in, which are the only thing that makes a ray's head a head
    bump = 0.042 * np.exp(-(((np.abs(x) - EYE_X) / 0.090) ** 2 + ((tt - EYE_T) / 0.080) ** 2))
    # and the fleshy sub-rostral lobe under the snout, which is where the smile lives
    lobe = 0.032 * np.exp(-(((tt - 0.042) / 0.060) ** 2 + (x / 0.125) ** 2))

    topH = mem + mound * TOP_BIAS + bump
    botH = mem + mound * (1.0 - TOP_BIAS) + lobe

    r = np.where(sn >= 0, topH, botH) * sn
    zmid = DIHEDRAL * (np.abs(x) / HALF_SPAN) ** DIHED_P + arch(tt)
    return x, -tt * BODY + 0 * u, r + zmid


def surface_n(tt, aa, h=2e-4):
    """The normal taken from the FUNCTION, not from the triangles.

    Blender averages a vertex normal over the faces touching it, and the two rings that close
    the loft also touch a fan that collapses on a single apex -- so their averaged normal rakes
    along the cap instead of standing off the skin. Fired down that, an occlusion ray crosses
    the closure and hits the far side of the same ring, and the bake printed a black nose that
    no amount of healing the result would honestly remove. The surface is analytic, so its
    normal is too: two finite differences and a cross product, with the sign settled against the
    outward radial of the cross-section rather than assumed."""
    # ---- and the step must not cross the SEAM in the definition ----
    # z is `where(sin a >= 0, topH, botH) * sin a`, so the surface is continuous at a=0 and a=pi
    # but its two halves are different functions there. A fixed +h difference straddles that
    # switch on the rim and returns a tangent built from one point on the back and one on the
    # belly -- garbage, and the normal it makes flips ring to ring. Rendered, that is a black
    # sawtooth running the whole length of the wing's leading edge. Stepping away from whichever
    # boundary is nearest keeps both samples on the same half.
    ha = np.where((aa % np.pi) < (np.pi / 2), h, -h)
    x0, y0, z0 = surface(tt, aa)
    x1, y1, z1 = surface(tt, aa + ha)
    x2, y2, z2 = surface(tt + h, aa)
    ta = np.stack([x1 - x0, y1 - y0, z1 - z0], -1)
    tt_ = np.stack([x2 - x0, y2 - y0, z2 - z0], -1)
    n = np.cross(ta, tt_)
    ln = np.linalg.norm(n, axis=-1, keepdims=True)
    n = np.divide(n, np.maximum(ln, 1e-12))
    u = -np.cos(aa); sn = np.sin(aa)
    w = W_OF(tt)
    xx = u * w
    mound = MOUND_OF(tt) * np.exp(-(xx / MOUND_W) ** 2)
    mem = MEM_OF(tt)
    bump = 0.042 * np.exp(-(((np.abs(xx) - EYE_X) / 0.090) ** 2 + ((tt - EYE_T) / 0.080) ** 2))
    lobe = 0.032 * np.exp(-(((tt - 0.042) / 0.060) ** 2 + (xx / 0.125) ** 2))
    hh = np.where(sn >= 0, mem + mound * TOP_BIAS + bump,
                  mem + mound * (1 - TOP_BIAS) + lobe)
    # ---- and OUTWARD is the ellipse's gradient, not its radius ----
    # The cross-section is x = w*u, z = h*sin a, and on a wing w is 0.66 against an h of 0.025 --
    # eccentric enough that the position vector (x, z) points very nearly ALONG the surface. Used
    # as the reference it disagrees with the true normal across the whole flat of the wing and
    # flips it, which turned a mid-wing ring from 0.998 occlusion to 0.911 with a band pinned at
    # the floor. The gradient of (x/w)^2 + (z/h)^2 is (x/w^2, z/h^2): straight up on the flat,
    # straight out at the rim, and right everywhere between.
    radial = np.stack([xx / np.maximum(w, 1e-6) ** 2,
                       np.zeros_like(xx),
                       hh * sn / np.maximum(hh, 1e-6) ** 2], -1)
    flip = np.sum(n * radial, axis=-1) < 0
    n[flip] *= -1.0
    return n


def build_body():
    ts = ring_t()
    aa = np.linspace(0.0, 2.0 * np.pi, NRING, endpoint=False)
    T, A = np.meshgrid(ts, aa, indexing="ij")
    X, Y, Z = surface(T, A)

    verts = np.stack([X.ravel(), Y.ravel(), Z.ravel()], axis=1).tolist()
    nose = len(verts); verts.append([0.0, -ts[0] * BODY + 0.012, float(arch(ts[0]))])
    tip  = len(verts); verts.append([0.0, -ts[-1] * BODY, float(arch(ts[-1]))])

    faces, uvs = [], []
    du = 1.0 / NRING
    for i in range(NT):
        for j in range(NRING):
            k = (j + 1) % NRING
            a, b = i * NRING + j, i * NRING + k
            c, d = (i + 1) * NRING + k, (i + 1) * NRING + j
            # ---- wound the other way round, and it matters twice ----
            # a->b->c->d is the order the loft generates and it faces INWARD: at the crown of the
            # back the ring tangent is +x and the sweep tangent is -y, and +x cross -y is -z. The
            # obvious cost is a model that renders inside out under a front-facing material. The
            # quiet one is the bake -- every occlusion ray is fired along the inverted normal, so
            # it starts inside the animal and hits it immediately, and the first run came back
            # with a mean of 0.115: not an ambient occlusion map, a black one.
            faces.append((a, d, c, b))
            u0, u1 = j * du, (j + 1) * du          # j+1, not k -- the wrap face gets u=1
            v0, v1 = i / NT, (i + 1) / NT
            uvs += [(u0, v0), (u0, v1), (u1, v1), (u1, v0)]
    for j in range(NRING):                          # snout cap
        k = (j + 1) % NRING
        faces.append((nose, j, k))
        uvs += [((j + 0.5) * du, 0.0), (j * du, 0.0), ((j + 1) * du, 0.0)]
    base = NT * NRING
    for j in range(NRING):                          # the very point of the whip
        k = (j + 1) % NRING
        faces.append((tip, base + k, base + j))
        uvs += [((j + 0.5) * du, 1.0), ((j + 1) * du, 1.0), (j * du, 1.0)]

    me = bpy.data.meshes.new("ray")
    me.from_pydata(verts, [], faces)
    me.validate()
    uvl = me.uv_layers.new(name="UVMap")
    uvl.data.foreach_set("uv", np.asarray(uvs, dtype=np.float32).ravel())
    ob = bpy.data.objects.new("ray", me)
    bpy.context.collection.objects.link(ob)
    return ob, ts, aa


def build_dorsal():
    """The little fin at the root of the tail. Small, and the side view is the only place you
    really see it -- but a ray without one reads as a kite with a string."""
    t0, t1, h = 0.930, 1.020, 0.075
    rows = []
    for s in np.linspace(0, 1, 14):
        tt = t0 + (t1 - t0) * s
        y = -tt * BODY
        rise = math.sin(math.pi * min(1.0, s * 1.25)) ** 0.75
        top = float(arch(tt)) + float(MOUND_OF(tt)) * TOP_BIAS + h * rise
        thick = 0.016 * (1.0 - s * 0.85) * rise
        rows.append((y, top, thick, float(arch(tt)) + float(MOUND_OF(tt)) * TOP_BIAS * 0.6))
    verts, faces = [], []
    for (y, top, th, root) in rows:
        verts += [(-th, y, root * 0.5 + top * 0.5), (0.0, y, top), (th, y, root * 0.5 + top * 0.5)]
    for i in range(len(rows) - 1):
        a = i * 3
        for j in range(2):
            faces.append((a + j, a + j + 1, a + 3 + j + 1, a + 3 + j))
    me = bpy.data.meshes.new("dorsal")
    me.from_pydata(verts, [], faces)
    me.validate()
    me.uv_layers.new(name="UVMap")
    ob = bpy.data.objects.new("dorsal", me)
    bpy.context.collection.objects.link(ob)
    return ob


def eye_ball(sx):
    """Eyes are their own material rather than paint on the body map: they are the one part of
    this animal that is not skin, and a dark sphere with a highlight on it reads as an eye from
    any angle, which a disc drawn on a texture does not."""
    out = []
    cx = sx * EYE_X
    cy = -EYE_T * BODY
    cz = float(arch(EYE_T)) + float(MOUND_OF(EYE_T)) * TOP_BIAS * 0.42 + 0.020
    # the glint is four millimetres across on a two-metre animal; it does not need a globe
    for name, r, off, mat, seg, rc in (("eye", EYE_R, 0.0, "eye", 44, 26),
                                       ("glint", EYE_R * 0.21, EYE_R * 0.86, "glint", 14, 10)):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rc, radius=r)
        ob = bpy.context.active_object
        ob.name = f"{name}{'L' if sx < 0 else 'R'}"
        d = Vector((sx * 0.62, 0.46, 0.64)).normalized()
        ob.location = (cx + d.x * off, cy + d.y * off, cz + d.z * off)
        ob.scale = (0.86, 1.0, 1.0)
        ob["mat"] = mat
        out.append(ob)
    return out


# ---------------------------------------------------------------- the bake
def bake_ao(ob, occluders, ts, aa):
    """Real occlusion, cast against the real mesh.

    Two things this got wrong before they were measured. The BVH was built from the BODY alone,
    so the eyes and the dorsal fin -- the only pieces with a hard contact to cast anything --
    occluded nothing at all; they are in it now, which is what darkens the eye sockets and the
    root of the fin. And the throw was a fifth of a half-span, which on an animal two metres
    across is shorter than any of its concavities: a ray is very nearly convex, so a short ray
    hits nothing and the first honest run came back with a mean of 0.999, an occlusion map with
    no occlusion in it. The cup under the wings is broad and shallow and needs a long throw to
    see at all, so hits are weighted by how CLOSE they are rather than counted -- near contacts
    darken, far ones barely register, and one distance serves both scales."""
    # ---- every occluder in ITS OWN place ----
    # bmesh.from_object hands back the object's mesh in the object's LOCAL space, and a sphere
    # made by primitive_uv_sphere_add carries its position in object.location, not in its
    # vertices -- so merging the four eye parts and then transforming the lot by the BODY's
    # matrix left all four of them stacked on the origin. The origin of this model is the tip of
    # the snout. Four spheres were therefore buried inside the bill for every bake so far, which
    # is the dark smudge that three rounds of chasing normals, ray offsets and ring singularities
    # never shifted: the occlusion was real, the occluder was in the wrong place.
    deps = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for o in occluders:
        tmp = bmesh.new()
        tmp.from_object(o, deps)
        tmp.transform(o.matrix_world)
        holder = bpy.data.meshes.new("_ao_tmp")
        tmp.to_mesh(holder); tmp.free()
        bm.from_mesh(holder)
        bpy.data.meshes.remove(holder)
    bm.transform(ob.matrix_world.inverted())
    bvh = BVHTree.FromBMesh(bm)

    me = ob.data
    n = len(me.vertices)
    co = np.empty(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(n, 3)
    nr = np.empty(n * 3); me.vertices.foreach_get("normal", nr); nr = nr.reshape(n, 3)
    # the grid's normals come off the parametrisation; only the two cap apexes keep the mesh's
    T, A = np.meshgrid(ts, aa, indexing="ij")
    nr[: (NT + 1) * NRING] = surface_n(T, A).reshape(-1, 3)

    RAYS, DIST = 64, 0.55
    rng = np.random.default_rng(7)
    # cosine-weighted, by construction rather than by rejection
    xi1 = rng.random(RAYS); xi2 = rng.random(RAYS)
    r = np.sqrt(xi1); th = 2 * np.pi * xi2
    loc = np.stack([r * np.cos(th), r * np.sin(th), np.sqrt(np.maximum(0, 1 - xi1))], 1)

    # ---- the ray has to start clear of its OWN neighbours ----
    # A fixed offset is wrong on a mesh whose faces vary by two orders of magnitude in size. At
    # the snout the loft closes to a ring 0.02 across and 2e-4 is far inside the gap to the next
    # vertex, so every ray launched there began inside the adjacent triangles and reported a hit:
    # measured on the painted map, the crown of the snout came out at luminance 30 against 100
    # a tenth of the body further back -- a black nose, baked in, and nothing to do with light.
    # Scaled off the local grid spacing instead, so it is always clear and never far enough out
    # to miss a real contact.
    g = co[: (NT + 1) * NRING].reshape(NT + 1, NRING, 3)
    d0 = np.linalg.norm(np.diff(g, axis=0), axis=2)
    d1 = np.linalg.norm(g - np.roll(g, 1, axis=1), axis=2)
    step = np.zeros((NT + 1, NRING), np.float64)
    step[:-1] += d0; step[1:] += d0; step[0] *= 2; step[-1] *= 2
    step = (step * 0.5 + d1) * 0.5
    eps = np.clip(step.ravel() * 0.6, 2e-4, 0.02)
    eps = np.concatenate([eps, np.full(n - eps.size, 5e-3)])

    ao = np.ones(n, dtype=np.float32)
    for i in range(n):
        nv = Vector(nr[i])
        if nv.length < 1e-6:
            continue
        nv.normalize()
        up = Vector((0, 0, 1)) if abs(nv.z) < 0.9 else Vector((1, 0, 0))
        tx = nv.cross(up).normalized(); ty = nv.cross(tx)
        o = Vector(co[i]) + nv * float(eps[i])
        occ = 0.0
        for k in range(RAYS):
            d = tx * loc[k, 0] + ty * loc[k, 1] + nv * loc[k, 2]
            h = bvh.ray_cast(o, d, DIST)
            if h[0] is not None:
                occ += 1.0 - (h[3] / DIST) ** 0.65    # a contact counts; a far wall hardly does
        # floored, because nothing on an animal this convex is genuinely in the dark, and a
        # pinch in the parametrisation should never be able to paint a black hole
        ao[i] = max(0.38, 1.0 - occ / RAYS)
    bm.free()

    grid = np.ones((NT + 1, NRING), dtype=np.float32)
    grid[:, :] = ao[: (NT + 1) * NRING].reshape(NT + 1, NRING)

    # ---- the two CAP rings are not measured, they are inherited ----
    # Profiled along the body the bake is clean everywhere -- 0.99 and up -- except at t=0 and
    # t=TAIL_END, where it collapses to the floor. Those two rings are shared with a fan that
    # closes on a single apex, so the averaged normal there is not the surface normal at all: it
    # rakes along the cap, and the rays launched down it cross the closure and hit the far side
    # of the same ring. That is a singularity in the parametrisation, not a cavity in the animal,
    # and it printed a black nose. Healed from the nearest ring that has a real cross-section.
    # The dip at t=0.98 is left alone on purpose -- that one is the genuine notch where the tail
    # leaves the disc, and it is exactly what an occlusion pass is for.
    for lo, hi, src in ((0, 3, 3), (NT - 2, NT + 1, NT - 3)):
        for i in range(lo, hi):
            if 0 <= i <= NT and grid[i].mean() < grid[src].mean() * 0.94:
                grid[i] = grid[src]
    # the profile along the body, because a bake that has gone wrong goes wrong in BANDS and the
    # single mean hides it completely -- 0.99 overall with the first three rings pinned to the
    # floor is a black nose on an otherwise clean animal
    if os.environ.get("AOPROF"):
        for i in range(0, NT + 1, max(1, NT // 22)):
            j = int(grid[i].argmin())
            print(f"   t={ts[i]:6.3f}  ao min {grid[i].min():.3f} at u={j/NRING:.3f}"
                  f"  mean {grid[i].mean():.3f}   crown {grid[i][NRING//4]:.3f}"
                  f"  belly {grid[i][3*NRING//4]:.3f}")
    return grid


def ao_to_image(grid, W, H):
    g = np.concatenate([grid, grid[:, :1]], axis=1)      # wrap the seam so u=1 meets u=0
    src = Image.fromarray((np.clip(g, 0, 1) * 255).astype(np.uint8), "L")
    return np.asarray(src.resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(1.1)),
                      dtype=np.float32) / 255.0


# ---------------------------------------------------------------- the paint
def paint(ts, ao):
    W = H = BAKE
    uu = np.linspace(0, 1, W)[None, :]
    vv = np.linspace(0, 1, H)[:, None]
    ang = uu * 2 * np.pi
    sn  = np.sin(ang) + 0 * vv
    t   = np.interp(vv[:, 0] * NT, np.arange(NT + 1), ts)[:, None] + 0 * uu
    x   = -np.cos(ang) * W_OF(t)
    ax  = np.abs(x)

    def rgb(h):
        return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], dtype=np.float32) / 255.0

    TOPC  = rgb(0x69747F)
    TOPD  = rgb(0x55616C)
    TOPL  = rgb(0x7C8794)
    BOT   = rgb(0xEFF1F1)
    BOTG  = rgb(0xD4DADD)
    BLUSH = rgb(0xE79184)
    BAND  = rgb(0x2A2C31)
    BANDL = rgb(0xE9EBEC)

    img = np.zeros((H, W, 3), dtype=np.float32)

    # ---- the top ----
    top = TOPC[None, None, :] * np.ones((H, W, 1), np.float32)
    top += (TOPD - TOPC)[None, None, :] * np.clip(1.0 - ax / 0.45, 0, 1)[..., None] * 0.75
    top += (TOPL - TOPC)[None, None, :] * np.clip((ax - 0.55) / 0.45, 0, 1)[..., None] * 0.55
    top += (TOPD - TOPC)[None, None, :] * np.clip((t - 0.86) / 0.16, 0, 1)[..., None] * 0.5
    # the faint mottle a smooth-skinned ray still has -- not scales, just unevenness
    rn = np.random.default_rng(3).normal(0, 1, (H // 8, W // 8)).astype(np.float32)
    rn = np.asarray(Image.fromarray(((rn * 40 + 128).clip(0, 255)).astype(np.uint8), "L")
                    .resize((W, H), Image.BICUBIC), np.float32) / 255.0 - 0.5
    top += rn[..., None] * 0.035

    # ---- the underside ----
    bot = BOT[None, None, :] * np.ones((H, W, 1), np.float32)
    bot += (BOTG - BOT)[None, None, :] * np.clip((ax / np.maximum(W_OF(t), 1e-6) - 0.52) / 0.48,
                                                 0, 1)[..., None]
    # the blush the sheet puts over the gills, and a smaller one on the cheek
    for (bt, bx, st, sx_, amp) in ((0.250, 0.110, 0.105, 0.090, 0.50),
                                   (0.300, 0.160, 0.100, 0.085, 0.42),
                                   (0.350, 0.210, 0.090, 0.075, 0.32),
                                   (0.128, 0.155, 0.070, 0.065, 0.30)):
        g = np.exp(-(((t - bt) / st) ** 2 + ((ax - bx) / sx_) ** 2))
        bot += (BLUSH - BOT)[None, None, :] * (g * amp)[..., None]
    # five gill slits a side, curved and raked the way the sheet draws them -- soft salmon
    # streaks on a white belly, not five red bars: at 0.85 of a saturated red they read as a
    # wound, and the sheet's are barely more than a flush with some structure in it
    for i in range(5):
        gt = 0.206 + i * 0.0345
        bend = gt + 0.042 * np.clip((ax - 0.05) / 0.20, 0, 1) ** 1.5
        g = np.exp(-((t - bend) / 0.0115) ** 2) * np.clip((ax - 0.040) / 0.055, 0, 1) \
            * np.clip(1 - (ax - 0.085) / 0.170, 0, 1)
        bot += (rgb(0xD99B92) - BOT)[None, None, :] * (g * 0.34)[..., None]
    # ---- the mouth, and the two nostrils above it ----
    # Held OFF the rim. The first pass ran the smile out to ax 0.13, which at the snout is the
    # edge of the animal, so it curled round the lip and printed a dark smear across the top of
    # the bill -- a mouth is on the underside of a ray and has no business being visible from
    # above. The lateral cut-off is now inside the widest the bill gets, and everything on this
    # surface is faded out as the skin turns upward as well, which is the same guard twice on
    # purpose: one in t and x, one in the direction the surface actually faces.
    under = np.clip((-sn - 0.12) / 0.40, 0, 1)
    # ...and it SMILES. The corners run toward the snout, not away from it: written the other way
    # the ends trail back toward the tail, and seen from underneath -- which is the only place
    # this is visible -- that is a frown. The sheet's three-quarter view is unambiguous about
    # which one this character has.
    mouth = 0.0855 - 0.0215 * np.clip(ax / 0.090, 0, 1) ** 2
    g = np.exp(-((t - mouth) / 0.0075) ** 2) * np.clip(1 - (ax - 0.078) / 0.040, 0, 1) * under
    bot += (rgb(0x6B5F62) - BOT)[None, None, :] * (g * 0.92)[..., None]
    g = np.exp(-(((t - 0.0545) / 0.0095) ** 2 + ((ax - 0.034) / 0.012) ** 2)) * under
    bot += (rgb(0x8B7E80) - BOT)[None, None, :] * (g * 0.75)[..., None]

    # ---- which of the two you are looking at ----
    # The break is at the rim, biased a hair onto the underside, because the sheet's bottom view
    # plainly shows the dark back wrapping the leading edge as a thin line all the way round.
    # Break at sin a = -0.22 rather than at the rim itself. The two readings this has to satisfy
    # at once are in the sheet: on the WING, where the section is 0.66 across and 0.025 thick,
    # -0.22 is 97% of the half-width and draws the thin dark line the bottom view shows wrapping
    # the leading edge; on the BODY, where the section is nearly round, the same number is a
    # quarter of the way down the flank and puts the boundary along the mid-line, which is where
    # the side view has it. Broken at the rim it satisfied the first and left the body almost
    # entirely white down the side.
    topness = np.clip((sn + 0.220) / 0.300, 0, 1)
    topness = topness * topness * (3 - 2 * topness)
    img = bot + (top - bot) * topness[..., None]

    # ---- the tail ----
    # A grey root, then the banding, then a whip that is dark on every view. The bands are laid
    # out in t, so they wrap the tail squarely however the loft has tapered underneath them.
    for (t0, t1, c) in ((1.045, 1.105, BANDL), (1.105, 1.170, BAND),
                        (1.170, 1.225, BANDL), (1.225, 1.300, BAND),
                        (1.300, 1.345, BANDL), (1.345, 1.430, BAND)):
        e = 0.010
        m = np.clip((t - t0) / e, 0, 1) * np.clip((t1 - t) / e, 0, 1)
        img = img + (c[None, None, :] - img) * m[..., None]
    m = np.clip((t - 1.430) / 0.05, 0, 1)
    img = img + (rgb(0x24262A)[None, None, :] - img) * m[..., None]
    m = np.clip((t - 1.000) / 0.045, 0, 1) * np.clip((1.045 - t) / 0.045, 0, 1)
    img = img + (rgb(0x8A929A)[None, None, :] - img) * (m * 0.5)[..., None]

    # ---- and the occlusion goes in ----
    # Multiplied rather than added, and only half way. At 0.78 against a floor of 0.38 the
    # darkest texel came out at 52% -- a bruise, not a shadow, and on the snout (where the
    # occlusion is real: a narrow bill in front of a head that swells behind it at thirty
    # degrees) it printed as a smudge over the top of the bill rather than as shading under it.
    # The sheet's animal is evenly lit smooth skin; the bake is here to seat the creases, not to
    # relight it.
    img = img * (1.0 - (1.0 - ao[..., None]) * 0.55)

    img = np.clip(img, 0, 1)
    out = Image.fromarray((img * 255).astype(np.uint8), "RGB")
    out = out.filter(ImageFilter.GaussianBlur(0.6))
    # ---- and it is flipped, because the two conventions disagree ----
    # Row 0 of a PIL image is the TOP; v=0 in a Blender UV is the BOTTOM. Painted straight, every
    # feature lands at 1-v: the first render came back with the tail's black-and-white banding
    # wrapped round the SNOUT and the dark tip of the whip capping the nose, which is a texture
    # upside down and not a model that is wrong.
    out = out.transpose(Image.FLIP_TOP_BOTTOM)
    return out.resize((TEX, TEX), Image.LANCZOS)


# ---------------------------------------------------------------- materials
def mat_textured(png):
    m = bpy.data.materials.new("ray")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png)
    nt.links.new(bsdf.inputs["Base Color"], tex.outputs["Color"])
    bsdf.inputs["Roughness"].default_value = 0.42
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.42
    return m


def mat_flat(name, col, rough, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*col, 1.0)
    b.inputs["Roughness"].default_value = rough
    if emit and "Emission Strength" in b.inputs:
        b.inputs["Emission Color"].default_value = (*col, 1.0)
        b.inputs["Emission Strength"].default_value = emit
    return m


# ---------------------------------------------------------------- go
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    body, ts, aa = build_body()
    dors = build_dorsal()
    eyes = []
    for sx in (-1, 1):
        eyes += eye_ball(sx)

    for o in (body, dors):
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
        bpy.ops.object.shade_smooth()
    for o in eyes:
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
        bpy.ops.object.shade_smooth()

    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"body {len(body.data.vertices)} verts  {tris} tris")

    print("baking occlusion ...")
    grid = bake_ao(body, [body, dors] + eyes, ts, aa)
    ao = ao_to_image(grid, BAKE, BAKE)
    print(f"  ao min {grid.min():.3f}  mean {grid.mean():.3f}")

    print("painting ...")
    png = os.path.join(os.path.dirname(os.path.abspath(OUT)), "_eagleray_tex.png")
    paint(ts, ao).save(png)
    print(f"  {png}  {os.path.getsize(png)/1024:.0f} KB")

    body.data.materials.append(mat_textured(png))
    dors.data.materials.append(body.data.materials[0])
    me = mat_flat("eye", (0.035, 0.038, 0.048), 0.18)
    mg = mat_flat("glint", (1.0, 1.0, 1.0), 0.06, emit=0.35)
    for o in eyes:
        o.data.materials.append(me if o["mat"] == "eye" else mg)

    bpy.ops.object.select_all(action="DESELECT")
    for o in [body, dors] + eyes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    ray = bpy.context.active_object
    ray.name = "EagleRay"

    bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True,
                              export_apply=True, export_materials="EXPORT",
                              export_image_format="AUTO", export_normals=True,
                              export_texcoords=True, export_tangents=False)
    if not os.environ.get("KEEPTEX"):
        os.remove(png)
    n = sum(len(p.vertices) - 2 for p in ray.data.polygons)
    print(f"wrote {OUT}  {os.path.getsize(OUT)/1024:.0f} KB  {n} tris  {TEX}px")


main()
