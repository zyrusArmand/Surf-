import bpy, math, os, sys
from mathutils import Vector, Euler

SRC=os.environ['SRC']
OUT=os.environ.get('OUT','/home/user/Surf-/models/questboard.glb')
SHOT=os.environ.get('SHOT','/home/user/Surf-/qb_max.png')
TARGET=int(os.environ.get('TARGET','90000'))
TEX=int(os.environ.get('TEX','1024'))
DO=os.environ.get('DO','inspect')

D=bpy.data; C=bpy.context
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

meshes=[o for o in C.scene.objects if o.type=='MESH']
def tris():
    n=0
    for o in meshes:
        o.data.calc_loop_triangles(); n+=len(o.data.loop_triangles)
    return n
print('objects', [o.name for o in meshes])
print('tris in', tris())
for im in D.images:
    print('image', im.name, im.size[0], 'x', im.size[1])

# world bounds, so the game knows what shape it is getting
bb=[Vector(c) for o in meshes for c in [o.matrix_world@Vector(v) for v in o.bound_box]]
mn=Vector((min(v.x for v in bb),min(v.y for v in bb),min(v.z for v in bb)))
mx=Vector((max(v.x for v in bb),max(v.y for v in bb),max(v.z for v in bb)))
size=mx-mn
print('bbox min %.3f %.3f %.3f'%tuple(mn))
print('bbox max %.3f %.3f %.3f'%tuple(mx))
print('size     %.3f %.3f %.3f'%tuple(size))
ax=['x','y','z']
print('thin axis', ax[min(range(3), key=lambda i:size[i])], '(the board faces along this)')

if DO=='opt':
    # ---- decimate ----
    # Collapse rather than un-subdivide: the source is subdivision output, so its topology is
    # regular and collapse keeps the silhouette while un-subdivide would only work if the cage
    # were recoverable, which it is not after an export.
    n0=tris()
    ratio=min(1.0, TARGET/max(1,n0))
    for o in meshes:
        bpy.context.view_layer.objects.active=o
        m=o.modifiers.new('dec','DECIMATE'); m.decimate_type='COLLAPSE'; m.ratio=ratio
        bpy.ops.object.modifier_apply(modifier=m.name)
    print('ratio %.4f -> tris %d'%(ratio, tris()))
    # ---- textures ----
    for im in D.images:
        if im.size[0]>TEX or im.size[1]>TEX:
            w,h=im.size
            s=TEX/max(w,h)
            im.scale(max(4,int(w*s)), max(4,int(h*s)))
            print('resized', im.name, '->', im.size[0], 'x', im.size[1])
    # Uncompressed on purpose. There is NO DRACOLoader in this project, and a Draco GLB does not
    # fail loudly -- GLTFLoader neither decodes it nor errors, it just never calls back, which is
    # the exact stall the loader watchdog exists for.
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True, export_draco_mesh_compression_enable=False,
                              export_image_format='AUTO')
    print('wrote', OUT, os.path.getsize(OUT))

# ---- preview, straight on the thin axis ----
sc=C.scene
ctr=(mn+mx)*0.5
thin=min(range(3), key=lambda i:size[i])
span=max(size[(thin+1)%3], size[(thin+2)%3])
cam_d=D.cameras.new('c'); cam=D.objects.new('cam',cam_d); sc.collection.objects.link(cam)
cam_d.lens=70
dist=span*2.0+size[thin]
if thin==1:      # faces along Y, the usual Blender front view
    cam.location=(ctr.x, ctr.y-dist, ctr.z); cam.rotation_euler=Euler((math.pi/2,0,0))
elif thin==0:
    cam.location=(ctr.x-dist, ctr.y, ctr.z); cam.rotation_euler=Euler((math.pi/2,0,-math.pi/2))
else:
    cam.location=(ctr.x, ctr.y, ctr.z+dist); cam.rotation_euler=Euler((0,0,0))
sc.camera=cam
for off,en in (((-1,-1.2,1),1200),((1.1,-1,0.6),700),((0,1,0.8),450)):
    l=D.lights.new('l','AREA'); l.energy=en*max(1,span*span)*0.35; l.size=span
    ob=D.objects.new('l',l); sc.collection.objects.link(ob)
    ob.location=(ctr.x+off[0]*dist, ctr.y+off[1]*dist, ctr.z+off[2]*dist)
    ob.rotation_euler=(ctr-Vector(ob.location)).to_track_quat('-Z','Y').to_euler()
