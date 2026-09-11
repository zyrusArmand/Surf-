"""
RIG THE STINGRAY SCAN so it swims.

    python3 models/scan.py <stingray_MAX>.glb /tmp/sting_raw.glb 24000 1024
    python3 models/stingray.py /tmp/sting_raw.glb models/stingray.glb

The scan arrives as one static posed sculpt -- no armature, no animation, 1.34M triangles. scan.py
takes it to 24k and shrinks the maps; this puts a skeleton in it and bakes the swim cycles, so the
.glb the game loads carries its own movement and needs nothing written in index.html to animate.

WHAT A RAY ACTUALLY DOES
It does not flap like a bird. The wing carries a TRAVELLING WAVE: the stroke starts at the body
and rolls outboard, so at any instant the inner wing is already coming up while the tip is still
going down, and the tip -- having the whole chain's rotation stacked on it -- sweeps furthest.
That is the entire read, and it falls out of a chain of four bones per wing given two things:
amplitude that grows outboard, and a phase that LAGS outboard. Everything else here is bookkeeping.

    angle_i(t) = AMP[i] * (sin(wt - i*LAG) - BIAS)

The bias is there because the sculpt was posed mid-upstroke -- its tips sit 0.27 above the body at
rest -- so a cycle centred on the rest pose would flap from "up" to "further up". Biased down, rest
becomes the top of the stroke and the animal swims from it.

ROLL IS THE WHOLE TRICK WITH THE BONES
A bone's local Y runs head to tail; the roll decides the other two. Aligned so local Z is world up,
a spanwise bone gets local X along the fore-and-aft axis -- so ONE Euler term, rotation_euler.x, is
the flap, on every wing bone, on both sides. Without that the flap is a different mixture of two
axes per bone and is impossible to reason about.

WEIGHTS ARE COMPUTED, NOT HEATED
Automatic weights on a decimated photogrammetry surface (8,001 non-manifold edges) is a coin toss,
and there is no need to gamble: the anatomy is known. Span position picks the wing bones, length
position picks the tail bones, and the two are blended by how far out from the spine a vertex sits.
Hat functions over the bone midpoints, so the weights sum to one everywhere by construction.
"""
import bpy, sys, os, math
import numpy as np
from mathutils import Vector

SRC = sys.argv[1]
OUT = sys.argv[2] if len(sys.argv) > 2 else "models/stingray.glb"

# ---------------------------------------------------------------- anatomy
# Measured off the mesh rather than assumed -- sliced along its length, the nose end carries a
# 0.12 half-width and the far end tapers to 0.009, which is which. In Blender: x is the span,
# y runs nose (negative) to tail (positive), z is up.
NOSE_Y, TAIL_Y = -0.951, 0.953
DISC_BACK      = 0.040           # where the disc stops being a disc and becomes a whip
WING_Y         = -0.330          # the station where the wing is widest
WING_ROOT_X    = 0.160           # where the wing leaves the body
WING_TIP_X     = 0.868
NWING, NTAIL   = 4, 5

FPS, FRAMES = 24, 48             # one cycle, two seconds -- an eagle ray's cruising beat

# amplitude grows outboard and the phase lags outboard: that is the travelling wave.
# ---- and it is HALF what the first pass used ----
# Measured in the viewer, [0.30,0.40,0.46,0.50] at a lag of 0.62 swept the tips through 1.00 of a
# 1.73 span -- 58% -- and at the top of the stroke the accumulated chain rotation curled them past
# vertical into a hook. That is a ray folding in half, not one swimming. A cruising eagle ray
# works through about a third of its span, and the shorter lag keeps the wing an ARC rather than
# letting the tip roll over the shoulder ahead of it.
AMP  = [0.200, 0.255, 0.290, 0.310]
LAG  = 0.44
BIAS = 0.45                      # ...and the stroke hangs below the posed rest, see the header
TAIL_AMP  = [0.055, 0.085, 0.110, 0.130, 0.150]
TAIL_LAG  = 0.55
TAIL_SIDE = 0.45                 # of the vertical sway, how much goes sideways


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    ob = [o for o in bpy.data.objects if o.type == "MESH"][0]
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = "stingray"
    return ob


