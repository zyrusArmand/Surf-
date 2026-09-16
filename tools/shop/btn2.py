"""Convert the bar, and MEASURE where its four buttons sit rather than splitting it.

Separating by loose parts does not work: decimating a 344k scan shatters it into 899 shells.
It is also unnecessary. The mesh can stay whole -- what the game needs is the four RECTANGLES,
to hang a hit area and a light on each, and those are findable. The buttons stand proud of the
backing board, so the frontmost layer of vertices is the buttons and nothing else, and a
histogram of that layer in X has four clusters in it.
"""
import bpy, math, os, sys
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
SRC="/root/.claude/uploads/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/d70863d0-Shopbuttons.glb"
TARGET,TEX=int(sys.argv[-2]),int(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc=bpy.context.scene
objs=[o for o in sc.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active=objs[0]
if len(objs)>1: bpy.ops.object.join()
ob=bpy.context.active_object; ob.name="buttons"

pts=[ob.matrix_world@v.co for v in ob.data.vertices]
ylo=min(p.y for p in pts); yhi=max(p.y for p in pts)
xlo=min(p.x for p in pts); xhi=max(p.x for p in pts)
zlo=min(p.z for p in pts); zhi=max(p.z for p in pts)
# the front of the bar: in Blender the depth is Y, and the buttons face -Y (as the shop does)
front=[p for p in pts if p.y < ylo + (yhi-ylo)*0.22]
NB=76
hist=[0]*NB
for p in front:
    hist[min(NB-1,int((p.x-xlo)/(xhi-xlo)*NB))]+=1
peak=max(hist)
print(f"  bar  X {xlo:+.3f}..{xhi:+.3f}   Z {zlo:+.3f}..{zhi:+.3f}   depth {yhi-ylo:.3f}")
print(f"  frontmost layer: {len(front)} of {len(pts)} verts")
print("  x-histogram of the front face (each column is a bin):")
for r in range(6,0,-1):
    print("   "+"".join('#' if h>peak*r/7 else ' ' for h in hist))
print("   "+"".join('-' for _ in hist))
# find the clusters
thr=peak*0.30
runs=[]; s=None
for i,h in enumerate(hist):
    if h>thr and s is None: s=i
    elif h<=thr and s is not None: runs.append((s,i-1)); s=None
if s is not None: runs.append((s,NB-1))
runs=[r for r in runs if r[1]-r[0]>=2]
print(f"  {len(runs)} clusters:")
for a,b in runs:
    x0=xlo+(xhi-xlo)*a/NB; x1=xlo+(xhi-xlo)*(b+1)/NB
    print(f"     x {x0:+.3f}..{x1:+.3f}   centre {(x0+x1)/2:+.3f}   width {x1-x0:.3f}")

d=ob.modifiers.new("Decimate",'DECIMATE'); d.decimate_type='COLLAPSE'
n0=len(ob.data.polygons)
d.ratio=min(1.0,TARGET/max(1,n0))
for im in bpy.data.images:
    if not im.size[0]: continue
    if max(im.size)>TEX:
        w0,h0=im.size; im.scale(TEX,int(TEX*h0/w0))
    im.file_format='JPEG'
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.gltf(filepath=f"{OUT}/shopbuttons.glb",export_format='GLB',
                          use_selection=True,export_apply=True,
                          export_image_format='JPEG',export_jpeg_quality=86)
print(f"  wrote shopbuttons.glb  {os.path.getsize(f'{OUT}/shopbuttons.glb')/1024/1024:.2f} MB")
