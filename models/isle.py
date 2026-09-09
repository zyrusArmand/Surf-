# The fork's island: one dune surface, built at a resolution that can actually hold dunes.
#
# The first version of this was a strip of 64 sections with FIVE vertices across it -- twenty
# feet between vertices on a surface you stand on -- so it had no relief of its own at all, and
# the relief had to come from a field of six hundred overlapping scanned tiles laid on top. That
# looked like cracked plates at any range you could actually see it from, and cost 640k
# triangles. This is 22k for the whole island, it has the dunes IN it, and it can never show a
# seam because there is nothing to seam.
#
# Plan and heights come from the game's own constants, so the model and the collision agree.
# Z is up here (Blender) and the exporter turns it into Y.
import bpy, bmesh, math, os

HALF, OPEN, STRAIGHT, SPLAY = 6.4, 95.0, 70.0, 0.30
LEN   = OPEN + STRAIGHT
# ---- how high it STANDS OUT of the sea ----
# 2.05 was measured against nothing. The swell at the fork's own distance runs to about 0.7 ft
# and grows through a run, and the island's TIP -- which is where he actually comes ashore --
# was only three tenths of the crown, so he landed on sand about a foot above mean water and
# the sea washed straight over the top of it. It is an island: it wants to be plainly out of
# the water everywhere you can stand on it, with a couple of feet in hand for the biggest
# swell the run ever builds.
CROWN = 4.20          # how far the middle stands above mean water
TIP   = 0.72          # ...and how much of that the point already has, where he comes ashore
RIM   = -3.20         # how far the edge runs under it, so the waterline is on the model
# ---- where the beach FACE is, as a fraction of the half-width ----
# The island is static and the sea is not, so the waterline walks up and down the sand as the
# swell passes. How far it walks is set entirely by how steep the sand is where it meets the
# water: on the first version the outer half was nearly flat, so a foot of swell flooded fifteen
# feet of beach and then drained it, and the sea read as a sheet of blue sliding about on top of
# the island. Dropping the whole of the crown-to-rim height across a third of the half-width
# instead puts the face at about one in two and a half -- a steep little beach, but the swell
# moves the line under three feet on it, and that reads as surf rather than as flooding.
# Widened from 0.62/0.95. That put the whole crown-to-rim drop across a third of the half-width
# -- about one in two and a half, which is a cliff with sand on it rather than a beach, and it
# looked like one. Over seven tenths of the half-width instead it is about one in four: a long
# curved shore that rolls into the water. The swell then walks further up and down it, which is
# what surf is; what matters is that the PLATEAU stays dry, and at a crown of 4.2 against a sea
# of 0.75 it has three feet in hand.
FACE0, FACE1 = 0.34, 1.00
NZ, NX = 220, 62      # ~27k triangles, about 0.75 ft between vertices across the arms

# ---- smooth value noise, so the dunes are dunes and not static ----
# random.uniform per vertex was what the first version used and it gives white noise: every
# vertex an independent spike, which at this resolution is sandpaper rather than sand.
def h2(i, j, s):
    n = math.sin(i * 127.1 + j * 311.7 + s * 74.7) * 43758.5453
    return n - math.floor(n)
def noise(x, y, s=0.0):
    i, j = math.floor(x), math.floor(y)
    fx, fy = x - i, y - j
    ux, uy = fx*fx*(3-2*fx), fy*fy*(3-2*fy)
    a, b = h2(i, j, s), h2(i+1, j, s)
    c, d = h2(i, j+1, s), h2(i+1, j+1, s)
    return (a*(1-ux)+b*ux)*(1-uy) + (c*(1-ux)+d*ux)*uy - 0.5
def fbm(x, y, s=0.0):
    return noise(x, y, s) + 0.5*noise(x*2.07, y*2.07, s+1)

def outer(t):
    """half-width of the island at t along its length -- the game's own wall arithmetic"""
    run = min(OPEN, t*LEN)
    return HALF*0.5 + math.tan(SPLAY)*run + HALF*min(1.0, t*8)

