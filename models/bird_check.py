import bpy, math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='/home/user/Surf-/models/bird_rigged.glb')
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
W=arm.matrix_world
B={b.name:b for b in arm.data.bones}
rest={n:(W@b.head_local, W@b.tail_local) for n,b in B.items()}
span=max(abs(rest[n][1].x) for n in rest)
# ---- THE CHAIN, FOUND BY WALKING UP FROM THE TIP ----
# Selecting "every bone whose tail is out past a threshold" catches the LEGS -- they are paired
# and a quarter of the span out -- and the first version of this check did exactly that, so its
# "shoulder" was an unanimated leg bone and both its articulation and its lag were measuring
# nothing. The tip is unambiguous: the bone reaching furthest out. Its own parents are the wing.
tip=min(B, key=lambda n: rest[n][1].x)
chain=[]; cur=B[tip]
while cur is not None and abs(rest[cur.name][1].x) > span*0.10:
    chain.append(cur.name); cur=cur.parent
chain.reverse()
sh=chain[0]
print('span %.3f  chain %d: %s'%(span,len(chain),' -> '.join(chain)))
acts={a.name:a for a in bpy.data.actions}
for nm in ('flap','glide'):
    if nm not in acts: continue
    arm.animation_data.action=acts[nm]
    zs=[];shz=[];bend=[];xs=[];ys=[]
    for f in range(1,CY:=26):
        bpy.context.scene.frame_set(f)
        P={n:(W@arm.pose.bones[n].head, W@arm.pose.bones[n].tail) for n in chain}
        t=P[tip][1]; zs.append(t.z); xs.append(abs(t.x)); ys.append(t.y)
        shz.append(P[sh][1].z)
        vin=P[sh][1]-P[sh][0]
        vout=P[tip][1]-P[chain[max(1,len(chain)//2)]][0]
        if vin.length>1e-6 and vout.length>1e-6: bend.append(math.degrees(vin.angle(vout)))
    rng=lambda a:(max(a)-min(a))
    print('%-6s tip vert %.3f (%.0f%% span)  fore-aft %.3f  span swing %.3f (%.0f%% shorter)'%(
        nm,rng(zs),100*rng(zs)/span,rng(ys),rng(xs),100*rng(xs)/max(xs)))
    if bend: print('       ARTICULATION inner vs outer %.1f..%.1f deg -> varies %.1f'%(min(bend),max(bend),rng(bend)))
    lt=zs.index(min(zs)); ls=shz.index(min(shz)); n=len(zs)
    lag=(lt-ls)%n
    print('       shoulder bottoms frame %d, tip frame %d -> tip lags %d/%d (%.0f%% of cycle)'%(
        ls+1,lt+1,lag,n,100*lag/n))
