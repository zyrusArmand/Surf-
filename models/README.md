# Models

Drop a `.glb` in this folder and it replaces the built-in shape in the game. If a
file isn't here the game keeps its procedural version, so you can replace one
thing at a time and nothing ever breaks.

`board.glb` and `pug.glb` ship with the game; the rest of the table is empty by
design, and the 404s those file names produce are the documented path rather than
a fault.

| File | Replaces |
| --- | --- |
| `board.glb` | the surfboard |
| `pug.glb` | the rider |
| `buoy.glb` | the striped buoy |
| `log.glb` | the floating log |
| `jelly.glb` | the jellyfish |
| `ramp.glb` | the wooden ramp |
| `octopus.glb` | the octopus |
| `bigfin.glb` | the shark fin |
| `jetski.glb` | the jet ski |

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

## Rigged riders

`pug.glb` may be a **rigged** export with a skeleton and animation clips, and the one
that ships is: a 24-bone skeleton with a handstand clip on it. If a skeleton is
present the game drives it, and three things happen automatically.

- **It is turned to face the wave.** Every built-in rider is modelled facing `-z`.
  A rigged one is asked which way it is looking — the line from its `Head` bone to a
  muzzle bone called `headfront` — and turned onto `-z` from wherever it started. A
  bone named `headfront` is what makes this work; without it the export keeps
  whatever orientation it arrived in.
- **A clip named for a trick drives that trick.** A clip whose name matches
  `handstand` becomes the HAND button. It is *scrubbed*, not played: forward through
  the kick-up, parked in the inverted stretch for as long as the button is held, then
  forward again through the dismount. So a clip that is a round trip — stand, kick up,
  hold, come down — works correctly, and most exported ones are.
- **The clip's ROOT is dropped and its limbs kept.** Tricks are animated on a floor,
  where the body is free to travel and to finish leaning; a surfboard is neither. The
  hips go back where they started, the game turns the rider over itself, and the posed
  body is measured each frame and stood on the deck. What survives is the part worth
  having: the plant of the arms, the scissor of the legs, the wobble of the balance.

### The face

**If the export has a texture or materials of its own, the game draws exactly that** —
no markings added, no eyes bolted on over yours. Paint him however you like and the
file wins.

Only a model that arrives as bare geometry gets a face guessed for him, which is what
an AI generator hands you by default: UVs it never got a texture for. In that case the
mask, nose, eye rings and ears are placed in head-local space off the `Hips`, `Head`
and `headfront` bones, and the eyes are added as geometry on the head bone — vertex
colour cannot draw a white sclera with a pupil in it at these vertex counts.

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
