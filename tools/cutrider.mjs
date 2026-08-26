// This one arrived already merged — 24 joints with the right names, headfront present, and its
// clips already called walk and rest. So there is nothing to merge; it only needs bringing into
// the budget the other rigged riders keep. Simplify is skin-aware, so the rig survives it.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, prune, dedup, textureCompress, unpartition } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';
const IN=process.argv[2], OUT=process.argv[3], TARGET=+(process.argv[4]||56000);
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc=await io.read(IN); const root=doc.getRoot();
const tris=()=>root.listMeshes().flatMap(m=>m.listPrimitives())
  .reduce((s,p)=>s+(p.getIndices()?p.getIndices().getCount():p.getAttribute('POSITION').getCount())/3,0);
const before=tris(); console.log('before:', Math.round(before),'tris');
await doc.transform(
  weld(),
  simplify({simplifier:MeshoptSimplifier, ratio:Math.min(1,TARGET/before), error:0.002}),
  // every slot, because this export puts the same image in baseColor AND emissive and the
  // 4096 square is most of the file
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:88, resize:[2048,2048]}),
  prune(), dedup(), unpartition(),
);
console.log('after: ', Math.round(tris()),'tris');
for(const m of root.listMaterials()) m.setDoubleSided(false);
await io.write(OUT, doc);
console.log('clips:', root.listAnimations().map(a=>a.getName()).join(', '));
console.log('bones:', new Set(root.listSkins().flatMap(s=>s.listJoints().map(j=>j.getName()))).size,
            '| headfront:', root.listSkins().some(s=>s.listJoints().some(j=>j.getName()==='headfront')));
for(const t of root.listTextures())
  console.log('tex', t.getSize().join('x'), t.getMimeType(), (t.getImage().byteLength/1048576).toFixed(2)+'MB');
