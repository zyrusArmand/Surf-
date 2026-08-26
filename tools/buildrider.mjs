// models/pig.glb, built to the same shape the angler and the alien were built to: one file,
// two clips named walk and rest, a JPEG texture, and a triangle budget in line with the other
// rigged riders rather than double it.
//
// The two exports carry the SAME character and the SAME 24-bone skeleton — they differ only in
// which clip is attached. So the merge is a merge of animations, not of meshes: bring the second
// document in, repoint its channels at the first document's bones BY NAME, then throw the
// duplicate body away. Repointing is what makes it safe to throw away; without it the clip would
// be driving bones that are about to be pruned and the pig would stand still.
// Needs: npm i @gltf-transform/core @gltf-transform/extensions @gltf-transform/functions \
//              meshoptimizer sharp
// Run it from wherever those are installed; it is not part of the game's own build, which has
// no node step at all — the game is one HTML file.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { mergeDocuments, unpartition, weld, simplify, prune, dedup, textureCompress } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

// usage: node tools/buildrider.mjs <dir-with-the-two-Meshy-glbs> <out.glb>
// The pair is Meshy's standard export: *_Character_output.glb is the body, and
// *_Animation_*_withSkin.glb is the same body again with a clip on it.
import { readdirSync } from 'node:fs';
import { join } from 'node:path';
const DIR=process.argv[2]||'.';
const OUT=process.argv[3]||'models/pig.glb';
const files=readdirSync(DIR).filter(f=>f.endsWith('.glb'));
const pick=re=>{ const f=files.find(x=>re.test(x));
  if(!f) throw new Error(`no file matching ${re} in ${DIR} — found: ${files.join(', ')}`);
  return join(DIR,f); };
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);

const base =await io.read(pick(/Animation.*withSkin\.glb$/i));
const other=await io.read(pick(/Character_output\.glb$/i));

const nodesBefore =new Set(base.getRoot().listNodes());
const animsBefore =new Set(base.getRoot().listAnimations());
const scenesBefore=new Set(base.getRoot().listScenes());
const skinsBefore =new Set(base.getRoot().listSkins());
const meshesBefore=new Set(base.getRoot().listMeshes());
mergeDocuments(base, other);

const byName=new Map([...nodesBefore].map(n=>[n.getName(),n]));
let repointed=0, orphaned=0;
for(const anim of base.getRoot().listAnimations()){
  if(animsBefore.has(anim))continue;                     // the walk clip is already correct
  for(const ch of anim.listChannels()){
    const t=ch.getTargetNode();
    if(t&&byName.has(t.getName())){ ch.setTargetNode(byName.get(t.getName())); repointed++; }
    else orphaned++;
  }
}
console.log(`repointed ${repointed} channels, ${orphaned} with no match by name`);
if(orphaned) throw new Error('a channel had no bone of that name in the surviving rig');

// ---- the duplicate body, disposed by NAME rather than left to prune ----
// Disposing the second scene orphans its nodes; it does not remove them, and prune keeps a node
// a Skin still lists as a joint — so the duplicate skeleton keeps the duplicate skin alive, which
// keeps the duplicate 60k-vertex mesh alive. The first attempt wrote a 6.7MB file against the
// angler's 2.7MB at the same triangle count, with prune reporting it had removed exactly one
// node. Everything that arrived in the merge is named explicitly here instead: the channels are
// already repointed at the surviving bones, so nothing in the file still needs any of it.
for(const s of base.getRoot().listScenes())  if(!scenesBefore.has(s)) s.dispose();
for(const m of base.getRoot().listMeshes())  if(!meshesBefore.has(m)) m.dispose();
for(const k of base.getRoot().listSkins())   if(!skinsBefore.has(k))  k.dispose();
for(const n of base.getRoot().listNodes())   if(!nodesBefore.has(n))  n.dispose();
await base.transform(prune(), dedup());

// clips named the way the other rigged riders name them
for(const a of base.getRoot().listAnimations()){
  const n=a.getName();
  a.setName(/walk/i.test(n) ? 'walk' : 'rest');
}

const tris=()=>base.getRoot().listMeshes()
  .flatMap(m=>m.listPrimitives())
  .reduce((s,p)=>s+(p.getIndices()?p.getIndices().getCount():p.getAttribute('POSITION').getCount())/3,0);
console.log('before:', Math.round(tris()), 'tris');

await base.transform(
  weld(),
  // 55k is where the angler and the alien landed. The pig arrived at 90k for a character that
  // is a few dozen pixels tall for most of a run.
  simplify({simplifier:MeshoptSimplifier, ratio:0.61, error:0.002}),
  // 2048 JPEG, matching the angler: a 4.54MB PNG of a hand-painted skin is four megabytes of
  // exactly the kind of smooth gradient JPEG was designed for.
  textureCompress({encoder:sharp, targetFormat:'jpeg', quality:86}),
  // the merge left the two files' buffers side by side, and a GLB may carry at most one
  unpartition(),
);
console.log('after: ', Math.round(tris()), 'tris');

// doubleSided costs every fragment twice on a closed body that never shows its inside
for(const m of base.getRoot().listMaterials()) m.setDoubleSided(false);

await io.write(OUT, base);
const r=base.getRoot();
console.log('clips:', r.listAnimations().map(a=>`${a.getName()}(${a.listChannels().length}ch)`).join(' '));
console.log('bones:', new Set(r.listSkins().flatMap(s=>s.listJoints().map(j=>j.getName()))).size);
console.log('headfront present:',
  r.listSkins().some(s=>s.listJoints().some(j=>j.getName()==='headfront')));
for(const t of r.listTextures())
  console.log('tex', t.getSize(), t.getMimeType(), (t.getImage().byteLength/1048576).toFixed(2)+'MB');
