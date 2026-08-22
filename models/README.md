# Models

Drop a `.glb` in this folder and it replaces the built-in shape in the game. If a
file isn't here the game keeps its procedural version, so you can replace one
thing at a time and nothing ever breaks.

The CHEST keeps its fittings when a model replaces it: the two glow sprites that make it
findable in open water, and the lid group the end-of-run ceremony swings open. Only the body
is swapped, so neither is lost.

Keep textures small. A rider is a background character a couple of hundred pixels tall;
`pug.glb` arrived with a 4096×4096 texture that made the file 10.5 MB, and at 1024×1024 it
is 1.7 MB and looks identical in game. Exporters also like wiring the texture to **emissive**
at full white, which makes the model glow flat and ignore every light in the scene — worth
checking if an import looks oddly unlit.

`board.glb`, `buoy.glb`, `chest.glb`, `log.glb`, `octopus.glb`, `palm.glb`, `pug.glb`,
`buoy2.glb`, `cat.glb`, `frog.glb`, `monkey.glb`, `palm2.glb`, `ramp.glb` and `plane.glb` ship with the game; the rest of the table is
empty by design, and the 404s those file names produce are the documented path
rather than a fault.

A rider model is named for the CHARACTER it belongs to, and only that character
rides it — everyone else keeps the built-in body, which is still there underneath.
To give another character his own model, name the file after his id and add that id
to `RIDER_MODELS` in `index.html`.

| File | Replaces |
| --- | --- |
| `board.glb` | the surfboard |
| `palm.glb` | every palm tree |
| `chest.glb` | the treasure chest |
| `pug.glb` | Astro the Pug |
| `cat.glb` | Miso the Cat |
| `frog.glb` | Allen the Frog |
| `monkey.glb` | Bongo the Monkey |
| `buoy.glb` | the striped buoy |
| `buoy2.glb` | a second buoy, picked at random against the first |
| `log.glb` | the floating log |
| `jelly.glb` | the jellyfish |
| `ramp.glb` | the wooden ramp |
| `octopus.glb` | the octopus |
| `bigfin.glb` | the shark fin |
| `jetski.glb` | the jet ski |
| `plane.glb` | the tow plane and the set-wave aircraft |
| `palm2.glb` | the title screen's own palm, in place of `palm.glb` there |

## Exporting from Blender

**File → Export → glTF 2.0**, and pick **glTF Binary (.glb)** as the format. Save
it with the name from the table above and put it in this folder.

Nothing else needs setting up:

- **Scale** — the model is measured and resized to match the shape it replaces, so
  a board modelled 80 m long comes in the right size.
- **Orientation** — its longest side is laid along the longest side of the original,
  so a board exported down the wrong axis still lands nose-to-tail. The board gets a
  second check: the fins are found and turned to hang down and trail behind, so it
  does not matter if it was modelled deck-down or pointing the wrong way.
- **A Mirror modifier you forgot to apply** — if the file holds only half an object,
  every vertex on one side of the origin and a clean edge sitting on it, the missing
  half is rebuilt on load, winding and normals and all. You do not have to re-export,
  though applying the modifier in Blender is tidier.
- **Position** — it is centred on exactly where the original sat, so it floats,
  collides and gets ridden the same way. The board is lined up deck-to-deck rather
  than box-to-box, so a thin one still sits down in the water with the rider's feet
  on the grip.

Collision size is unchanged — the game still uses the original's radius and height,
so a model that is wildly a different shape will feel slightly off even though it
looks right.

**A rigged obstacle is cloned properly.** `Object3D.clone()` copies a SkinnedMesh and leaves
it pointing at the ORIGINAL's bones, so every clone is drawn from the template's skeleton —
which sits at the origin and is never animated. Rigged imports are rebound on clone: each one
gets a skeleton of its own bones. Without it a spawn a hundred feet out is drawn at your feet
and its animation does nothing, however correct every number about it reads.

**More than one model for the same thing.** Every obstacle also looks for a file with a `2`
on the end — `buoy2.glb`, `log2.glb`, `jelly2.glb` — and if one is there, each spawn picks
between them at random, so a run passes a danger buoy and then a channel marker rather than
the same object twenty times. Both are fitted against the ORIGINAL procedural shape and both
carry its collision size, so a second import cannot quietly rescale the first.

## Colours

If you export with materials or textures, those are used as-is — only the reflection
strength is matched to the rest of the game, which is tone mapped and lit by an
environment map that would otherwise wash an imported colour out.

If you export bare geometry, the game paints it in its own palette, and it picks
the colour from each object's **name**. Naming your objects in Blender is all it
takes:

| Model | Names it recognises |
| --- | --- |
| board | `fin`/`skeg`, `deck`/`pad`/`grip`, `wood`/`stringer`, `rail`/`hull`/`body` |
| pug | `eye`, `nose`, `muzzle`, `ear`, `tongue`, `belly`/`chest`/`paw` |
| buoy | `flag`, `pole`/`mast`, `light`/`lamp`, `band`/`stripe`/`cone` |
| log | `end`/`cut`/`ring`, `branch`/`stub`/`knot` |
| jelly | `tentacle`/`arm`/`frill`, `core`/`glow` |
| ramp | `lip`/`edge`/`kicker`, `plank`/`seam`/`slat` |
| octopus | `sucker`, `pupil`, `eye`/`iris`, `arm`/`tentacle` |
| jetski | `seat`/`saddle`/`handle`, `rail`/`trim`/`deck`, `stripe`/`lime` |

Anything not matched gets that object's main colour — white for the board, fawn for
the pug, and so on.