w=sc.world or D.worlds.new('w'); sc.world=w; w.use_nodes=True
w.node_tree.nodes['Background'].inputs[0].default_value=(0.93,0.90,0.83,1)
w.node_tree.nodes['Background'].inputs[1].default_value=0.8
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=40
sc.cycles.use_denoising=True
sc.view_settings.view_transform='Standard'
sc.render.resolution_x=620; sc.render.resolution_y=1240
sc.render.filepath=SHOT
bpy.ops.render.render(write_still=True)
print('rendered', SHOT)

# ---- WHERE THE FIVE NOTICES ARE ----
# The upload is ONE welded mesh with one material, so there is nothing named to project against.
# Finding them by connectivity failed and failed informatively: the mesh is dense enough that a
# flood fill over front-facing faces shattered into 1486 fragments whose largest area was a
# fiftieth of a sheet. So this reads them off a PICTURE instead -- an ORTHOGRAPHIC render framed
# exactly to the bounding box, where a pixel maps linearly to a fraction of the board and paper
# is the only thing on it that is both bright and colourless. Wood and bamboo are saturated
# orange; the sheets are near-white. That distinction cannot shatter.
if os.environ.get('SLOTS'):
    W=520; Hp=int(round(W*size.z/size.x))
    oc=D.cameras.new('oc'); ocam=D.objects.new('ocam',oc); sc.collection.objects.link(ocam)
    oc.type='ORTHO'; oc.ortho_scale=max(size.x,size.z)
    ocam.location=(ctr.x, mn.y-size.y*4, ctr.z); ocam.rotation_euler=Euler((math.pi/2,0,0))
    sc.camera=ocam
    sc.render.resolution_x=W; sc.render.resolution_y=Hp
    sc.cycles.samples=16
    sc.render.film_transparent=True
    sc.render.image_settings.color_mode='RGBA'
    # and belt-and-braces: a BLACK backdrop cannot pass a brightness test either,
    # so the segmentation no longer depends on the alpha channel surviving a file format
    w.node_tree.nodes['Background'].inputs[0].default_value=(0,0,0,1)
    w.node_tree.nodes['Background'].inputs[1].default_value=0.0
    flat=os.path.join(os.path.dirname(SHOT),'qb_flat.png')
    sc.render.filepath=flat
    bpy.ops.render.render(write_still=True)
    im=D.images.load(flat)
    px=list(im.pixels)
    def at(x,y):
        i=((Hp-1-y)*W+x)*4
        return px[i],px[i+1],px[i+2],px[i+3]
    mask=bytearray(W*Hp)
    for y in range(Hp):
        for x in range(W):
            r,g,b,a=at(x,y)
            if a<0.5: continue
            mn3=min(r,g,b); mx3=max(r,g,b)
            if mn3>0.42 and (mx3-mn3)<0.115: mask[y*W+x]=1
    print('bright colourless pixels', sum(mask))
    seen=bytearray(W*Hp); groups=[]
    for y in range(Hp):
        for x in range(W):
            i=y*W+x
            if not mask[i] or seen[i]: continue
            st=[(x,y)]; seen[i]=1; xs=[]; ys=[]
            while st:
                cx,cy=st.pop(); xs.append(cx); ys.append(cy)
                for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    nx,ny=cx+dx,cy+dy
                    if 0<=nx<W and 0<=ny<Hp:
                        k=ny*W+nx
                        if mask[k] and not seen[k]: seen[k]=1; st.append((nx,ny))
            if len(xs)>400: groups.append((len(xs),min(xs),max(xs),min(ys),max(ys)))
    groups.sort(reverse=True)
    print('groups', len(groups), [g[0] for g in groups[:8]])
    keep=groups[:5]
    keep.sort(key=lambda g:(g[3], g[1]))
    print('QB_SLOTS  // u0,u1,v0,v1 as fractions of the board box, v down from the top')
    out=[]
    for n,x0,x1,y0,y1 in keep:
        out.append((x0/W,(x1+1)/W,y0/Hp,(y1+1)/Hp))
        print('  [%.4f,%.4f,%.4f,%.4f],  // %d px'%(out[-1]+(n,)))
