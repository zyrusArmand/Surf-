# The fork's LEFT / RIGHT signpost, made by EDITING menusign.glb rather than rebuilding it.
#
# The first attempt at this built a post and two planks from boxes with a painted texture, and
# it looked like boxes with a painted texture. The real model is a scan: 61k triangles of carved
# wood with hand-painted lettering, and nothing modelled from primitives is going to sit beside
# it. So this takes the actual file apart.
#
# The obstacle is that the three words are BAKED INTO A SHARED UV ATLAS -- 1024px of wood with
# "PL", "AY" and the rest scattered across fragment islands, in no order anything describes. The
# way through is not to edit the atlas by eye but to go through the geometry:
#
#   1. find the words in 3D by asking which FACES sample yellow texels. That gives three clean
#      bands of z, one per plank, all reading toward -y;
#   2. FLATTEN a plank -- rasterise its triangles in plank-local coordinates, sampling the atlas
#      through their UVs -- which reconstructs the board as a rectangle you can actually look at
#      and edit;
#   3. edit that rectangle: clone-stamp the old word out along the grain, paint the new one in;
#   4. BAKE it back the other way, rasterising the same triangles into the atlas.
#
# Step 2 run on the top plank is what identified it as "Play". The menu's signpost is untouched:
# this writes a new file.
import bpy, bmesh, math, os, struct, json, io, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = '/home/user/Surf-/models'
SRC  = HERE + '/menusign.glb'
DST  = HERE + '/forksign.glb'
SCR  = '/tmp/claude-0/-home-user-Surf-/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/scratchpad/'

# ---- the three bands, measured (see the yellow-face pass) ----
PLAY   = (0.632, 0.868)        # the plank that goes
UPPER  = (0.358, 0.522)        # becomes LEFT
LOWER  = (0.055, 0.212)        # becomes RIGHT

def glb_images(path):
    d = open(path, 'rb').read(); off, j, bins = 12, None, None
    while off < len(d):
        ln, ty = struct.unpack_from('<II', d, off); ch = d[off+8:off+8+ln]
        if ty == 0x4E4F534A: j = json.loads(ch)
        else: bins = ch
        off += 8 + ln
    out = []
    for im in j['images']:
        bv = j['bufferViews'][im['bufferView']]; o = bv.get('byteOffset', 0)
        out.append(Image.open(io.BytesIO(bins[o:o+bv['byteLength']])).convert('RGB'))
    return j, out

