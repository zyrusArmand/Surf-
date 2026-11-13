// Offline bake of the cumulus atlas for Surf/Cumulus: real 3D clouds, lit from six directions.
//
//   node bake.mjs [outDir] [--bin dir] [--preview file.png] [--only k] [--puffs]
//
// outDir defaults to ../../Assets/Resources/Clouds next to this script. It writes two 512x512
// RGBA PNGs, eight 256x128 tiles each (two across, four down, tile 0 at the bottom left, the
// cloud's base at the bottom of its tile, as Unity's v runs up):
//   cumulus_pos.png  R G B = light arriving from +X +Y +Z, A = coverage (1 - transmittance)
//   cumulus_neg.png  R G B = light arriving from -X -Y -Z, A = the single-scattered part of the
//                    light from straight behind (what the silver lining is made of)
// in the sprite's own frame: X right, Y up, Z toward the viewer. Each light value is the radiance
// the viewer sees for a sun of unit brightness from that direction, divided by coverage so it
// survives filtering (and spread into the clear texels round the cloud so mips never pull in a
// dark fringe), scaled by 1/ENC and stored square-rooted for precision in the shadows; the
// shader squares it back.
//
// Why six directions: a light from any direction is very nearly a blend of these six, each
// taking the share the light leans its way, so a flat sprite can be lit like a volume for two
// texture reads. The lightmaps carry what a sprite cannot work out on its own: the
// self-shadowing of a thick cloud, the light diffusing through it, its thin fringes and the
// dark creases between its billows.
//
// What each cloud is: one of eight hand-laid shapes (CLOUDS: flat fair-weather heaps, middling
// ones, towering congestus, a broken one, a wind-torn one), each a few clumps -- a squat heap,
// or a column narrowing up to a crown -- merged low down with a smooth minimum. Big lobes boil
// out of each clump and smaller ones out of those, of mixed sizes, so it reads as billows on
// billows rather than one size of ball; then a two-scale domain warp bends them all out of
// round and soft cellular noise roughens the surface below them. The base is only roughly
// level: each thermal condenses at its own height, the level wanders and is lumpy, lobes sag
// below it and ragged vapour hangs under it, and it is softer than the crowns. The fringe is
// eaten into wisps by finer noise, more down the flanks and under the base than on the
// crowns, and a torn cloud's downwind side is drawn out into streaks.
//
// How it is lit, per voxel and per direction:
//   - single scattering: the sun's beam after the optical depth between it and the voxel
//     (Beer-Lambert, summed along the axis), times a cloud droplet's phase for that direction
//     seen from the front (two Henyey-Greenstein lobes). The forward peak for light from behind
//     is left to the shader, which knows where the sun really is (the silver lining).
//   - multiple scattering: the octave approximation (each octave sees the cloud thinner and
//     adds less), then let diffuse a few voxels through the cloud, since scattered light does
//     not stay on the line it came in on. Without that a thin layer under a dark body is lit as
//     if it stood alone in the sun, which draws a bright line along every base.
//   - the powder effect: the scattered light only builds up where there is cloud round a point
//     to scatter it, 1 - exp(-k * local density), so lone wisps and the fringe stay dim rather
//     than glowing like a lit surface (which is what reads as an outline).
// Then each pixel marches front to back through its column, gathering what each voxel sends
// toward the viewer through the cloud in front of it.
//
// SPRAY PUFFS (--puffs): the same bake for the whitewater a board throws when it carves and
// leaves bubbling in its wake (Surf/Puff, Fx.cs). Eight small round puffs, 128 px tiles four
// across and two down, written as puff_pos.png and puff_neg.png (512x256, same channels as
// above). A puff is a dense lumpy ball of foam -- a core with lobes boiling out of it and a few
// torn-off bits round it -- with no flat base (it is thrown, not floating on a condensation
// level), its fringe eaten into wisps. Lit exactly as the clouds are, so the shader's sun side,
// shade side and through-light read the same way at any sun.
import { writeFileSync, mkdirSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Worker, isMainThread, parentPort, workerData } from 'node:worker_threads';
import { cpus } from 'node:os';

// the workers are told the mode; the main thread reads it off the command line
const PUFF = isMainThread ? process.argv.includes('--puffs') : !!workerData.puff;
const TW = PUFF ? 128 : 256, TH = 128, COLS = PUFF ? 4 : 2, ROWS = PUFF ? 2 : 4, AW = TW * COLS, AH = TH * ROWS, SHAPES = COLS * ROWS;
const NZ = PUFF ? 128 : 112;    // depth of the volume, in the same voxels as the pixels (a puff is as deep as it is wide)
const SIG = PUFF ? 0.40 : 0.45; // extinction per voxel at full density
const OCT = 6, OA = 0.45, OB = 0.55;   // multiple-scattering octaves: extinction and weight per octave
const ENC = 2.4;                // stored light = sqrt(radiance / ENC)
const MSK = PUFF ? 6.0 : 3.0;   // how quickly multiple scattering builds up with the cloud round a point (foam's wisps are
                                // nearly as bright as its body: dark fringes read as grit against the sea)
const hg = (c, g) => (1 - g * g) / Math.pow(1 + g * g - 2 * g * c, 1.5);
const phase = c => 0.7 * hg(c, 0.8) + 0.3 * hg(c, -0.3);
const PH90 = phase(0), PHBACK = phase(-1);
const PHTHRU = 0.16;            // light from behind: the forward peak is the shader's (see above)
const DIFF = 4;                 // how far the scattered light diffuses, in voxels