bm = bmesh.new()
ring = []
for j in range(NZ+1):
    t = j/NZ
    y = t*LEN
    e = outer(t)
    # the crown is low at the point and full height once the island has opened
    crown = CROWN*(TIP + (1.0-TIP)*min(1.0, t*4))
    row = []
    for i in range(NX+1):
        u = i/NX
        k = u*2 - 1                       # -1 at one rim, +1 at the other
        x = k*e
        # ---- the cross section ----
        # Flat through the middle and rolling over to the water at the rim, rather than a dome:
        # a dome has no beach on it, and the whole of the near half of this is beach.
        a = abs(k)
        # flat-ish top with a little camber, then the face
        if a <= FACE0:
            prof = 1.0 - 0.10*(a/FACE0)**2
        else:
            w = min(1.0, (a-FACE0)/(FACE1-FACE0))
            prof = 0.90*(1.0 - w*w*(3-2*w))
        # ---- and the near END rises out of the sea rather than off a cliff ----
        # At t=0 the island stood at 72 per cent of its crown already, so its first cross section
        # was three feet of sand with a skirt dropping straight to fourteen under it: a little
        # rectangular bluff sticking out of the water where the beach should start.
        # Only the part ABOVE water is ramped. Scaling the whole span from the rim was the first
        # try and it put the entire tip UNDERWATER -- he then ran aground on a submerged bar and
        # stood in the sea, because the beaching triggers at the leading edge whatever is there.
        # Ramping from the waterline instead leaves the underwater shape alone, brings the tip in
        # at exactly sea level, and lifts it into a beach over the first twenty feet.
        base = RIM + (crown - RIM)*prof
        em = min(1.0, t/0.12)
        em = 0.06 + 0.94*em*em*(3-2*em)
        z = base if base <= 0.0 else base*em
        # how far above the water this point is, which is what decides how much dune it gets:
        # sand is rippled where it is dry and the sea irons it flat where it has been over it
        up = max(0.0, min(1.0, (z + 0.15)/CROWN))
        # ---- and NOTHING FINER THAN THE VERTEX GRID ----
        # The first pass had three octaves of noise at three scales, the smallest of them a
        # two-foot ripple. The grid is 0.75 ft along the island and 1.26 ft across it at the
        # widest, so that ripple had barely three vertices to a period: it could not be a
        # ripple, it came out as zigzag facets, and the material's sheen picked every one of
        # them out as a dark chevron. The whole near beach was covered in them. Shading them
        # flat hid it, which is what made it look like a texture problem for a while -- it is
        # not, the geometry really is that shape.
        # Twenty-two feet and nine feet, both well sampled. Anything smaller than that belongs
        # in the normal map, which is where the beach's grain has always lived.
        # ---- and it has HILLS in it, not a whisper ----
        # 0.70 and 0.24 of a foot across a seventy-foot island is a surface you have to be told
        # is not flat. The menu beach reads as sand largely because it has real relief at the
        # scale of a few paces -- little dunes you walk over and round -- and that is the thing
        # this was missing. Two and a half feet at twenty-two feet across, most of a foot at
        # nine, both still damped to nothing at the waterline where the sea irons sand flat.
        z += fbm(x*0.045, y*0.045, 0.0)*1.70*up
        z += noise(x*0.11, y*0.11, 3.0)*0.62*up
        # ...and the rim wanders, so the waterline is not a drawn curve
        row.append(bm.verts.new((x + fbm(0.0, y*0.06, 9.0)*1.5*(1-up), y, z)))
    ring.append(row)
for j in range(NZ):
    a, b = ring[j], ring[j+1]
    for i in range(NX):
        try: bm.faces.new((a[i], a[i+1], b[i+1], b[i]))
        except ValueError: pass

# ---- AND IT HAS A BOTTOM ----
# The island was an open surface: a single sheet of sand with nothing underneath it and nothing
# closing its ends. From a low camera at the near end you looked straight in under the rim and
# out the other side, which is the little hole at the front of the beach. A skirt is dropped
# from the whole boundary to well below the sea, so there is no angle that sees through it.
SKIRT = -14.0
def skirt(a, b):
    lo_a = bm.verts.new((a.co.x, a.co.y, SKIRT))
    lo_b = bm.verts.new((b.co.x, b.co.y, SKIRT))
    try: bm.faces.new((a, b, lo_b, lo_a))
    except ValueError: pass
for j in range(NZ):                      # the two long shores
    skirt(ring[j][0],  ring[j+1][0])
    skirt(ring[j+1][NX], ring[j][NX])
for i in range(NX):                      # the point, and the far end
    skirt(ring[0][i+1], ring[0][i])
    skirt(ring[NZ][i],  ring[NZ][i+1])

me = bpy.data.meshes.new('isle'); bm.to_mesh(me); bm.free()
bpy.ops.wm.read_factory_settings(use_empty=True) if False else None
for o in list(bpy.context.scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)
obj = bpy.data.objects.new('isle', me)
bpy.context.scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.shade_smooth()
me.materials.clear()
print('isle verts', len(me.vertices), 'faces', len(me.polygons))
DST = '/home/user/Surf-/models/isle.glb'
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_image_format='NONE',
    export_yup=True, export_normals=True, export_texcoords=False, export_apply=True)
print('wrote', DST, os.path.getsize(DST)//1024, 'KB')
