"""Authored garments for the pug, skinned to its own armature.

Why this exists. Two rounds of cutting clothes out of the pug's body mesh -- offset the
selected triangles, weld, add rims -- produced a pug-shaped skin with a pattern on it. The
body is a barrel with fur-noise geometry and no gap between arm and torso, so anything cut
from it is a barrel with the same noise and the same lack of gap. Real cloth is SMOOTH and it
BRIDGES concavities; a hem is a circle, a collar is a band that stands off the neck, a sleeve
is a tube. None of that can come from the body's own triangles.

So the garments are built here from clean tubes with their own parametric UVs, wrapped onto the
body from the inside out (shrinkwrap OUTSIDE: every vertex is pushed to the surface plus an
offset, none is pulled in), then smoothed so the cloth spans the hollows the way fabric does,
wrapped once more so the smoothing cannot push it back into the fur, given thickness with a
rim in the trim material, and skinned by transferring the body's own vertex weights. The game
loads the result, swaps the skin indices onto the pug's live skeleton, and paints the same
canvas textures onto them it already had.

Coordinates are Blender's after glTF import (z up, the pug faces -y). Landmarks come from the
armature's bone heads, which are what the weights are relative to.

usage: python3 garments.py <pug.glb> <out.glb>
"""
import bpy, bmesh, sys, math
from mathutils import Vector

SRC, DST = sys.argv[1], sys.argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
ARM = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
BODY = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
# The importer leaves the armature POSED (the node transforms), and a shrinkwrap targets the
# evaluated mesh -- so the first build wrapped the cloth onto a pose the game never binds
# against, and every garment came out inside the body. The garments must be built against the
# bind pose, which is the rest pose, which is the raw mesh data.
ARM.data.pose_position = 'REST'
bpy.context.view_layer.update()
for o in (ARM, BODY):
    o.select_set(True)
bpy.context.view_layer.objects.active = BODY
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def bone(n):
    b = ARM.data.bones[n]
    return ARM.matrix_world @ b.head_local


L = {n: bone(n) for n in ARM.data.bones.keys()}
_lo = Vector((min(v.co.x for v in BODY.data.vertices), min(v.co.y for v in BODY.data.vertices), min(v.co.z for v in BODY.data.vertices)))
_hi = Vector((max(v.co.x for v in BODY.data.vertices), max(v.co.y for v in BODY.data.vertices), max(v.co.z for v in BODY.data.vertices)))
print("body raw bbox", [round(x, 3) for x in _lo], [round(x, 3) for x in _hi], "body matrix", [list(map(lambda x: round(x, 3), r)) for r in BODY.matrix_world], flush=True)
print("landmarks:", {k: [round(x, 3) for x in v] for k, v in L.items() if k in
      ('Hips', 'Spine', 'LeftShoulder', 'LeftArm', 'LeftForeArm', 'LeftHand', 'neck', 'Head',
       'LeftUpLeg', 'LeftLeg', 'LeftFoot')}, flush=True)

MAT = {}
for name, col in (('cloth', (0.8, 0.8, 0.8, 1)), ('trim', (0.3, 0.3, 0.3, 1)), ('button', (0.95, 0.93, 0.88, 1))):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = col
    MAT[name] = m


def new_obj(name, bm, uv):
    """bmesh -> object with the three materials and a UV layer from the per-vertex (u,v) list"""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    for k in ('cloth', 'trim', 'button'):
        ob.data.materials.append(MAT[k])
    ob['uv0'] = [c for p in uv for c in p]      # kept until the modifiers are applied
    return ob


