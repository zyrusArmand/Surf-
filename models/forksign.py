# The fork's signpost: two planks, LEFT and RIGHT, on a post at the head of the island.
#
# NOT an edit of menusign.glb. That was the first plan and it does not survive contact with the
# file: its three words are BAKED INTO A SHARED UV ATLAS -- 1024px of wood with "PL", "AY" and
# the rest scattered across islands -- so removing a plank leaves its letters in the texture and
# writing LEFT and RIGHT means reconstructing wood grain underneath the old ones, by hand, on a
# layout nothing describes. This is built instead, in the same family: the wood palette is
# SAMPLED off menusign's own base map so it belongs to the same signpost, and the letters are
# painted in its yellow and carved with a shadow above and a highlight below.
#
# The menu's signpost is untouched.
import bpy, bmesh, math, os, struct, json, io
from PIL import Image, ImageDraw, ImageFont

HERE = '/home/user/Surf-/models'
TEX  = '/tmp/claude-0/-home-user-Surf-/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/scratchpad/forksign.png'

# ---- the wood, taken off the sign it stands next to ----
def palette():
    d = open(HERE + '/menusign.glb', 'rb').read()
    off, j, bins = 12, None, None
    while off < len(d):
        ln, ty = struct.unpack_from('<II', d, off)
        ch = d[off+8:off+8+ln]
        if ty == 0x4E4F534A: j = json.loads(ch)
        else: bins = ch
        off += 8 + ln
    bv = j['bufferViews'][j['images'][1]['bufferView']]
    o = bv.get('byteOffset', 0)
    im = Image.open(io.BytesIO(bins[o:o+bv['byteLength']])).convert('RGB')
    px = list(im.resize((64, 64)).getdata())
    # the yellow of the lettering is the brightest thing on it; the wood is everything else
    wood = sorted([p for p in px if not (p[0] > 200 and p[1] > 180 and p[2] < 160)],
                  key=lambda p: sum(p))
    yellow = max(px, key=lambda p: p[0] + p[1] - p[2])
    n = len(wood)
    return wood[int(n*0.12)], wood[int(n*0.50)], wood[int(n*0.86)], yellow

DARK, MID, LITE, INK = palette()
print('wood', DARK, MID, LITE, 'ink', INK)

# ---- the texture: three bands, LEFT / RIGHT / plain ----
W, H = 1024, 768
img = Image.new('RGB', (W, H), MID)
dr = ImageDraw.Draw(img)
import random
random.seed(11)
def plank(y0, y1):
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0 - 1)
        base = [int(DARK[i] + (LITE[i] - DARK[i]) * (0.35 + 0.5 * abs(math.sin(y * 0.11)))) for i in range(3)]
        dr.line([(0, y), (W, y)], fill=tuple(base))
    for _ in range(220):                      # grain
        gy = random.randint(y0, y1 - 1)
        gx = random.randint(0, W)
        L = random.randint(40, 300)
        c = LITE if random.random() < 0.5 else DARK
        dr.line([(gx, gy), (gx + L, gy + random.randint(-1, 1))],
                fill=tuple(int(c[i] * 0.85 + MID[i] * 0.15) for i in range(3)))
    dr.line([(0, y0), (W, y0)], fill=tuple(int(c * 0.55) for c in DARK), width=3)
    dr.line([(0, y1 - 2), (W, y1 - 2)], fill=tuple(int(c * 0.55) for c in DARK), width=3)
for band in (0, 256, 512):
    plank(band, band + 256)

def font(sz):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
              '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()

def carve(word, arrow, y0):
    """the word, cut in and painted: a shadow above, the paint, a highlight below"""
    f = font(150)
    cy = y0 + 128
    bb = dr.textbbox((0, 0), word, font=f)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    # ---- the arrow owns one end and the word gets what is left ----
    # The first version centred the word on the panel and offset it a little, which on RIGHT put
    # the T straight through the arrow's shaft. The arrow's footprint is worked out first and the
    # word is centred in the remainder, so they cannot meet whatever the word is.
    ax = 150 if arrow < 0 else W - 150
    PAD = 60
    if arrow < 0: lo, hi = ax + 100 + PAD, W - PAD          # arrow on the left
    else:         lo, hi = PAD, ax - 100 - PAD              # arrow on the right
    tx = lo + ((hi - lo) - tw) / 2
    for dx, dy, col in ((0, -5, tuple(int(c*0.35) for c in DARK)),
                        (0,  5, tuple(min(255, int(c*1.25)) for c in LITE)),
                        (0,  0, INK)):
        dr.text((tx + dx, cy - th / 2 - bb[1] + dy), word, font=f, fill=col)
    # a solid triangle head with a shaft, pointing the way the plank does
    hx = ax + arrow * 92
    dr.polygon([(hx, cy), (ax, cy - 74), (ax, cy + 74)], fill=INK)
    x0, x1 = sorted((ax, ax - arrow * 96))
    dr.rectangle([x0, cy - 26, x1, cy + 26], fill=INK)

carve('LEFT',  -1, 0)
carve('RIGHT', +1, 256)
img.save(TEX)
print('texture', TEX)

