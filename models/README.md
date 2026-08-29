# Models

Drop a `.glb` in this folder and it replaces the built-in shape in the game. If a
file isn't here the game keeps its procedural version, so you can replace one
thing at a time and nothing ever breaks.

The CHEST keeps its fittings when a model replaces it: the two glow sprites that make it
findable in open water, and the lid group the end-of-run ceremony swings open. Only the body
is swapped, so neither is lost.

Keep textures small. A rider is a background character a couple of hundred pixels tall;
`pug.glb` arrived with a 4096×4096 texture that made the file 10.5 MB, and at 1024×1024 it
is 1.7 MB and looks identical in game. The same again for the second pug: 11.6 MB in, 1.6 MB
out, and then simplified from 31k triangles to 9.4k, which at the size he is ever drawn is
indistinguishable — checked side by side on the shop card, which is the closest lens in the
game. `palm.glb` came in at 28 MB on three 4096² maps and lands at 3.8 MB. Exporters also like wiring the texture to **emissive**
at full white, which makes the model glow flat and ignore every light in the scene — worth
checking if an import looks oddly unlit.

`board.glb`, `buoy.glb`, `chest.glb`, `log.glb`, `octopus.glb`, `palm.glb`, `pug.glb`,
`buoy2.glb`, `cat.glb`, `frog.glb`, `monkey.glb`, `rat.glb`, `ramp.glb`, `sand.glb`, `jelly.glb`, `jelly2.glb`, `bigfin.glb`, `cow.glb`, `boat.glb`, `paraglider.glb` and `plane.glb` ship with the game; the rest of the table is
empty by design, and the 404s those file names produce are the documented path
rather than a fault.

A rider model is named for the CHARACTER it belongs to, and only that character
rides it — everyone else keeps the built-in body, which is still there underneath.
To give another character his own model, name the file after his id and add that id
to `RIDER_MODELS` in `index.html`.

| File | Replaces |
| --- | --- |
| `board.glb` | the surfboard |
| `palm.glb` | every palm tree, the title screen's included — rigged and painted on import if it arrives without either |
| `chest.glb` | the treasure chest |
| `pug.glb` | Astro the Pug |
| `cat.glb` | Miso the Cat |
| `frog.glb` | Allen the Frog |
| `monkey.glb` | Bongo the Monkey |
| `rat.glb` | Fatty the Rat |
| `cow.glb` | Moo the Cow — what the saucer sends back, see the UFO in `index.html` |
| `boat.glb` | The tow boat — comes down the lane and takes station ahead of you, see `updateBoat` |
| `paraglider.glb` | The paraglide's wing — arrives WITH its lines, and is re-seated on the point they converge on rather than on its centre, because that point is the harness. Built at 45k tris with 64px base and normal maps: the game paints the canopy per-vertex and nulls both, because the baked base colour is a blue ripstop patchwork and the baked normal is a grid of squares. Shipping them at size was 2.1 MB nobody can see |
| `buoy.glb` | the striped buoy |
| `buoy2.glb` | a second buoy, picked at random against the first |
| `log.glb` | the floating log |
| `jelly.glb` | the jellyfish, red |
| `jelly2.glb` | the jellyfish, blue — one of the two at random per spawn |
| `ramp.glb` | the wooden ramp |
| `octopus.glb` | the octopus |
| `bigfin.glb` | the shark — swims submerged, only the fin above water |
| `jetski.glb` | the jet ski — turned bow-first by measurement, see below |
| `plane.glb` | the tow plane and the set-wave aircraft |
| `palm2.glb` | a DIFFERENT tree for the title screen, in place of `palm.glb` there — not shipped |
| `sand.glb` | one piece of modelled ground, scattered over the preview beach |

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
its middle joints take it over to the side and its top keeps going the same way, which is the
long C the procedural tree had and the shape every photograph of a board against a palm has.
Because the base stays upright, the root sits exactly where it is placed however far the top
leans — which is what keeps the trunk beside the chest and the board finding the crook of it.
It also stands **shorter than a grove tree** (`MENU_PALM_H`), because the two shots are
nothing alike: a grove tree is forty units back and reads as scenery, and this one is ten
feet from a wide lens, where a full-height crown fills the top half of the frame.

