# 🏄 Pug Surf

A small 3D endless surfing game — a pug on a longboard, carving a procedurally
generated ocean. Built with [three.js](https://threejs.org/); no build step, no
dependencies to install.

## Play it

**Hosted:** https://zyrusarmand.github.io/Surf-/
(available once GitHub Pages is enabled — see below)

**Locally:** clone the repo and open `index.html` in any modern browser, or serve
the folder:

```bash
npx http-server -p 8080 .   # then open http://localhost:8080
```

## Controls

| Action | Touch | Keyboard | Mouse |
| --- | --- | --- | --- |
| Carve left / right | swipe left / right | `A` / `D` or `←` / `→` | drag left / right |
| Shift weight (nose / tail) | swipe up / down | `W` / `S` or `↑` / `↓` | drag up / down |
| Jump | JUMP button | `Space` | — |
| Spin | SPIN button | `J` (hold) | — |
| Flip | FLIP button | `K` (hold) | — |
| Grab (in air) / hand drag (on water) | GRAB button | `L` (hold) | — |
| Grab the seaplane's rope | ROPE! button | — | click ROPE! |

Land your rotations square or you'll eat it. Hitting a ramp launches you, jellyfish
bounce you, and buoys, logs and shark fins end the run.

## Files

- `index.html` — the whole game (markup, styles, and logic in one file)
- `vendor/three.min.js` — three.js r128, vendored so the page works offline and
  depends on no third-party host
- `.github/workflows/pages.yml` — publishes the repo root to GitHub Pages

## Enabling GitHub Pages

One-time setup: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
The workflow then publishes on every push to `main` (or to the game branch), and
the URL above starts working.