The PLANE keeps its propeller when a model replaces it. The engine-failure sequence spins that
one part by name, and a single-mesh import has no propeller to give it — so the modelled body
is added and the original's parts are hidden rather than thrown away. It arrives nose down its
own `-z`, which is the direction every aircraft in this game is built to fly.

## The palm, and its rig

`palm.glb` replaces every palm in the game — the three standing back on the preview beach
behind the shop's cards, and the two on the title screen. If the file carries a **bone chain
up its trunk** the game bends it, because a straight palm is a telegraph pole: each tree gets
its own curve, some a gentle one, some a real C, and about a quarter of them none at all,
picked from the tree's seed so the same tree is the same shape every time you look at it.

The pose is **baked into plain geometry** rather than left skinned. A tree never animates, so
a skeleton and a skinning shader per tree pay for a pose that is set once — and three.js
shares one skeleton between clones of a skinned mesh, so every cloned palm would have had to
bend the same way, which is the opposite of what is wanted. Baking is also what makes the
trunk's centreline knowable, and the centreline is what the title screen leans a board on.

The one tree that is not left to chance is the title screen's: it leaves the sand upright,
its middle three joints take it over to the side and its top keeps going the same way, which
is the long C the procedural tree had and the shape every photograph of a board against a
palm has.

A model with no bones is used as it is, standing straight.

## Rigged riders

A rider model may be a **rigged** export with a skeleton and animation clips, and all four
that ship are: a 24-bone skeleton each, off the same biped, with the same bone names. If a
skeleton is present the game drives it, and these happen automatically.

- **It is turned to face the wave.** Every built-in rider is modelled facing `-z`.
  A rigged one is asked which way it is looking — the line from its `Head` bone to a
  muzzle bone called `headfront` — and turned onto `-z` from wherever it started. A
  bone named `headfront` is what makes this work; without it the export keeps
  whatever orientation it arrived in.
- **He is painted in the coat the roster already gives him**, so an imported body and
  the built-in one are the same character in the same colour. Fur is matt and barely
  reflects; a model left on the loader's default material mirrors the sky instead, and
  a grey cat comes out slate blue.
- **A clip named for a trick drives that trick.** A clip whose name matches
  `handstand` becomes the HAND button. It is *scrubbed*, not played: forward through the
  kick-up, parked in the inverted stretch for as long as the button is held, then forward
  again through the dismount. So a clip that is a round trip — stand, kick up, hold, come
  down — works correctly, and most exported ones are.
- **A model with no such clip still does the trick.** The timeline runs either way and
  the game turns him over itself; it simply has no limb animation to lay on top. In practice
  nobody is in that position any more — see below — but the path is still there for a model
  that arrives with no clips at all.
- **Every rider does every motion any of them brought.** They come off the same biped with the
  same bone names, and each export ships a different half of the same repertoire: the pug
  brings standing and chatting, getting up off the floor and lying with his paws spread; the
  cat brings a backflip; the frog and the monkey bring one motion each. The clips are POOLED —
  whatever any one of them arrives with, all of them can perform, including the handstand.
  Borrowed clips are **rotation only**: a track that MOVES a bone rather than turning it is
  written in the proportions of the body it was animated on, and a frog is not a pug. Rotations
  retarget cleanly across a shared skeleton, which is the whole reason this works.
- **Every other clip runs on the title screen.** If the model belongs to the character you
  have equipped, he performs on the beach: standing and chatting, folding down onto the
  sand, lying with his paws spread, getting back up, a handstand, with pauses between. Each
  beat ends clamped on its last frame, so the pause after it is spent in the pose it arrived
  at — which is what spaces the show out without a held pose having to be authored.
  The sit and the lie-down are the **get-up clip run backwards**; there is no sit clip in the
  file, and a six-second get-up reversed is exactly the descent on the animator's own timing.
- **The skeleton is put into its BIND pose on load.** A glTF writes the bones wherever the
  exporter left them, and an export carrying several clips leaves them wherever the last one
  ended — which is not a neutral. Everything is built from that pose, so it has to be the one
  pose the rig is defined in.
- **The clip's ROOT is dropped and its limbs kept.** Tricks are animated on a floor,
  where the body is free to travel and to finish leaning; a surfboard is neither. The
  hips go back where they started, the game turns the rider over itself, and the posed
  body is measured each frame and stood on the deck. What survives is the part worth
  having: the plant of the arms, the scissor of the legs, the wobble of the balance.

### The face

**If the export has a texture or materials of its own, the game draws exactly that** —
no markings added, no eyes bolted on over yours. Paint him however you like and the
file wins.

Only a model that arrives as bare geometry gets a face made for him, which is what an AI
generator hands you by default: UVs it never got a texture for. In that case he wears one
clean coat in his roster colour, and gets EYES — two small spheres each, placed off the
`Hips`, `Head` and `headfront` bones and hung on the head bone so they ride every
animation. Markings were tried three ways and taken out: at these vertex counts a marking
has to be a soft-edged field to avoid showing its own seams, and a soft-edged brown field
on a brown coat is a smudge. Eyes are geometry because geometry has hard edges.

To paint your own, texture the model in whatever you generated it in — most tools will
take a reference image — and **re-export the RIGGED version with the texture on it**.
Downloading a fresh untextured mesh loses the skeleton, and with it the handstand.

## What you lose

Rolls, flips, spins and the helicopter still belong to the built-in rig, so a rigged
import rides those out without changing shape — only the handstand is its own. The
built-in jellyfish and octopus are animated by hand too — the bell pulses, the arms
flow on jointed chains — and an imported mesh for either has none of that, so both
drift and float rigidly. Everything else — the board, buoy, log, ramp, fin, jet ski —
was never animated internally and looks exactly as intended.
