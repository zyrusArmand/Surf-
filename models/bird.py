"""Give models/bird.glb a wingbeat that is actually a wingbeat.

    python3 models/bird.py models/bird.glb models/bird_rigged.glb

WHAT ARRIVES: an auto-rigged gull with 28 bones called Bone_000..Bone_027 in no meaningful
order, and ZERO animation clips. The game has been driving the beat itself by rolling every
wing bone about one axis, which is a wing HINGING -- a pair of flat paddles going up and down.
A bird does not do that, and no amount of tuning a single roll will make it.

WHAT A WINGBEAT ACTUALLY IS, and all four parts matter:

  1. FLAP. The humerus swings down and up about the bird's own forward axis. This is the part
     that was already there, and on its own it is a paddle.
  2. SWEEP. The wing also goes FORWARD on the downstroke and back on the upstroke, about the
     vertical. Without it the wingtip traces a line; with it, an ellipse -- which is the shape
     that reads as flying rather than as flapping.
  3. FOLD. The hand -- everything past the wrist -- tucks in on the upstroke and extends on the
     down. A bird cannot afford to push air upwards on the recovery, so it takes the area away.
     This is the single biggest reason the old beat looked wrong: a wing at full span going up
     is a bird trying to fly backwards.
  4. TWIST. The primaries pronate on the downstroke, leading edge down, which is what turns a
     downstroke into thrust instead of just lift; and supinate on the way up so air slips
     through the feathers.

And the body is not nailed to the sky: it rises through the downstroke and sinks on the
recovery, the tail pitches with it, and the head counter-rotates to stay level, because a bird
stabilises its gaze and a head that bobs with the body reads as a toy.

THE SKELETON IS MEASURED, NOT NAMED. Bone_017 means nothing and a re-export renumbers it, so
every bone is found by where it sits at rest: forward is -Y and up is +Z (the glTF importer
turns the file's +Z-forward into Blender's -Y), the two wings are the chains running out in
+/-X off the shoulder, and the neck and tail are the two ends of the spine. If the file is
re-rigged this still finds them.

AXES ARE CONVERTED PER BONE. Which local axis rolls a wing is a fact about the export, not
about birds, and every one of the 28 bones has its own. So each rotation is written as an axis
in ARMATURE space and converted into that bone's local space before it is keyed -- the same
trick the old JS did in world space, done once at bake time instead of every frame.

Two clips come out: `flap`, one full cycle over 24 frames at 24fps so the game can set its own
rate per bird, and `glide`, wings held out and barely breathing, because gulls spend most of
their time not flapping at all.
"""
import sys, math, os
import bpy
from mathutils import Vector, Quaternion, Matrix

SRC = sys.argv[1] if len(sys.argv) > 1 else 'models/bird.glb'
DST = sys.argv[2] if len(sys.argv) > 2 else 'models/bird_rigged.glb'
FPS = 24
CYCLE = 24            # frames in one beat, so the clip is exactly one second at 24fps
DOWN = 0.38           # share of the cycle spent on the downstroke -- the fast, working half

# ---- how far each part of the beat goes, in radians ----
# Every one of these is the angle the WINGTIP reaches, not the angle a bone turns: the shares
# down each chain are normalised to one, so what is written here is what the end of the wing
# actually does and the joints inboard take a graded part of the way there. The first cut of
# this swept the chain up to 2.4 and folded every hand bone by 0.55 each, which came to 2.8
# radians of fold: the wing shut like a fan and the tip ended up near the bird's own spine.
# Measured off the export -- tip travel and span swing both come out of check.py.
FLAP_UP, FLAP_DN = 0.78, -0.62      # tip, at the top and bottom of the stroke: about 80 degrees
SWEEP = 0.30                        # forward on the downstroke, back on the up
FOLD = 0.62                         # how far the hand tucks at the top of the upstroke
TWIST = 0.34                        # primaries pronating on the downstroke
GLIDE_DROP = -0.58                  # the rest pose has the wings held high; a glide is level
GLIDE_BREATHE = 0.07                # ...and still breathing, because a frozen wing is a prop
BODY_RISE = 0.035                   # of the bird's length, up through the downstroke
TAIL_PITCH = 0.10
HEAD_HOLD = 0.55                    # share of the body's pitch the head cancels


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(SRC))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    return arm


