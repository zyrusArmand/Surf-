# Turns a photographed line drawing into a transparent PNG the game can use as a CSS mask.
#   python3 icons/make.py <name> <image.jpg> [more pairs...]
# The alpha channel is the artwork; the colour is thrown away, because every place these are
# used paints them with `background:currentColor` through `mask-image`. That is what lets one
# file be brown ink on a paper note, white on a dark panel, and gold when a quest is done.
import sys, os
from PIL import Image
SIZE = 160
def build(name, src, out_dir):
    im = Image.open(src).convert("L")
    w, h = im.size
    px = im.load()
    # The paper is not white -- these are photographs -- and it is the brightest thing in frame,
    # so the cut is taken from the image's own histogram. The alpha ramps across it rather than
    # snapping, which is what stops a one-pixel stroke disappearing.
    paper = max(range(256), key=lambda i: im.histogram()[i])
    lo, hi = int(paper * 0.45), int(paper * 0.82)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0)); o = out.load()
    x0, y0, x1, y1 = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            v = px[x, y]
            if v >= hi: continue
            a = 255 if v <= lo else int(255 * (hi - v) / max(1, hi - lo))
            if a < 8: continue
            o[x, y] = (20, 20, 22, a)
            x0, y0 = min(x0, x), min(y0, y); x1, y1 = max(x1, x), max(y1, y)
    if x1 < 0: raise SystemExit("no ink found in " + src)
    out = out.crop((x0, y0, x1 + 1, y1 + 1))
    # squared and padded, so every icon sits on the same baseline whatever shape it is
    cw, ch = out.size
    side = max(cw, ch); side += int(side * 0.06) * 2
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(out, ((side - cw) // 2, (side - ch) // 2), out)
    sq.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(out_dir, name + ".png"))
    print("%-9s %dx%d -> %s.png" % (name, cw, ch, name))
if __name__ == "__main__":
    d = os.path.dirname(os.path.abspath(__file__))
    a = sys.argv[1:]
    if len(a) < 2 or len(a) % 2: raise SystemExit(__doc__ or "need <name> <image> pairs")
    for i in range(0, len(a), 2): build(a[i], a[i + 1], d)
