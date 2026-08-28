// usage: node tools/decimate.mjs <in.glb> <out.glb> [targetTris] [maxTex] [quality]
//
// WHY THIS EXISTS. gltf-transform's simplify() can do nothing with a scanned mesh, and the
// reason is not the error bound: a scan exported per-triangle has no shared vertices at all,
// so every edge is a BORDER edge, and a simplifier will not collapse a border. weld() is the
// answer to that — except weld() in v4 has no tolerance (WELD_DEFAULTS is {overwrite:true}),
// so it merges only vertices that are bit-identical, which in a scan is none of them. I passed
// it a tolerance once, it was accepted and ignored, and the mesh came back the same size.
//
// So the weld is done here, by quantising positions onto a grid and merging what lands in the
// same cell. That fuses the UV seams along with everything else, which is a real cost — it is
// fine on a coconut, which is a ball with a noise texture, and would not be on a face.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dequantize, textureCompress, prune, dedup } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const IN = process.argv[2], OUT = process.argv[3];
const WANT = +(process.argv[4] || 1500);
const TEX  = +(process.argv[5] || 512);
const Q    = +(process.argv[6] || 82);

const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(IN);
await doc.transform(dequantize());
const root = doc.getRoot();

const triCount = () => root.listMeshes().reduce((s, m) => s + m.listPrimitives().reduce((t, p) => {
  const ix = p.getIndices();
  return t + (ix ? ix.getCount() : p.getAttribute('POSITION').getCount()) / 3;
}, 0), 0);
console.log('before:', Math.round(triCount()), 'tris');

await MeshoptSimplifier.ready;

for (const mesh of root.listMeshes()) {
  for (const prim of mesh.listPrimitives()) {
    const pos = prim.getAttribute('POSITION');
    if (!pos) continue;
    const n = pos.getCount();
    const ixAcc = prim.getIndices();
    const idx = ixAcc ? Array.from(ixAcc.getArray()) : Array.from({ length: n }, (_, i) => i);

    // ---- the weld ----
    // The cell is a share of the model's own size, so this is scale-free: 1/2000th of the
    // bounding diagonal fuses a scan's duplicated corners without pulling real detail together.
    const P = pos.getArray();
    let mnx = Infinity, mny = Infinity, mnz = Infinity, mxx = -Infinity, mxy = -Infinity, mxz = -Infinity;
    for (let i = 0; i < n; i++) {
      const x = P[i * 3], y = P[i * 3 + 1], z = P[i * 3 + 2];
      if (x < mnx) mnx = x; if (x > mxx) mxx = x;
      if (y < mny) mny = y; if (y > mxy) mxy = y;
      if (z < mnz) mnz = z; if (z > mxz) mxz = z;
    }
    const diag = Math.hypot(mxx - mnx, mxy - mny, mxz - mnz) || 1;
    const cell = diag / 2000;

    // ---- KEYED ON POSITION AND UV, not position alone ----
    // Welding by position alone fuses the UV seams along with the duplicated corners, and a
    // seam is exactly where two far-apart parts of the atlas meet. The texture then stretches
    // across the join and the model comes back looking chewed -- which is what happened to the
    // coconuts. Including a coarsely quantised UV in the key keeps vertices that sit at the
    // same PLACE but read from different parts of the texture apart, so the simplifier still
    // gets interior edges to collapse and the atlas stays intact.
    const UV = prim.getAttribute('TEXCOORD_0');
    const U = UV ? UV.getArray() : null;
    const ucell = 1 / 2048;
    const map = new Map(), remap = new Int32Array(n);
    const keep = [];
    for (let i = 0; i < n; i++) {
      let k = Math.round(P[i * 3] / cell) + '|' + Math.round(P[i * 3 + 1] / cell) + '|' + Math.round(P[i * 3 + 2] / cell);
      if (U) k += '|' + Math.round(U[i * 2] / ucell) + '|' + Math.round(U[i * 2 + 1] / ucell);
      let j = map.get(k);
      if (j === undefined) { j = keep.length; map.set(k, j); keep.push(i); }
      remap[i] = j;
    }
    const m = keep.length;
    if (m < n) {
      for (const name of prim.listSemantics()) {
        const acc = prim.getAttribute(name);
        const el = acc.getElementSize(), src = acc.getArray();
        const dst = new src.constructor(m * el);
        for (let j = 0; j < m; j++) {
          const i = keep[j];
          for (let c = 0; c < el; c++) dst[j * el + c] = src[i * el + c];
        }
        acc.setArray(dst);
      }
      for (let i = 0; i < idx.length; i++) idx[i] = remap[idx[i]];
    }
    // drop the triangles the weld collapsed to a line or a point
    const tri = [];
    for (let i = 0; i < idx.length; i += 3) {
      const a = idx[i], b = idx[i + 1], c = idx[i + 2];
      if (a !== b && b !== c && a !== c) tri.push(a, b, c);
    }

    // ---- and then the simplify, which now has interior edges to work with ----
    const want = Math.max(60, Math.round(WANT * (tri.length / 3) / Math.max(1, triCount())));
    const out = MeshoptSimplifier.simplify(
      new Uint32Array(tri), prim.getAttribute('POSITION').getArray(), 3,
      // The seams are real borders now that the weld respects them, and locking every border
      // on a model made mostly of charts leaves nothing to collapse. The error bound is what
      // protects the silhouette instead.
      want * 3, 0.04, []
    )[0];

    // ---- AND THE VERTICES NOTHING POINTS AT ARE THROWN AWAY ----
    // simplify() returns a shorter index list and nothing else: every vertex the original
    // mesh had is still sitting in the attribute buffers, referenced or not, and prune() does
    // not look inside a primitive. A UFO came out of here at 157k triangles carrying 695,007
    // vertices — a clean mesh of that size has about 78,000 — and at 48 bytes a vertex those
    // orphans WERE the file: 34MB of a 35MB output. The triangle count said the decimation had
    // worked and the file size said it had not, and only the second one is what downloads.
    // So the surviving indices are renumbered onto a compacted set of vertices, and every
    // attribute is rebuilt to match.
    const used = new Map();
    const packed = new Uint32Array(out.length);
    for (let i = 0; i < out.length; i++) {
      const v = out[i];
      let n = used.get(v);
      if (n === undefined) { n = used.size; used.set(v, n); }
      packed[i] = n;
    }
    const order = new Uint32Array(used.size);
    for (const [oldI, newI] of used) order[newI] = oldI;
    for (const name of prim.listSemantics()) {
      const acc = prim.getAttribute(name);
      const src = acc.getArray(), n = acc.getElementSize();
      const dst = new src.constructor(used.size * n);
      for (let i = 0; i < order.length; i++)
        for (let k = 0; k < n; k++) dst[i * n + k] = src[order[i] * n + k];
      acc.setArray(dst);
    }
    const ixNew = doc.createAccessor().setType('SCALAR')
      .setArray(used.size > 65535 ? new Uint32Array(packed) : new Uint16Array(packed));
    prim.setIndices(ixNew);
  }
}
await doc.transform(prune(), dedup());
console.log('welded+simplified:', Math.round(triCount()), 'tris');

await doc.transform(textureCompress({ encoder: sharp, targetFormat: 'jpeg', quality: Q, resize: [TEX, TEX] }));
const mem = root.listTextures().reduce((s, t) => { const z = t.getSize(); return s + (z ? z[0] * z[1] * 4 * 1.333 : 0); }, 0) / 1048576;
console.log('textures:', root.listTextures().map(t => (t.getSize() || []).join('x')).join(' '), '=', mem.toFixed(1), 'MB of GPU memory');
await io.write(OUT, doc);
