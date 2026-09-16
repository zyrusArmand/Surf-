"""Photograph the pug in it -- framed on the whole animal, not his nostril."""
import bpy, math, sys, os
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=sys.argv[-2])
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=40
sc.cycles.use_denoising=True
sc.render.resolution_x=460; sc.render.resolution_y=620
w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True
bg=[x for x in w.node_tree.nodes if x.type=="BACKGROUND"][0]
bg.inputs[0].default_value=(0.44,0.54,0.62,1); bg.inputs[1].default_value=1.3
for nm,rot,en in [("K",(math.radians(56),0,math.radians(30)),3.4),
                  ("F",(math.radians(78),0,math.radians(-70)),1.2)]:
    d=bpy.data.lights.new(nm,'SUN'); d.energy=en; d.angle=math.radians(14)
    o=bpy.data.objects.new(nm,d); o.rotation_euler=rot; sc.collection.objects.link(o)
# ---- FRAME THE WHOLE ANIMAL ----
# 70mm at 3.1 units showed 1.6 units of a 2.6-unit pug: a close-up of his face with the
# bandana cropped, which read as a giant cowl when it is nothing of the sort.
# FRAMED EXPLICITLY. Computing it from the scene bounds put the pug at the size of a thumbnail
# with a hairline across the frame: something in the imported hierarchy reports a bounding box
# it does not occupy, and a camera solved from that is solving for the wrong thing. The animal
# is a known size -- he is right there, 2.7 units tall -- so the shot is set from that.
ctr=Vector((0,0,0.92)); H=2.55
cam_d=bpy.data.cameras.new("C"); cam_d.lens=50
cam=bpy.data.objects.new("C",cam_d); sc.collection.objects.link(cam); sc.camera=cam
FOV=2*math.atan(18.0/cam_d.lens)
D=(H*0.62)/math.tan(FOV/2)
shots=[]
for i,ang in enumerate([-90,-55,-15]):
    a=math.radians(ang)
    pos=ctr+Vector((math.cos(a)*D, math.sin(a)*D, H*0.16))
    cam.location=pos
    cam.rotation_euler=(ctr-pos).to_track_quat('-Z','Y').to_euler()
    p=f"{OUT}/_s{i}.png"; sc.render.filepath=p
    bpy.ops.render.render(write_still=True); shots.append(p)
    print("  shot",i,flush=True)
from PIL import Image
ims=[Image.open(p) for p in shots]
sheet=Image.new('RGB',(sum(i.width for i in ims),max(i.height for i in ims)),(30,36,42))
x=0
for i in ims: sheet.paste(i,(x,0)); x+=i.width
sheet.save(sys.argv[-1]); print("  sheet ->",sys.argv[-1],flush=True)
