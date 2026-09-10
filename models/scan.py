import bpy, os, sys, io
SRC=sys.argv[1]; OUT=sys.argv[2]; TARGET=int(sys.argv[3])
TEX=int(sys.argv[4]) if len(sys.argv)>4 else 1024
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

# ---- one photogrammetry scan in, one cheap textured glb out ----
#   python3 models/scan.py <source>.glb <out>.glb <tris> [<colour map px>]
# Every scan that has come through here arrives the same way: quantised positions, one material
# carrying base colour + metallic/roughness + normal, TEXCOORD_0 already on the mesh, and the
# whole thing split into primitives by the 65535-vertex limit. So there is nothing to unwrap and
# nothing to bake -- decimate, shrink the maps, ship it.
#
# ---- JOIN FIRST ----
# The first pack through here was one mesh and this script took [0] and got the whole model.
# The second was thirteen primitives, so [0] was a thirteenth of a jetpack -- and decimating a
# thirteenth to the whole model's budget produces a perfectly clean object that is one slice of
# the thing you wanted, with nothing anywhere saying so. Everything is joined into one mesh
# before anything else touches it.
ms=[x for x in bpy.data.objects if x.type=='MESH']
print("meshes in", len(ms), "tris in", sum(sum(len(p.vertices)-2 for p in m.data.polygons) for m in ms))
bpy.context.view_layer.objects.active=ms[0]
bpy.ops.object.select_all(action='DESELECT')
for m in ms: m.select_set(True)
if len(ms)>1: bpy.ops.object.join()
o=bpy.context.view_layer.objects.active
# the scan arrives under a node scale; bake it in so the exported units are the model's own
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0002)
bpy.ops.object.mode_set(mode='OBJECT')
nt=sum(len(p.vertices)-2 for p in o.data.polygons)
print("merged", nt, "uv layers", [l.name for l in o.data.uv_layers])

# ---- and the PAINT JOB stays ----
# There is nothing to bake here and there never was: the scan ships one material carrying a
# base colour, a metallic/roughness map and a normal map, and every primitive already has
# TEXCOORD_0. A collapse decimate carries UVs through, so the whole job is to make the mesh
# cheap and the images small and then get out of the way. (An earlier pass threw all three away
# and painted it flat white, which was asked for at the time and is not what it wears now.)
m=o.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,TARGET/float(nt))
bpy.ops.object.modifier_apply(modifier=m.name)
print("out tris", sum(len(p.vertices)-2 for p in o.data.polygons), "verts", len(o.data.vertices))

bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
try: bpy.ops.object.shade_auto_smooth(angle=0.52)
except Exception: bpy.ops.object.shade_smooth()

# (The maps are shrunk AFTER the export, below. Doing it here through bpy.data.images looked
# right and silently did nothing -- the loop printed not one line against three 2048s that a
# separate check found present, loaded and packed -- and a packed image re-exports from its
# packed bytes anyway, so scaling the pixels would not have shrunk the file even if it had run.)
for mt in o.data.materials: print("  mat", mt.name if mt else None)
d=o.dimensions
print("dims x %.4f y %.4f z %.4f" % (d.x, d.y, d.z))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', export_yup=True,
                          export_skins=False, export_animations=False)
print("wrote", OUT, os.path.getsize(OUT))

# ---- and the MAPS come down, in the file rather than in Blender ----
# Three 2048s, five megabytes between them, on a prop worn at about a foot across thirty feet
# down in fog. The colour keeps the most because it is the only one whose detail survives being
# that small; the metallic/roughness and the normal are carrying surface at a scale this
# silhouette cannot show. Done on the finished GLB with PIL: the bufferViews are rebuilt in
# order with their offsets recomputed, which is a thing this file can be sure it has done.
from PIL import Image
import struct, json as _json

def shrink(path, base=1024, aux=512, q=88):
    d=open(path,'rb').read()
    off=12; js=None; binc=None
    while off<len(d):
        ln,ty=struct.unpack_from('<II',d,off); off+=8
        ch=d[off:off+ln]; off+=ln
        if ty==0x4E4F534A: js=_json.loads(ch)
        else: binc=bytearray(ch)
    g=js
    # which bufferView is the COLOUR, asked of the material rather than of the arrival order
    baseIdx=None
    for mt in g.get('materials',[]):
        t=(mt.get('pbrMetallicRoughness') or {}).get('baseColorTexture')
        if t is not None: baseIdx=g['textures'][t['index']]['source']
    repl={}
    for i,im in enumerate(g.get('images',[])):
        if 'bufferView' not in im: continue
        bv=g['bufferViews'][im['bufferView']]
        o=bv.get('byteOffset',0)
        raw=bytes(binc[o:o+bv['byteLength']])
        img=Image.open(io.BytesIO(raw)); img.load()
        lim=base if i==baseIdx else aux
        if max(img.size)>lim:
            k=lim/float(max(img.size))
            img=img.resize((max(1,int(img.size[0]*k)),max(1,int(img.size[1]*k))),Image.LANCZOS)
        out=io.BytesIO(); img.convert('RGB').save(out,'JPEG',quality=q,optimize=True)
        repl[im['bufferView']]=out.getvalue()
        im['mimeType']='image/jpeg'
        print("  image",i,"colour" if i==baseIdx else "aux",img.size,
              len(raw),"->",len(repl[im['bufferView']]))
    if not repl: return
    order=sorted(range(len(g['bufferViews'])), key=lambda k: g['bufferViews'][k].get('byteOffset',0))
    nb=bytearray()
    for k in order:
        bv=g['bufferViews'][k]
        o=bv.get('byteOffset',0)
        data=repl.get(k) or bytes(binc[o:o+bv['byteLength']])
        while len(nb)%4: nb.append(0)
        bv['byteOffset']=len(nb); bv['byteLength']=len(data)
        nb+=data
    while len(nb)%4: nb.append(0)
    g['buffers'][0]['byteLength']=len(nb)
    jb=_json.dumps(g,separators=(',',':')).encode('utf-8')
    while len(jb)%4: jb+=b' '
    glb=b'glTF'+struct.pack('<II',2,12+8+len(jb)+8+len(nb))
    glb+=struct.pack('<II',len(jb),0x4E4F534A)+jb
    glb+=struct.pack('<II',len(nb),0x004E4942)+bytes(nb)
    open(path,'wb').write(glb)

shrink(OUT, TEX, max(256,TEX//2))
print("shrunk", OUT, os.path.getsize(OUT))