// ------------------------------------------------------------------ noise
function rngOf(seed) { let s = (Math.imul(seed, 2654435761) + 1013904223) >>> 0; if (!s) s = 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }
function hash3(x, y, z, s) {
  let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(z, 1440662683) + Math.imul(s, 1442695041)) >>> 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
  return (h ^ (h >>> 16)) >>> 0;
}
const GR = [[1,1,0],[-1,1,0],[1,-1,0],[-1,-1,0],[1,0,1],[-1,0,1],[1,0,-1],[-1,0,-1],[0,1,1],[0,-1,1],[0,1,-1],[0,-1,-1],[1,1,0],[-1,1,0],[0,-1,1],[0,-1,-1]];
const fade = t => t * t * t * (t * (t * 6 - 15) + 10);
function grad(ix, iy, iz, s, x, y, z) { const g = GR[hash3(ix, iy, iz, s) & 15]; return g[0] * x + g[1] * y + g[2] * z; }
// gradient noise, about -1..1
function perlin(x, y, z, s) {
  const ix = Math.floor(x), iy = Math.floor(y), iz = Math.floor(z);
  const fx = x - ix, fy = y - iy, fz = z - iz, u = fade(fx), v = fade(fy), w = fade(fz);
  const a = grad(ix, iy, iz, s, fx, fy, fz), b = grad(ix + 1, iy, iz, s, fx - 1, fy, fz);
  const c = grad(ix, iy + 1, iz, s, fx, fy - 1, fz), d = grad(ix + 1, iy + 1, iz, s, fx - 1, fy - 1, fz);
  const e = grad(ix, iy, iz + 1, s, fx, fy, fz - 1), f = grad(ix + 1, iy, iz + 1, s, fx - 1, fy, fz - 1);
  const g = grad(ix, iy + 1, iz + 1, s, fx, fy - 1, fz - 1), h = grad(ix + 1, iy + 1, iz + 1, s, fx - 1, fy - 1, fz - 1);
  const ab = a + (b - a) * u, cd = c + (d - c) * u, ef = e + (f - e) * u, gh = g + (h - g) * u;
  const l1 = ab + (cd - ab) * v, l2 = ef + (gh - ef) * v;
  return l1 + (l2 - l1) * w;
}
function fbm(x, y, z, s, oct) { let v = 0, a = 0.5, n = 0; for (let k = 0; k < oct; k++) { v += a * perlin(x, y, z, s + k * 31); n += a; x *= 2.02; y *= 2.02; z *= 2.02; a *= 0.5; } return v / n; }
// cellular noise: distance to the nearest of one jittered point per cell, 0 at a point, about 1 between
function worley(x, y, z, s) {
  const ix = Math.floor(x), iy = Math.floor(y), iz = Math.floor(z);
  let m = 9;
  for (let k = -1; k <= 1; k++) for (let j = -1; j <= 1; j++) for (let i = -1; i <= 1; i++) {
    const h = hash3(ix + i, iy + j, iz + k, s);
    const px = ix + i + (h & 1023) / 1023, py = iy + j + ((h >>> 10) & 1023) / 1023, pz = iz + k + ((h >>> 20) & 1023) / 1023;
    const dx = px - x, dy = py - y, dz = pz - z, d = dx * dx + dy * dy + dz * dz;
    if (d < m) m = d;
  }
  return Math.min(1, Math.sqrt(m));
}
// billows: rounded lumps at three scales, the bigger carrying the smaller (cauliflower)
function billow(x, y, z, s) { return 0.60 * (1 - worley(x, y, z, s)) + 0.27 * (1 - worley(x * 2.1, y * 2.1, z * 2.1, s + 7)) + 0.13 * (1 - worley(x * 4.3, y * 4.3, z * 4.3, s + 13)); }
const clamp01 = v => v < 0 ? 0 : v > 1 ? 1 : v;
const sstep = (a, b, x) => { const t = clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); };