j, IMGS = glb_images(SRC)
# material: normalTexture 0, baseColor 1, metallicRoughness 2
BASE, NRM, RGH = IMGS[1], IMGS[0], IMGS[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
me  = obj.data
uvl = me.uv_layers.active.data

def is_yellow(c):
    return c[0] > 185 and c[1] > 165 and c[2] < 150 and (c[0]+c[1])/2 - c[2] > 70

# ---- the post, so the plank we delete can be told from the post it hangs on ----
# Measured ABOVE every plank, where whatever is left is certainly post: at z 0.86..0.95 the
# geometry sits within 0.09 of the axis. The first version took a band BETWEEN two planks and
# measured a footprint 0.45 across -- half the width of the sign -- because that band is not
# plank-free at all, and the result was that most of the Play plank counted as post and stayed
# on. That is why it was still saying Play after seven thousand faces had been deleted.
# 0.095, not 0.12. At 0.12 a strip of the Play plank right at the post survived the cut and
# the sign still read "la" up there. The bare post above every plank measures 0.09.
POST_CX, POST_CY, POST_R = 0.0, 0.07, 0.095
print('post axis %.3f,%.3f r %.3f' % (POST_CX, POST_CY, POST_R))

def band_faces(z0, z1, front_only=True):
    fs = []
    for p in me.polygons:
        if not (z0 <= p.center.z <= z1): continue
        if front_only and p.normal.y >= -0.25: continue
        fs.append(p)
    return fs

# ---------------------------------------------------------------- flatten / bake
class Panel:
    """a plank's reading face, as a rectangle, both ways round"""
    def __init__(self, faces, OW=1100):
        self.faces = faces
        cx = sum(p.center.x for p in faces)/len(faces)
        cy = sum(p.center.y for p in faces)/len(faces)
        sxx = sxy = syy = 0.0
        for p in faces:
            dx, dy = p.center.x-cx, p.center.y-cy
            sxx += dx*dx; sxy += dx*dy; syy += dy*dy
        th = 0.5*math.atan2(2*sxy, sxx-syy)
        self.cx, self.cy, self.ax, self.ay = cx, cy, math.cos(th), math.sin(th)
        ss = [self._s(me.vertices[i].co) for p in faces for i in p.vertices]
        zs = [me.vertices[i].co.z      for p in faces for i in p.vertices]
        self.s0, self.s1, self.t0, self.t1 = min(ss), max(ss), min(zs), max(zs)
        self.OW = OW
        self.OH = max(8, int(OW*(self.t1-self.t0)/max(1e-6, self.s1-self.s0)))
    def _s(self, co): return (co.x-self.cx)*self.ax + (co.y-self.cy)*self.ay
    def local(self, co):
        s = (self._s(co)-self.s0)/max(1e-9, self.s1-self.s0)*self.OW
        t = (1-(co.z-self.t0)/max(1e-9, self.t1-self.t0))*self.OH
        return s, t
    def tris(self):
        for p in self.faces:
            vi, li = list(p.vertices), list(p.loop_indices)
            for k in range(1, len(vi)-1):
                yield ([self.local(me.vertices[vi[t]].co) for t in (0, k, k+1)],
                       [tuple(uvl[li[t]].uv) for t in (0, k, k+1)])
    @staticmethod
    def _raster(P, W, H, grow=0.0):
        if grow:
            gx = sum(q[0] for q in P)/3; gy = sum(q[1] for q in P)/3
            P = [(gx+(q[0]-gx)*(1+grow), gy+(q[1]-gy)*(1+grow)) for q in P]
        den = (P[1][1]-P[2][1])*(P[0][0]-P[2][0])+(P[2][0]-P[1][0])*(P[0][1]-P[2][1])
        if abs(den) < 1e-9: return
        xs = [q[0] for q in P]; ys = [q[1] for q in P]
        for X in range(max(0, int(min(xs))), min(W, int(max(xs))+2)):
            for Y in range(max(0, int(min(ys))), min(H, int(max(ys))+2)):
                w0 = ((P[1][1]-P[2][1])*(X-P[2][0])+(P[2][0]-P[1][0])*(Y-P[2][1]))/den
                w1 = ((P[2][1]-P[0][1])*(X-P[2][0])+(P[0][0]-P[2][0])*(Y-P[2][1]))/den
                w2 = 1-w0-w1
                if w0 < -0.003 or w1 < -0.003 or w2 < -0.003: continue
                yield X, Y, w0, w1, w2
    def read(self, img, fill=True):
        """flatten, and CLOSE THE GAPS.

        A UV atlas of a scan is islands with space between them, so rasterising the plank's
        triangles leaves texels the triangles never cover -- black holes scattered through the
        flattened board. Harmless to look at and NOT harmless to write back: those holes get
        baked into the texture as black patches on the wood. Grown outward from whatever did get
        covered until nothing is left uncovered."""
        W, H = img.size; sp = img.load()
        out = Image.new('RGB', (self.OW, self.OH), (0, 0, 0)); op = out.load()
        got = [[False]*self.OH for _ in range(self.OW)]
        for P, U in self.tris():
            for X, Y, a, b, c in self._raster(P, self.OW, self.OH):
                u = a*U[0][0]+b*U[1][0]+c*U[2][0]
                v = a*U[0][1]+b*U[1][1]+c*U[2][1]
                op[X, Y] = sp[int(min(W-1, max(0, u*W))), int(min(H-1, max(0, (1-v)*H)))]
                got[X][Y] = True
        if fill:
            for _ in range(24):
                todo = [(x, y) for x in range(self.OW) for y in range(self.OH) if not got[x][y]]
                if not todo: break
                add = []
                for x, y in todo:
                    for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                        nx, ny = x+dx, y+dy
                        if 0 <= nx < self.OW and 0 <= ny < self.OH and got[nx][ny]:
                            add.append((x, y, op[nx, ny])); break
                if not add: break
                for x, y, c in add: op[x, y] = c; got[x][y] = True
        return out
    def write(self, img, panel):
        """bake the panel back into the atlas, grown a touch so island seams close"""
        W, H = img.size; dp = img.load(); pp = panel.load()
        for P, U in self.tris():
            UP = [(U[i][0]*W, (1-U[i][1])*H) for i in range(3)]
            for X, Y, a, b, c in self._raster(UP, W, H, grow=0.09):
                s = a*P[0][0]+b*P[1][0]+c*P[2][0]
                t = a*P[0][1]+b*P[1][1]+c*P[2][1]
                si = int(min(self.OW-1, max(0, s))); ti = int(min(self.OH-1, max(0, t)))
                dp[X, Y] = pp[si, ti]

# ---------------------------------------------------------------- the repaint
def font(sz):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf',
              '/usr/share/fonts/truetype/freefont/FreeSerifBold.ttf'):
        if os.path.exists(p): return ImageFont.truetype(p, sz)
    return ImageFont.load_default()

