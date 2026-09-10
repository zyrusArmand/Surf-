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
| `paraglider.glb` | The paraglide's wing — arrives WITH its lines, and is re-seated on the point they converge on rather than on its centre, because that point is the harness. Built at 45k tris and flown in its own texture — `GL_PAINT` in `index.html` switches between that and the per-vertex bands, and the maps ship at size or at 64px to match. It is fitted from what the file MEASURES, not from ratios written down for an earlier one, so a replacement of different proportions lands correctly without a code change |
| `buoy.glb` | the striped buoy |
| `buoy2.glb` | a second buoy, picked at random against the first |
| `log.glb` | the floating log |
| `jelly.glb` | the jellyfish, red |
| `jelly2.glb` | the jellyfish, blue — one of the two at random per spawn |
| `ramp.glb` | the wooden ramp |
| `octopus.glb` | the octopus |
| `bigfin.glb` | the shark — swims submerged, only the fin above water; rigged `Body`+`Jaw` so the mouth bites |
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

#### The jaw

`bigfin.glb` is rigged for a bite. It arrived from its generator with the mouth modelled wide
open and an eleven-bone auto-rig that had no jaw in it — generic `Bone_000`..`Bone_010`
scattered through the head, one of them owning the entire body, none of them ever posed by
anything. That rig is gone. In its place are **two bones, `Body` and `Jaw`**, with the lower
jaw, its teeth and the floor of the mouth weighted off the hinge the mouth actually turns on.

The hinge was measured rather than eyeballed. Casting rays up through the head at a spread of
widths gives four surface crossings wherever the mouth is open — chin, mouth floor, roof, skull
— and two wherever it has closed, so the gap between the middle pair *is* the mouth, and the
slice where it vanishes is the corner. That puts the hinge at (−0.78, 0, 0.555) and the split
between the jaws on a near-flat plane at z ≈ 0.52–0.56.

The corner is **not** a straight line across the head: it sits at x = −0.77 on the midline and
runs forward to −0.94 at the cheeks. Weighted off the midline value the cheek gets dragged into
the rotation and folds — a visible crease and a dark notch at the corner at anything past about
thirty degrees. The boundary follows the measured arc instead.

Both bones point along **+Y**, which is the convention glTF keeps, so the jaw bone's own local Y
is the hinge axis and one axis-angle drives it whatever the export did to the model's world
frame. Positive **shuts** it: the rest pose is the gape the model was built in, about sixty
degrees closes the lips, and negative opens it wider still. That sign was checked by loading the
exported file, turning the bone and reading the skinned vertex back through `boneTransform` —
the tip of the jaw sits at y = 0.06 at rest and y = 0.90 at sixty degrees.

Re-rigging made the file *smaller*: 1.50 MB to 1.27 MB, nine bones and nine vertex groups
lighter, with the geometry and all three textures untouched.

**And it uses that jaw.** Some sharks (two in three) leave the water at you: they commit a
beat before they would have reached your lane, aim at where you were AT THAT INSTANT, and
arc over it with the mouth gaping. Nothing in the flight re-reads your position, which is
what makes the dodge real — steer out of the line and it lands where you used to be, and you
are paid for it. Fail to and the jaws shut on you at the top of the arc, and it worries you
at the surface before taking you under; the run ends there.

Two things that had to be true for the leap to work at all, both of which were wrong first:
the model was swimming BACKWARDS (see the quarter turn above), and the mouth hangs several
feet below the object's origin because `BIGFIN_SHOW` sinks the whole animal inside its frame,
so every height in the leap is written against the mouth rather than the origin.

**What you can see of it while it is swimming: the fin, and nothing else.** `BIGFIN_SHOW` keeps the animal
under the surface on purpose, and the water is not clear enough to read a body through, so the
bite is currently below the waterline the whole time it happens. The rig is right and the game
drives it; making it *visible* is a separate decision — a lunge that lifts the head clear at
the moment it commits, rather than raising `BIGFIN_SHOW` and undoing the reason it is 0.36.

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

## isle.glb — bare geometry, dressed in the game

The fork's island ships as **bare geometry**: no material, no textures, no props, fifty
kilobytes for a thousand triangles. It gets the BEACH's own material and clones of the
title screen's palm at load time (`forkDress`), so there is one sand and one palm in the
whole game rather than a second of each built to look like them.