def tube(name, p0, p1, radius, n_around=40, n_along=12, mat=0, off=0.02, clamp=1.5, smooth_r=2, hang=0.0, target=None, band_lo=0, band_hi=0, band_step=0.008):
    """A tube from p0 to p1 MEASURED off the body: each ring casts rays out from its own centre
    and takes the distance to the body in each direction, plus the stand-off. Shrinkwrap was
    tried twice and failed both times, because a limb pressed against a body has no surface
    of its own on the pressed side -- the cloth went to the torso and tore. Here a direction
    that sees no limb wall (it exits into the torso) is clamped to the ring's median, so the
    sleeve stays a tube and its buried side is simply hidden in the body. The radii are then
    smoothed across the ring and along the tube, which is what makes it cloth and not fur.
    u = 0.5 dead ahead, v = 0 at p0 .. 1 at p1. `radius` is only the fallback when no ray hits."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0)
    ln = axis.length
    axis.normalize()
    fwd = Vector((0, -1, 0))
    fwd = (fwd - axis * fwd.dot(axis))
    if fwd.length < 1e-4:
        fwd = Vector((0, 0, 1)) - axis * axis.z
    fwd.normalize()
    right = axis.cross(fwd).normalized()
    dirs = []
    for i in range(n_around):
        a = 2 * math.pi * i / n_around
        dirs.append((a, (fwd * math.cos(a) + right * math.sin(a)).normalized()))
    R = []
    for j in range(n_along + 1):
        t = j / n_along
        c = p0 + axis * (ln * t)
        row = []
        for a, d in dirs:
            hit, loc, nrm, idx = (target or BODY).ray_cast(c, d, distance=1.5)
            row.append((loc - c).length if hit else None)
        good = sorted(x for x in row if x is not None)
        if len(good) >= n_around // 3:
            med = good[len(good) // 2]
            row = [min(x, clamp * med) if x is not None else med for x in row]
        else:
            row = [None] * n_around
        R.append(row)
    # rings that saw nothing take their neighbour's shape
    for j in range(n_along + 1):
        if R[j][0] is None:
            src = next((R[k] for k in list(range(j - 1, -1, -1)) + list(range(j + 1, n_along + 1)) if R[k][0] is not None), None)
            R[j] = list(src) if src else [radius] * n_around
    # cloth bridges hollows and ignores fur: a max over the neighbourhood, then a blur
    def nb(j, i, k=1):
        out = []
        for dj in range(-k, k + 1):
            jj = j + dj
            if 0 <= jj <= n_along:
                for di in range(-k, k + 1):
                    out.append(R[jj][(i + di) % n_around])
        return out
    R = [[max(nb(j, i)) for i in range(n_around)] for j in range(n_along + 1)]
    for _ in range(smooth_r):
        R = [[sum(nb(j, i)) / len(nb(j, i)) for i in range(n_around)] for j in range(n_along + 1)]
    bm = bmesh.new()
    rings = []
    uv = []
    for j in range(n_along + 1):
        t = j / n_along
        c = p0 + axis * (ln * t)
        ring = []
        # a hem or collar is the tube's own end rows stepped out a hair and coloured trim --
        # a separately measured ring floated round the body like a hoop
        band = (j < band_lo) or (j > n_along - band_hi)
        for i, (a, d) in enumerate(dirs):
            p = c + d * (R[j][i] + off + hang * (1 - t) + (band_step if band else 0.0))
            ring.append(bm.verts.new(p))
            uv.append((0.5 + a / (2 * math.pi), t))
        rings.append(ring)
    bm.verts.ensure_lookup_table()
    for j in range(n_along):
        for i in range(n_around):
            a, b = rings[j][i], rings[j][(i + 1) % n_around]
            c, d = rings[j + 1][(i + 1) % n_around], rings[j + 1][i]
            f = bm.faces.new((a, b, c, d))
            f.material_index = 1 if (j < band_lo or j >= n_along - band_hi) else mat
    ob = new_obj(name, bm, uv)
    mean_r = sum(sum(r) for r in R) / (n_around * (n_along + 1))
    ob['circ_over_h'] = 2 * math.pi * (mean_r + off) / max(1e-4, ln)
    return ob


def shell(name, bones, smooth_it):
    """the part of the body those bones own, on its own -- so a sleeve wraps the ARM and not
    the torso the arm is pressed against, and smoothed so the cloth ignores the fur"""
    ob = BODY.copy()
    ob.data = BODY.data.copy()
    ob.name = name
    bpy.context.scene.collection.objects.link(ob)
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
    gi = {g.index: g.name for g in ob.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    dl = bm.verts.layers.deform.verify()
    kill = []
    for v in bm.verts:
        w = sum(wt for idx, wt in v[dl].items() if gi.get(idx) in bones)
        if w < 0.5:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(ob.data)
    bm.free()
    if smooth_it:
        m = ob.modifiers.new('sm', 'SMOOTH')
        m.factor = 1.0
        m.iterations = smooth_it
        apply_mod(ob, m)
    ob.hide_render = True
    ob.hide_viewport = False
    return ob


def apply_mod(ob, m):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=m.name)


def wrap(ob, offset, target=None):
    m = ob.modifiers.new('sw', 'SHRINKWRAP')
    m.target = target or BODY
    m.wrap_method = 'NEAREST_SURFACEPOINT'
    m.wrap_mode = 'OUTSIDE'
    m.offset = offset
    apply_mod(ob, m)


def smooth(ob, factor=0.5, iterations=8):
    m = ob.modifiers.new('sm', 'SMOOTH')
    m.factor = factor
    m.iterations = iterations
    apply_mod(ob, m)


def write_uv(ob, seam_fix=True):
    """the (u,v) stored at build time onto a UV layer; a face that straddles u=0|1 gets its
    low corners moved up by one so the print does not smear across the back seam"""
    uv0 = ob['uv0']
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name='UVMap')
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    for f in bm.faces:
        us = [uv0[l.vert.index * 2] for l in f.loops]
        hop = seam_fix and (max(us) - min(us) > 0.5)
        for l in f.loops:
            u = uv0[l.vert.index * 2]
            v = uv0[l.vert.index * 2 + 1]
            if hop and u < 0.5:
                u += 1.0
            l[uvl].uv = (u, v)
    bm.to_mesh(me)
    bm.free()
    del ob['uv0']


def solidify(ob, thickness, rim_mat=1):
    m = ob.modifiers.new('so', 'SOLIDIFY')
    m.thickness = thickness
    m.offset = 1.0           # grows outward
    m.use_rim = True
    m.material_offset_rim = rim_mat
    apply_mod(ob, m)
    # the rim offset ADDS to the face's own index, so a band's rim (1+1) landed on the button
    # material; nothing solidified here is ever a button
    for f in ob.data.polygons:
        if f.material_index > 1:
            f.material_index = 1


def fit(ob, off, T, it=8):
    """onto the smooth shell, smooth the cloth so it bridges, onto the shell again so the
    smoothing cannot have pushed it in, then a guard against the raw part so no fur pokes"""
    wrap(ob, off, T[0])
    if it:
        smooth(ob, 0.5, it)
        wrap(ob, off, T[0])
    wrap(ob, off * 0.45, T[1])


def flare(ob, amount, axis_p0, axis_p1):
    """push vertices out along their normal, more towards axis_p0 (the hem) -- a shirt hangs"""
    p0, p1 = Vector(axis_p0), Vector(axis_p1)
    ax = (p1 - p0)
    ln = ax.length
    ax.normalize()
    me = ob.data
    me.calc_normals_split() if hasattr(me, 'calc_normals_split') else None
    for v in me.vertices:
        t = max(0.0, min(1.0, (v.co - p0).dot(ax) / ln))
        v.co += v.normal * (amount * (1 - t))


def sphere(name, c, r):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=r, location=c)
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    for k in ('cloth', 'trim', 'button'):
        ob.data.materials.append(MAT[k])
    for f in ob.data.polygons:
        f.material_index = 2
    return ob


def front_point(z, y_from=0.6):
    """where the body's front surface is at height z, on the centre line -- for buttons"""
    hit, loc, nrm, idx = BODY.ray_cast(Vector((0.0, -y_from, z)), Vector((0, 1, 0)))
    return (loc, nrm) if hit else (None, None)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def skin(ob):
    for g in BODY.vertex_groups:
        ob.vertex_groups.new(name=g.name)
    m = ob.modifiers.new('dt', 'DATA_TRANSFER')
    m.object = BODY
    m.use_vert_data = True
    m.data_types_verts = {'VGROUP_WEIGHTS'}
    m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'
    m.layers_vgroup_select_dst = 'NAME'
    apply_mod(ob, m)
    a = ob.modifiers.new('arm', 'ARMATURE')
    a.object = ARM
    ob.parent = ARM
    # normalise: transferred weights can sum to a hair under 1 and the exporter complains
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)