def wing_line(ob):
    """Where the wing actually IS at each span station -- its chord centre and its height.

    A swept wing's middle moves aft as you go out, and this one's tips are lifted, so bones laid
    on a straight line at y=WING_Y and z=0 would leave the outer two sitting outside the mesh
    they are meant to deform. Sampled from the vertices instead: at each station, the mean of the
    slab around it."""
    me = ob.data; n = len(me.vertices)
    co = np.empty(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(n, 3)
    xs = np.linspace(WING_ROOT_X, WING_TIP_X, NWING + 1)
    pts = []
    for x in xs:
        m = (np.abs(co[:, 0]) > x - 0.055) & (np.abs(co[:, 0]) < x + 0.055) & (co[:, 1] < DISC_BACK)
        if m.sum() < 6:
            m = (np.abs(co[:, 0]) > x - 0.12) & (np.abs(co[:, 0]) < x + 0.12) & (co[:, 1] < DISC_BACK)
        s = co[m]
        pts.append((float(x), float(s[:, 1].mean()), float(s[:, 2].mean())))
    return pts, co


def tail_line(co):
    ys = np.linspace(DISC_BACK, TAIL_Y, NTAIL + 1)
    pts = []
    for y in ys:
        m = (co[:, 1] > y - 0.09) & (co[:, 1] < y + 0.09) & (np.abs(co[:, 0]) < 0.10)
        s = co[m] if m.sum() > 4 else co[(co[:, 1] > y - 0.20) & (co[:, 1] < y + 0.20)]
        pts.append((0.0, float(y), float(s[:, 2].mean())))
    return pts


def build_arm(ob, wing, tail):
    arm = bpy.data.armatures.new("stingrayArm")
    ao = bpy.data.objects.new("stingrayArm", arm)
    bpy.context.collection.objects.link(ao)
    bpy.context.view_layer.objects.active = ao
    bpy.ops.object.select_all(action="DESELECT"); ao.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.edit_bones

    body = eb.new("body")
    body.head = Vector((0.0, NOSE_Y + 0.12, 0.0))
    body.tail = Vector((0.0, DISC_BACK, tail[0][2]))
    body.align_roll(Vector((0, 0, 1)))

    made = {"body": body}
    for side, sgn in (("L", -1.0), ("R", 1.0)):
        parent = body
        for i in range(NWING):
            b = eb.new(f"wing.{side}.{i+1}")
            hx, hy, hz = wing[i]
            tx, ty, tz = wing[i + 1]
            b.head = Vector((sgn * hx, hy, hz))
            b.tail = Vector((sgn * tx, ty, tz))
            # ...and this is the line that makes rotation_euler.x mean "flap", everywhere
            b.align_roll(Vector((0, 0, 1)))
            b.parent = parent
            b.use_connect = (i > 0)
            made[b.name] = b
            parent = b
    parent = body
    for i in range(NTAIL):
        b = eb.new(f"tail.{i+1}")
        b.head = Vector(tail[i]); b.tail = Vector(tail[i + 1])
        b.align_roll(Vector((0, 0, 1)))
        b.parent = parent
        b.use_connect = (i > 0)
        made[b.name] = b
        parent = b
    bpy.ops.object.mode_set(mode="OBJECT")
    return ao


def hats(p, nodes):
    """Partition of unity over a set of node positions: a vertex between two nodes splits between
    them in proportion, outside the ends it belongs wholly to the end. Weights sum to 1 by
    construction, which is the point -- normalising afterwards hides mistakes."""
    w = np.zeros(len(nodes))
    if p <= nodes[0]:
        w[0] = 1.0
    elif p >= nodes[-1]:
        w[-1] = 1.0
    else:
        for i in range(len(nodes) - 1):
            if nodes[i] <= p <= nodes[i + 1]:
                s = (p - nodes[i]) / max(1e-9, nodes[i + 1] - nodes[i])
                w[i] = 1 - s; w[i + 1] = s
                break
    return w


def skin(ob, ao, wing, tail, co):
    names = ["body"] + [f"tail.{i+1}" for i in range(NTAIL)]
    for side in ("L", "R"):
        names += [f"wing.{side}.{i+1}" for i in range(NWING)]
    vg = {n: ob.vertex_groups.new(name=n) for n in names}

    wnodes = [ (wing[i][0] + wing[i+1][0]) * 0.5 for i in range(NWING) ]
    tnodes = [NOSE_Y * 0.5] + [ (tail[i][1] + tail[i+1][1]) * 0.5 for i in range(NTAIL) ]

    for vi, (x, y, z) in enumerate(co):
        ax = abs(float(x))
        # how much of this vertex is WING rather than body-and-tail. The tail is 0.01 across and
        # the body 0.16, so this is only ever ambiguous in the shoulder.
        k = (ax - WING_ROOT_X * 0.88) / (0.255 - WING_ROOT_X * 0.88)
        k = min(1.0, max(0.0, k)); k = k * k * (3 - 2 * k)
        if float(y) > DISC_BACK:
            k = 0.0                                   # nothing on the whip is wing
        if k > 1e-4:
            ww = hats(ax, wnodes) * k
            side = "L" if x < 0 else "R"
            for i, v in enumerate(ww):
                if v > 1e-4:
                    vg[f"wing.{side}.{i+1}"].add([vi], float(v), "REPLACE")
        if k < 1 - 1e-4:
            tw = hats(float(y), tnodes) * (1.0 - k)
            for i, v in enumerate(tw):
                if v > 1e-4:
                    vg[names[i]].add([vi], float(v), "REPLACE")

    m = ob.modifiers.new("Armature", "ARMATURE")
    m.object = ao
    ob.parent = ao


def clip(ao, name, scale):
    """One loop of the cycle, keyed every other frame and left for the exporter to sample."""
    ao.animation_data_clear()
    ad = ao.animation_data_create()
    act = bpy.data.actions.new(name)
    ad.action = act
    # Blender 4.4+ puts keys in a SLOT; assigning the action is not enough on its own
    try:
        if hasattr(ad, "action_slot") and ad.action_slot is None:
            ad.action_slot = act.slots.new(id_type="OBJECT", name=ao.name) \
                if not len(act.slots) else act.slots[0]
    except Exception:
        pass

    for pb in ao.pose.bones:
        pb.rotation_mode = "XYZ"

    for f in range(1, FRAMES + 1):
        th = 2.0 * math.pi * (f - 1) / FRAMES
        bpy.context.scene.frame_set(f)
        for side, sgn in (("L", 1.0), ("R", 1.0)):
            for i in range(NWING):
                pb = ao.pose.bones[f"wing.{side}.{i+1}"]
                a = AMP[i] * scale * (math.sin(th - i * LAG) - BIAS)
                pb.rotation_euler = (a * sgn, 0.0, 0.0)
                pb.keyframe_insert("rotation_euler", frame=f)
        for i in range(NTAIL):
            pb = ao.pose.bones[f"tail.{i+1}"]
            a = TAIL_AMP[i] * scale * math.sin(th - (i + 1) * TAIL_LAG)
            s = TAIL_AMP[i] * scale * TAIL_SIDE * math.sin(th - (i + 1) * TAIL_LAG - 1.1)
            pb.rotation_euler = (a, 0.0, s)
            pb.keyframe_insert("rotation_euler", frame=f)
        pb = ao.pose.bones["body"]
        pb.rotation_euler = (0.035 * scale * math.sin(th + 0.9), 0.0, 0.0)
        pb.keyframe_insert("rotation_euler", frame=f)

    # ---- and the curves are not on the action any more ----
    # Blender 4.4 moved f-curves into the action's layers/strips/channelbags so that one action
    # can drive several things through slots; action.fcurves is simply gone in 5.0. Bezier is
    # already the default for inserted keys, so this only has to not fall over -- both shapes are
    # walked so the script survives either side of that change.
    for fc in _curves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
    return act


def _curves(act):
    if hasattr(act, "fcurves"):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


def main():
    ob = load()
    wing, co = wing_line(ob)
    tail = tail_line(co)
    print("wing stations", [tuple(round(v, 3) for v in p) for p in wing])
    print("tail stations", [tuple(round(v, 3) for v in p) for p in tail])

    ao = build_arm(ob, wing, tail)
    skin(ob, ao, wing, tail, co)
    print("bones", len(ao.data.bones), "groups", len(ob.vertex_groups))

    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.frame_start, sc.frame_end = 1, FRAMES

    acts = [clip(ao, "swim", 1.0)]
    # ...and a second, quieter one, so the game can cross-fade by speed rather than only change
    # the rate: a ray coasting still moves, it just stops driving.
    acts.append(clip(ao, "glide", 0.34))
    for a in acts:
        a.use_fake_user = True

    # leave the file on the cycle it should ship on
    ao.animation_data.action = acts[0]
    sc.frame_set(1)

    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); ao.select_set(True)
    bpy.context.view_layer.objects.active = ao
    bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True,
                              use_selection=True, export_apply=False,
                              export_skins=True, export_animations=True,
                              export_animation_mode="ACTIONS",
                              export_bake_animation=True,
                              export_materials="EXPORT", export_image_format="AUTO",
                              export_normals=True, export_texcoords=True)
    n = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print(f"wrote {OUT}  {os.path.getsize(OUT)/1024:.0f} KB  {n} tris  "
          f"{len(ao.data.bones)} bones  clips {[a.name for a in acts]}")


main()
