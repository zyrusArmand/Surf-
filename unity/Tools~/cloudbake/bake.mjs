// Offline bake of the cumulus atlas for Surf/Cumulus: real 3D clouds, lit from six directions.
//
//   node bake.mjs [outDir] [--bin dir] [--preview file.png] [--only k]
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
// What each cloud is: a cluster of turrets (columns of spheres, each topped by a dome boiling
// with smaller puffs), merged with a smooth minimum, cut flat at the condensation level, its
// surface displaced into cauliflower lumps by domain-warped cellular noise, and its fringe
// eaten into wisps by finer noise, more down the flanks than on the crowns.
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
import { writeFileSync, mkdirSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Worker, isMainThread, parentPort, workerData } from 'node:worker_threads';
import { cpus } from 'node:os';

const TW = 256, TH = 128, COLS = 2, ROWS = 4, AW = TW * COLS, AH = TH * ROWS, SHAPES = COLS * ROWS;
const NZ = 112;                 // depth of the volume, in the same voxels as the pixels
const SIG = 0.45;               // extinction per voxel at full density
const OCT = 6, OA = 0.45, OB = 0.55;   // multiple-scattering octaves: extinction and weight per octave
const ENC = 2.4;                // stored light = sqrt(radiance / ENC)
const MSK = 3.0;                // how quickly multiple scattering builds up with the cloud round a point
const hg = (c, g) => (1 - g * g) / Math.pow(1 + g * g - 2 * g * c, 1.5);
const phase = c => 0.7 * hg(c, 0.8) + 0.3 * hg(c, -0.3);
const PH90 = phase(0), PHBACK = phase(-1);
const PHTHRU = 0.16;            // light from behind: the forward peak is the shader's (see above)
const BASE = 15;               // the flat base, in voxels up from the tile's bottom
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
function density(seed) {
  const R = rngOf(seed * 7919 + 17);
  const kind = seed % 4;
  const B = BASE;                                       // the condensation level: the flat base
  const cw = TW * (0.60 + R() * 0.12);                  // the heap's width
  const hk = [0.32, 0.42, 0.50, 0.22][kind];
  const ch = Math.min(cw * hk, TH - B - 14);            // the tallest turret above the base
  const cx = TW / 2 + (R() - 0.5) * 10, cz = NZ / 2;
  const dzH = Math.min(NZ / 2 - 14, cw * 0.30);         // half its depth
  const S = [];
  const tri = () => (R() + R()) - 1;
  // Turrets: a cumulus is a cluster of rising thermals, each a column topped by a dome, the
  // middle ones tallest. A congestus has a few tall ones, a fair-weather cloud more and lower.
  const nT = [4, 4, 3, 6][kind] + ((R() * 2) | 0);
  for (let t = 0; t < nT; t++) {
    const u = nT === 1 ? 0 : (t / (nT - 1)) * 2 - 1;
    const x = cx + u * cw * 0.30 + (R() - 0.5) * cw * 0.08;
    const z = cz + (R() - 0.5) * dzH * 1.1;
    const env = Math.exp(-u * u * (kind === 2 ? 0.9 : 1.6));
    const h = Math.max(14, ch * env * (0.62 + 0.38 * R()));
    const r = Math.min(h * 0.62, cw / nT * (0.52 + 0.26 * R()) + 6);
    // the column, overlapping spheres from the base up to the dome
    for (let y = B + r * 0.5; y < B + h - r; y += r * 0.6) S.push([x + (R() - 0.5) * r * 0.3, y, z + (R() - 0.5) * r * 0.3, r * (0.85 + 0.15 * R())]);
    const dy = B + Math.max(r * 0.5, h - r * 1.15);
    S.push([x, dy, z, r]);
    // its dome boils: puffs on the upper half, each standing half out of it
    const np = 5 + ((R() * 4) | 0);
    for (let p = 0; p < np; p++) {
      const th = R() * Math.PI * 2, el = 0.25 + R() * 1.1;
      const pr = r * (0.34 + 0.22 * R());
      const k = r * 0.86;
      S.push([x + Math.cos(th) * Math.cos(el) * k, dy + Math.sin(el) * k * 0.92, z + Math.sin(th) * Math.cos(el) * k * 0.8, pr]);
    }
  }
  const nb = 7 + ((R() * 3) | 0);                       // the base: wide lumps the length of it
  for (let k = 0; k < nb; k++) {
    const t = (k + 0.5) / nb * 2 - 1, x = cx + t * cw * 0.40 + (R() - 0.5) * 6;
    const r = cw * (0.085 + 0.03 * R()) * (1 - 0.30 * Math.abs(t));
    S.push([x, B + r * 0.35, cz + tri() * dzH * 0.5, r]);
  }
  // the heap's distance field, a smooth minimum over the spheres (soft unions, no creases)
  const N = TW * TH * NZ, K = 4.5, MARGIN = 16;
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
        const d = Math.sqrt(dx * dx + dy * dy + dz * dz) - r;
        if (d < MARGIN) sum[row + x] += Math.exp(-d / K);
      }
    }
  }
  const sd = seed * 101 + 7;
  const D = new Float32Array(N);
  const win = (v, n, m) => sstep(0, m, v) * sstep(n, n - m, v);   // nothing at the tile's edges
  for (let z = 0; z < NZ; z++) for (let y = 0; y < TH; y++) for (let x = 0; x < TW; x++) {
    const i = (z * TH + y) * TW + x, s = sum[i];
    if (s <= 0) continue;
    let d = -K * Math.log(s);
    if (d > MARGIN - 1) continue;
    const px = x + 0.5, py = y + 0.5, pz = z + 0.5;
    // domain warp: the lumps lean and stream instead of sitting on a grid
    const wx = px + 9 * fbm(px / 38, py / 38, pz / 38, sd + 1, 2);
    const wy = py + 6 * fbm(px / 38 + 5.2, py / 38, pz / 38, sd + 2, 2);
    const wz = pz + 9 * fbm(px / 38, py / 38 + 3.1, pz / 38, sd + 3, 2);
    const hgt = clamp01((py - B) / Math.max(1, ch));
    // cauliflower: the tops boil, the sides less, the base hardly at all
    const bl = billow(wx / 15, wy / 15, wz / 15, sd + 11);
    const amp = 2.5 + 7.5 * sstep(0.05, 0.55, hgt);
    d -= (bl - 0.42) * 2 * amp;
    // the flat base, fading out over a few feet and a little ragged: a knife cut, lit from
    // below at either end of the day, drew a bright straight line along the bottom
    const base = B - py + 2.6 * fbm(px / 11, py / 5, pz / 11, sd + 21, 2);
    d = Math.max(d, base * 1.15);
    if (d > 6) continue;
    let dens = sstep(6, -5, d);
    // the fringe eaten into wisps: finer noise takes away most where the cloud is thinnest, and
    // more down the flanks than on the crowns, which stay crisp where the thermals are rising
    const ero = (0.5 + 0.5 * fbm(wx / 6.5, wy / 6.5, wz / 6.5, sd + 31, 3)) * (0.55 + 0.22 * (1 - sstep(0.35, 0.85, hgt)));
    dens = clamp01((dens - ero * (1 - dens * 0.6)) / (1 - ero * 0.4));
    // and a little variation inside, so the shadows are not smooth as plaster
    dens *= 0.82 + 0.18 * (0.5 + 0.5 * fbm(px / 20, py / 20, pz / 20, sd + 41, 2));
    dens *= win(px, TW, 10) * win(py, TH, 6) * win(pz, NZ, 8);
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
function bakeShape(seed) {
  const t0 = Date.now();
  const D = density(seed);
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
    // light from below meets the flat base all along one straight edge; its first bounce
    // diffuses with the rest, so the base glows softly instead of drawing a lit skin
    const flat = ax === 1 && sg < 0 ? ph : 0;
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
      // the silver lining's raw stuff; not along the flat base, where the cut is so sharp it
      // would draw a hard bright line (the base's glow at either end of the day is its own term)
      for (let i = 0; i < D.length; i++) { const y = ((i / TW) | 0) % TH; src[i] = D[i] > 0 ? Math.exp(-T[i]) * sstep(BASE + 2, BASE + 12, y) : 0; }
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
    if (args[i] === '--bin') binDir = args[++i];
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
      const w = new Worker(fileURLToPath(import.meta.url), { workerData: { seed: k + 1 } });
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
  writeFileSync(join(outDir, 'cumulus_pos.png'), png(pos, AW, AH));
  writeFileSync(join(outDir, 'cumulus_neg.png'), png(neg, AW, AH));
  if (binDir) { mkdirSync(binDir, { recursive: true }); writeFileSync(join(binDir, 'cumulus_pos.bin'), pos); writeFileSync(join(binDir, 'cumulus_neg.bin'), neg); }
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
