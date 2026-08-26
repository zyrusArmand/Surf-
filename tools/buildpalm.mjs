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
import { dequantize, weld, simplify, prune, dedup, textureCompress, unpartition }
  from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const IN=process.argv[2], OUT=process.argv[3];
const TARGET=+(process.argv[4]||30000), ERR=+(process.argv[5]||0.02);
const TEX=+(process.argv[6]||1024), Q=+(process.argv[7]||86);
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
// A frond is a flat strip and you see the underside of half of them from below, which is
// exactly the angle a title screen looks at a palm from.
for(const m of root.listMaterials()) m.setDoubleSided(true);
await io.write(OUT, doc);
const skins=root.listSkins();
console.log('skins', skins.length, 'joints', skins.reduce((s,k)=>s+k.listJoints().length,0));
for(const t of root.listTextures())
  console.log('tex', t.getSize().join('x'), t.getMimeType(), (t.getImage().byteLength/1048576).toFixed(2)+'MB');
