// usage: node tools/buildpalm.mjs <in.glb> <out.glb> [triTarget] [error] [tex] [quality]
//
// A TREE IS NOT A PROP, and the difference is the error bound. buildprop and cutrider both
// simplify at error 0.002, which is right for a body or a chest where a tenth of a percent of
// drift is a visible dent. A palm is a few hundred separate leaflets, each one a long thin
// strip: at that tolerance meshopt cannot collapse anything without moving a leaf edge past
// the bound, so it stops early and the target is simply not met. Asked for 40k from 293k it
// returned 168k — and a 21MB file, because the weight was in vertices rather than textures.
// Foliage tolerates far more: a leaf that moves a hundredth of its own width is a leaf.
//
// The skin is why this exists as its own tool rather than a flag on buildprop: joining
// primitives would fold the frond groups together, and this file is rigged with 39 joints
// that the game poses a bend through. Nothing here touches the skeleton.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dequantize, weld, simplify, prune, dedup, textureCompress, unpartition,
         flatten, join } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const IN=process.argv[2], OUT=process.argv[3];
const TARGET=+(process.argv[4]||30000), ERR=+(process.argv[5]||0.02);
const TEX=+(process.argv[6]||1024), Q=+(process.argv[7]||86);
// ---- AND THE SKIN COMES OFF ----
// Counter-intuitive, and it is the whole reason the first attempt exploded on screen.
// The game does not want a rigged palm; it wants a SHAPE, which it then rigs itself through
// rigStaticPalm into a bone chain its own bend-posing code knows the shape of. Every palm in
// the grove gets a different bend posed into that chain, which is what stops nine copies of
// one tree reading as nine copies of one tree.
// Hand it a file that already carries a skeleton and it skips building its own and poses the
// FILE's joints instead — 39 of them, named and ordered by whatever exported it, with no
// relationship to the trunk chain the code is addressing. The result is not a bent tree, it
// is fronds flung in every direction, which is exactly what the render showed.
// The bind pose is the tree standing up, so dropping the joint weights leaves the shape
// correct and lets the known-good path take it.
const STATIC=process.argv[8]!=='keep-skin';
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc=await io.read(IN);
const root=doc.getRoot();
const tris=()=>root.listMeshes().flatMap(m=>m.listPrimitives())
  .reduce((s,p)=>s+(p.getIndices()?p.getIndices().getCount():p.getAttribute('POSITION').getCount())/3,0);
const before=tris();
console.log('before:', Math.round(before), 'tris');

await doc.transform(
  // r128 does not read KHR_mesh_quantization — see the note in buildprop
  dequantize(),
);
if(STATIC){
  // ---- THE SKIN COMES OFF FIRST, SO THE PIECES CAN BE JOINED ----
  // See the note above for why the skin goes. It goes HERE, before anything else, because the
  // only reason not to join a rigged file is the skin — and once it is off, the seven separate
  // frond groups can be merged into the one mesh the game actually reads.
  for(const mesh of root.listMeshes())
    for(const pr of mesh.listPrimitives())
      for(const sem of ['JOINTS_0','WEIGHTS_0','JOINTS_1','WEIGHTS_1']){
        const at=pr.getAttribute(sem); if(at){ pr.setAttribute(sem,null); at.dispose(); }
      }
  for(const n of root.listNodes()) if(n.getSkin()) n.setSkin(null);
  for(const sk of root.listSkins()) sk.dispose();
}
await doc.transform(
  // ---- AND DOWN TO ONE MESH ----
  // The game takes the FIRST mesh in palm.glb and rigs that one, warning about the rest and
  // carrying on — and this file arrives as seven. Measured through the game's own palmRig, the
  // piece it picked spans y 0.515 to 0.989: the top half, a frond cluster, with no trunk in it
  // at all. So it rigged a handful of leaves and called it a tree, the trunk line came back
  // meaningless, and the pug and the board — which are placed against that line — left the
  // frame with it. Exactly the fault the new sand had, in a different file.
  flatten(), join(),
  weld(),
  simplify({simplifier:MeshoptSimplifier, ratio:Math.min(1,TARGET/before), error:ERR}),
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:Q,
                   slots:/baseColorTexture/, resize:[TEX,TEX]}),
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:Q,
                   slots:/normalTexture/,    resize:[TEX,TEX]}),
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:Math.max(78,Q-4),
                   slots:/metallicRoughnessTexture/, resize:[Math.min(512,TEX),Math.min(512,TEX)]}),
  prune(), dedup(), unpartition(),
);
console.log('after: ', Math.round(tris()), 'tris');
console.log('meshes:', root.listMeshes().length,
            ' prims:', root.listMeshes().flatMap(m=>m.listPrimitives()).length,
            ' skins:', root.listSkins().length);
// ---- AND STOOD ON ITS OWN TRUNK ----
// The game places a palm by its ORIGIN and expects the trunk to be there. This export's origin
// is somewhere off in the corner of the scene it was authored in: measured through palmProbe,
// the tree was positioned at x=0 with its geometry spanning -19.2 to -0.58, so the whole tree
// stood ten units to the left of where the game had put it — and the pug and the board, placed
// against the trunk, went off frame with it.
// The BASE of the trunk, not the middle of the bounding box. A palm's crown is wider than its
// trunk and hangs further one way than the other, so centring on the box centres on the
// leaves: the trunk would still be off to one side, just less. The lowest slice of the mesh is
// all trunk and nothing else, so its centroid is the one point on this model that is certainly
// the middle of the trunk.
{
  const prim=root.listMeshes().flatMap(m=>m.listPrimitives())[0];
  const pos=prim.getAttribute('POSITION');
  const el=[0,0,0];
  let y0=Infinity, y1=-Infinity;
  for(let i=0;i<pos.getCount();i++){ pos.getElement(i,el);
    if(el[1]<y0)y0=el[1]; if(el[1]>y1)y1=el[1]; }
  const band=y0+(y1-y0)*0.05;
  let sx=0, sz=0, n=0;
  for(let i=0;i<pos.getCount();i++){ pos.getElement(i,el);
    if(el[1]<=band){ sx+=el[0]; sz+=el[2]; n++; } }
  const cx=n?sx/n:0, cz=n?sz/n:0;
  for(let i=0;i<pos.getCount();i++){ pos.getElement(i,el);
    el[0]-=cx; el[1]-=y0; el[2]-=cz; pos.setElement(i,el); }
  console.log('trunk base moved to origin from',
              [cx.toFixed(3),y0.toFixed(3),cz.toFixed(3)].join(', '),
              '— from', n, 'vertices in the bottom 5%');
}
// A frond is a flat strip and you see the underside of half of them from below, which is
// exactly the angle a title screen looks at a palm from.
for(const m of root.listMaterials()) m.setDoubleSided(true);
await io.write(OUT, doc);
const skins=root.listSkins();
console.log('skins', skins.length, 'joints', skins.reduce((s,k)=>s+k.listJoints().length,0));
for(const t of root.listTextures())
  console.log('tex', t.getSize().join('x'), t.getMimeType(), (t.getImage().byteLength/1048576).toFixed(2)+'MB');
