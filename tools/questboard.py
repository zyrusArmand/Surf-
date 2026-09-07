import bpy, bmesh, math, os, random
from mathutils import Vector, Euler

random.seed(7)
D=bpy.data; C=bpy.context

# ---- clean slate ----
bpy.ops.wm.read_factory_settings(use_empty=True)

# The reference is a 703 x 1500 painting. Everything below is written in ITS pixels and mapped
# through px()/pz() once, so the layout can be read straight off the picture instead of being
# guessed in world units and re-guessed every time something moves.
IW, IH = 703.0, 1500.0
BOARD_TOP, BOARD_BOT = 130.0, 1290.0      # top of the poles, bottom of the feet
H = 8.0                                    # feet tall in game, and the game's unit IS the foot
S = H/(BOARD_BOT-BOARD_TOP)
CX = 355.0
def px(v): return (v-CX)*S
def pz(v): return (BOARD_BOT-v)*S
def pw(v): return v*S

# ---------- materials ----------
def mat(name, rgb, rough=0.72, spec=0.35):
    m=D.materials.new(name); m.use_nodes=True
    b=m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value=(rgb[0],rgb[1],rgb[2],1)
    b.inputs['Roughness'].default_value=rough
    if 'Specular IOR Level' in b.inputs: b.inputs['Specular IOR Level'].default_value=spec
    return m
def hx(s):
    s=s.lstrip('#'); v=[int(s[i:i+2],16)/255.0 for i in (0,2,4)]
    return [c**2.2 for c in v]          # the picture is sRGB, the shader wants linear

M = {
 'bamboo':   mat('bamboo',   hx('C79A5C'), .62),
 'bamboo_d': mat('bamboo_d', hx('A87C44'), .70),
 'rope':     mat('rope',     hx('C6A46A'), .85),
 'teal':     mat('teal',     hx('7FA6A3'), .68),
 'plank':    mat('plank',    hx('7A5433'), .78),
 'plank_d':  mat('plank_d',  hx('5E3E24'), .80),
 'logwall':  mat('logwall',  hx('A8845A'), .78),
 'sign':     mat('sign',     hx('6E3F22'), .70),
 'letter':   mat('letter',   hx('F3CD86'), .55),
 'paper':    mat('paper',    hx('EFE0BE'), .88),
 'rule':     mat('rule',     hx('B4915C'), .88),
 'star':     mat('star',     hx('E2703A'), .60),
 'shell':    mat('shell',    hx('F3DCCB'), .48),
 'shell2':   mat('shell2',   hx('E9BCA9'), .48),
 'foot':     mat('foot',     hx('B98A55'), .74),
}
PIN = {n: mat('pin_'+n, hx(c), .18, .9) for n,c in
       [('red','D93A32'),('yellow','E8C020'),('blue','3AA0C8'),
        ('purple','8A3FBF'),('teal','2FA8C4')]}

parts=[]
def keep(o, m, smooth=False):
    o.data.materials.append(m)
    if smooth: bpy.ops.object.shade_smooth()
    parts.append(o); return o

def box(x0,x1,z0,z1,y0,y1,m,name='box',bev=0.0,seg=2):
    bpy.ops.mesh.primitive_cube_add(size=2)
    o=C.object; o.name=name
    o.scale=((x1-x0)/2,(y1-y0)/2,(z1-z0)/2)
    o.location=((x0+x1)/2,(y0+y1)/2,(z0+z1)/2)
    bpy.ops.object.transform_apply(scale=True)
    if bev>0:
        b=o.modifiers.new('b','BEVEL'); b.width=bev; b.segments=seg; b.limit_method='ANGLE'
    return keep(o,m,name)

def cyl(x,z,y,r,h,m,name='cyl',axis='Z',v=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=v,radius=r,depth=h,location=(x,y,z))
    o=C.object; o.name=name
    if axis=='X': o.rotation_euler=Euler((0,math.pi/2,0))
    if axis=='Y': o.rotation_euler=Euler((math.pi/2,0,0))
    bpy.ops.object.transform_apply(rotation=True)
    return keep(o,m,True)

def torus(x,z,y,R,r,m,name='t',rot=(0,0,0),maj=20,mnr=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=R,minor_radius=r,location=(x,y,z),
                                     major_segments=maj,minor_segments=mnr,rotation=rot)
    o=C.object; o.name=name
    return keep(o,m,True)

