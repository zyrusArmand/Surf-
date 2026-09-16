"""The bandana's cloth, drawn rather than downloaded.

A real bandana is not a tiled motif on a colour -- it is a BORDERED square: a wide patterned
band round the edge, a plain inner margin, and a field of motifs in the middle, all in two or
three inks. Drawing it that way rather than as wallpaper is most of what makes it read as a
bandana at a glance instead of as patterned fabric.

Colours are mine: a sun-faded ocean teal with a cream ink and a deep indigo shadow ink. Teal
because this is a surf game and the reference was in that family, and because against a tan pug
and a blue sea it is the fur it has to separate from, not the water.
"""
from PIL import Image, ImageDraw, ImageFilter
import math, random

S = 2048                      # the cloth is square; this is one whole bandana, not a tile
TEAL   = (26, 106, 106)
TEAL_D = (18,  78,  80)
CREAM  = (242, 233, 210)
INDIGO = (22,  48,  66)

random.seed(20260916)
img = Image.new('RGB', (S, S), TEAL)
d = ImageDraw.Draw(img)


def paisley(dr, cx, cy, r, rot, fill, ink, detail=True):
    """One teardrop with a curled tip, outlined and seeded -- the motif that says 'bandana'."""
    pts = []
    N = 160
    for i in range(N + 1):
        t = i / N * math.tau
        # a teardrop: a circle whose radius collapses toward one end, with the end swept over
        k = 0.5 - 0.5 * math.cos(t)
        rr = r * (0.28 + 0.72 * k)
        sweep = (1 - k) ** 2 * 1.45          # the hook
        a = t + sweep
        pts.append((cx + math.cos(a + rot) * rr * 0.78,
                    cy + math.sin(a + rot) * rr - r * 0.35 * (1 - k)))
    dr.polygon(pts, fill=fill, outline=ink)
    if not detail:
        return
    # the inner cartouche, a smaller copy of the same curve
    inner = [(cx + (x - cx) * 0.60, cy + (y - cy) * 0.60) for x, y in pts]
    dr.polygon(inner, fill=None, outline=ink)
    # seeds inside the body, thinning toward the tip
    for i in range(16):
        t = 0.18 + 0.66 * (i / 16)
        k = 0.5 - 0.5 * math.cos(t * math.tau)
        rr = r * (0.28 + 0.72 * k) * 0.34
        a = t * math.tau + (1 - k) ** 2 * 1.45
        px = cx + math.cos(a + rot) * rr * 0.78 * 1.4
        py = cy + math.sin(a + rot) * rr * 1.4 - r * 0.35 * (1 - k)
        s = r * 0.055 * (0.5 + k)
        dr.ellipse([px - s, py - s, px + s, py + s], fill=ink)


def floret(dr, cx, cy, r, fill, ink):
    for i in range(8):
        a = i / 8 * math.tau
        px, py = cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55
        dr.ellipse([px - r * 0.30, py - r * 0.30, px + r * 0.30, py + r * 0.30],
                   fill=fill, outline=ink)
    dr.ellipse([cx - r * 0.26, cy - r * 0.26, cx + r * 0.26, cy + r * 0.26], fill=ink)


# ---- the border: a wide patterned band, then a hairline, then the field ----
B = int(S * 0.145)
d.rectangle([0, 0, S, S], fill=TEAL)
d.rectangle([B * 0.30, B * 0.30, S - B * 0.30, S - B * 0.30], outline=CREAM, width=int(S*0.004))
d.rectangle([B, B, S - B, S - B], outline=CREAM, width=int(S * 0.010))
d.rectangle([B * 1.14, B * 1.14, S - B * 1.14, S - B * 1.14], outline=CREAM, width=int(S*0.0035))

# motifs marching round the band
n = 13
for side in range(4):
    for i in range(n):
        f = (i + 0.5) / n
        pos = B * 0.66 + f * (S - B * 1.32)
        cx, cy, rot = (pos, B * 0.65, -math.pi/2)
        if side == 1: cx, cy, rot = (S - B * 0.65, pos, 0)
        if side == 2: cx, cy, rot = (S - pos, S - B * 0.65, math.pi/2)
        if side == 3: cx, cy, rot = (B * 0.65, S - pos, math.pi)
        if i % 2 == 0: paisley(d, cx, cy, B * 0.30, rot, CREAM, TEAL_D, detail=False)
        else:          floret(d, cx, cy, B * 0.17, CREAM, TEAL_D)

# ---- the field: paisleys on a diagonal lattice, with florets between them ----
F0, F1 = B * 1.32, S - B * 1.32
step = (F1 - F0) / 4.0
for r_ in range(5):
    for c_ in range(5):
        cx = F0 + c_ * step + (step * 0.5 if r_ % 2 else 0)
        cy = F0 + r_ * step
        if cx > F1 + step * 0.1:
            continue
        j = step * 0.10
        cx += random.uniform(-j, j); cy += random.uniform(-j, j)
        paisley(d, cx, cy, step * 0.40, random.uniform(0, math.tau), CREAM, TEAL_D)
        floret(d, cx + step * 0.5, cy + step * 0.5, step * 0.10, CREAM, TEAL_D)

# ---- and it is cloth, not paper ----
# A flat fill reads as plastic under a light. A little weave and a little ink bleed is the whole
# difference at the size this is actually seen.
px = img.load()
for y in range(S):
    for x in range(0, S, 1):
        if (x + y) % 3 == 0:
            r, g, b = px[x, y]
            px[x, y] = (max(0, r - 6), max(0, g - 6), max(0, b - 6))
img = img.filter(ImageFilter.GaussianBlur(0.6))
img.save('bandana_col.png')
print("wrote bandana_col.png", img.size)