// ------------------------------------------------------------------ one cloud's density
// The eight shapes, drawn from photographs of fair-weather cumulus and cumulus congestus rather
// than from one recipe, so the sky is not one cauliflower repeated. Each is a few clumps (a
// thermal, or a few that have merged): x across the tile from its middle as a share of its
// width, hw its half-width and h its height above the base in voxels, db how far its own base
// sits above or below the cloud's (neighbouring thermals do not condense at quite one level),
// lean how far its top is carried sideways by the wind, as a share of its height.
//   boil: how hard the tops churn (a growing turret is crisp and lumpy, an old one smoother)
//   tear: how much of the fringe is torn into wisps; tearX leans that to one side (downwind)
//   hang: how many lobes sag below the base; scud: how much ragged vapour trails under it
//   join: how high up neighbouring clumps are merged (gaps: which neighbours stay apart)
const CLOUDS = [
  // wide, flat fair-weather cumulus (humilis): low heaps of different sizes, some in front of
  // others, the downwind end a little ragged
  { base: 22, boil: 0.75, tear: 1.0, tearX: 0.5, hang: 3, scud: 0.8, c: [
    { x: -0.33, hw: 14, h: 14, db: 3, z: -6 }, { x: -0.17, hw: 30, h: 30, db: 0 }, { x: 0.02, hw: 17, h: 21, db: 1, z: 12 },
    { x: 0.15, hw: 27, h: 26, db: -1, z: -8 }, { x: 0.33, hw: 16, h: 15, db: 3, z: 4 }] },
  // a middling heap (mediocris): a few turrets, one standing above the rest
  { base: 21, boil: 1.0, tear: 1.0, tearX: 0.0, hang: 3, scud: 1.0, c: [
    { x: -0.29, hw: 16, h: 24, db: 3, z: 6 }, { x: -0.11, hw: 28, h: 36, db: 0 }, { x: 0.06, hw: 26, h: 62, db: -2, lean: 0.06, z: -8 },
    { x: 0.20, hw: 20, h: 40, db: 1, z: 12 }, { x: 0.32, hw: 14, h: 20, db: 2 }] },
  // towering congestus: one great tower off the middle, shoulders stepping down either side
  { base: 19, boil: 1.25, tear: 0.8, tearX: 0.0, hang: 4, scud: 1.0, join: 1.4, c: [
    { x: -0.35, hw: 13, h: 20, db: 4, z: 6 }, { x: -0.20, hw: 24, h: 50, db: 1, z: -8 }, { x: 0.02, hw: 34, h: 98, db: -2, lean: 0.07 },
    { x: 0.20, hw: 20, h: 58, db: 0, z: 10 }, { x: 0.34, hw: 14, h: 26, db: 3 }] },
  // broken: a main heap, a smaller one with clear sky between, and torn-off fragments
  { base: 24, boil: 1.0, tear: 1.2, tearX: 0.4, hang: 2, scud: 0.7, gaps: [0, 1, 3], c: [
    { x: -0.20, hw: 29, h: 40, db: 0 }, { x: -0.38, hw: 9, h: 13, db: 3 }, { x: 0.13, hw: 18, h: 30, db: 3, z: 6 },
    { x: 0.27, hw: 11, h: 17, db: 6, z: -6 }, { x: 0.40, hw: 7, h: 9, db: 8 }] },
  // torn by the wind: a heap sheared over, its downwind side drawn out and shredded into wisps
  { base: 23, boil: 0.85, tear: 1.7, tearX: 1.0, hang: 2, scud: 1.4, c: [
    { x: -0.27, hw: 16, h: 22, db: 2 }, { x: -0.10, hw: 27, h: 42, db: 0, lean: 0.35 }, { x: 0.10, hw: 20, h: 28, db: 1, lean: 0.45, z: 8 },
    { x: 0.27, hw: 14, h: 14, db: 3, lean: 0.5 }, { x: 0.38, hw: 9, h: 8, db: 5 }] },
  // a broad heap with one turret pushing up well off its middle
  { base: 21, boil: 1.1, tear: 1.0, tearX: -0.3, hang: 3, scud: 1.0, c: [
    { x: -0.36, hw: 13, h: 22, db: 3 }, { x: -0.19, hw: 32, h: 80, db: -1, lean: -0.05 }, { x: 0.0, hw: 20, h: 44, db: 0, z: 10 },
    { x: 0.15, hw: 24, h: 28, db: 2, z: -8 }, { x: 0.33, hw: 15, h: 18, db: 4 }] },
  // congestus, two towers leaning downwind, a heavy sagging base
  { base: 20, boil: 1.2, tear: 0.9, tearX: 0.0, hang: 5, scud: 1.2, join: 1.7, c: [
    { x: -0.33, hw: 15, h: 34, db: 2, z: -6 }, { x: -0.12, hw: 34, h: 88, db: -2, lean: 0.10 }, { x: 0.18, hw: 25, h: 58, db: 0, lean: 0.06, z: 4 },
    { x: 0.34, hw: 13, h: 26, db: 3 }] },
  // two heaps joined low down, a notch of sky between their tops, ragged vapour under both
  { base: 22, boil: 1.0, tear: 1.1, tearX: -0.5, hang: 5, scud: 1.3, c: [
    { x: -0.22, hw: 28, h: 52, db: -1 }, { x: -0.38, hw: 10, h: 18, db: 3, z: 6 }, { x: -0.01, hw: 15, h: 18, db: 2, z: -8 },
    { x: 0.19, hw: 23, h: 42, db: 1, lean: 0.08, z: 6 }, { x: 0.35, hw: 12, h: 20, db: 4 }] },
];
// cellular noise with the creases between cells rounded off (a soft minimum over the cells'
// points): plain cellular noise folds sharply where two cells meet, which in a cloud draws thin
// dark scratches between the billows
function sworley(x, y, z, s) {
  const ix = Math.floor(x), iy = Math.floor(y), iz = Math.floor(z);
  let acc = 0;
  for (let k = -1; k <= 1; k++) for (let j = -1; j <= 1; j++) for (let i = -1; i <= 1; i++) {
    const h = hash3(ix + i, iy + j, iz + k, s);
    const dx = ix + i + (h & 1023) / 1023 - x, dy = iy + j + ((h >>> 10) & 1023) / 1023 - y, dz = iz + k + ((h >>> 20) & 1023) / 1023 - z;
    acc += Math.exp(-7 * Math.sqrt(dx * dx + dy * dy + dz * dz));
  }
  return Math.min(1, Math.max(0, -Math.log(acc) / 7));
}
const smin = (a, b, k) => { const h = clamp01(0.5 + 0.5 * (b - a) / k); return b + (a - b) * h - k * h * (1 - h); };
const smax = (a, b, k) => -smin(-a, -b, k);
function density(seed) {
  // a stream of random numbers per clump and one for the rest, so retuning one part of a
  // shape does not reshuffle all the others
  const stream = k => rngOf(seed * 7919 + 17 + k * 104729);
  let R = stream(0);
  const spec = CLOUDS[(seed - 1) % CLOUDS.length];
  const B = spec.base;                                   // the condensation level, roughly
  const cz = NZ / 2;
  const tri = () => (R() + R()) - 1;
  // each clump a little different every bake of the same spec
  const cl = spec.c.map((c, ci) => {
    R = stream(ci + 1);
    const hw = c.hw * (0.92 + 0.16 * R());
    return { x: TW / 2 + c.x * TW + tri() * 4, z: cz + (c.z || 0) + tri() * 6, hw, hd: Math.min(NZ / 2 - 16, hw * (0.80 + 0.25 * R())),
             h: c.h * (0.93 + 0.14 * R()), b: B + (c.db || 0) * 1.4 + tri() * 2.5, lean: c.lean || 0, ci };
  });
  const top = Math.max(...cl.map(c => c.b + c.h));
  const ch = Math.max(20, top - B);
  // Primitives, as ellipsoids [x, y, z, rx, ry, rz]: the body (S, cut soft at the base) and the
  // lobes that sag below it (Hg, not cut).
  const S = [], Hg = [];
  const lobes = (px, py, pz, r, ox, oy, oz, depth, list) => {
    // lobes boiling out of a lobe: each smaller than its parent by a random share (a few big,
    // more small, never one size), standing about half out of it on the side turned outward
    // and up, the same again on them: large billows carrying smaller ones
    if (depth <= 0 || r < 4) return;
    const n = 1 + ((R() * (depth > 1 ? 4 : 3)) | 0);
    for (let k = 0; k < n; k++) {
      // a direction near the outward one, tipped up: billows climb
      let dx = ox + tri() * 1.2, dy = oy + 0.45 + tri() * 0.9, dz = (oz + tri() * 1.2) * 0.6;
      const l = Math.hypot(dx, dy, dz) || 1; dx /= l; dy /= l; dz /= l;
      const rr = r * (0.32 + 0.30 * Math.pow(R(), 1.5)) * (1 - 0.4 * Math.abs(dz));
      const m = r * (0.85 + 0.20 * R());
      const qx = px + dx * m, qy = py + dy * m, qz = pz + dz * m * 0.85;
      list.push([qx, qy, qz, rr, rr * (0.82 + 0.22 * R()), rr]);
      lobes(qx, qy, qz, rr, dx, dy, dz, depth - 1, list);
    }
  };
  for (const c of cl) {
    R = stream(c.ci + 21);
    // the core: a squat rounded heap, or for a tall thermal a column of them narrowing upward
    // to the dome, its top carried along by the wind; its foot rounded, at its own level
    const segs = [];
    if (c.h < c.hw * 1.2) {
      const ry = c.h * 0.50;
      segs.push([c.x, c.b + ry, c.z, c.hw, ry, c.hd]);
    } else {
      const ry0 = c.hw * 0.55;
      segs.push([c.x, c.b + ry0, c.z, c.hw, ry0, c.hd]);
      let y = c.b + ry0;
      // a bell: broad at the foot where it spreads into the body, narrowing toward the top,
      // so the flanks step in rather than standing as straight walls
      const taper = y => c.hw * Math.max(0.38, 1.0 - 0.72 * Math.pow(clamp01((y - c.b) / c.h), 1.3));
      for (;;) {
        const ny = Math.min(y + taper(y) * 0.5, c.b + c.h - taper(y) * 0.85);
        if (ny <= y + 1) break;
        y = ny;
        const r2 = taper(y) * (0.86 + 0.24 * R());
        segs.push([c.x + c.lean * (y - c.b) + tri() * r2 * 0.25, y, c.z + tri() * r2 * 0.1, r2, r2 * 0.9, Math.min(r2, c.hd)]);
      }
    }
    for (const s of segs) S.push(s);
    // a tower's column bulges out in lobes down its flanks too, so it does not stand as a
    // straight-sided block
    for (let si = 1; si < segs.length; si++) {
      const [sx, sy, sz, rx, ry, rz] = segs[si];
      for (const sideX of [-1, 1]) {
        if (R() < 0.25) continue;
        const ox0 = sideX * (0.75 + 0.25 * R()), oy0 = tri() * 0.5, oz0 = tri() * 0.6;
        const l = Math.hypot(ox0, oy0, oz0), ox = ox0 / l, oy = oy0 / l, oz = oz0 / l;
        const rr = rx * (0.35 + 0.30 * R());
        const qx = sx + ox * rx * 0.85, qy = sy + oy * ry * 0.85, qz = sz + oz * rz * 0.85;
        S.push([qx, qy, qz, rr, rr * (0.8 + 0.25 * R()), rr]);
        lobes(qx, qy, qz, rr, ox, oy, oz, 1, S);
      }
    }
    // the crown: a few lobes heaped on the top, so a turret ends in a rounded head of
    // billows rather than a flat lid
    {
      const [sx, sy, sz, rx, ry, rz] = segs[segs.length - 1];
      const nc = 2 + ((R() * 3) | 0);
      for (let k = 0; k < nc; k++) {
        const th = R() * Math.PI * 2, el = 0.75 + 0.75 * R();
        const ox0 = Math.cos(th) * Math.cos(el), oy0 = Math.sin(el), oz0 = Math.sin(th) * Math.cos(el) * 0.6;
        const ol = Math.hypot(ox0, oy0, oz0), ox = ox0 / ol, oy = oy0 / ol, oz = oz0 / ol;
        const rr = Math.min(rx, ry) * (0.38 + 0.30 * R());
        const qx = sx + ox * rx * 0.75, qy = sy + oy * ry * 0.75, qz = sz + oz * rz * 0.75;
        S.push([qx, qy, qz, rr, rr * (0.85 + 0.2 * R()), rr]);
        lobes(qx, qy, qz, rr, ox, oy, oz, 2, S);
      }
    }
    // the billows: big lobes over the top and down the flanks of the core, carrying smaller
    // ones (three levels); how many and how far they stand out is the clump's boil
    const n1 = Math.round((4 + (c.hw + c.h) / 8) * (0.7 + 0.3 * spec.boil));
    for (let k = 0; k < n1; k++) {
      // most on the upper segments: a tower boils at its top, its column is smoother
      const si = Math.min(segs.length - 1, Math.floor(Math.pow(R(), 0.8) * segs.length));
      const [sx, sy, sz, rx, ry, rz] = segs[si];
      const th = R() * Math.PI * 2, el = -0.35 + 1.9 * Math.pow(R(), 0.85);
      // round the sides and over the top rather than straight at the lens, where a big lobe
      // adds nothing to the outline and reads as a ball stuck on the front
      const ox0 = Math.cos(th) * Math.cos(el), oy0 = Math.sin(el), oz0 = Math.sin(th) * Math.cos(el) * 0.45;
      const ol = Math.hypot(ox0, oy0, oz0), ox = ox0 / ol, oy = oy0 / ol, oz = oz0 / ol;
      const rr = Math.min(rx, ry * 1.4) * (0.22 + 0.50 * Math.pow(R(), 1.8)) * (0.85 + 0.15 * spec.boil) * (1 - 0.5 * Math.abs(oz));
      const m = 0.92 + 0.16 * R();
      const qx = sx + ox * rx * m, qy = sy + oy * ry * m, qz = sz + oz * rz * m;
      // a lobe low on the flank may bulge out below the base: it goes with the sagging ones
      const list = qy - rr < c.b + 2 && R() < 0.6 ? Hg : S;
      list.push([qx, qy, qz, rr, rr * (0.8 + 0.25 * R()), rr]);
      lobes(qx, qy, qz, rr, ox, oy, oz, 2, S);
    }
  }
  R = stream(41);
  // the clumps merged low down into one body, as thermals spread out under the inversion;
  // a broken cloud keeps clear sky between some of them
  const byX = [...cl].sort((a, b) => a.x - b.x);
  for (let k = 0; k + 1 < byX.length; k++) {
    const a = byX[k], b = byX[k + 1];
    if (spec.gaps && spec.gaps.includes(k)) continue;
    const lo = Math.min(a.h, b.h, a.hw * 1.2, b.hw * 1.2);
    const ry = lo * (0.35 + 0.1 * R()) * (spec.join || 1), rx = (b.x - a.x) * 0.5 + Math.min(a.hw, b.hw) * 0.35;
    S.push([(a.x + b.x) * 0.5 + tri() * 3, (a.b + b.b) * 0.5 + ry * 1.05, (a.z + b.z) * 0.5, rx, ry, Math.min(a.hd, b.hd) * 0.85]);
  }
  // kept inside the tile: a congestus whose billows would reach its top is pressed down a
  // little rather than cut off flat
  {
    let hi = 0;
    for (const [, sy, , , ry] of S) hi = Math.max(hi, sy + ry);
    const k = Math.min(1, (TH - 14 - B) / Math.max(1, hi - B));
    if (k < 1) for (const q of S) { q[1] = B + (q[1] - B) * k; q[4] *= k; }
  }
  // lobes sagging under the base: a thermal's foot is not cut level, it bulges down in places.
  // Spread along the whole base (not left to chance, or one cloud in four is flat again),
  // each under whichever clump stands over it, of mixed sizes, some hanging lower than others.
  {
    const x0 = Math.min(...cl.map(c => c.x - c.hw * 0.8)), x1 = Math.max(...cl.map(c => c.x + c.hw * 0.8));
    const n = spec.hang * 2;
    for (let k = 0; k < n; k++) {
      const x = x0 + (x1 - x0) * (k + 0.2 + 0.6 * R()) / n;
      let c = cl[0];
      for (const q of cl) if (Math.abs(q.x - x) / q.hw < Math.abs(c.x - x) / c.hw) c = q;
      if (Math.abs(c.x - x) > c.hw * 0.9) continue;      // clear sky here: nothing to sag
      const rx = c.hw * (0.22 + 0.35 * Math.pow(R(), 1.3)), ry = rx * (0.45 + 0.2 * R());
      Hg.push([x, c.b + ry * (0.3 * R() - 0.15), c.z + tri() * c.hd * 0.25, rx, ry, Math.min(rx * 1.2, c.hd * 0.8)]);
    }
  }
  // the smooth union of each set: soft unions, no creases where lobes meet
  const N = TW * TH * NZ, K = 3.2, MARGIN = 14;
  const field = list => {
    const sum = new Float32Array(N);
    for (const [sx, sy, sz, rx, ry, rz] of list) {
      const e = Math.max(rx, ry, rz) + MARGIN, rm = Math.min(rx, ry, rz);
      const x0 = Math.max(0, Math.floor(sx - e)), x1 = Math.min(TW - 1, Math.ceil(sx + e));
      const y0 = Math.max(0, Math.floor(sy - e)), y1 = Math.min(TH - 1, Math.ceil(sy + e));
      const z0 = Math.max(0, Math.floor(sz - e)), z1 = Math.min(NZ - 1, Math.ceil(sz + e));
      for (let z = z0; z <= z1; z++) for (let y = y0; y <= y1; y++) {
        const row = (z * TH + y) * TW;
        for (let x = x0; x <= x1; x++) {
          const dx = (x + 0.5 - sx) / rx, dy = (y + 0.5 - sy) / ry, dz = (z + 0.5 - sz) / rz;
          const d = (Math.sqrt(dx * dx + dy * dy + dz * dz) - 1) * rm;
          if (d < MARGIN) sum[row + x] += Math.exp(-d / K);
        }
      }
    }
    return sum;
  };
  const sumB = field(S), sumH = field(Hg);
  // where the cloud stands over the sea, for the base's level and the vapour trailing under it
  const foot = (x, z, grow) => {
    let f = 0;
    for (const c of cl) { const u = (x - c.x) / (c.hw * grow), v = (z - c.z) / (c.hd * grow); f += Math.exp(-(u * u + v * v) * 1.6); }
    return Math.min(1, f);
  };
  const baseAt = (x, z) => {                             // each thermal's own condensation level, blended
    let s = 0, w = 0;
    for (const c of cl) { const u = (x - c.x) / c.hw, v = (z - c.z) / c.hd, q = Math.exp(-(u * u + v * v)) + 1e-6; s += q * c.b; w += q; }
    return s / w;
  };
  const sd = seed * 101 + 7;
  const D = new Float32Array(N);
  const win = (v, n, m) => sstep(0, m, v) * sstep(n, n - m, v);   // nothing at the tile's edges
  for (let z = 0; z < NZ; z++) for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
    const i = (z * TH + y) * TW + x, sB = sumB[i], sH = sumH[i];
    const px = x + 0.5, py = y + 0.5, pz = z + 0.5;
    const nearBase = py < B + 14 && py > B - 20;
    if (sB <= 0 && sH <= 0 && !nearBase) continue;
    let d = sB > 0 ? -K * Math.log(sB) : 99;
    const dh = sH > 0 ? -K * Math.log(sH) : 99;
    let dens = 0;
    if (Math.min(d, dh) < MARGIN - 1) {
      const hgt = clamp01((py - B) / ch);
      // domain warp, at two scales: the billows lean, stretch and run into one another
      // instead of sitting as balls on a grid
      const wx = px + 10 * fbm(px / 34, py / 34, pz / 34, sd + 1, 2);
      const wy = py + 7 * fbm(px / 34 + 5.2, py / 34, pz / 34, sd + 2, 2);
      const wz = pz + 10 * fbm(px / 34, py / 34 + 3.1, pz / 34, sd + 3, 2);
      const vx = wx + 6 * fbm(wx / 12, wy / 12, wz / 12, sd + 4, 2);
      const vy = wy + 6 * fbm(wx / 12 + 7.7, wy / 12, wz / 12, sd + 5, 2);
      const vz = wz + 6 * fbm(wx / 12, wy / 12 + 1.3, wz / 12, sd + 6, 2);
      // the surface detail below the lobes: cellular billows at two scales, the smaller
      // carried on the larger, how hard they stand out itself varying across the cloud so some
      // parts boil and some are smooth vapour; the crowns boil most, the base least
      const bl = 0.70 * (1 - sworley(vx / 12, vy / 12, vz / 12, sd + 11)) + 0.30 * (1 - sworley(vx / 6, vy / 6, vz / 6, sd + 13));
      const patch = 0.45 + 0.9 * (0.5 + 0.5 * fbm(px / 26, py / 26, pz / 26, sd + 17, 2));
      const amp = (2.0 + 4.2 * sstep(0.04, 0.5, hgt)) * patch * spec.boil;
      d -= (bl - 0.53) * 2 * amp + 1.2 * fbm(vx / 9, vy / 9, vz / 9, sd + 19, 2);
      // the base: soft and only roughly level, rising and falling from thermal to thermal and
      // lumpy within each, rounded off where it meets the flanks rather than cut square
      const bz = baseAt(px, pz) + 11 * fbm(px / 24, 0.5, pz / 70, sd + 23, 2) + 2.6 * fbm(wx / 10, 1.5, wz / 24, sd + 25, 2) - 3.5 * (1 - sworley(wx / 15, 0.5, wz / 15, sd + 26));
      d = smax(d, (bz - 2.5 - wy) * 0.9, 6);
      // and the lobes that sag below it, lumpy like the rest
      const dl = dh - (bl - 0.53) * 4 - 2.2 * fbm(vx / 7, vy / 7, vz / 7, sd + 27, 2);
      d = smin(d, dl, 4);
      // where the wind tears at it, the edge is drawn out into streaks: stretched noise pulls
      // strands out and opens gaps between them, most on a torn cloud's downwind side
      const side = (px - TW / 2) / (TW * 0.4);
      const tw = clamp01((spec.tear - 0.7) * (0.5 + spec.tearX * side) * (0.6 + 0.8 * (0.5 + 0.5 * fbm(px / 30, py / 30, pz / 30, sd + 45, 2))));
      if (tw > 0) d += tw * 7 * (0.15 - fbm(vx / 18, vy / 6, vz / 10, sd + 43, 3));
      if (d < 8) {
        // the edge is soft, softest under the base where the vapour is only just condensing,
        // and where it is torn
        const soft = 1 - sstep(-2, 12, py - bz);
        dens = sstep(2.5 + 4 * soft + 6 * tw, -4 - 1 * soft, d) * (1 - 0.35 * tw);
        // the fringe eaten into wisps: finer noise takes away most where the cloud is thinnest,
        // more down the flanks and under the base than on the crowns, which stay crisp where the
        // thermals are rising; a torn cloud more so, and most on its downwind side
        const tear = spec.tear * Math.max(0.3, 1 + spec.tearX * side) * (0.75 + 0.5 * (0.5 + 0.5 * fbm(px / 18, py / 18, pz / 18, sd + 29, 2)));
        const ero = (0.5 + 0.5 * fbm(vx / 8, vy / 8, vz / 8, sd + 31, 2)) * Math.min(0.92, (0.12 + 0.40 * (1 - sstep(0.15, 0.6, hgt)) + 0.2 * soft) * tear);
        dens = clamp01((dens - ero * (1 - dens * 0.6)) / (1 - ero * 0.4));
      }
    }
    // ragged vapour under the base: soft tatters hanging from it, torn and drawn out sideways
    // by the wind under the cloud, fewer and thinner the further down, only where the cloud
    // stands over them
    if (nearBase && spec.scud > 0) {
      const f = foot(px, pz, 0.9);
      if (f > 0.04) {
        const yb = baseAt(px, pz);
        const below = clamp01((yb + 2 - py) / 13);       // 0 at the base, 1 well under it
        if (below > 0 && below < 1) {
          const qx = px + 7 * fbm(px / 22, py / 10, pz / 22, sd + 33, 2), qz = pz + 7 * fbm(px / 22 + 3.3, py / 10, pz / 22, sd + 35, 2);
          const n = fbm(qx / 15, py / 8, qz / 15, sd + 37, 3) + 0.25 * fbm(qx / 6, py / 5, qz / 6, sd + 39, 2);
          const w = sstep(0.02 + 0.40 * below, 0.30 + 0.40 * below, n) * f * (1 - below * below) * 0.38 * spec.scud;
          dens = Math.max(dens, Math.min(0.5, w));
        }
      }
    }
    if (dens <= 0) continue;
    // and a little variation inside, so the shadows are not smooth as plaster
    dens *= 0.80 + 0.20 * (0.5 + 0.5 * fbm(px / 20, py / 20, pz / 20, sd + 41, 2));
    dens *= win(px, TW, 10) * win(py, TH, 6) * win(pz, NZ, 8);
    D[i] = dens;
  }
  return D;
}

