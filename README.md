# 🏄 Pug Surf

A small 3D endless surfing game — a pug on a longboard, carving a procedurally
generated ocean. Built with [three.js](https://threejs.org/); no build step, no
dependencies to install.

## Play it

**▶ https://zyrusarmand.github.io/Surf-/**

Published from `main` by the workflow below, on every push.

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
| Handstand (water or air) | HAND button | `H` (hold) | — |
| Grab the seaplane's rope | ROPE! button | — | click ROPE! |

Runs pay coins — from how far you get and how well you score getting there — and the
board rack on the menu spends them. Every board is cosmetic; they all ride the same.

Land your rotations square or you'll eat it. Hitting a ramp launches you, jellyfish
bounce you, and buoys, logs and shark fins end the run.

## Files

- `index.html` — the whole game (markup, styles, and logic in one file)
- `vendor/three.min.js` — three.js r128, vendored so the page works offline and
  depends on no third-party host
- `vendor/GLTFLoader.js` — the glTF loader from the same release, for imported models
- `models/` — drop a `.glb` here to replace a built-in shape (see `models/README.md`)
- `.github/workflows/pages.yml` — publishes the repo root to GitHub Pages

## Versions

The running version is shown at the bottom of the screen. Bump `VERSION` in
`index.html` whenever something ships.

- **v1.25.0** — Three fixes from playtesting. The board floats: v1.23 gave it weight but
  sank it too far, so water washed over the deck on a straight line. It now rides about a
  third of a unit higher and only a genuine turn drives the inside rail under — there is a
  dead zone, so holding a line no longer quietly buries it. The octopus's arms are one
  continuous tube rather than a string of beads: fourteen shorter links with a ball at
  every joint filling the corners, and each arm's shoulder now swings the whole limb from
  up by the animal's face right down to hanging beneath it, every arm at its own pace.
  The jet ski's wake launch is floaty instead of fast — the fall runs at roughly a quarter
  gravity, so hang time goes from 2.6 to 4.2 seconds and the apex comes down from 17 units
  to 12, with the camera climbing harder and aiming higher to keep the rider in frame.
  The ski itself crosses about forty units out instead of a hundred, larger, slower and
  with a proper rooster tail, so you can see what threw the wave — and it comes round
  every 40 to 64 seconds rather than every 22.
- **v1.24.0** — Clouds drift across the sky and warm into the sunset with it, and
  flocks of gulls cross behind them, wings beating out of step. Four new boards, each
  lofted from a spec — outline, rocker, rail profile, paint — rather than modelled, at
  around sixteen thousand triangles apiece: a cream triple-stringer log, an orange
  twin-keel swallowtail fish, a navy pintail gun with a neon rail, and a seventies
  single-fin in resin bands. Runs now pay coins, and the board rack on the menu spends
  them; what you own and what you ride is remembered between sessions. New trick: the
  handstand, held on the water as well as in the air, paying while it lasts and paying
  more with a rotation going — but lay it over on the rail up there and you go over the
  front. Sky, boards and coins are all cosmetic: nothing about how the game plays has
  changed.
- **v1.23.0** — The board has weight in the water. It was hovering: it floated on the
  highest point under the hull, a touch above it, and snapped to that line every frame,
  which left it gliding over the sea rather than through it. It now displaces water —
  it sits about a third of a unit lower, answers to the mean depth under its whole
  length as well as the high point, and rides on a spring it has to work against
  instead of a rail it slides along. So it lags a fast-building face, plunges when the
  water falls away and rebounds when it comes back. A landing no longer stops dead on
  the surface: it keeps driving down about a third of a unit, buries itself and gets
  thrown back out, with spray and a camera kick scaled to how hard it hit. Carving
  buries the inside rail up to 0.4 deeper, and the hull banks and pitches further with
  the water it is lying on.
- **v1.22.1** — Half a surfboard. The export carried an unapplied Mirror modifier, so
  the file genuinely only held the left side. Models are now checked for it — all the
  geometry on one side of the origin with a clean edge sitting on it — and the missing
  half is rebuilt at load, with the face winding and normals turned back the right way
  so both sides light identically.
- **v1.22.0** — The surfboard is now a real model, built in Blender and imported.
  Getting it in raised three things the pipeline had to learn: colours exported with
  the model are kept instead of being painted over, only their reflection strength
  matched to the scene; a board modelled deck-down or pointing the wrong way is
  turned the right way up by finding the fins — the layer of the mesh that covers the
  most ground is the deck, and whatever stands clear of it is the fins, so they are
  put underneath and behind; and the board is aligned deck-to-deck rather than
  box-to-box, so a thin board sits down in the water like the thick built-in one
  instead of perching on top of it.
- **v1.21.0** — Blender models can be dropped straight into the game. Put a `.glb`
  in `models/` and it replaces the built-in shape — board, pug, buoy, log, jellyfish,
  ramp, octopus, shark fin or jet ski — while a missing file just leaves the
  procedural version in place. Each import is measured against the shape it replaces
  and rescaled, turned onto the same long axis and centred where the original sat, so
  an export at any size or orientation drops in correctly. Untextured parts are
  painted from the game's palette by object name. See `models/README.md`.
- **v1.20.0** — The jet ski crosses far out now, so its wake has a long run to build:
  born around 110 units out, it grows the whole way in and arrives about 23 times the
  height of the surrounding swell.
- **v1.19.0** — The wake is now a towering set wave — about sixteen times the height of
  the surrounding swell, broader, and building over a longer approach, with the same
  water shading and foam as before. The camera's height is measured from the water under
  the rider rather than from zero, so a big face lifts it instead of swallowing it.
- **v1.18.0** — The jet ski's wake is no longer a separate object. It is added straight
  into the ocean's own height field as a broad ridge, so the water shader lights it,
  foams it and fogs it exactly like every other wave — it simply arrives several times
  taller. The board and everything else afloat ride over it for free.
- **v1.17.0** — The jet ski was driving backwards: its bow points along local -z, and
  its heading was set to the opposite of its travel. Its wake is rebuilt too — shaded
  from the ocean's own palette with foam breaking along the lip instead of a pale sheet,
  opaque so it covers the swell it rolls over, and roughly half again as wide, deep and
  tall.
- **v1.16.0** — Shark fins hunt you again. Removing the head-on ones in v1.15.0 took
  their tracking with it; most fins now turn onto the rider at a limited rate while a
  floor on the heading keeps the approach angled rather than straight down the line.
- **v1.15.0** — The board floats like a hull: the water is sampled under its nose, tail
  and both rails, and it rests on the highest of them and lies along that surface, so
  waves no longer cut through it. The screen splat gets its own render pass so it stops
  showing through itself, plus a speckled texture and a bevel that no longer folds into
  the star's valleys. White discs removed from the jellyfish, sharks and octopus, and
  shark fins now cruise diagonal lines instead of swimming straight at you.
- **v1.14.0** — The board is collided as the 5m plank it is, not a point: a capsule
  from nose to tail, swept over the ground an obstacle covered that frame, so the nose
  makes contact instead of sliding into things first. Whatever you hit is knocked aside,
  lifted and set spinning rather than standing still while you tumble off.
- **v1.13.0** — Slower, especially early: about 9 mph off the line instead of 16, on a
  gentler ramp, with the swell surging you along less. The rider is no longer pinned to
  one spot — he travels fore and aft, driven up the board by leaning and lunging forward
  as he drops down a face, and the camera only partly follows so the movement reads.
- **v1.12.0** — Waves actually propagate now. Each component travels at its own deep
  water speed (omega = sqrt(g*k)), so long swell outruns short chop and the interference
  between them never settles — the sea is no longer a frozen pattern you drive through.
  Two chop components added for surface life. On a crash the rider genuinely comes off:
  he was parented to the board, so he had been riding it all the way down. He is now
  thrown one way and the board kicked the other, and he flails in the air and washes
  limp once he is in the water.
- **v1.11.0** — Foam rebuilt on rotated value noise. It was a product of
  axis-aligned sines, which tiles into a grid — the fish-net look. Now it reads as
  crest caps, torn patches and flecks. Two starfish can no longer splat your view
  back to back: a second hit while the first is still across the screen breaks apart
  beside you instead of stacking.
- **v1.10.0** — The camera rises with you. On the water it still only loosely tracks
  the swell so the board visibly lifts and drops, but off it — jump, ramp, wake launch
  or plane tow — it follows most of the way up, chases faster, eases back and lifts its
  aim, keeping you framed instead of drifting toward the top of the screen.
- **v1.9.0** — Waves build and fade as they roll in instead of a rigid pattern
  sliding past: every component now carries a travelling envelope, so the sea comes
  in sets with quiet stretches between. Crashes carry real momentum — forward speed
  throws the rider, lateral drift carries across, and a blown trick puts him in the
  water mid-rotation, still turning, rather than snapping upright first. Ramps give
  enough hang time for over two full spins or flips.
- **v1.8.0** — HUD split evenly across the top (score, distance, mph, best). Day now
  begins at sunrise and arcs up before settling into the sunset. Tentacle sweep roughly
  quadrupled so the arms visibly flow. The screen splat is sized from the viewport, so
  it fits within a phone's width instead of swallowing the screen. Speed climbs further
  with distance, every landed trick leaves you carrying extra speed, and leaning is a
  real throttle with a dead zone so carving no longer nudges it.
- **v1.7.0** — A visible sun and a sunset. The day cycle existed but needed ~4300 m
  to reach golden hour, so a normal run never saw it; it now warms within a few
  hundred metres and is a full sunset past a kilometre. The sun is a bright disc with
  a soft radial glow that sinks and reddens as you go. Sky only: the scene lights and
  the ocean's colours are deliberately left untouched, so the water, animals, board
  and pug render exactly as before.
- **v1.6.0** — Octopus reshaped to the reference: a broad round dome instead of an
  upright egg, arms fanning from around its rim with contrasting orange suckers, and
  nine joints per arm. Every arm gets its own splay, coil, length and tip hook, and
  every joint its own pair of rates, phases and amplitudes, so the motion flows,
  never repeats on a loop, and no two octopuses wave in step.
- **v1.5.0** — Distance travelled shown in the HUD and on the wipeout card. Jumping
  onto a jellyfish now bounces reliably — any airborne contact above the bell counts,
  rising or falling, and the contact window widens with speed. A splat can no longer
  stay stuck over the game-over screen.
- **v1.4.0** — The splat is now the real 3D starfish model parented to the camera,
  hanging in front of the lens and drawn over the scene, rather than a flat SVG. It
  squashes on impact, then peels off and slides down the view.
- **v1.3.0** — Fixes steering freezing permanently after iOS cancelled a touch.
  The jellyfish is no longer treated as rock: landing on the bell bounces you,
  a direct hit stings with a lightning flash. The splat that covers your view is
  now drawn to match the starfish that threw it, in that starfish's colour.
- **v1.2.0** — Octopus reworked toward the real animal: one deep purple colourway
  instead of three, papillae on the mantle, amber eyes with horizontal slit pupils,
  and no cartoon face. Arms are much longer and rebuilt as eight-joint chains, so a
  travelling wave runs down each one and they coil and swirl instead of swinging
  rigidly.
- **v1.1.0** — Octopuses replace the giant squid: they float past like any other
  obstacle, turn to track you, and lob starfish. Each one rolls its own accuracy
  between 20% and 90%, so plenty of shots are thrown deliberately wide. Octopus
  and starfish both remodelled (curling suckered arms, plump dotted star with a
  face). Version number now shown on screen.
- **v1.0.0** — First playable release.

## Deployment notes

Pages is already set up (**Settings → Pages → Source: GitHub Actions**) and the
site is live. Two things that had to be done by hand, worth knowing if this is
ever rebuilt elsewhere:

- The workflow token cannot create a Pages site, so Pages must be switched on in
  repository settings first — otherwise `configure-pages` fails with
  `Create Pages site failed … Resource not accessible by integration`.
- The `github-pages` environment only allows deployments from the default branch.
  A run triggered on a feature branch is rejected before any step executes, so
  the job fails in seconds with no logs. Deploy from `main`.