Sharing a material across files has one trap, and it cost a session. `sand.glb` ships a
TANGENT accessor, so `GLTFLoader` sets `vertexTangents` on its material — and that flag
lives on the MATERIAL while tangents are an attribute of the GEOMETRY. Put that material
on a mesh with no tangents and the attribute defaults to `(0,0,0,1)`; the shader runs
`normalize( normalMatrix * tangent.xyz )` on a zero vector, gets NaN, and NaN carries
through every lighting term. The island came out flat `#000000` — black *before* tone
mapping, which is the tell, because dim lighting can never reach exactly zero.

So: **if you hand an imported mesh a material that came from another file, check what
that material reads.** The island's UVs and tangents are both generated in `forkDress` —
a flat projection down y at the beach's own feet-per-tile, measured off `sandTile` rather
than typed in, which fixes the texture scale at the same time. `__surf.forkLook()` reports
`wantTan` and `tan` side by side for exactly this reason.

## palmlod.glb — the same tree, twenty times cheaper

`palm2.glb` is 305,735 triangles and 24 MB of texture. That is the right tree for the title
screen, where there is ONE of it filling a third of the frame, and the wrong one to plant
twenty of half a lane away — six clones of it on the fork's island were already 1.8 million
triangles of scenery.

`palmlod.glb` is that tree collapsed to ~46,000 triangles and shipped with **no images at
all**.

It started at 18,344 (6% of the original), which read as a palm from forty feet and, once he
could *walk under one*, read as torn leaves with bark hanging off the trunk in flakes and white
specular sparks all over the wreckage. The sparks are the tell: they are the material's sheen
catching normals the collapse left pointing in random directions. Two things were needed and
neither alone was enough — enough triangles that a frond is still a frond (15%), and
`normals_make_consistent` + shade-smooth afterwards so what survives is shaded as one surface. It wears the menu palm's own material, assigned in `islePalmSource()`, so there is
still one palm texture in the game and the two trees can never drift apart. Twenty of these
come to 350k triangles: three times as many trees for a fifth of the geometry.

It is sized by MEASUREMENT against the tree it stands in for — both bounding boxes, ratio of
heights — rather than by a typed scale, so it stays the same tree at the same size whichever
file is rebuilt next. Rebuild it with `palmlod.py`: import, one Collapse decimate, clear the
materials, export with `export_image_format='NONE'`.

Note that `palm2.glb` carries no TANGENT accessor (it has a normal map but no tangents), so
its material uses three's screen-space derivative tangent frame and works on any geometry
with UVs. That is why this one needs no tangent work, and `isle.glb` did — see above.

## isle.glb — the fork's island, dunes and all

The island is **one dune surface**, 27k triangles for the whole thing, built by
`models/isle.py` from the game's own constants so the model and the collision agree.

It was not always. The first version was a strip of 64 sections with **five vertices across
its whole 78-foot width** — twenty feet between vertices on a surface you stand on — so it had
no relief of its own, and the relief had to come from a field of six hundred overlapping
scanned sand tiles laid on top of it. That was the right idea for the wrong surface: the menu
beach needs a tile field because it is a flat painted plane with no relief of its own, whereas
the island is a mesh and the honest place to put dunes in a mesh is *in* it. The field cost
640k triangles and, at any range you could actually see it from, read as cracked plates.

Two things carry the look now:

- **Dunes in the geometry.** Layered smooth value noise at eighteen feet, seven feet and two
  feet, damped toward the waterline because the sea irons sand flat where it has been over it.
  Use coherent noise, not `random.uniform` per vertex — white noise at this resolution is
  sandpaper, not sand.
- **A steep beach face.** The island is static and the sea is not, so the waterline walks up
  and down the sand as the swell passes, and how far it walks is set entirely by how steep the
  sand is where it meets the water. On a nearly flat outer half, a foot of swell floods fifteen
  feet of beach and then drains it — the sea reads as a sheet of blue sliding about on top of
  the island. The whole crown-to-rim height now drops across a third of the half-width, about
  one in two and a half, and the swell moves the line under three feet.

### What the island wears, and why it is not `SAND_MAT`

It wore the beach's material outright, on the principle that one sand in the game cannot drift
into two. The principle is right and the material was the wrong thing to share, because of
what is actually inside it. Pulled out and looked at:

- the **base colour map is not a photograph of sand.** It is a chaotic patchwork of angular
  shards and chevrons, 2048px of them.
- the **normal map is very nearly flat** — a uniform `#7f7fff` with a whisper of noise. It is
  not where the beach's grain comes from; the grain is the separate `sandGrain` texture patched
  in through `onBeforeCompile`.

