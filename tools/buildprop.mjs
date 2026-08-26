// usage: node tools/buildprop.mjs <in.glb> <out.glb> [triTarget]
// Brings a scanned/generated prop into the game's budget. Not part of the game's build —
// the game is one HTML file with no node step. Needs:
//   npm i @gltf-transform/core @gltf-transform/extensions @gltf-transform/functions \
//         meshoptimizer sharp
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, prune, dedup, textureCompress, unpartition } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const IN=process.argv[2], OUT=process.argv[3], TARGET=+(process.argv[4]||95000);
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc=await io.read(IN);
const root=doc.getRoot();

const tris=()=>root.listMeshes().flatMap(m=>m.listPrimitives())
  .reduce((s,p)=>s+(p.getIndices()?p.getIndices().getCount():p.getAttribute('POSITION').getCount())/3,0);
const before=tris();
console.log('before:', Math.round(before), 'tris');

await doc.transform(
  weld(),
  simplify({simplifier:MeshoptSimplifier, ratio:Math.min(1,TARGET/before), error:0.002}),
  // Per SLOT, because the three maps do not deserve the same budget. Base colour is what you
  // actually look at; the normal carries the surface at a scale you pass in a fifth of a
  // second; metallic-roughness is two near-flat channels and 1024 is generous for it.
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:88,
                   slots:/baseColorTexture/, resize:[2048,2048]}),
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:88,
                   slots:/normalTexture/,    resize:[2048,2048]}),
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:85,
                   slots:/metallicRoughnessTexture/, resize:[1024,1024]}),
  prune(), dedup(), unpartition(),
);
console.log('after: ', Math.round(tris()), 'tris');
// a closed prop never shows its inside, and doubleSided costs every fragment twice
for(const m of root.listMaterials()) m.setDoubleSided(false);
await io.write(OUT, doc);
for(const t of root.listTextures())
  console.log('tex', t.getSize().join('x'), t.getMimeType(), (t.getImage().byteLength/1048576).toFixed(2)+'MB');
