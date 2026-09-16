"""Look at whatever is in the blend, from three sides, with the neck it has to fit drawn in."""
import bpy, math, sys, os
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
blend, out, ghost = sys.argv[-3], sys.argv[-2], sys.argv[-1]=='ghost'
bpy.ops.wm.open_mainfile(filepath=blend)
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=40
sc.cycles.use_denoising=True
sc.render.resolution_x=620; sc.render.resolution_y=740
if ghost:   # a stand-in neck, so the fit can be judged and not guessed
    # A NECK, not the old collider. The first ghost was a leftover of the simulation's chest
    # cone -- 1.30 radii where the bandana's own drape only reaches 1.22 -- so the model was
    # rendered INSIDE its stand-in and read as a bandana that had vanished.
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=0.93, depth=2.6, location=(0,0,1.0))
    n=bpy.context.active_object
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=20, radius=0.80,
                                         location=(0,-0.05,-1.55))
    c=bpy.context.active_object; c.scale=(1.05,0.95,1.30)
    m=bpy.data.materials.new("Ghost"); m.use_nodes=True
    b=m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value=(0.62,0.50,0.38,1); b.inputs["Roughness"].default_value=0.9
    n.data.materials.append(m); c.data.materials.append(m)
w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True
bg=[x for x in w.node_tree.nodes if x.type=="BACKGROUND"][0]
bg.inputs[0].default_value=(0.30,0.36,0.42,1); bg.inputs[1].default_value=1.1
for nm,loc,rot,en in [("K",(3,-4,4),(math.radians(52),0,math.radians(38)),4.0),
                      ("F",(-4,-2,1),(math.radians(80),0,math.radians(-60)),1.6)]:
    d=bpy.data.lights.new(nm,'SUN'); d.energy=en; d.angle=math.radians(12)
    o=bpy.data.objects.new(nm,d); o.rotation_euler=rot; sc.collection.objects.link(o)
pts=[o.matrix_world@Vector(c) for o in sc.objects if o.type=='MESH' for c in o.bound_box]
lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
ctr=(lo+hi)/2; R=max(hi-lo)
cam_d=bpy.data.cameras.new("C"); cam_d.lens=58
cam=bpy.data.objects.new("C",cam_d); sc.collection.objects.link(cam); sc.camera=cam
import tempfile
shots=[]
for i,(ang,tag) in enumerate([(-90,'front'),(-35,'three-quarter'),(20,'side')]):
    a=math.radians(ang)
    pos=ctr+Vector((math.cos(a)*R*2.0, math.sin(a)*R*2.0, R*0.42))
    cam.location=pos
    cam.rotation_euler=(ctr-pos).to_track_quat('-Z','Y').to_euler()
    p=f"{OUT}/_v{i}.png"; sc.render.filepath=p
    bpy.ops.render.render(write_still=True); shots.append(p)
    print("  rendered",tag,flush=True)
from PIL import Image
ims=[Image.open(p) for p in shots]
W=sum(i.width for i in ims); H=max(i.height for i in ims)
sheet=Image.new('RGB',(W,H),(30,36,42)); x=0
for i in ims: sheet.paste(i,(x,0)); x+=i.width
sheet.save(out); print("  sheet ->",out,flush=True)
