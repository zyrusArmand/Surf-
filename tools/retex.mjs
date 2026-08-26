// usage: node tools/retex.mjs <in.glb> <out.glb> [maxTex] [quality]
//
// TEXTURE MEMORY, not download size. A 4096 map is 4096*4096*4 bytes on the GPU — 67MB, or
// about 89MB once mipmaps are counted — whatever it happens to compress to on disk. A phone
// has a few hundred MB for the whole page, so two 4K maps on one model is most of the budget
// spent on one object, and the tab is reloaded out from under the player: "a problem repeatedly
// occurred". The download is the number everybody looks at and the memory is the number that
// crashes.
//
// Textures ONLY. buildprop and buildpalm both join primitives, which folds a skinned mesh's
// groups together and destroys the rig — so neither can be pointed at a rider. This touches
// nothing but the images, which makes it safe on anything: rigged, jointed, or plain.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { textureCompress } from '@gltf-transform/functions';
import sharp from 'sharp';

const IN=process.argv[2], OUT=process.argv[3];
const TEX=+(process.argv[4]||2048), Q=+(process.argv[5]||88);
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc=await io.read(IN);
const root=doc.getRoot();

const mem=()=>root.listTextures().reduce((s,t)=>{
  const z=t.getSize(); return s+(z?z[0]*z[1]*4*1.333:0); },0)/1048576;
console.log('before:', mem().toFixed(1), 'MB of texture memory —',
  root.listTextures().map(t=>(t.getSize()||[]).join('x')).join(' '));

await doc.transform(
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:Q, resize:[TEX,TEX]}),
);
await io.write(OUT, doc);
console.log('after: ', mem().toFixed(1), 'MB of texture memory —',
  root.listTextures().map(t=>(t.getSize()||[]).join('x')).join(' '));
console.log('skins', root.listSkins().length, 'animations', root.listAnimations().length,
            '(unchanged — this tool does not touch geometry)');