// ------------------------------------------------------------------ one spray puff's density
// A ball of whitewater as it bursts off a rail: a dense core, lobes boiling out of it all round
// (it has no up or down of its own, so no base), a few torn-off bits hanging just outside it,
// the lot drawn out a little along a random line as thrown water is. Kept well inside the tile
// so nothing ever touches its edge: a puff must never show the square it is drawn on.
function puffDensity(seed) {
  const R = rngOf(seed * 6151 + 29);
  const cx = TW / 2 + (R() - 0.5) * 6, cy = TH / 2 + (R() - 0.5) * 6, cz = NZ / 2;
  const R0 = TW * (0.16 + 0.03 * R());                  // the core's radius
  const ang = R() * Math.PI, st = 1 + 0.22 * R();       // drawn out along (cos, sin) by st
  const ca = Math.cos(ang), sa = Math.sin(ang);
  const at = (dx, dy, dz) => {                          // an offset from the centre, stretched along the throw
    const a = dx * ca + dy * sa, b = -dx * sa + dy * ca, s = a * st;
    return [s * ca - b * sa, s * sa + b * ca, dz];
  };
  const dir = () => { const u = R() * 2 - 1, t = R() * Math.PI * 2, q = Math.sqrt(1 - u * u); return [q * Math.cos(t), q * Math.sin(t), u]; };
  const S = [[0, 0, 0, R0]];
  // the lobes: big ones standing well out of the core, so the outline is lumpy all the way
  // round, and a second ring of smaller ones boiling out of those
  const nl = 6 + ((R() * 4) | 0);
  for (let k = 0; k < nl; k++) {
    const d = dir(), r = R0 * (0.42 + 0.30 * R()), m = R0 * (0.62 + 0.40 * R());
    const c = at(d[0] * m, d[1] * m, d[2] * m * 0.8);
    S.push([...c, r]);
    const nb = 1 + ((R() * 3) | 0);
    for (let j = 0; j < nb; j++) {
      const e = dir(), rr = r * (0.38 + 0.25 * R()), mm = r * (0.75 + 0.25 * R());
      S.push([c[0] + e[0] * mm, c[1] + e[1] * mm, c[2] + e[2] * mm * 0.8, rr]);
    }
  }
  // torn-off bits: small, just clear of the body or barely joined to it
  const ns = 3 + ((R() * 5) | 0);
  for (let k = 0; k < ns; k++) {
    const d = dir(), r = R0 * (0.12 + 0.14 * R()), m = R0 * (1.25 + 0.35 * R());
    S.push([...at(d[0] * m, d[1] * m, d[2] * m * 0.6), r]);
  }
  // fitted to the tile: shrunk about the centre until the farthest lump clears its edge by a
  // margin the surface's boiling cannot cross
  let ext = 1, extZ = 1;
  for (const [x, y, z, r] of S) { ext = Math.max(ext, Math.hypot(x, y) + r); extZ = Math.max(extZ, Math.abs(z) + r); }
  const fit = Math.min(1, (TW / 2 - 15) / ext, (NZ / 2 - 15) / extZ);
  for (const q of S) { q[0] = cx + q[0] * fit; q[1] = cy + q[1] * fit; q[2] = cz + q[2] * fit; q[3] *= fit; }
  const N = TW * TH * NZ, K = 2.4, MARGIN = 12;
  const sum = new Float32Array(N);
  for (const [sx, sy, sz, r] of S) {
    const e = r + MARGIN;
    const x0 = Math.max(0, Math.floor(sx - e)), x1 = Math.min(TW - 1, Math.ceil(sx + e));
    const y0 = Math.max(0, Math.floor(sy - e)), y1 = Math.min(TH - 1, Math.ceil(sy + e));
    const z0 = Math.max(0, Math.floor(sz - e)), z1 = Math.min(NZ - 1, Math.ceil(sz + e));
    for (let z = z0; z <= z1; z++) for (let y = y0; y <= y1; y++) {
      const row = (z * TH + y) * TW;
      for (let x = x0; x <= x1; x++) {
        const dx = x + 0.5 - sx, dy = y + 0.5 - sy, dz = z + 0.5 - sz;
        const dd = Math.sqrt(dx * dx + dy * dy + dz * dz) - r;
        if (dd < MARGIN) sum[row + x] += Math.exp(-dd / K);
      }
    }
  }
  const sd = seed * 131 + 977;
  const D = new Float32Array(N);
  const win = (v, n, m) => sstep(0, m, v) * sstep(n, n - m, v);
  for (let z = 0; z < NZ; z++) for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
    const i = (z * TH + y) * TW + x, s = sum[i];
    if (s <= 0) continue;
    let d = -K * Math.log(s);
    if (d > MARGIN - 1) continue;
    const px = x + 0.5, py = y + 0.5, pz = z + 0.5;
    const wx = px + 5 * fbm(px / 22, py / 22, pz / 22, sd + 1, 2);
    const wy = py + 5 * fbm(px / 22 + 5.2, py / 22, pz / 22, sd + 2, 2);
    const wz = pz + 5 * fbm(px / 22, py / 22 + 3.1, pz / 22, sd + 3, 2);
    // cauliflower all over: foam boils on every side
    d -= (billow(wx / 9, wy / 9, wz / 9, sd + 11) - 0.42) * 2 * 4.0;
    if (d > 7) continue;
    let dens = sstep(7, -5, d);
    // the fringe torn into wisps, most where it is thinnest: rags at one scale, fuzz at another
    const ero = (0.5 + 0.5 * fbm(wx / 4.2, wy / 4.2, wz / 4.2, sd + 31, 3)) * (0.40 + 0.38 * (0.5 + 0.5 * fbm(wx / 10, wy / 10, wz / 10, sd + 51, 2)));
    dens = clamp01((dens - ero * (1 - dens * 0.6)) / (1 - ero * 0.4));
    dens *= 0.84 + 0.16 * (0.5 + 0.5 * fbm(px / 12, py / 12, pz / 12, sd + 41, 2));
    dens *= win(px, TW, 8) * win(py, TH, 8) * win(pz, NZ, 8);
    D[i] = dens;
  }
  return D;
}

