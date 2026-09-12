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

ONE WAVE, NOT FOUR MOTIONS. The first rig that came out of this file had all four parts, and
still read wrong, because each part was authored on its own clock: the flap on a two-piece
asymmetric ease, the elbow and wrist on `1 - flap` (which is the flap, so those joints snapped
open and shut in lockstep with it), and the curl on the ease's derivative, which has corners
where the two pieces join and so put kinks in the feathers. Four sub-motions sharing one axis
do not add up to a wingbeat; they add up to a mechanism.

The stingray in this same repo reads right, and it is far simpler: ONE continuous sine
travelling outboard, amplitude growing along the chain and phase lagging along it --
`angle_i(t) = AMP[i] * sin(wt - i*LAG)` -- and nothing else. That is what this file does now.
Every part of the beat above is a term on that ONE wave: the flap is the wave, the sweep is the
wave a quarter-cycle over, the hinges and the fold are the wave a little later still (a joint
trails the segment that drives it), and the curl is the wave's own derivative -- which, a sine
being a sine, is a cosine, and therefore smooth everywhere. Same four motions, one clock.

Two clips come out: `flap`, one full cycle over 24 frames at 24fps so the game can set its own
rate per bird, and `glide`, wings held out and barely breathing, because gulls spend most of
their time not flapping at all.
"""
import sys, math, os
import bpy
from mathutils import Vector, Quaternion, Matrix

def _n(name, dflt):
    """Every tuning number is overridable from the environment, so the amplitudes can be swept
       against the measurements in check.py instead of edited and eyeballed one at a time."""
    try:
        return float(os.environ[name])
    except (KeyError, ValueError):
        return dflt

SRC = sys.argv[1] if len(sys.argv) > 1 else 'models/bird.glb'
DST = sys.argv[2] if len(sys.argv) > 2 else 'models/bird_rigged.glb'
FPS = 24
CYCLE = 24            # frames in one beat, so the clip is exactly one second at 24fps

# ---- how far each part of the beat goes, in radians ----
# Every one of these is the angle the WINGTIP reaches, not the angle a bone turns: the shares
# down each chain are normalised to one, so what is written here is what the end of the wing
# actually does and the joints inboard take a graded part of the way there. The first cut of
# this swept the chain up to 2.4 and folded every hand bone by 0.55 each, which came to 2.8
# radians of fold: the wing shut like a fan and the tip ended up near the bird's own spine.
# Measured off the export -- tip travel and span swing both come out of check.py.
FLAP_UP, FLAP_DN = _n('FLAP_UP', 1.00), _n('FLAP_DN', -0.80)      # tip, at the top and bottom of the stroke
SWEEP = _n('SWEEP', 0.14)                        # forward on the downstroke, back on the up
# FOLD is small now and ELBOW/WRIST carry the folding, which is the anatomy: a bird folds at two
# joints, it does not shrink its hand evenly. Swept against the measurements -- at FOLD 0.30 and
# the hinges soft, the wing shortened as much but the inner-vs-outer angle only varied 28 degrees
# instead of 32, and that angle IS the bend you can see.
FOLD = _n('FOLD', 0.20)                         # how far the hand tucks at the top of the upstroke
TWIST = _n('TWIST', 0.34)                        # primaries pronating on the downstroke
# ---- AND THE THING THAT MAKES IT JOINTED: THE WAVE ARRIVES LATE OUTBOARD ----
# Every bone runs the SAME sine, sampled further back in the cycle the further out it sits. That
# one substitution is what turns a rotating plane into a wing: at any instant the inner wing is
# already coming down while the hand is still going up, so the wing is bent into an S the whole
# time instead of being flat the whole time. It is the stingray's `- i*LAG` and it is the only
# reason either animal reads as alive.
# 0.062 and not more: at 0.090 the bend was the same 32 degrees but the tip's vertical travel
# fell from 41% of the span to 31%, because a big enough lag smears the extremes of the stroke
# away. The bend is free up to about here and paid for after it.
LAG = _n('LAG', 0.062)                         # of a cycle, per joint out from the shoulder
# The two real hinges, flexing in the plane of the wing. An elbow and a wrist do not just pass
# the flap along; they open on the downstroke and close on the recovery, which is what shortens
# the wing and puts the kink in its leading edge.
ELBOW = _n('ELBOW', 0.40)
WRIST = _n('WRIST', 0.52)
# ...but they do it LATE. A joint is driven by the segment inboard of it, so it reaches its
# extreme after that segment does -- which is the same statement as LAG, applied to the hinge
# instead of to the next bone. Driving the hinges off `1 - flap` instead, as the first cut did,
# is driving them off the flap itself: they opened and shut at the exact instant the wing
# reached the top and bottom of its stroke, which is a pair of scissors, not an elbow.
# Small, and kept small on the measurements' say-so rather than the theory's. The hinges flex
# about the VERTICAL, which is the same axis the sweep uses, so phase put on them comes straight
# out of the tip's fore-aft travel -- the ellipse. Swept 0 to 0.14: every step costs both the
# fore-aft (0.63 down to 0.40) and the bend (34 degrees of variation down to 19), and buys
# nothing either number can see. 0.05 keeps 85% of the ellipse and nearly all the bend for a
# trail that is real but does not dominate.
HINGE = _n('HINGE', 0.05)                      # of a cycle, behind the flap that drives it
# ...and the hand itself CURLS. A primary feather is not a rod: the tip trails the load, so it
# bends up at the bottom of the downstroke and down at the top of the recovery. Driven off the
# wave's own derivative -- the load follows the stroke's SPEED -- which for a sine is a cosine,
# so the curl is smooth. The old ease's derivative had a corner where its two halves met, and
# that corner was a visible kink passing out along the feathers once a beat.
# Raised from 0.40 because a cosine peaks at 1 where that ease's derivative peaked at 4.1, so
# the same constant was buying less than half the curl it used to. 0.55 is still well under the
# 0.83 radians the old one actually reached, and it earns its keep on the measurements: it puts
# the bend variation back to 29 degrees and the ellipse to 0.565 at a 2% cost in tip travel.
CURL = _n('CURL', 0.55)
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
        depths.update(depth)
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

    depths = {}
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

    return {'L': L, 'R': R, 'handL': handL, 'handR': handR, 'legs': legs, 'depth': depths,
            'neck': neck, 'tail': tailb, 'body': body, 'span': span,
            'head': head, 'tailp': tail}


def local_axis(pb, axis_arm):
    """An axis given in ARMATURE space, expressed in this bone's local space."""
    m = pb.bone.matrix_local.to_3x3()
    try:
        return (m.inverted() @ Vector(axis_arm)).normalized()
    except ValueError:
        return Vector((0.0, 0.0, 1.0))