def measure_tile(ob, p0, p1):
    """circumference over height of the finished cloth, for the game's pattern tiling"""
    p0, p1 = Vector(p0), Vector(p1)
    ax = (p1 - p0).normalized()
    rs = []
    for v in ob.data.vertices:
        d = v.co - p0
        rs.append((d - ax * d.dot(ax)).length)
    r = sum(rs) / max(1, len(rs))
    ob['circ_over_h'] = 2 * math.pi * r / max(1e-4, (p1 - p0).length)


# ---------------------------------------------------------------- landmarks
X = lambda n: L[n].x
hips, spine, neck, head = L['Hips'], L['Spine'], L['neck'], L['Head']
sh = {'L': L['LeftShoulder'], 'R': L['RightShoulder']}
arm = {'L': (L['LeftArm'], L['LeftForeArm'], L['LeftHand']), 'R': (L['RightArm'], L['RightForeArm'], L['RightHand'])}
leg = {'L': (L['LeftUpLeg'], L['LeftLeg'], L['LeftFoot']), 'R': (L['RightUpLeg'], L['RightLeg'], L['RightFoot'])}
depth_y = hips.y                     # the spine's y; the body is centred on it
torso_lo = hips.z - 0.03            # the shirt hem, just under the hips
torso_hi = neck.z + 0.01            # the neckline
waist_lo = L['LeftUpLeg'].z - 0.07  # the trousers' waist tube runs down into the thighs, under the leg tubes...
waist_hi = hips.z + 0.09            # ...to under the shirt's hem
OFF = 0.022                          # cloth stands this far off the fur
THK = 0.014                          # and is this thick
# what each tube measures against: the torso ring must not see the arms, or the shirt is a
# barrel with the arms inside it and the sleeves buried; a sleeve must not see the torso, or
# its pressed side goes there. A ray out through the opening where a limb was cut away sees
# nothing, and a miss falls back to the ring's median -- which is exactly the tube wanted.
CORE_B = {'Hips', 'Spine', 'Spine01', 'Spine02', 'LeftShoulder', 'RightShoulder', 'neck', 'Head', 'head_end', 'headfront'}
T_CORE = shell('t_core', CORE_B, 0)
T_LOWER = shell('t_lower', CORE_B | {'LeftUpLeg', 'RightUpLeg'}, 0)
T_ARM = {s: shell('t_arm' + s, {p + 'Arm', p + 'ForeArm', p + 'Hand'}, 0) for s, p in (('L', 'Left'), ('R', 'Right'))}
T_LEG = {s: shell('t_leg' + s, {p + 'UpLeg', p + 'Leg', p + 'Foot', p + 'ToeBase'}, 0) for s, p in (('L', 'Left'), ('R', 'Right'))}