// ------------------------------------------------------------------ lighting
// optical depth toward the light along one axis: what the light crosses to reach each voxel
function tauToward(D, axis, sign) {
  const T = new Float32Array(D.length);
  const n = axis === 0 ? TW : axis === 1 ? TH : NZ;
  const stride = axis === 0 ? 1 : axis === 1 ? TW : TW * TH;
  const outer = D.length / n;
  for (let o = 0; o < outer; o++) {
    // the base index of this line
    let b;
    if (axis === 0) b = o * TW;
    else if (axis === 1) { const x = o % TW, z = (o / TW) | 0; b = z * TW * TH + x; }
    else b = o;
    let acc = 0;
    if (sign > 0) for (let k = n - 1; k >= 0; k--) { const i = b + k * stride, s = D[i] * SIG; T[i] = acc + s * 0.5; acc += s; }
    else for (let k = 0; k < n; k++) { const i = b + k * stride, s = D[i] * SIG; T[i] = acc + s * 0.5; acc += s; }
  }
  return T;
}
// a box blur of the density, for the powder term's sense of "how much cloud is round here"
function blur3(D, r) {
  let a = D, b = new Float32Array(D.length);
  const dims = [[1, TW], [TW, TH], [TW * TH, NZ]];
  for (const [st, n] of dims) {
    b = new Float32Array(D.length);
    for (let i = 0; i < D.length; i++) {
      const k = Math.floor(i / st) % n; let s = 0, c = 0;
      for (let j = -r; j <= r; j++) { const kk = k + j; if (kk < 0 || kk >= n) continue; s += a[i + j * st]; c++; }
      b[i] = s / c;
    }
    a = b;
  }
  return a;
}
// what the viewer sees: march each pixel front (z = NZ-1, toward the viewer) to back
function view(D, src) {
  const L = new Float32Array(TW * TH), A = new Float32Array(TW * TH);
  for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
    let tv = 1, lum = 0;
    for (let z = NZ - 1; z >= 0; z--) {
      const i = (z * TH + y) * TW + x, d = D[i];
      if (d <= 0) continue;
      const dt = Math.exp(-d * SIG);
      lum += tv * src[i] * (1 - dt);
      tv *= dt;
      if (tv < 1e-4) break;
    }
    L[y * TW + x] = lum; A[y * TW + x] = 1 - tv;
  }
  return { L, A };
}
// the lowest voxel with any body to it in each vertical column (x, z), for the silver lining
function cloudBottom(D) {
  const bot = new Float32Array(TW * NZ).fill(TH);
  for (let z = 0; z < NZ; z++) for (let x = 0; x < TW; x++)
    for (let y = 0; y < TH; y++) if (D[(z * TH + y) * TW + x] > 0.25) { bot[z * TW + x] = y; break; }
  return bot;
}
function bakeShape(seed) {
  const t0 = Date.now();
  const D = PUFF ? puffDensity(seed) : density(seed);
  // how much cloud is round each point: light only scatters many times where there is depth
  // for it to do so, so a lone wisp gets the single bounce and little else (the powder effect)
  const Db = blur3(D, 4);
  const DbD = DIFF === 4 ? Db : blur3(D, DIFF);
  // +X +Y +Z -X -Y -Z: the axis, the side, and the phase of single scattering toward the viewer
  // for a light that way (a cloud droplet's two lobes, 0.7 HG(0.8) + 0.3 HG(-0.3), against an
  // isotropic 1): a quarter turn for the four sides, back-scatter for a light behind the viewer.
  // Straight-through light from behind is left at the side value here; the shader adds the
  // forward peak (the silver lining) itself, where it can follow the sun.
  const dirs = [[0, 1, PH90], [1, 1, PH90], [2, 1, PHBACK], [0, -1, PH90], [1, -1, PH90], [2, -1, PHTHRU]];
  const maps = [];
  let alpha = null, single = null;
  for (const [ax, sg, ph] of dirs) {
    const T = tauToward(D, ax, sg);
    // the octaves, for the light scattered round inside: each sees the cloud thinner and adds less
    const ms = new Float32Array(D.length);
    // light from below meets the whole broad base at once; its first bounce diffuses with
    // the rest, so the base glows softly instead of drawing a lit skin
    const flat = ax === 1 && sg < 0 && !PUFF ? ph : 0;   // a puff has no base
    for (let i = 0; i < D.length; i++) {
      if (D[i] <= 0) continue;
      let s = flat * Math.exp(-T[i]), a = OA, b = OB;
      for (let k = 1; k < OCT; k++) { s += b * Math.exp(-T[i] * a); a *= OA; b *= OB; }
      ms[i] = s * D[i];
    }
    // and that light diffuses: spread over the cloud round each point (a blur over cloud only),
    // so a thin layer under a dark body is not lit as if it stood alone in the sun -- which is
    // what drew a bright line along every base and a bright rim round every back-lit cloud
    const msd = blur3(ms, DIFF);
    const src = new Float32Array(D.length);
    for (let i = 0; i < D.length; i++) {
      if (D[i] <= 0) continue;
      const around = 1 - Math.exp(-Db[i] * MSK);
      src[i] = Math.exp(-T[i]) * (ph - flat) + (msd[i] / Math.max(1e-4, DbD[i])) * around;
    }
    const v = view(D, src);
    maps.push(v.L); alpha = v.A;
    if (ax === 2 && sg < 0) {
      // the silver lining's raw stuff; not along the base, where it would draw a bright line
      // under the cloud (the base's glow at either end of the day is its own term). The base is
      // not level, so that is measured up from the cloud's lowest point in each column.
      const bot = PUFF ? null : cloudBottom(D);
      for (let i = 0; i < D.length; i++) {
        if (D[i] <= 0) { src[i] = 0; continue; }
        const y = ((i / TW) | 0) % TH, col = (i % TW) + ((i / (TW * TH)) | 0) * TW;
        src[i] = Math.exp(-T[i]) * (PUFF ? 1 : sstep(bot[col] + 1, bot[col] + 10, y));
      }
      single = view(D, src).L;
    }
  }
  maps.push(single);
  if (parentPort) parentPort.postMessage({ log: `shape ${seed}: ${((Date.now() - t0) / 1000).toFixed(1)}s` });
  return { maps, alpha };
}