A bend is written as eight numbers and **resampled onto however many joints the rig has**, so
the same profile describes the same tree whatever it is boned with. Indexing them straight in
worked only while every palm had eight bones: on a six-joint trunk the last two entries — the
part that says the top keeps going rather than standing back up — were silently dropped, and
the long C stopped half way up. The total turn is preserved through the resample too, so a
trunk does not straighten out just because it was cut into fewer pieces.

**A model with no bones is given some.** The grove is one mesh bent a different amount per
tree, and used as it arrives an unrigged export makes every palm in the game the same shape,
which reads as wallpaper. So a chain is built on import: bones evenly up the **trunk only**,
each trunk vertex blended between the two it sits between, and everything above the crown line
bound rigidly to the top bone — that last part is the trick, because a crown weighted by height
shears apart as the trunk bends where a real one rides on top of it as one piece.

The crown line is found from the **85th percentile** of radius in each height band, not the
widest thing in it. One frond drooping low is enough to make a max-radius test call the crown
at a quarter of the tree's height, which leaves the bone chain spanning the bottom of the trunk
and the whole canopy hanging off a bone half way down it.

**A model with no material is painted here.** Trunk and frond are told apart from the shape
alone — everything within a hand of the measured trunk radius is trunk, everything that runs
out from the top is leaf — and the colour goes onto the vertices rather than into a texture,
because the bend is baked per tree and vertex colour rides through that bake for free. Two
things to know if you touch it:

- **Vertex colours are LINEAR.** An ordinary mid grey written straight in comes back out of the
  encode near white, which is a bleached trunk and a pale yellow canopy whatever green went in.
  They are converted from sRGB on the way in. The procedural palm hides this by multiplying its
  own vertex colours down by a dark constant (`PALM_LIT`); a painted import has no such
  constant.
- **The menu's daylight tint skips it.** That tint is applied to any palm material with vertex
  colours and no map — a rule written to separate the procedural tree from an imported one,
  which a model painted *here* answers exactly like the procedural tree it is not. Painted
  materials carry `userData.painted` and are left alone.

## The sand, and how one piece becomes a beach

`sand.glb` is a single small patch of modelled ground, and it is used the way a texture is
used: laid down over and over until it covers the beach. Around a hundred and fifty copies on
the preview beach, each turned freely about the vertical, scaled within a fifth either way, and
placed against the beach's own height function — so the field follows the dunes rather than
lying flat across them. They **overlap by a third**, because sand has no seams and the joins
are the one thing that would give a repeat away.

It is an **InstancedMesh**: one object, one draw call, and the cost is triangles alone. That is
why the piece is simplified hard on the way in — 95k triangles is a reasonable budget for one
hero prop and an impossible one for a hundred and fifty of them.

Four things had to be got right, and each of them looked like a different bug:

- **A piece is a plane and a dune is curved.** Laid at the height of its own centre, a piece
  cuts into the crest either side of it and the painted beach comes up *through* the middle of
  the field. Each one is lifted clear of the highest point under its whole footprint.
- **The slope it follows is measured at its own size.** Read over half a unit, the gradient is
  whatever ripple happens to be under the middle of the piece; extrapolated across five units
  that throws the far edge into the air, which then needs more lifting — so the field got
  higher the harder it tried to follow the ground.
- **It sits INTO the beach, not on it.** Anchored on its own mean, half of every piece stands
  above the ground everything else was placed at, and that half is what hides a rider's feet
  and swallows the foot of the board.
- **The spacing is per axis, because the piece is not square.** It is 1.9 by 1.17 — ten units
  across is ten by six — and stepping ten times the overlap in *both* directions leaves a gap
  in the short one every single row. That was the whole of the bleed-through: not a placement
  fault, just a field that was never covering the ground it was told to. The turn is limited
  to a quarter either side of straight for the same reason; a free turn lays a piece across
  its own short axis and re-opens the gap.
- **Every piece is the same height, whatever size it is across.** A piece's border is a
  straight cut, so wherever one stands proud of its neighbour that cut is a cliff with a
  shadow under it — which is a tray of slabs, not a beach. Across they vary; upward they are
  identical, so any two touching pieces meet at the same level.
