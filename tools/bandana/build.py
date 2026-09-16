"""The bandana: a folded square, wrapped and pleated by hand rather than dropped and hoped for.

A free cloth drop was the first approach and it does not survive contact with this shape. Twice:
once born inside its own collider, which a solver does not resolve but panics at; and once
started clear, where seventy frames were nowhere near enough for a 1500-vertex skirt with self
collision to settle -- it slid down the cone and hung off one side in a torn-looking spiral.

Draped cloth tied at the top is not chaotic anyway. It pleats, and the pleats radiate from the
tie and deepen as they fall, because the same material has to cover more circumference the
further down it goes. That is a formula, not a simulation, and writing it as one gives folds
that are even, symmetrical and tunable by looking -- none of which a drop gives you.

  A NECKERCHIEF IS A SQUARE FOLDED ON ITS DIAGONAL. The fold goes round the neck; the opposite
  corner hangs at the front. So the mesh is a triangle, and its UVs put the fold on the
  texture's diagonal -- which is what lands the printed BORDER on the two hanging edges, where
  a real bandana's border is, rather than somewhere down the middle of the drape.
"""
import bpy, bmesh, math, os
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
R_NECK  = 1.00                 # everything in neck radii; the game scales from a measured neck
WRAP    = math.radians(300)    # how far round, leaving the back for the knot
N, M    = 132, 66              # across the cloth, and down it
FLARE   = 0.30                 # how far it stands off by the time it reaches the point
DROP    = 2.30                 # how far the point hangs below the fold
PLEATS  = 7                    # folds down the drape
PL_AMP  = 0.115                # how deep they get
THICK   = 0.022

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


def drape(t, s):
    """t: 0 at the fold, 1 at the point.  s: -0.5..0.5 across.  Returns a world position."""
    span = WRAP * (1.0 - t) ** 1.06
    ang  = s * span
    # ---- the pleats ----
    # Amplitude grows from nothing at the tie, because a tied edge cannot pleat and the slack
    # has to go somewhere further down. Squared-ish so the top stays tight and the bottom opens.
    # Zero at BOTH ends and fullest in between. Zero at the tie because a tied edge is pulled
    # flat, and zero at the point because the point is a single corner of cloth and a corner
    # cannot pleat -- leaving amplitude on down there is what turned the tip into a fan of
    # spikes, adjacent vertices alternating across an angular span that had collapsed to
    # nothing. Peaks at t=0.57; the 2.05 puts that peak back at PL_AMP.
    amp  = PL_AMP * 2.05 * (t ** 0.6) * ((1.0 - t) ** 0.45)
    # ---- AND THE PLEATS MERGE AS THE CLOTH NARROWS ----
    # Fixed frequency across a row that is shrinking means the same seven folds crowd into ever
    # fewer vertices, and near the point there are five samples trying to carry seven cycles --
    # which is not a pleat, it is aliasing, and it rendered as a fan of spikes at the tip that
    # survived even after the amplitude was faded out. Real pleats merge as cloth narrows, so
    # the frequency falls with the width and the sampling never runs out.
    ripple = math.sin(s * PLEATS * (1.0 - 0.78 * t) * math.tau) * amp
    rad  = R_NECK + FLARE * (t ** 1.45) + ripple
    # a pleat is not only in and out -- the cloth also rides up a little where it folds forward
    z    = -DROP * (t ** 1.04) + ripple * 0.30 * (1.0 - t)
    # and the whole drape leans off the chest as it falls, rather than hugging a cone
    lean = 0.16 * (t ** 2.0)
    return Vector((math.sin(ang) * rad, -math.cos(ang) * rad - lean, z))


verts, uvs, rows = [], [], []
for j in range(M + 1):
    t = j / M
    cnt = max(1, N - int(round(N * t)))
    row = []
    for k in range(cnt + 1):
        s = (k / cnt - 0.5) if cnt else 0.0
        verts.append(drape(t, s))
        # fold corners at (0,0) and (1,1), hanging point at (1,0)
        a, b, c = Vector((0, 0)), Vector((1, 1)), Vector((1, 0))
        left, right = a.lerp(c, t), b.lerp(c, t)
        uvs.append(left.lerp(right, (k / cnt) if cnt else 0.0))
        row.append(len(verts) - 1)
    rows.append(row)

faces = []
for j in range(M):
    a, b = rows[j], rows[j + 1]
    ia = ib = 0
    while ia < len(a) - 1 or ib < len(b) - 1:
        if ib >= len(b) - 1:
            faces.append((a[ia], a[ia + 1], b[ib])); ia += 1
        elif ia >= len(a) - 1:
            faces.append((a[ia], b[ib + 1], b[ib])); ib += 1
        elif (ia + 1) / (len(a) - 1) <= (ib + 1) / (len(b) - 1):
            faces.append((a[ia], a[ia + 1], b[ib])); ia += 1
        else:
            faces.append((a[ia], b[ib + 1], b[ib])); ib += 1

me = bpy.data.meshes.new("Bandana")
me.from_pydata([tuple(v) for v in verts], [], faces)
me.update()
uvl = me.uv_layers.new(name="UVMap")
for poly in me.polygons:
    for li in poly.loop_indices:
        uvl.data[li].uv = uvs[me.loops[li].vertex_index]
ob = bpy.data.objects.new("Bandana", me)
sc.collection.objects.link(ob)
print(f"  drape: {len(me.vertices)} verts, {len(me.polygons)} faces", flush=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/bandana_draped.blend")
print("  saved", flush=True)