// ------------------------------------------------------------------ packing
// unpremultiply (radiance per unit of coverage) and spread the colour out into the clear
// texels around the cloud, so filtering and mips never pull a dark fringe in from nothing
function unpremulAndDilate(Lm, A) {
  const N = TW * TH, out = new Float32Array(N), ok = new Uint8Array(N);
  for (let i = 0; i < N; i++) if (A[i] > 0.002) { out[i] = Lm[i] / A[i]; ok[i] = 1; }
  for (let pass = 0; pass < 300; pass++) {
    let changed = 0; const nx = new Uint8Array(ok);
    for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
      const i = y * TW + x; if (ok[i]) continue;
      let s = 0, c = 0;
      for (let j = -1; j <= 1; j++) for (let k = -1; k <= 1; k++) { const xx = x + k, yy = y + j; if (xx < 0 || yy < 0 || xx >= TW || yy >= TH) continue; const q = yy * TW + xx; if (ok[q]) { s += out[q]; c++; } }
      if (c) { out[i] = s / c; nx[i] = 1; changed++; }
    }
    ok.set(nx);
    if (!changed) break;
  }
  return out;
}
const B8 = v => Math.max(0, Math.min(255, Math.round(v * 255)));
function crc32(buf) { let c, crc = 0xffffffff; for (let n = 0; n < buf.length; n++) { c = (crc ^ buf[n]) & 0xff; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crc = (crc >>> 8) ^ c; } return (crc ^ 0xffffffff) >>> 0; }
function chunk(type, data) { const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type, 'ascii'), data]); const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td)); return Buffer.concat([len, td, crc]); }
// RGBA8 PNG; px is bottom-up (row 0 is v = 0, as the GPU and Unity have it), PNG rows run top-down
function png(px, w, h) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let r = 0; r < h; r++) {
    const src = (h - 1 - r) * w * 4, prev = r > 0 ? (h - r) * w * 4 : -1, o = r * (w * 4 + 1);
    raw[o] = 4;   // Paeth
    for (let i = 0; i < w * 4; i++) {
      const a = i >= 4 ? px[src + i - 4] : 0, b = prev >= 0 ? px[prev + i] : 0, c = prev >= 0 && i >= 4 ? px[prev + i - 4] : 0;
      const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
      const pr = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      raw[o + 1 + i] = (px[src + i] - pr) & 255;
    }
  }
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