# ---- the sign ----
bpy.ops.wm.read_factory_settings(use_empty=True)
for o in list(bpy.context.scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)

# ---- and it is built BASE AT THE ORIGIN ----
# The first version centred the post on the origin, so half of it was underground wherever it
# was placed and the planks came out at chest height on a dog -- a sign you have to be told is
# a sign. Base at zero and a foot of it buried, planks up where a signpost's planks are.
# Bigger, too: this is read at forty feet from a moving board, not from arm's length.
POST_H, POST_R, POST_DOWN = 12.4, 0.42, 1.2
PL_L, PL_H, PL_T = 8.2, 2.35, 0.30      # plank length, height, thickness
bm = bmesh.new()

def box(cx, cy, cz, sx, sy, sz, uvband, point=0):
    """a box, its two big faces UV'd into one band of the texture; point tapers the +x or -x end"""
    v = []
    for ix in (-1, 1):
        for iy in (-1, 1):
            for iz in (-1, 1):
                x = cx + ix * sx / 2
                # ---- the arrow end tapers in HEIGHT ----
                # It tapered the THICKNESS first, which is three tenths of a foot and reads as
                # nothing at all: the plank still ended square and there was no point on it.
                k = 1.0
                if point and ix == point: k = 0.16
                v.append(bm.verts.new((x, cy + iy * sy / 2 * k, cz + iz * sz / 2)))
    def q(a, b, c, d, band=None):
        f = bm.faces.new((v[a], v[b], v[c], v[d]))
        f.material_index = 0
        return f
    idx = [(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
    faces = [q(*t) for t in idx]
    return faces, v

# the post, a foot of it buried so it never floats off a dune
post, _ = box(0, (POST_H - POST_DOWN)/2 - POST_DOWN/2, 0,
              POST_R*2, POST_H, POST_R*2, 2)
for f in post: f.material_index = 0
# LEFT plank on top, RIGHT below it
def arm(top_gap, sgn, band):
    return box(sgn * (PL_L/2 - 0.2), POST_H - POST_DOWN - top_gap, 0,
               PL_L, PL_H, PL_T, band, point=sgn)
lf, lv = arm(1.5, -1, 0)
rf, rv = arm(4.3, +1, 1)

me = bpy.data.meshes.new('forksign')
bm.to_mesh(me); bm.free()
# ---- AND IT IS STOOD UP, BECAUSE BLENDER IS Z-UP ----
# Everything above is written with Y as height, which is the game's convention and not this
# program's. Exported as it stood, the post lay on its side and the two planks pointed at the
# camera instead of left and right -- the third time this file has caught somebody out that
# way (see isle.glb in models/README.md). Rotated a quarter turn about X so height ends up on
# Blender's Z, which export_yup then puts back on Y where the game wants it.
from mathutils import Matrix
me.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
obj = bpy.data.objects.new('forksign', me)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj; obj.select_set(True)

# ---- UVs, set per face rather than unwrapped ----
# The two plank faces that face the lens get a whole band each; everything else gets the plain
# band. Planar and explicit, because the whole point is knowing exactly where the word lands.
me.uv_layers.new(name='UVMap')
uv = me.uv_layers.active.data
BANDS = {0: (2/3, 1.0), 1: (1/3, 2/3), 2: (0.0, 1/3)}
for poly in me.polygons:
    ctr = poly.center
    nz = poly.normal.y                                    # the faces that look down the lane
    band = 2
    if abs(nz) > 0.7 and abs(ctr.x) > 0.6:
        band = 0 if ctr.x < 0 else 1
    v0, v1 = BANDS[band]
    xs = [me.vertices[i].co.x for i in poly.vertices]
    ys = [me.vertices[i].co.z for i in poly.vertices]     # height, after the stand-up above
    x0, x1 = min(xs), max(xs); y0, y1 = min(ys), max(ys)
    for li in poly.loop_indices:
        vi = me.loops[li].vertex_index
        co = me.vertices[vi].co
        u = 0.0 if x1 - x0 < 1e-5 else (co.x - x0) / (x1 - x0)
        # No flip. There was one here on the reasoning that the LEFT plank faces the other way;
        # it does not -- both planks present the same face down the lane -- and all the flip did
        # was mirror the word and turn its arrow round, so the top plank read "TFEL" with an
        # arrow pointing right.
        t = 0.0 if y1 - y0 < 1e-5 else (co.z - y0) / (y1 - y0)
        uv[li].uv = (u, v0 + t * (v1 - v0))

mat = bpy.data.materials.new('signwood')
mat.use_nodes = True
bsdf = mat.node_tree.nodes['Principled BSDF']
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(TEX)
mat.node_tree.links.new(bsdf.inputs['Base Color'], tex.outputs['Color'])
bsdf.inputs['Roughness'].default_value = 0.82
me.materials.append(mat)
bpy.ops.object.shade_flat()

DST = HERE + '/forksign.glb'
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_yup=True,
    export_normals=True, export_texcoords=True, export_apply=True)
print('verts', len(me.vertices), 'faces', len(me.polygons))
print('wrote', DST, os.path.getsize(DST)//1024, 'KB')