On the menu that colour map never reads as shards because no piece of it is ever seen whole:
the beach is six hundred small scanned chunks, overlapped and turned to random angles, so the
texture is broken up by the geometry before you can read it. Tiled continuously across an
island at nineteen feet a repeat you can read every shard — and that pattern sat all over the
near beach for three versions while I blamed the normal map, then aliasing, then the mesh's own
noise. **Shading the island with a flat Lambert made it vanish. That is the test that said
"the map".**

So the island takes what is worth taking and leaves the rest: the colour (the albedo's own
*mean*, measured off the file rather than picked, so it is still the beach's sand), the
roughness, and the grain shader. One trap in doing that — three only declares `vUv` when
something asks for it, and `USE_UV` is switched on by the presence of a map. Take every map off
and the varying is gone, but the grain patch still reads it, so the program fails to compile.
A failed program is not an exception and not a page error: three logs it and the mesh is not
drawn, which rendered as an island-shaped hole with the sea showing through and the palms
standing in the water. `material.defines = {USE_UV:''}` asks for it explicitly.

Historical, and still worth knowing if the maps ever go back on:

- **Tangents.** `sand.glb` carries a TANGENT accessor, so GLTFLoader sets `vertexTangents` on
  its material — a flag on the MATERIAL, where tangents are an attribute of the GEOMETRY. On a
  mesh without them the shader normalizes a zero vector and the NaN carries through every
  lighting term: the island rendered flat `#000000`, black *before* tone mapping, which is the
  tell, since no amount of dim lighting reaches exactly zero.
- **Texture scale.** `beachFeetPerUV()` measured the tile in FILE units — 1.9 across for one
  turn of the texture — and handed that back as feet. The beach never draws the piece at file
  size: every instance is scaled by `SAND_TILE` over its long side, very nearly ten. So the
  beach turns its sand over about every nineteen feet and the island was turning it over every
  1.9 — a texture ten times too fine, which reads as sandpaper. Same scan, same map, same
  material, and nothing like the same beach.

## forksign.glb — the LEFT / RIGHT signpost, cut from the real one

It is `menusign.glb` **edited**, not a lookalike. The first attempt built a post and two planks
out of boxes with a painted texture, and it looked exactly like boxes with a painted texture —
the real sign is a 61k-triangle scan of carved wood with hand-painted lettering, and nothing
built from primitives sits beside it.

The obstacle is that its three words are baked into a **shared UV atlas**: 1024px of wood with
`PL`, `AY` and the rest scattered across fragment islands in no order anything describes. You
cannot edit that by eye. The way through is to go via the geometry, and `models/forksign.py`
does four things:

1. **Find the words in 3D** by asking which *faces* sample yellow texels. That gives three clean
   bands of z, one per plank, all reading toward −y.
2. **Flatten a plank** — rasterise its triangles in plank-local coordinates, sampling the atlas
   through their UVs — which reconstructs the board as a rectangle you can look at and edit.
   Run on the top plank, this is what identified it as `Play`.
3. **Edit the rectangle**: clone the old word out *horizontally*, along the grain, so every
   grain line stays at the height it already was and there is no seam (vertical and blurred
   fills both showed as a smudge exactly where the word had been). Paint the new one in its
   place, in ink sampled from the old letters.
4. **Bake it back** the other way, rasterising the same triangles into the atlas, then **read it
   back again** and count surviving yellow texels — because a texel the read missed is a texel
   the write misses too, and that would leave a fragment of the old word behind.

Five things went wrong on the way, all worth keeping:

- **A flattened panel has holes.** A scan's atlas is islands with space between them, so the
  triangles never cover some texels — harmless to look at, and *not* harmless to write back:
  those holes bake into the texture as black patches. They are grown outward from what was
  covered before anything is written.
- **The old lettering has relief.** The normal map carries the raised edge of the paint and the
  roughness map its sheen. Repaint the colour alone and the old word is still there in the light
  — a ghost of `Shop` embossed under the new letters. All three maps get the same clone.
- **The post was mis-measured.** Taking a z band *between* two planks and calling whatever is
  there "post" gave a footprint 0.45 across — half the width of the sign — because that band is
  not plank-free. Most of the `Play` plank then counted as post and stayed on. Measure *above*
  every plank: the bare post is 0.095.
- **Textures rebound by guesswork.** Matching each image node to a file by its corner pixel
  mis-assigned two of the three, putting the normal map on Base Color — pale blue patches across
  the planks. Which socket a node feeds is a fact; walk it.
- **The word must follow the wood.** These planks are cut to a point at one end: flattened, the
  upper comes to a head on the right and the lower on the left. That carved point *is* the
  arrow, and a better one than anything paintable. The first pass put `LEFT` on the plank that
  points right.

**`menusign.glb` is not modified.** This writes a separate file.

---

## `turtle.glb` — the underwater turtle, built by `turtle.py`

682 KB, 9,000 tris, 4,683 verts, one 512² JPEG, 17 joints. **It arrived at 25 MB** — 305,850
triangles, a 2048² base colour and a 4096² metallic-roughness map costing 9.4 MB on its own.

Source: `turtle_MAX.glb` (supplied). It is rigged and it ships **no animation clips**, so the
swimming is written in `index.html` (`turtleStep`) against the bones directly.

### The bones have no names worth the word

Seventeen joints called `Bone_000` … `Bone_016`. Nothing in the file says which is a leg. They
are mapped by where they sit in the bind pose, in three.js space (x across, y up, z back; the
model faces **−Z**, which is the game's forward, and its origin is at its belly):

| role | bone | head position |
|---|---|---|
| neck → head | `Bone_003` → `Bone_002` | (0, 0.60, −1.30) → (0, 0.49, −1.56) |
| front left / right leg | `Bone_007` / `Bone_009` | (∓0.53, 0.24, −1.03) |
| rear left / right leg | `Bone_011` / `Bone_013` | (∓0.58, 0.42, +0.69) |
| tail | `Bone_016` → `015` → `014` | (0, 0.73, +0.78) running back and up |

Overall 1.93 wide × 1.70 tall × 3.58 long in its own units. `TURT_LEN` in the game is that 3.58,
which is how a wanted size in feet becomes a scale.

The leg bones hang straight down out of the shell and have no useful axis of their own, so the
stroke is applied with `premultiply` — a rotation in the **parent's** frame, which is near enough
the body's: x swings a leg fore and aft, z swings it out and in, and a quarter turn between them
is the figure of eight that stops it reading as a windscreen wiper.

### Four traps, all of which were walked into

- **Merge before you decimate.** 268,380 verts for 305,850 tris is not a surface, it is a
  shattered one — near enough every face carrying its own copies of its corners. Collapse cannot
  cross a seam it believes is a boundary, so on that mesh it does not simplify the shape, it eats
  **holes** in it. `remove_doubles` first takes it to 153k verts and the collapse then behaves:
  9,000 tris from 4,683 verts, which is the ratio a closed mesh should give.
- **Re-unwrap, then re-bake.** The file's UVs address an atlas baked for the dense mesh. Simplify
  the mesh and those islands are still addressing texels that belonged to faces a hundredth of
  the size, and it arrives looking like **camouflage** — a scatter of unrelated colours. Smart UV
  Project on the decimated mesh, then bake the original's colour onto the new UVs, selected-to-
  active.
- **A bake target is not a linked texture.** An image node only has to be *active* to be baked
  into, so the node sat unconnected through a bake that worked perfectly. The exporter writes the
  material it can see, and an unlinked texture is not part of the shader: the turtle came out with
  a `baseColorFactor` of grey and no map at all — a clean white tortoise. Link it after baking.
- **The metallic-roughness map is 9.4 MB describing how shiny a turtle is** to a scene lit by one
  sun through thirty feet of fog. It is dropped for a constant roughness of 0.72.

### And it is cloned, not re-loaded

`cloneModel()` — three.js copies a `SkinnedMesh`'s skeleton **by reference**, so a bare
`clone(true)` deforms to the original's bones and arrives inside out.

---

## `kelp.glb` — the sea-bed kelp, built by `kelp.py`

1.2 MB, 16,000 tris, one 1024² JPEG, no rig and no clips. (It was 7,000 tris and a 512 map, which
is fine for a clump five feet tall and not for one fifteen feet tall standing next to the lens:
3,347 leaf islands across a 512 atlas is about eight texels a leaf.) **It arrived at 30 MB** — 1,244,748
triangles, 700,151 verts and three 2048² JPEGs (3.7 + 2.7 + 1.9 MB).

Source: `kelp_MAX.glb` (supplied). Same pipeline as `turtle.glb` — merge, decimate, Smart UV
Project, bake the original's colour onto the new UVs — and every note there applies here. Read
that section first; only what is different is below.

### Thin the leaves BEFORE simplifying them

The clump is a mass of separate leaf surfaces. Spending a triangle budget on *keeping every
leaf* gives each of them about three triangles, and three triangles is not a leaf, it is a
shard — which is what "the leaves look wrong" was. `KEEP` in `prop.py`/`kelpbake.py` drops a
fraction of the small islands first (components at or above a quarter of the largest are stalks
and are always kept), and the decimator then has a much smaller mesh to spend the same budget
on. Same size, same shape, less dense, and every leaf still there reads as a leaf. Shipped at
`KEEP=0.34`.

Note the island count *after* `remove_doubles` is 199, not 3,347 — the weld joins leaves that
touch. Thin after the merge, not before, or the fractions mean something else entirely.

Leaf edges also keep their crease: `shade_auto_smooth` rather than a blanket `shade_smooth`,
which smears normals across the boundary between two leaves that the weld joined.

### 3,347 islands

That is the number that decides the triangle budget. It is a clump of stalks and **every leaf is
its own piece of surface**, so there is a floor under how far a collapse can usefully go: past it
the decimator stops simplifying leaves and starts deleting them. 12,000 and 7,000 were both baked
and rendered side by side and are not tellable apart at any range the game shows them at, so it
ships at 7,000. Much below that and the fronds start dropping out.

The merge still matters and matters more: 700,151 verts for 1,244,748 tris is a shattered
surface, and collapse cannot cross a seam it believes is a boundary.

### Geometry, and where its roots are

1.90 wide × **1.17 tall** × 1.90 deep in its own units, centred on itself — `min.y` is −0.59, so
its origin is at its middle and **not** at the ground. The stalks run up +Y, so no reorientation
is needed. Two numbers in `index.html` come straight off this box:

- `KELP_H` = 1.173 — turns a wanted height in feet into a scale.
- `KELP_BASE` = 0.59 — how far below the origin the roots sit.

In the game each clump is lifted by `KELP_BASE` inside a wrapper group, so **the wrapper's origin
is where it meets the sand**. That is what lets it sway: rotate a self-centred model and the roots
swing through the ground while the tips stay put, which is the opposite of a plant.

`doubleSided` is true in the exported material and needs to stay that way — the fronds are thin.

### Tall clumps are STACKED, not scaled

The model is 1.9 across and 1.17 high. Scaling it to reach a fifteen-foot water column gives a
plant **twenty-four feet wide** — a hedge, not kelp. So `kelpBuild()` chains lengths of it
instead: each length keeps the proportions it was modelled with and only the count changes, which
is also what a real stipe is — the same frond repeated up a stem.

They are **nested, not siblings**. Siblings each pivot about their own base and slide apart at
every joint; chained, a bend low down carries everything above it, which is what a stem does and
comes free from the scene graph. Each length's wrapper origin is its own base (the model lifted
by `KELP_BASE × its y scale`), so the sway hinges where it should.

Height is asked of the **water column above that spot**, not taken from a constant — the bed rolls
by a couple of feet, so how far "almost the surface" is depends on the dune it is standing on.

---

## `searock.glb`, `coral1.glb`, `coral2.glb` — the sea-floor props, built by `prop.py`

| file | from | tris | size |
|---|---|---|---|
| `searock.glb` | 28.9 MB, 1,298,066 tris | 4,500 | 274 KB |
| `coral1.glb` | 28.8 MB, 896,142 tris | 5,999 | 463 KB |
| `coral2.glb` | 29.1 MB, 947,506 tris | 5,999 | 479 KB |

`prop.py` is `kelp.py` with the source path taken as an argument — same pipeline, same traps, and
the notes under `kelp.glb` and `turtle.glb` are the ones to read. Every one of these three is a
single static mesh with no rig, three 2048² maps, and a vert:tri ratio saying the surface is
shattered; all three needed the merge before the collapse would behave.

### Nothing in `index.html` is hard-coded off these boxes

They are scans, so their origins are wherever the scanner's was — `searock` is 1.20 tall with its
underside 0.60 below the origin, `coral1` 1.03/0.52, `coral2` 1.65/0.83. `floorAsk()` measures
both on load and the placement works off that, so re-baking one at a different size or
re-centring it needs no code change.

Rotation is **yaw only**, plus a couple of degrees of tilt. A free rotation stands a boulder on a
corner; yaw is the axis that leaves a scan sitting the way it was scanned.
