import bpy, math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/pug.glb')
for o in bpy.context.scene.objects:
    if o.type=='ARMATURE': o.data.pose_position='REST'
bpy.context.view_layer.update()
sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=96; sc.cycles.use_denoising=False
sc.render.film_transparent=True
sc.render.resolution_x=1024; sc.render.resolution_y=1024
sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=0.55
w.node_tree.nodes['Background'].inputs[0].default_value=(0.85,0.9,1.0,1)
def light(name,kind,energy,loc,rot,size=None,col=(1,1,1)):
    l=bpy.data.lights.new(name,kind); l.energy=energy; l.color=col
    if size: l.size=size
    o=bpy.data.objects.new(name,l); sc.collection.objects.link(o); o.location=loc; o.rotation_euler=rot; return o
light('key','AREA',420,(2.2,-2.6,2.4),(math.radians(55),0,math.radians(40)),size=2.0,col=(1,0.95,0.85))
light('fill','AREA',200,(-2.6,-2.2,1.4),(math.radians(70),0,math.radians(-50)),size=3.0,col=(0.8,0.9,1))
light('rim','AREA',380,(0,2.5,2.2),(math.radians(-60),0,0),size=1.5,col=(1,0.8,0.6))
cam=bpy.data.cameras.new('c'); cam.lens=70
co=bpy.data.objects.new('cam',cam); sc.collection.objects.link(co); sc.camera=co
# head centre ~ (0,-0.1,1.25); shoot slightly from above, 3/4 left
tgt=Vector((0.0,-0.12,1.15))
d=Vector((-0.55,-1.0,0.28)).normalized()
co.location=tgt+d*2.95
co.rotation_euler=(Vector((0,0,0))).to_tuple()
co.rotation_mode='QUATERNION'
co.rotation_quaternion=(-d).to_track_quat('Z','Y')
co.rotation_quaternion=(d).to_track_quat('-Z','Y') if False else co.rotation_quaternion
# proper look-at: camera looks down its -Z
co.rotation_quaternion=(co.location-tgt).to_track_quat('Z','Y')
sc.render.filepath='/tmp/claude-0/icon/pug_head.png'
bpy.ops.render.render(write_still=True)