// ------------------------------------------------------------------ main
if (!isMainThread) {
  const r = bakeShape(workerData.seed);
  parentPort.postMessage({ done: true, maps: r.maps, alpha: r.alpha });
} else {
  const args = process.argv.slice(2);
  const here = dirname(fileURLToPath(import.meta.url));
  let outDir = join(here, '../../Assets/Resources/Clouds'), binDir = null, preview = null, only = -1;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--puffs') continue;
    else if (args[i] === '--bin') binDir = args[++i];
    else if (args[i] === '--preview') preview = args[++i];
    else if (args[i] === '--only') only = +args[++i];
    else outDir = args[i];
  }
  const res = new Array(SHAPES);
  const queue = [...Array(SHAPES).keys()].filter(k => only < 0 || k === only);
  const t0 = Date.now();
  await new Promise((done, fail) => {
    let running = 0;
    const next = () => {
      if (!queue.length) { if (!running) done(); return; }
      const k = queue.shift(); running++;
      const w = new Worker(fileURLToPath(import.meta.url), { workerData: { seed: k + 1, puff: PUFF } });
      w.on('message', m => { if (m.log) console.log(m.log); if (m.done) { res[k] = m; } });
      w.on('error', fail);
      w.on('exit', () => { running--; next(); });
    };
    for (let i = 0; i < Math.max(1, Math.min(cpus().length, 8)); i++) next();
  });
  const pos = new Uint8Array(AW * AH * 4), neg = new Uint8Array(AW * AH * 4);
  let peak = 0;
  for (let k = 0; k < SHAPES; k++) {
    const r = res[k]; if (!r) continue;
    const ox = (k % COLS) * TW, oy = ((k / COLS) | 0) * TH;
    const U = r.maps.map(m => unpremulAndDilate(m, r.alpha));
    for (let i = 0; i < TW * TH; i++) for (let m = 0; m < 6; m++) if (r.alpha[i] > 0.05) peak = Math.max(peak, U[m][i]);
    for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
      const i = y * TW + x, d = ((oy + y) * AW + ox + x) * 4;
      const e = m => B8(Math.sqrt(Math.max(0, U[m][i]) / ENC));
      pos[d] = e(0); pos[d + 1] = e(1); pos[d + 2] = e(2); pos[d + 3] = B8(r.alpha[i]);
      neg[d] = e(3); neg[d + 1] = e(4); neg[d + 2] = e(5); neg[d + 3] = B8(Math.min(1, U[6][i]));
    }
  }
  console.log(`baked in ${((Date.now() - t0) / 1000).toFixed(1)}s, brightest light ${peak.toFixed(2)} (encoded up to ${ENC})`);
  mkdirSync(outDir, { recursive: true });
  const stem = PUFF ? 'puff' : 'cumulus';
  writeFileSync(join(outDir, stem + '_pos.png'), png(pos, AW, AH));
  writeFileSync(join(outDir, stem + '_neg.png'), png(neg, AW, AH));
  if (binDir) { mkdirSync(binDir, { recursive: true }); writeFileSync(join(binDir, stem + '_pos.bin'), pos); writeFileSync(join(binDir, stem + '_neg.bin'), neg); }
  if (preview) {
    // each tile lit three ways over a sky blue: from the upper right in front, from the side,
    // and from behind; a quick look at the shapes without the game's shader
    const PW = AW * 3, P = new Uint8Array(PW * AH * 4);
    const Ls = [[0.55, 0.55, 0.63], [0.97, 0.25, -0.05], [0.25, 0.15, -0.95]];
    for (let v = 0; v < 3; v++) {
      const Lv = Ls[v], n = Math.hypot(...Lv), L = Lv.map(c => c / n);
      for (let y = 0; y < AH; y++) for (let x = 0; x < AW; x++) {
        const s = (y * AW + x) * 4, d = (y * PW + v * AW + x) * 4;
        const dec = c => (c / 255) * (c / 255) * ENC;
        const p = [dec(pos[s]), dec(pos[s + 1]), dec(pos[s + 2])], q = [dec(neg[s]), dec(neg[s + 1]), dec(neg[s + 2])];
        const ws = Math.abs(L[0]) + Math.abs(L[1]) + Math.abs(L[2]);
        let l = 0; for (let a = 0; a < 3; a++) l += (L[a] > 0 ? p[a] : q[a]) * Math.abs(L[a]) / ws;   // as the shader blends them
        const amb = 0.5 * p[1] * 0.25 + 0.125 * 0.25 * (p[0] + q[0] + p[2] + q[2]);
        const a = pos[s + 3] / 255;
        const sky = [0.30, 0.52, 0.80];
        for (let c = 0; c < 3; c++) {
          const lin = (l * 0.9 + amb * [0.7, 0.8, 1.0][c]) * a + Math.pow(sky[c], 2.2) * (1 - a);
          P[d + c] = B8(Math.pow(Math.min(1, lin), 1 / 2.2));
        }
        P[d + 3] = 255;
      }
    }
    writeFileSync(preview, png(P, PW, AH));
  }
}
