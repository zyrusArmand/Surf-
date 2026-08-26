// usage: node tools/buildprop.mjs <in.glb> <out.glb> [triTarget]
// Brings a scanned/generated prop into the game's budget. Not part of the game's build —
// the game is one HTML file with no node step. Needs:
//   npm i @gltf-transform/core @gltf-transform/extensions @gltf-transform/functions \
//         meshoptimizer sharp
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, prune, dedup, textureCompress, unpartition, dequantize,
         flatten, join } from '@gltf-transform/functions';
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
  // ---- FIRST, ALWAYS ----
  // three.js r128 does not read KHR_mesh_quantization. A quantized file does not fail to load,
  // which is the trap: it loads with packed integers taken as coordinates, and the model turns
  // up somewhere out past the horizon at a size nothing can measure. That cost a whole round
  // on the squirrel — its deck gap came back as -1.28e+303 — and this tool had the same hole
  // in it, waiting for the next quantized export to come through.
  dequantize(),
  // ---- AND DOWN TO ONE PIECE ----
  // The game takes the FIRST mesh it finds in a prop and ignores the rest, which is fine for
  // every file that has been through here so far and silently wrong for one that does not.
  // The new sand arrived as two primitives, 0.997 wide and 0.532 wide: loaded as it came, the
  // beach would have been tiled with two thirds of a tile and nothing would have looked
  // broken — just a gap-prone field that no amount of overlap tuning could close, because a
  // third of every piece was never there.
  // flatten bakes the node transforms down first so joining cannot move anything.
  flatten(), join(),
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