# ---------- the two uprights, and the rails ----------
# Bamboo is a stack of segments with a swollen ring at every node. Modelling the ring rather
# than painting it is what makes a cylinder read as bamboo at a glance, and it is four hundred
# triangles a pole.
def bamboo(x, z0, z1, r, name):
    cyl(x,(z0+z1)/2,0,r,z1-z0,M['bamboo'],name,'Z',18)
    n=max(2,int((z1-z0)/pw(150)))
    for i in range(1,n):
        z=z0+(z1-z0)*i/n
        torus(x,z,0,r*0.98,r*0.20,M['bamboo_d'],name+'_node',maj=18,mnr=8)

RP = pw(23)                                  # pole radius, from the painting
bamboo(px(85),  pz(1238), pz(132), RP, 'pole_L')
bamboo(px(624), pz(1238), pz(132), RP, 'pole_R')

# horizontal rails: the top one is the weathered teal driftwood the sign hangs on
def rail(z, x0, x1, r, m, name):
    cyl((x0+x1)/2, z, 0, r, x1-x0, m, name, 'X', 18)
    n=max(2,int((x1-x0)/pw(190)))
    for i in range(1,n):
        x=x0+(x1-x0)*i/n
        torus(x,z,0,r*0.98,r*0.18,m,name+'_node',rot=(0,math.pi/2,0),maj=18,mnr=8)

rail(pz(232), px(18),  px(692), pw(35), M['teal'],   'rail_top')
rail(pz(1132), px(35), px(680), pw(27), M['bamboo'], 'rail_bot')

# ---------- lashings ----------
# Rope where a rail crosses a pole, plus the X of cord over the teal rail. Two tori crossed at
# forty degrees reads as a wrap from any angle a menu camera will ever see it from.
def lash(x,z,R,name):
    for a in (0.62,-0.62):
        torus(x,z,0,R,pw(7),M['rope'],name,rot=(a,0,0),maj=20,mnr=8)
    torus(x,z,0,R*0.96,pw(6),M['rope'],name,rot=(0,0,0),maj=20,mnr=8)
for xx in (px(85),px(624)):
    lash(xx,pz(232),pw(34),'lash_top')
    lash(xx,pz(1132),pw(31),'lash_bot')

# ---------- the plank wall behind the notices ----------
PL_X0,PL_X1 = px(95), px(618)
PL_Z0,PL_Z1 = pz(1110), pz(275)
YB0,YB1 = pw(-16), pw(6)                     # the wall sits behind the frame's centre line
box(PL_X0,PL_X1,PL_Z0,PL_Z1,YB1-pw(2),YB1+pw(9),M['plank_d'],'wall_back')
nrow=7
for i in range(nrow):
    z0=PL_Z0+(PL_Z1-PL_Z0)*i/nrow; z1=PL_Z0+(PL_Z1-PL_Z0)*(i+1)/nrow
    box(PL_X0,PL_X1,z0+pw(2.6),z1-pw(2.6),YB0,YB1,
        M['plank'] if i%2 else M['plank_d'],'wall_%d'%i,bev=pw(1.6),seg=1)
# the stacked log-ends down the left edge of the wall
for i in range(11):
    z=PL_Z0+(PL_Z1-PL_Z0)*(i+0.5)/11
    cyl(px(120), z, pw(7), pw(23), pw(48), M['logwall'], 'logend', 'Y', 12)

# ---------- feet ----------
for sx in (-1,1):
    x=CX+sx*270
    box(px(x-52),px(x+52),pz(1290),pz(1232),pw(-26),pw(26),M['foot'],'foot',bev=pw(5),seg=2)
    box(px(x-16),px(x+16),pz(1236),pz(1180),pw(-13),pw(13),M['foot'],'ankle',bev=pw(4),seg=1)

# ---------- the sign ----------
SG_X0,SG_X1 = px(172), px(552)
SG_Z0,SG_Z1 = pz(268), pz(133)
SY0,SY1 = pw(-40), pw(-18)
box(SG_X0,SG_X1,SG_Z0,SG_Z1,SY0,SY1,M['sign'],'signplank',bev=pw(7),seg=3)
# a shallower lip around it, so the face is a panel rather than a slab
box(SG_X0+pw(9),SG_X1-pw(9),SG_Z0+pw(9),SG_Z1-pw(9),SY0-pw(5),SY0+pw(1),
    M['sign'],'signface',bev=pw(4),seg=2)

FONT=None
for f in ('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',):
    if os.path.exists(f): FONT=D.fonts.load(f); break