- **The painted beach ducks half a unit under the field** and eases back outside it. Two
  surfaces at the same height cannot both be the visible one, and the painted one carries the
  wind-ripple map that reads as corduroy at a grazing angle. Nothing standing on the beach
  moves: everything is placed off the height *function*, which is untouched — this is the
  drawn surface alone.
- **The camera measures its eye height off the sand, not the terrain.** Two feet of lens over
  a beach is extremely sensitive to what is directly in front of it: a mound a quarter of a
  unit proud three feet away subtends as much of the frame as the rider does twenty feet away.

The piece is simplified to **2.8k triangles and no further**. At 1k the decimation's own faces
become the visible relief — large flat plates with straight edges — and no amount of placement
work hides them. Nor can the pieces be made much smaller: they must be simplified further to
stay in budget, and that is the same failure again.

## Obstacles, and the second model each of them can have

Every obstacle kind asks for two files — `jelly.glb` and `jelly2.glb`, `buoy.glb` and
`buoy2.glb` — and where both are present, **each spawn picks one at random**. That is how the
jellyfish come in two colours: red in one file, blue in the other, nothing in the code that
knows a colour. The spawn rate is untouched; only which mesh gets cloned changes.

An imported obstacle is fitted into the box of the built-in one it replaces, and `fitToBox`
turns a model's long axis onto the target's. That is right for a buoy and wrong for a **shark**:
the built-in `bigfin` is a blade with no body and a box taller than it is long, so a whole shark
fitted to it is stood on its tail. It gets a box shaped like the animal instead.

Two more things the shark needs, both because it is an animal rather than a fin:

- **It swims under the water.** Obstacles float by putting their ORIGIN at the surface plus a
  per-kind offset, so the waterline sits at a known height in the model's own frame, and the
  shark is dropped until only `BIGFIN_SHOW` of it is above that line. Sink it by half and its
  whole back is out, which reads as a fish sitting on the sea; the dorsal fin is about the top
  fifth of this model, so the shark is scaled up as well — a bigger animal, deeper down, with
  a fin worth seeing.
- **It swims the way it is going.** The built-in fin carries a quarter turn inside its own
  template, which cancels the quarter turn every `bigfin` clone is given to point it along its
  sweep. An imported model has no such turn, so it crossed the line sideways.

### Which end is the bow

`fitToBox` lines a model's long axis up with the target's, and a long axis has two ends. The
ride points local −z along the direction of travel, so a hull whose bow is at +z crosses the
whole screen backwards, with its wake — emitted behind the way it is going — coming off the
sharp end.

It is decided by **measuring the hull**, not by eye: a jet ski seen side-on at forty metres is
a red shape with a dark lump on it either way round. The stern is the low wide end (the flat
platform sits at about a tenth of the hull's height), the bow is the narrow one past the
console. Both votes are summed because some hulls are barely tapered and some barely stepped,
and the sign of the total turns the model or leaves it alone.

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
- **A rig that bends its FRONDS as well as its trunk still works.** `palm.glb` is posed by
  walking the bone chain from the ground up and bending each joint a little, and the walk
  stops where the tree forks: one bone child means carry on up the trunk, nine means this is
  the crown. The first tree here was a single straight line of eight bones, so "take the
  first child" happened to walk the trunk; the second forks into nine frond chains at the
  top, and taking the first child there would apply the trunk's bend to one leaf as well and
  swing it out of the crown on its own.
- **The bend is BAKED, not skinned.** Each tree's posed vertices are read back once with
  `boneTransform` and written into a plain static mesh, so nine trees on the beach cost
  nothing per frame beyond their triangles — and the far ones are built lazily, so a heavier
  tree does not show up in the boot time.
- **Every rider does every motion any of them brought.** They come off the same biped with the
  same bone names, and each export ships a different half of the same repertoire: the pug
  brings standing and chatting, getting up off the floor and lying with his paws spread; the
  cat brings a backflip; the frog, the monkey and the rat bring one motion each. The clips are POOLED —
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