TAU = 2 * math.pi

# The beat, and there is only one of it. `wave(w)` is +1 at the top of the stroke and -1 at the
# bottom; `rate(w)` is how fast the stroke is going, positive on the way down, and it is (bar a
# constant) the derivative of `wave`. Everything the wing does is one of these two sampled at
# some phase, which is the whole point: sub-motions on separate clocks are what read as a
# mechanism, and a single wave read at four offsets reads as one thing moving.
def wave(w):
    return math.cos(TAU * w)


def rate(w):
    return math.sin(TAU * w)


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

    DEP = A['depth']
    maxd = max([DEP.get(b, 0) for b in (A['L'] + A['R'])] or [1]) or 1

    # share of the flap each bone takes, shoulder to tip. A wing does not hinge at one joint:
    # the turn is spread down the chain so the tip travels furthest.
    def shares(chain):
        n = len(chain)
        if n == 0:
            return {}
        raw = {b: 0.35 + 0.65 * (i / max(1, n - 1)) for i, b in enumerate(chain)}
        t = sum(raw.values()) or 1.0
        # Normalised to ONE. Each bone inherits its parent's turn, so the shares add up down the
        # chain and the tip reaches exactly the angle asked for -- while the inboard joints take
        # a graded part of it.
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

    FLAP_MID = 0.5 * (FLAP_UP + FLAP_DN)
    FLAP_AMP = 0.5 * (FLAP_UP - FLAP_DN)

    for f in range(CYCLE + 1):
        w = (f % CYCLE) / CYCLE
        ph = math.sin(2 * math.pi * w)

        for chain, sh, sgn, hand, fd in ((A['L'], shL, -1.0, A['handL'], fdL),
                                         (A['R'], shR, 1.0, A['handR'], fdR)):
            for b in chain:
                pb = arm.pose.bones[b]
                k = sh.get(b, 0.0)
                d = DEP.get(b, 0)
                # ---- EACH JOINT IS A LITTLE LATER THAN THE ONE INSIDE IT ----
                # This is the line that turns a rotating plane into a wing, and it is the whole
                # of the stingray's trick: ONE wave, read further back in the cycle the further
                # out the bone sits, so the wing is bent at every instant instead of flat at
                # every instant. The four terms below are all this same wave -- read here, a
                # quarter-turn on for the sweep, HINGE later for the joints that it drives, and
                # differentiated for the curl -- so nothing on the wing has a clock of its own.
                if beats:
                    wl = w - LAG * d
                    s = wave(wl)                       # +1 top of stroke, -1 bottom
                    flap = FLAP_MID + FLAP_AMP * s
                else:
                    wl, s = w, 0.0
                    flap = GLIDE_DROP + GLIDE_BREATHE * math.sin(2 * math.pi * w)
                q = Quaternion(local_axis(pb, FWD), flap * k * sgn)
                # forward on the downstroke, back on the up: the same wave a quarter-cycle on,
                # which is what turns the tip's path from a line into an ellipse.
                sweep = (-SWEEP * rate(wl)) if beats else 0.0
                q = q @ Quaternion(local_axis(pb, UP), sweep * k * sgn)
                # the two real hinges: the elbow is the second bone out, the wrist the third.
                # Both open through the downstroke and close on the recovery -- late, trailing
                # the segment that drives them. `0.5 + 0.5*wave` is 1 at the top of the stroke
                # and 0 at the bottom, so this is the old flexion with a phase on it.
                if beats and d in (1, 2):
                    shut = 0.5 + 0.5 * wave(wl - HINGE)
                    q = q @ Quaternion(local_axis(pb, UP),
                                       (ELBOW if d == 1 else WRIST) * shut * sgn)
                if b in hand:
                    kf = fd.get(b, 0.0)
                    if beats:
                        # the hand folds late too -- the wrist leads it -- and the primaries
                        # pronate in phase with the flap, which is where the thrust comes from.
                        fold = FOLD * (0.5 + 0.5 * wave(wl - HINGE))
                        twist = -TWIST * s
                    else:
                        fold, twist = 0.10, 0.0
                    q = q @ Quaternion(local_axis(pb, UP), fold * kf * sgn)
                    q = q @ Quaternion(local_axis(pb, Vector((sgn, 0.0, 0.0))), twist * kf)
                    # and the feathers CURL against the stroke: `rate` is the stroke's own speed,
                    # positive going down, and a loaded feather bends the other way. A sine's
                    # derivative is a cosine, so this is smooth all the way round -- the old
                    # two-piece ease put a corner here once a beat.
                    if beats:
                        q = q @ Quaternion(local_axis(pb, FWD),
                                           CURL * rate(wl) * kf * sgn)
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