def carve(body, cx_px, cy_px, size_px, name):
    bpy.ops.object.text_add(location=(0,0,0))
    o=C.object; o.name=name; o.data.body=body
    if FONT: o.data.font=FONT
    o.data.align_x='CENTER'; o.data.align_y='CENTER'
    o.data.size=pw(size_px)
    o.data.extrude=pw(7); o.data.bevel_depth=pw(1.6); o.data.bevel_resolution=1
    o.rotation_euler=Euler((math.pi/2,0,0))
    o.location=(px(cx_px), SY0-pw(4), pz(cy_px))
    bpy.ops.object.convert(target='MESH')
    return keep(C.object, M['letter'], True)

carve('SURF',   378, 182, 62, 'w_surf')
carve('QUESTS', 362, 234, 52, 'w_quests')

# the two little marks that flank the words: a curl of wave, and a palm
torus(px(233), pz(203), SY0-pw(4), pw(26), pw(7), M['letter'], 'wave',
      rot=(math.pi/2,0,0), maj=22, mnr=7)
torus(px(233), pz(212), SY0-pw(4), pw(13), pw(6), M['letter'], 'wave2',
      rot=(math.pi/2,0,0), maj=18, mnr=7)
cyl(px(492), pz(218), SY0-pw(4), pw(4.5), pw(46), M['letter'], 'palmtrunk','Z',8)
for a in (-2.05,-1.15,0.0,1.15,2.05):
    # Flattened and widened, because five thin cones fanned round a point is an asterisk. A
    # frond is a broad blade that hangs, so each one is squashed on the sign's normal and given
    # an arc away from the crown.
    L=pw(36)
    bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=pw(11), radius2=0, depth=L,
        location=(px(492)+math.sin(a)*L*0.46, SY0-pw(4), pz(192)+math.cos(a)*L*0.46))
    o=C.object; o.name='frond'
    o.rotation_euler=Euler((0,a,0)); o.scale=(1.0,0.42,1.0)
    bpy.ops.object.transform_apply(scale=True)
    keep(o, M['letter'], True)

# ---------- the notices ----------
# Kept as five separate objects on purpose. The quest text will be laid over them the same way
# the shop's three lit panels are: DOM in front, projected from the quad's own world matrix, so
# the words stay in register with the paper however the board ends up framed.
NOTES=[ (118,315,338,560,  -3.4, 'red',    214,321),
        (372,335,612,620,   2.6, 'yellow', 492,337),
        (112,578,340,818,   3.0, 'blue',   233,589),
        (372,722,614,1022, -2.2, 'purple', 497,728),
        (115,858,348,1100,  2.4, 'teal',   230,865) ]

SLOTS=[]
def ruled(w,h,t,y0,y1,rot,cx,cz):
    # built around the origin and only then placed, so the tilt is one rotation of one object
    # instead of five points hand-rotated about a centre
    bits=[]
    ix,iz=w*0.40,h*0.38
    for a,b_,c,d in ((-ix,ix,iz-t,iz),(-ix,ix,-iz,-iz+t),
                     (-ix,-ix+t,-iz,iz),(ix-t,ix,-iz,iz)):
        bpy.ops.mesh.primitive_cube_add(size=2)
        o=C.object; o.scale=((b_-a)/2,(y1-y0)/2,(d-c)/2)
        o.location=((a+b_)/2,(y0+y1)/2,(c+d)/2)
        bpy.ops.object.transform_apply(scale=True); bits.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in bits: o.select_set(True)
    C.view_layer.objects.active=bits[0]; bpy.ops.object.join()
    o=C.object; o.name='rule'
    o.rotation_euler=Euler((0,rot*math.pi/180,0)); o.location=(cx,0,cz)
    return keep(o,M['rule'])

