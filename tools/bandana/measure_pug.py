"""Measure the actual pug: where its neck is, how wide, and how the head sits on the chest.

The fit was guessed from a fraction of the skull box because the game has no neck to ask. In
Blender the animal is right there, so it can be asked directly -- and a neck is findable
without knowing anything about the rig: walk up the body in slices and the neck is the WAIST
between two bulges, the narrowest ring between the chest below and the skull above.
"""
import bpy, math, sys
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
sc=bpy.context.scene
deps=bpy.context.evaluated_depsgraph_get()
pts=[]
for o in sc.objects:
    if o.type!='MESH': continue
    m=o.evaluated_get(deps).to_mesh()
    for v in m.vertices: pts.append(o.matrix_world @ v.co)
    o.evaluated_get(deps).to_mesh_clear()
print(f"  {len(pts)} vertices")
lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
print(f"  bounds  X {lo.x:+.3f}..{hi.x:+.3f}   Y {lo.y:+.3f}..{hi.y:+.3f}   Z {lo.z:+.3f}..{hi.z:+.3f}")
up='Z' if (hi.z-lo.z)>=max(hi.x-lo.x,hi.y-lo.y) else ('Y' if (hi.y-lo.y)>=(hi.x-lo.x) else 'X')
print(f"  tallest axis: {up}")
# slice up the tall axis and report the horizontal half-width of each ring
gi={'X':0,'Y':1,'Z':2}[up]
a,b=[lo,hi][0][gi],[lo,hi][1][gi]
N=44
print("  slice   height      half-width   (ring radius about the body's own centre)")
prof=[]
for i in range(N):
    t0=a+(b-a)*i/N; t1=a+(b-a)*(i+1)/N
    ring=[p for p in pts if t0<=p[gi]<t1]
    if len(ring)<12: prof.append(None); continue
    ox=sum(p[(gi+1)%3] for p in ring)/len(ring); oy=sum(p[(gi+2)%3] for p in ring)/len(ring)
    r=sorted(math.hypot(p[(gi+1)%3]-ox,p[(gi+2)%3]-oy) for p in ring)
    prof.append((t0, r[int(len(r)*0.86)], len(ring)))
for i,pr in enumerate(prof):
    if pr and i>N*0.45:
        bar='#'*int(pr[1]/max(x[1] for x in prof if x)*46)
        print(f"   {i:2d}   {pr[0]:+7.3f}   {pr[1]:6.4f}  {bar}")