def anatomy(arm):
    """Find what each bone is from where it sits, not from its name."""
    B = {b.name: b for b in arm.data.bones}
    head = {n: (arm.matrix_world @ b.head_local) for n, b in B.items()}
    tail = {n: (arm.matrix_world @ b.tail_local) for n, b in B.items()}
    span = max(abs(head[n].x) for n in B) or 1.0

    # ---- A WING IS A CHAIN THAT REACHES, NOT JUST A BONE THAT IS OFF-CENTRE ----
    # First cut of this asked for every bone clear of the centreline, and got the LEGS: they are
    # paired and symmetric too, they sit a quarter of the span out, and they were being flapped.
    # A leg and a wing are told apart by how far the chain they belong to actually reaches. So
    # the off-centre bones are grouped into chains first -- walk up parents until the centreline
    # -- and only the chain that gets out past half the span is a wing.
    off = [n for n in B
           if abs(head[n].x) > span * 0.05 or abs(tail[n].x) > span * 0.05]

    def rootof(n):
        cur = B[n]
        while cur.parent is not None and cur.parent.name in off:
            cur = cur.parent
        return cur.name

    groups = {}
    for n in off:
        groups.setdefault(rootof(n), []).append(n)

    def wing(sgn):
        best, reach = None, 0.0
        for r, mem in groups.items():
            if (tail[r].x * sgn) <= 0 and (head[r].x * sgn) <= 0:
                continue
            far = max(abs(tail[m].x) for m in mem)
            if far > reach:
                best, reach = (r, mem), far
        if not best or reach < span * 0.5:
            return [], []
        r, mem = best
        # ordered by DEPTH from the shoulder, not by how far out a bone happens to sit: the
        # secondaries and the primaries fork at the wrist and interleave if sorted by x.
        depth = {}
        for m in mem:
            d, cur = 0, B[m]
            while cur.name != r:
                cur = cur.parent
                d += 1
            depth[m] = d
        order = sorted(mem, key=lambda m: (depth[m], abs(tail[m].x)))
        # the wrist is the first bone in the chain with more than one child inside it; the HAND
        # is everything descended from it -- that is the part a bird folds away on the upstroke.
        kids = {}
        for m in mem:
            pn = B[m].parent.name if B[m].parent else None
            if pn in mem or pn == r:
                kids.setdefault(pn, []).append(m)
        wrist = next((m for m in order if len(kids.get(m, [])) > 1), None)
        if wrist is None:
            return order, order[len(order) // 2:]
        hand, stack = [], list(kids.get(wrist, []))
        while stack:
            m = stack.pop()
            hand.append(m)
            stack.extend(kids.get(m, []))
        return order, hand

    L, handL = wing(-1)
    R, handR = wing(1)
    used = set(L) | set(R)
    # everything paired and off-centre that is NOT a wing is a leg, and a leg does not flap
    legs = [n for n in off if n not in used]

    # the spine is what is left on the centreline; its two ends are the head and the tail.
    # Forward is -Y, so the most forward is the head and the most rearward the tail.
    mid = [n for n in B if n not in off]
    mid.sort(key=lambda n: tail[n].y)
    neck = mid[:2]
    tailb = [mid[-1]]
    body = [n for n in mid if n not in neck and n not in tailb]

    return {'L': L, 'R': R, 'handL': handL, 'handR': handR, 'legs': legs,
            'neck': neck, 'tail': tailb, 'body': body, 'span': span,
            'head': head, 'tailp': tail}


def local_axis(pb, axis_arm):
    """An axis given in ARMATURE space, expressed in this bone's local space."""
    m = pb.bone.matrix_local.to_3x3()
    try:
        return (m.inverted() @ Vector(axis_arm)).normalized()
    except ValueError:
        return Vector((0.0, 0.0, 1.0))


def eased(w, dw):
    """One beat: cosine in and out of each half, and the two halves are not equal lengths.
       0 at the top of the stroke, 1 at the bottom, back to 0."""
    if w < dw:
        return 0.5 - 0.5 * math.cos(math.pi * (w / dw))
    return 0.5 + 0.5 * math.cos(math.pi * ((w - dw) / (1 - dw)))


def build(arm, A, name, beats=True):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'

    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act

    FWD = Vector((0.0, -1.0, 0.0))      # the way the bird points
    UP = Vector((0.0, 0.0, 1.0))

    # share of the flap each bone takes, shoulder to tip. A wing does not hinge at one joint:
    # the turn is spread down the chain so the tip travels furthest and lags.
    def shares(chain):
        n = len(chain)
        if n == 0:
            return {}
        raw = {b: 0.35 + 0.65 * (i / max(1, n - 1)) for i, b in enumerate(chain)}
        t = sum(raw.values()) or 1.0
        # Normalised to ONE. Each bone inherits its parent's turn, so the shares add up down the
        # chain and the tip reaches exactly the angle asked for -- while the inboard joints take
        # a graded part of it, which is what makes the wing whip rather than hinge.
        return {b: v / t for b, v in raw.items()}

    shL, shR = shares(A['L']), shares(A['R'])

    def hshares(hand):
        """The fold belongs mostly at the WRIST -- that is the joint a bird folds -- and tapers
           out through the primaries. Normalised, so FOLD is the angle the hand actually makes
           with the forearm and not the sum of six bones each turning by it."""
        n = len(hand)
        if n == 0:
            return {}
        raw = {b: 1.0 - 0.6 * (i / max(1, n - 1)) for i, b in enumerate(hand)}
        t = sum(raw.values()) or 1.0
        return {b: v / t for b, v in raw.items()}

    fdL, fdR = hshares(A['handL']), hshares(A['handR'])

    for f in range(CYCLE + 1):
        w = (f % CYCLE) / CYCLE
        e = eased(w, DOWN) if beats else 0.0
        # 0 at the top, 1 at the bottom of the stroke
        flap = (FLAP_UP + (FLAP_DN - FLAP_UP) * e) if beats \
            else (GLIDE_DROP + GLIDE_BREATHE * math.sin(2 * math.pi * w))
        # sweep leads the flap by a quarter cycle: furthest forward mid-downstroke
        ph = math.sin(2 * math.pi * w)
        sweep = -SWEEP * ph if beats else 0.0
        # the hand is tucked at the TOP and extended at the bottom
        # tucked at the TOP of the stroke, extended at the bottom; a glide holds a little bend
        fold = (FOLD * (1.0 - e)) if beats else 0.10
        twist = TWIST * (2 * e - 1) if beats else 0.0

        for chain, sh, sgn, hand, fd in ((A['L'], shL, -1.0, A['handL'], fdL),
                                         (A['R'], shR, 1.0, A['handR'], fdR)):
            for b in chain:
                pb = arm.pose.bones[b]
                k = sh.get(b, 0.0)
                q = Quaternion(local_axis(pb, FWD), flap * k * sgn)
                q = q @ Quaternion(local_axis(pb, UP), sweep * k * sgn)
                if b in hand:
                    kf = fd.get(b, 0.0)
                    q = q @ Quaternion(local_axis(pb, UP), fold * kf * sgn)
                    q = q @ Quaternion(local_axis(pb, Vector((sgn, 0.0, 0.0))), twist * kf)
                pb.rotation_quaternion = q
                pb.keyframe_insert('rotation_quaternion', frame=f + 1)

        # the body rides the beat, and the head refuses to
        rise = BODY_RISE * A['span'] * (-math.cos(2 * math.pi * w)) if beats else 0.0
        pitch = TAIL_PITCH * ph if beats else 0.0
        for b in A['body']:
            pb = arm.pose.bones[b]
            pb.location = (0.0, 0.0, rise) if b == A['body'][0] else (0.0, 0.0, 0.0)
            pb.rotation_quaternion = Quaternion(local_axis(pb, Vector((1.0, 0.0, 0.0))),
                                                pitch * 0.35)
            pb.keyframe_insert('rotation_quaternion', frame=f + 1)
            pb.keyframe_insert('location', frame=f + 1)
        for b in A['tail']:
            pb = arm.pose.bones[b]
            pb.rotation_quaternion = Quaternion(local_axis(pb, Vector((1.0, 0.0, 0.0))),
                                                -pitch)
            pb.keyframe_insert('rotation_quaternion', frame=f + 1)
        for i, b in enumerate(A['neck']):
            pb = arm.pose.bones[b]
            pb.rotation_quaternion = Quaternion(local_axis(pb, Vector((1.0, 0.0, 0.0))),
                                                pitch * HEAD_HOLD * (1.0 if i == 0 else 0.6))
            pb.keyframe_insert('rotation_quaternion', frame=f + 1)

    # bpy 5.0: action.fcurves is gone -- the curves live under layers/strips/channelbags
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = 'BEZIER'
    bpy.ops.object.mode_set(mode='OBJECT')
    return act


def main():
    arm = load()
    A = anatomy(arm)
    print('span %.3f' % A['span'])
    print('wing L  ', A['L'])
    print('  hand  ', A['handL'])
    print('wing R  ', A['R'])
    print('  hand  ', A['handR'])
    print('legs    ', A['legs'])
    print('neck    ', A['neck'], ' tail ', A['tail'], ' body ', A['body'])

    bpy.context.scene.render.fps = FPS
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = CYCLE + 1

    acts = [build(arm, A, 'flap', True), build(arm, A, 'glide', False)]
    # both actions have to be exported, so each gets its own NLA track
    arm.animation_data.action = None
    for a in acts:
        tr = arm.animation_data.nla_tracks.new()
        tr.name = a.name
        tr.strips.new(a.name, 1, a)

    bpy.ops.export_scene.gltf(filepath=os.path.abspath(DST), export_format='GLB',
                              export_animations=True, export_animation_mode='ACTIONS',
                              export_bake_animation=True, export_apply=False,
                              export_yup=True)
    print('wrote', DST, os.path.getsize(DST) // 1024, 'KB')


main()