PAPER_Y = YB0-pw(6)
for i,(x0,y0,x1,y1,rot,pc,pxx,pyy) in enumerate(NOTES,1):
    w,h = px(x1)-px(x0), pz(y0)-pz(y1)
    cx,cz = (px(x0)+px(x1))/2, (pz(y0)+pz(y1))/2
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=9, y_subdivisions=9, size=1)
    o=C.object; o.name='quest_slot_%d'%i
    o.scale=(w,h,1); o.rotation_euler=Euler((math.pi/2,0,0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # Verts are centred on the origin at this point -- that is the whole reason the first
    # attempt sheared: it measured them against the sheet's world centre instead.
    for v in o.data.vertices:
        u=v.co.x/w; t=v.co.z/h                     # both run -0.5 .. +0.5
        drop=max(0.0,0.5-t)
        v.co.y -= pw(4.0)*(drop**1.6)*(0.35+0.65*min(1.0,(2*abs(u))**1.5))
    sd=o.modifiers.new('s','SOLIDIFY'); sd.thickness=pw(2.2)
    o.location=(cx, PAPER_Y, cz)
    o.rotation_euler=Euler((0,rot*math.pi/180,0))
    keep(o, M['paper'])
    SLOTS.append(o)
    ruled(w,h,pw(2.5), PAPER_Y-pw(7.6), PAPER_Y-pw(6.0), rot, cx, cz)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,radius=pw(13),
        location=(px(pxx), PAPER_Y-pw(11), pz(pyy)))
    keep(C.object, PIN[pc], True)
    cyl(px(pxx), pz(pyy), PAPER_Y-pw(3), pw(3), pw(14), PIN[pc], 'pinstem','Y',8)

# ---------- what is lying at the feet of it ----------
def starfish(x,z,y,R):
    bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=R*0.42, radius2=R*0.30, depth=R*0.34,
                                    location=(x,y,z))
    keep(C.object, M['star'], True)
    for a in range(5):
        th=a*math.tau/5
        bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=R*0.24, radius2=R*0.05, depth=R*0.9,
            location=(x+math.cos(th)*R*0.46, y, z+math.sin(th)*R*0.46))
        o=C.object; o.rotation_euler=Euler((math.pi/2,0,-th+math.pi/2))
        keep(o, M['star'], True)

def shell(x,z,y,R,m):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=14,ring_count=8,radius=R,location=(x,y,z))
    o=C.object; o.scale=(1,0.42,0.78); bpy.ops.object.transform_apply(scale=True)
    keep(o,m,True)

starfish(px(152), pz(1248), pw(-6), pw(52))
shell(px(66),  pz(1252), pw(-4), pw(17), M['shell'])
shell(px(203), pz(1272), pw(-4), pw(14), M['shell2'])
shell(px(576), pz(1246), pw(-4), pw(18), M['shell2'])
shell(px(612), pz(1266), pw(-4), pw(15), M['shell'])

# ---------- one object for the frame, five for the notices ----------
bpy.ops.object.select_all(action='DESELECT')
body=[o for o in parts if not o.name.startswith('quest_slot')]
for o in body: o.select_set(True)
C.view_layer.objects.active=body[0]
bpy.ops.object.join()
frame=C.object; frame.name='questboard'

root=D.objects.new('QuestBoard', None)
C.collection.objects.link(root)
frame.parent=root
for s in SLOTS: s.parent=root

for o in list(C.scene.objects):
    if o.type=='MESH':
        o.select_set(True); C.view_layer.objects.active=o
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(32))
        o.select_set(False)

tri=sum(len(o.data.loop_triangles) for o in C.scene.objects if o.type=='MESH'
        for _ in [o.data.calc_loop_triangles()])
print('objects', len([o for o in C.scene.objects if o.type=='MESH']))
print('tris', sum(len(o.data.loop_triangles) for o in C.scene.objects if o.type=='MESH'))

OUT=os.environ.get('OUT','/home/user/Surf-/models/questboard.glb')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=True,
                          export_apply=True, export_yup=True)
print('wrote', OUT, os.path.getsize(OUT))

# ---------- preview ----------
if os.environ.get('RENDER'):
    sc=C.scene
    cam_d=D.cameras.new('c'); cam=D.objects.new('cam',cam_d); sc.collection.objects.link(cam)
    cam_d.lens=70
    cam.location=(0,-16.5,H*0.52); cam.rotation_euler=Euler((math.pi/2,0,0))
    sc.camera=cam
    for pos,en,sz in (((-6,-9,9),900,5),((7,-7,6),450,5),((0,6,7),300,6)):
        l=D.lights.new('l','AREA'); l.energy=en; l.size=sz
        ob=D.objects.new('l',l); sc.collection.objects.link(ob); ob.location=pos
        ob.rotation_euler=(Vector((0,0,H*0.5))-Vector(pos)).to_track_quat('-Z','Y').to_euler()
    w=sc.world or D.worlds.new('w'); sc.world=w; w.use_nodes=True
    w.node_tree.nodes['Background'].inputs[0].default_value=(0.93,0.90,0.83,1)
    w.node_tree.nodes['Background'].inputs[1].default_value=0.75
    sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=48
    sc.cycles.use_denoising=True
    sc.view_settings.view_transform='Standard'
    sc.render.resolution_x=620; sc.render.resolution_y=1240
    sc.render.filepath=os.environ.get('SHOT','/home/user/Surf-/questboard_preview.png')
    bpy.ops.render.render(write_still=True)
    print('rendered', sc.render.filepath)