def shirt(name, sleeves):
    parts = []
    ax0, ax1 = (0, depth_y, torso_lo), (neck.x, neck.y, torso_hi)
    t = tube('torso', ax0, ax1, 0.3, n_around=48, n_along=18, off=OFF, clamp=1.6, smooth_r=3, hang=0.02,
             target=T_CORE, band_lo=1, band_hi=2, band_step=0.009)
    tile = t['circ_over_h']
    write_uv(t)
    solidify(t, THK)
    parts.append(t)
    if sleeves != 'none':
        for side in ('L', 'R'):
            a0, a1, a2 = arm[side]
            start = a0 + (sh[side] - a0) * 0.45          # begins inside the shoulder
            end = a0 + (a1 - a0) * 0.8 if sleeves == 'short' else a2 + (a2 - a1) * 0.1
            s = tube('sleeve' + side, start, end, 0.08, n_around=28, n_along=10, off=OFF + 0.006, clamp=1.35, smooth_r=2,
                     target=T_ARM[side], band_hi=2, band_step=0.007)
            write_uv(s)
            solidify(s, THK)
            parts.append(s)
    for k in range(5):
        z = torso_lo + 0.10 + k * (torso_hi - 0.09 - torso_lo - 0.10) / 4
        loc, nrm = front_point(z)
        if loc is not None:
            parts.append(sphere('btn%d' % k, loc + nrm * (OFF + THK + 0.008), 0.017))
    ob = join(parts, name)
    ob['circ_over_h'] = tile
    return ob


def trousers(name, length):
    parts = []
    w = tube('waist', (0, depth_y, waist_lo), (0, depth_y, waist_hi), 0.3, n_around=44, n_along=8, off=OFF - 0.006, clamp=1.6, smooth_r=3,
             target=T_LOWER, band_hi=2, band_step=0.009)
    tile = w['circ_over_h']
    write_uv(w)
    solidify(w, THK)
    parts.append(w)
    for side in ('L', 'R'):
        l0, l1, l2 = leg[side]
        start = Vector((l0.x * 0.9, l0.y, l0.z + 0.02))
        end = l1 + (l2 - l1) * 0.2 if length == 'shorts' else l2 + Vector((0, 0, 0.03))   # shorts to just past the knee
        s = tube('leg' + side, start, end, 0.08, n_around=28, n_along=10, off=OFF, clamp=1.35, smooth_r=2,
                 target=T_LEG[side], band_hi=2, band_step=0.007)
        write_uv(s)
        solidify(s, THK)
        parts.append(s)
    ob = join(parts, name)
    ob['circ_over_h'] = tile
    return ob


OUT = []
OUT.append(shirt('shirt_short', 'short'))
OUT.append(shirt('shirt_long', 'long'))
OUT.append(shirt('vest', 'none'))
OUT.append(trousers('pants_short', 'shorts'))
OUT.append(trousers('pants_long', 'long'))
for ob in OUT:
    skin(ob)
    print("  %-12s %6d verts %6d faces  tile %.2f" % (ob.name, len(ob.data.vertices), len(ob.data.polygons), ob['circ_over_h']), flush=True)

bpy.ops.object.select_all(action='DESELECT')
ARM.select_set(True)
for ob in OUT:
    ob.select_set(True)
bpy.context.view_layer.objects.active = ARM
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', use_selection=True,
                          export_apply=True, export_skins=True, export_extras=True,
                          export_materials='EXPORT', export_image_format='NONE',
                          export_animations=False)
import os
print("wrote", DST, round(os.path.getsize(DST) / 1e6, 2), "MB", flush=True)