def clone_out(img, box, tag=None):
    """lift a rectangle out of a flattened panel by cloning along the grain"""
    W, H = img.size; px = img.load()
    x0, y0, x1, y1 = box
    span = x1-x0
    src = x0-span-30 if x0-span-30 > 0 else x1+30
    for x in range(x0, x1):
        sx = src + (x-x0)
        if not (0 <= sx < W): sx = max(0, min(W-1, 2*x0-x))
        for y in range(y0, y1):
            px[x, y] = px[sx, y]
    return img

def repaint(pan, word, arrow, tag):
    """clone the old word out along the grain, paint the new one in its place"""
    img = pan.read(BASE)
    img.save(SCR + 'was_%s.png' % tag)
    W, H = img.size; px = img.load()
    ys = [(x, y) for y in range(H) for x in range(W) if is_yellow(px[x, y])]
    if not ys: raise SystemExit('no lettering found on ' + tag)
    lx0 = min(p[0] for p in ys); lx1 = max(p[0] for p in ys)
    ly0 = min(p[1] for p in ys); ly1 = max(p[1] for p in ys)
    ink = tuple(sum(px[p][i] for p in ys)//len(ys) for i in range(3))
    # ---- the old word goes, along the grain ----
    # Wood grain on these planks runs the length of the board, so a HORIZONTAL clone keeps every
    # grain line at the height it was already at and there is no seam to see. Vertical or
    # blurred fills both showed as a smudge exactly where the word had been.
    pad = 14
    box = (max(0, lx0-pad), max(0, ly0-pad), min(W, lx1+pad), min(H, ly1+pad))
    clone_out(img, box)
    # ---- and the new one is painted in the same place, in the same paint ----
    dr = ImageDraw.Draw(img)
    # a little under the old word's height: DejaVu Serif Bold is a heavier face than the
    # sign's own hand-painted one, and matching cap height exactly made it look crowded
    target_h = (ly1-ly0)*0.90
    sz = 10
    while True:
        f = font(sz); bb = dr.textbbox((0, 0), word, font=f)
        if bb[3]-bb[1] >= target_h or sz > 400: break
        sz += 2
    f = font(sz); bb = dr.textbbox((0, 0), word, font=f)
    tw, th = bb[2]-bb[0], bb[3]-bb[1]
    ah = int(th*0.78)                       # the arrow, a little shorter than the caps
    agap = int(th*0.42)
    total = tw + agap + ah*1.25
    cx = (lx0+lx1)/2; cy = (ly0+ly1)/2
    if arrow < 0: ax = cx-total/2+ah*0.62; tx = cx-total/2+ah*1.25+agap
    else:         ax = cx+total/2-ah*0.62; tx = cx-total/2
    ty = cy-th/2-bb[1]
    # The original letters sit on a soft BROWN edge, not a black one. At 0.30 of the ink and a
    # 1.7x alpha this was a hard black halo -- readable, and nothing like the sign.
    dark = tuple(max(0, int(c*0.34)) for c in ink)
    # a soft dark edge under the paint, which is what the original letters have
    edge = Image.new('L', (W, H), 0); ed = ImageDraw.Draw(edge)
    ed.text((tx, ty), word, font=f, fill=255)
    hx = ax+arrow*ah*0.62
    ed.polygon([(hx, cy), (ax-arrow*ah*0.12, cy-ah*0.62), (ax-arrow*ah*0.12, cy+ah*0.62)], fill=255)
    sx0, sx1 = sorted((ax-arrow*ah*0.10, ax-arrow*ah*0.72))
    ed.rectangle([sx0, cy-ah*0.22, sx1, cy+ah*0.22], fill=255)
    soft = edge.filter(ImageFilter.GaussianBlur(3))
    img.paste(Image.new('RGB', (W, H), dark), (0, 0), soft.point(lambda v: min(200, int(v*0.80))))
    img.paste(Image.new('RGB', (W, H), ink), (0, 0), edge)
    img.save(SCR + 'now_%s.png' % tag)
    print('%s: letters %d..%d x %d..%d  ink %s  size %d' % (tag, lx0, lx1, ly0, ly1, ink, sz))
    return img, box

# ---- and the word goes on the plank whose END ALREADY POINTS THAT WAY ----
# These planks are cut to a point at one end: flattened, the upper one (Shop) comes to a head on
# the right and the lower one (Quests) on the left. That carved point IS the arrow, and it is a
# better one than anything paintable because it is the model's own geometry. So the words follow
# the wood rather than the wood being argued with, and the painted arrow agrees with the point
# instead of contradicting it -- the first pass had LEFT on the plank that points right.
for band, word, arrow, tag in ((UPPER, 'RIGHT', +1, 'upper'), (LOWER, 'LEFT', -1, 'lower')):
    fs = band_faces(*band)
    pan = Panel(fs)
    new, box = repaint(pan, word, arrow, tag)
    pan.write(BASE, new)
    # ---- and the old word's RELIEF goes with its paint ----
    # The lettering is not only in the base map: the normal map carries the raised edge of the
    # paint and the roughness map carries its sheen. Repaint the colour alone and the old word
    # is still there in the light -- a ghost of "Shop" embossed under the new letters, which is
    # exactly what it looked like. Same panel, same box, same clone along the grain.
    for m in (NRM, RGH):
        pan.write(m, clone_out(pan.read(m), box))
# ---- and it is READ BACK to check ----
# The bake writes through the same triangles the read came from, so a texel the read missed is a
# texel the write misses too -- which would leave a fragment of the old word behind. The only
# honest check is to flatten the FINISHED atlas and look at it.
for band, tag in ((UPPER, 'upper'), (LOWER, 'lower')):
    chk = Panel(band_faces(*band)).read(BASE)
    chk.save(SCR + 'check_%s.png' % tag)
    cp = chk.load(); Wc, Hc = chk.size
    stray = sum(1 for y in range(Hc) for x in range(Wc) if is_yellow(cp[x, y]))
    print('check %s: yellow texels after bake %d' % (tag, stray))
print('baked')

# ---------------------------------------------------------------- take the Play plank off
bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table()
def off_post(c):
    return math.hypot(c.x-POST_CX, c.y-POST_CY) > POST_R
gone = [f for f in bm.faces
        if PLAY[0] <= f.calc_center_median().z <= PLAY[1] and off_post(f.calc_center_median())]
print('removing', len(gone), 'faces of the Play plank')
bmesh.ops.delete(bm, geom=gone, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(me); bm.free()
print('faces left', len(me.polygons))

# ---------------------------------------------------------------- write it out
for nm, im in (('base', BASE), ('nrm', NRM), ('rgh', RGH)):
    im.save(SCR + 'fs_%s.png' % nm)
mat = obj.data.materials[0]
bsdf = [n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0]
# ---- rebind ALL THREE, BY THE SOCKET THEY DRIVE ----
# The first version matched each image node to a file by comparing its corner pixel against the
# originals. Two of these three maps have a similar corner and it mis-assigned them: the normal
# map ended up on Base Color, which is why the planks came out with pale blue patches across
# them. Which socket a node feeds is a fact, not a guess -- walk it.
def feeder(sock):
    ls = [l for l in mat.node_tree.links if l.to_socket == sock]
    node = ls[0].from_node if ls else None
    while node is not None and node.type != 'TEX_IMAGE':
        ins = [l for l in mat.node_tree.links if l.to_node == node]
        node = ins[0].from_node if ins else None
    return node
for sock, path in (('Base Color', SCR+'fs_base.png'),
                   ('Roughness',  SCR+'fs_rgh.png'),
                   ('Normal',     SCR+'fs_nrm.png')):
    n = feeder(bsdf.inputs[sock])
    if n is None: print('no image behind', sock); continue
    n.image = bpy.data.images.load(path)
    if sock != 'Base Color': n.image.colorspace_settings.name = 'Non-Color'
    print('rebound', sock, '->', os.path.basename(path))

bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_yup=True,
    export_normals=True, export_texcoords=True, export_apply=True)
print('wrote', DST, os.path.getsize(DST)//1024, 'KB')
