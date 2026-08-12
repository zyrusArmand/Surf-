# 🏄 Surf

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

| Action | Touch | Keyboard |
| --- | --- | --- |
| Carve left / right | swipe left / right on the water | `A` / `D` or `←` / `→` |
| Jump | tap | `Space` |
| Barrel roll | swipe up or down — or tap again in the air | `K` (hold) |
| Front / back flip | swipe right / left while airborne | — |
| Spin | draw a circle with one finger | `J` (hold) |
| Grab (air) / hand drag (water) | press and hold | `L` (hold) |
| Handstand (water or air) | hold a second finger | `H` (hold) |
| Grab the seaplane's rope | ROPE! button | — |

Every tapped rotation is a whole turn, so tapping lands you square — you only eat it if
you asked for more spin than you had air for.

Land your rotations square or you'll eat it. Hitting a ramp launches you, jellyfish
always bounce you — they can never end a run — and buoys, logs and shark fins do. Every so often a whirlpool
opens ahead: it drags everything afloat round and down, and pulls at you too. There is
always a ramp and a jellyfish held on its near rim — that is your way out.

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

- **v2.25.0** — **The tube narrows to an almond down the line.** A wave only barrels for a
  stretch of its length; past that it runs out into an unbroken shoulder, and from inside that
  reads as the mouth closing to an almond. It is in every reference photo of a barrel and it
  was the thing most obviously absent. The far-end radius went from 0.58 of full — a
  near-constant bore, which is a pipe rather than a wave — down to 0.24, with the exponent
  raised so it stays open around you instead of pinching in immediately. The lip also feathers
  white earlier and harder, the way it does in the photos.
  - **Still outstanding:** the wave does not *peel*. The break does not travel along its
    length, and there is no whitewater trailing behind it.
- **v2.24.0** — **Barrel colour off the reference photos, and an exit that breaks instead of
  fading.** Two things, both from real surf photography.
  - **A barrel is not the colour of the sea around it.** `uDeep`/`uShallow` were taken
    straight off the ocean, which made a navy tunnel. Lit *through*, every reference is the
    same: bottle green where the water is thick, turquoise glass where it thins, brilliant
    white at the lip. Repalletted to `0x0d7a6e`/`0x46e0c8`, with the through-light term up
    about 30% and the ambient floor lifted, because the inside of a tube is bright, not dim.
  - **A wave does not dissolve, it closes out.** The exit used to fade the amplitude to zero.
    It now drives a `uClose` term: the lip folds back down onto the face and the whole section
    goes to whitewater.
  - **Still outstanding**, and honestly the larger half of "behaves like a real wave": the
    wave does not *peel*. A real one breaks progressively along its length — unbroken wall
    ahead of you, barrel at you, whitewater behind — where this one presents the same
    cross-section down its whole length at once, and leaves no foam trail behind the break.
- **v2.23.0** — **The face and the lip are one surface now, not a slab and a spiral.** The
  join was still reading as separate, and the cause was in the height field rather than in
  the curl: the front face used a **smoothstep, whose slope is zero at its top**. So the wave
  arrived at the crest as a rounded hump with a flat summit, and the lip then left that
  summit at nearly a hundred degrees. A ~100° crease, exactly where the seam looked wrong.
  - The face is a concave power curve now — `k^3.2` instead of `smoothstep(k)`. Flat water,
    then a wall that is **still steepening as it reaches the lip** (about 66° at the crest,
    against 0° before), which is what a wave about to barrel actually does. Mirrored in the
    ocean shader with the matching analytic derivative.
  - The lip's launch angle came down from 1.75 rad to 1.42 to continue that sweep rather than
    kink off a flat top.
- **v2.22.0** — **The lip is a solid now, not a membrane.** It still read as a flat circle
  going round, and the reason was simple once named: it was a **zero-thickness sheet**. A
  surface with no volume has no depth to see, and the one place you look straight at its edge
  — the mouth of the tube — is exactly where that gives it away.
  - The cross-section now runs out along the **outer** face (still welded to the crest, so
    that join is unchanged), wraps a rounded tip, and comes back along the **inner** face,
    which is the roof you ride under. Thickness is zero where it leaves the crest, so the
    water still grows out of the wave rather than starting as a step; it swells immediately,
    and stays substantial at the tip, because that silhouetted thickness is the whole 3D read.
  - Radius up from 5.8 to 7.0 to keep the interior roomy once the walls have thickness.
  - **Thickness is a fraction of the radius, not an absolute.** The first pass multiplied an
    absolute 1.5 by the radius and produced a slab ~10 units thick that self-intersected —
    caught from the bounding box, not the render. It also has to scale with the radius on
    principle: the tube tapers along its length, and a lip that held one thickness while the
    arc shrank would read as thicker and thicker toward the far end.
- **v2.21.0** — **The curl grows out of the crest instead of hovering behind it.** It read as
  "water wave, then flat circle" because that is literally what it was: a cylinder with its
  own centre and its own radius, parked near the wave. Wherever it happened to cut the height
  field you got a hard seam between two unrelated surfaces.
  - It is built off the crest now. The arc **starts at the wave's own crest line**, leaves it
    along the water's own tangent, and only then throws forward and over. Every z slice starts
    at local (0,0), so the shell's entire leading edge lies exactly on the crest — the same
    straight line the height field peaks along. The two surfaces are continuous *by
    construction* rather than by tuning.
  - **The first arc was far too flat and buried the camera.** Apex height off the crest is
    `R·(1−cos α)`, and at α=1.0 that is only 2.7 units — a roof at y 9.7 with the camera at
    8.8, so the shell filled the screen and the rider vanished. At α=1.75 the apex is 6.8, the
    roof sits at 13.8, and there is 5.1 units of clearance over the camera.
  - **Geometry alone was not enough.** The height field's crest is a band of white foam, so
    the shell had to leave it white too — otherwise the surfaces join perfectly and the
    *colour* still draws a line exactly where they meet.
- **v2.20.0** — **The barrel roof is water now.** It was reading as a stack of concentric
  glass rings hanging in the air, and there were two reasons.
  - **`depthWrite:false`** was the real one. With depth writing off, the shell's fifty-odd
    rings never occlude each other, so every slice of the tube blends into the frame at once
    — which is precisely a stack of rings. Writing depth means the near wall hides the far
    wall and it reads as one continuous surface.
  - **It was not lit like the sea.** It had a flat gradient and two sine streaks. It now uses
    the ocean's *own* `hash21`/`vnoise`/`fbm`, its own deep/shallow colour pair, and layered
    lace-and-fleck foam dragged around the curl and along its length, so the roof is visibly
    the same substance as the face under the board. It takes the day cycle's light and sky
    colour from the same place the ocean does.
  - **Transmission, deliberately not gated on the diffuse term.** The inside of a tube faces
    away from the sun, so the direct light is zero everywhere in there — which is exactly why
    a real barrel is lit *through* its wall rather than on its surface. Gate the glow on
    `diff` and the green room comes out as a dark hole instead of the brightest thing in the
    game.
- **v2.19.0** — **The set wave was pitched so late almost nobody would ever meet it.** Reported
  from a real run: four kilometres, no barrel. That was not bad luck, it was arithmetic I
  should have done when I set the pacing.
  - The clock opened at 900 m and then wanted a further 64–110 s. But because speed
    compounds, **the whole stretch from 900 m to 4 km is only 114 seconds of riding** — so
    the worst-case roll had barely expired by 4 km, and a run could easily end first.
  - Worse, the clear-lane test `!skiOn && !whirl && !hanging` sat on the *countdown* rather
    than on the *firing*, so a jet ski or a whirlpool stopped the clock dead instead of
    nudging it along. Now the clock always runs and only the moment it fires waits for a
    clear lane.
  - Retuned: opens at **600 m**, countdown **30–45 s**, repeat gap **45–75 s**. First barrel
    lands about **1,200–1,550 m**, then roughly one every **1,100–2,100 m** — two or three in
    a 4 km run. Still special, no longer hypothetical.
  - The trigger path itself was never broken: verified IDLE → FLY → IMPACT → SWELL → RIDE
    end to end with no errors. Every deploy had shipped, too. The pacing was the whole bug.
- **v2.18.0** — **Sunday Best**, replacing Ice Crackle. "China" meant *crockery*, not the
  country — so the board is now a dinner service rather than a kiln glaze. Warm ivory body,
  a **scalloped gilt rim** (the inner edge of the gilding waves, which is the single detail
  that makes it read as crockery instead of a cream surfboard), two fine bands inside it, a
  bouquet where the well of the plate would be, a chain of small blue flowers as the nod to
  the blue-and-white half of the shelf, and hand-painted sprigs scattered over the rest.
  Qinghua stays the cobalt piece; this is the polychrome one. Same 6'5" glider underneath —
  grip 1.26, glide 1.32.
  - New paint helpers `_bloom` and `_sprigLeaf`. Both take an aspect correction, because `u`
    spans six feet of board and `v` only the seventy centimetres across it: draw a circle
    without correcting and every flower comes out as a long smear down the deck.
- **v2.17.0** — **Ice Crackle.** A white Chinese board, and deliberately the opposite of the
  one already in the rack: Qinghua is cobalt *on* white, this one is white on white. It comes
  off Ru- and Ge-kiln ware, which was fired to craze on purpose — the two networks it cracks
  into even have names, 金丝铁线, *golden threads and iron wires*: a coarse dark web with a
  finer warm one laid through it. That, a pair of ruyi cloud scrolls, a lotus-petal panel
  standing out of the tail and one gold hairline inside the rail are the whole graphic. A
  6'5" narrow, low-rockered single-fin glider to carry it — grip 1.26 and glide 1.32, the
  most locked-in board in the shop, against Qinghua's loose 0.82/0.93.
  - **The crackle came out as two and a half cracks across the entire board**, and the reason
    is worth writing down: `_fbm` only ever spans about 0.3, so contouring it with
    `_rep(f*8.0)` gives 0.3 × 8 ≈ 2 lines, not 8. Thirty gives about nine, which is a
    crackle. The line *width* is then bounded the other way by the mesh — same rule as the
    leaf's veins — so the count and the width have to be solved together.
  - **And every band across `v` was one vertex wide.** At `nr:100` there are ~25 rings from
    stringer to rail, so `dv` is 0.04 and the 0.02-wide ruyi and pinline bands rendered as
    nothing at all. Anything drawn across the width has to be at least 0.06.
  - Near-white also sits at the top of the ACES curve where variation crushes — the same
    thing that made the beach sand unreadable — so the ground had to come down off pure
    white before any of the greys could show at all.
- **v2.16.0** — **The set wave.** A seaplane crosses in front of you, banks hard away, drops
  a wing and goes into the water off to one side. What it throws up walks back across the
  lane at you, growing the whole way, and you ride the barrel it forms.
  - **The wave is not an object on the sea, it IS the sea.** Same trick the jet-ski wake and
    the whirlpool already use: one more additive term in the shared height field, mirrored
    in the ocean shader with an analytic derivative. So the shader lights it, foams its crest
    and fogs it with no new code, the board floats on it without knowing it exists, and
    steering into the face genuinely climbs you up it — the water there really is higher.
    The rider sits at about 5.2 units up the face against 0.5 on flat water.
  - **The curling lip is the one thing a height field can't do.** A surface single-valued in
    y can build a wall of any steepness but it can never overhang itself, so the curl is its
    own shell: a tube lying along the lane with a slice taken out of the bottom front, which
    is exactly what you're looking out through from the inside.
  - **Riding it.** Steering toward the wave climbs the face and takes you deeper; steering
    away drops you toward the shoulder and the daylight. Sitting deep pays, and it pays
    steeply, because deep is where it can end you — too far and the lip lands on you
    (PITCHED), too far the other way and you run out onto the shoulder and the ride is over.
    Jumping into the roof ends it too. 6–16 seconds, with a spit-out bonus scaled by how
    deep you got.
  - **New obstacle: wreckage.** Fuel drums and torn wing struts off the plane, tumbling round
    inside the tube. Hazard orange and white on purpose — inside a barrel the light goes
    strange, and the one thing that must never be in doubt is what will hit you.
  - Everything already on the water is swept past you by the swell rather than vanishing, and
    nothing new spawns from the moment the plane is in trouble, so the lane is empty by the
    time the face arrives.
  - Fires past 900 m and then rarely — it should feel like weather, not like a timer.
- **v2.15.0** — **De Soto's Leaf.** A board for the ant, and an actual leaf rather than a leaf
  painted onto a surfboard: the outline *is* the leaf. The nose is the point, so it closes on
  a fine tip; the widest part sits at 55% because a leaf carries its width low toward the
  stem; the tail is the rounded base with a stub of petiole painted on it; and there is one
  small fin the colour of the stem, because a leaf has no business with a thruster. Thin,
  wide and nearly flat — which makes it the loosest thing in the rack that still carries its
  speed (grip 0.77, glide 1.00). Blade colour runs yellow at the point into deep green at the
  base, with a tapering midrib, lateral veins that leave the rib steeply and bend toward the
  tip, a soft mottle, sun-bleaching along the margin, a painted serration, and a few of the
  dead spots every real leaf has.
  - **The veins came out as dashed stitching first, and the reason was not the one I
    assumed.** It is not resolution along the board — it is *across* it. The vein family is
    `u + k·|v|`, so travelling from midrib to rail crosses two whole vein cycles; the ring
    count, not the length count, is what decides whether a vein is a line or a row of dots.
    At `nr:84` each vein was about one vertex wide. Fewer veins, wider, and `nr:124` fixed
    it, and every feature on the board now fades in with `_sm` rather than switching on a
    threshold, so it is sampled correctly at any resolution.
- **v2.14.0** — **The boards ride differently now, because they *are* different boards.**
  Two problems, one cause. The rack had twelve near-identical shortboards in it — a dozen
  paint jobs over the same 5'2"–5'4" outline — and the physics only ever read three numbers
  off a board, all of them *magnitude*: top speed, carve rate, pop. Magnitude is the hardest
  kind of difference to feel. Nobody notices ten per cent more top end without a second
  board to hold it against.
  - **Character, not magnitude.** Two constants used to be identical for every board in the
    game: how hard it resists sliding sideways (`vx -= vx*3.4*dt`) and how long it carries
    speed it earned off a trick (`boost -= dt*3.0`). Those are **grip** and **glide**, and
    they now come off the hull — length, width, tail width, thickness, rocker — rather than
    off the price. A third, **float**, sets how high the board sits in the water. Measured
    in-game: holding full lock for the same window runs a wakeboard 11.1 units across the
    face and a race board 4.7. Boost carry ranges from about three seconds to over seven.
  - **A soft limit, not a clamp.** The first cut clamped a linear fit, which put *twenty of
    the forty-four boards* flat on the same rail — the original problem, moved to the ends.
    `tanh` is monotone and never quite reaches its limit, so every board keeps its own
    number and the extremes merely crowd instead of merging.
  - **Still dodgeable.** The worry with a slower board is that it stops being able to get out
    of the way. It doesn't: damping barely bites in the first moments, so a 4-unit dodge from
    a standstill spans 0.55 s to 0.89 s across the whole fleet. The board's character shows
    up in sustained carving and line-holding, not in emergency reaction.
  - **Sixteen boards reshaped into real archetypes**, each with fins to match — shortboard
    (Razorline, Pit Crew), groveller (Hydro GT), retro fish on twin keels (Wildstyle),
    modern fish (Acid R), Mini Simmons (Turbo 44), hybrid egg (Houndstooth, Liquid Marble),
    funboard (Cup 22, Glitch), mini-mal (Boxcar), longboard (Quilted Noir), gun (Nitro Works,
    Laurel 11), race board (Aurora) and SUP (Bubblegum Foamie). The eFoil, Astro Original,
    Tiki Mal, Carbon Skim, Reef Runner, Maison Monarch and every wakeboard are untouched.
  - All 44 boards re-lofted and checked for degenerate geometry: no NaN in any position or
    colour buffer.
- **v2.13.1** — **Astro's board is pinned back to five across.** Giving it a hull to be
  measured from also gave it *derived* stats, and it came out 7/2/2/7 like any other board
  — which quietly moved the yardstick. It is the one you start on and the one every other
  board in the shop is read against: a bar longer than Astro's means "better than what you
  have", and that only means anything if Astro's sits in the middle. Pinned through the
  same `spec.stat` mechanism the electric boards use, so the price budgeting cannot walk it
  back. It rides as the neutral reference again too — every multiplier at 1.0.
- **v2.13.0** — **Astro's board is a board now.** It was the one board in the rack that was
  not lofted — a flat model with no rocker, no rail and no foil, which is exactly why it
  read as a plank next to everything else. It has a spec like the rest of them: a 5'10 egg,
  wide through the middle and thick, with a rounded pin and a domed deck. Everything about
  the shape forgives, which is what a first board is for.
  Painted **DAWN PATROL** — gold at the nose falling through orange and coral into hot pink,
  violet and the last of the night at the tail, with a cream stringer, twin pinlines inside
  the rail, and the 17 he learned on over the back foot. Poured rather than sprayed: the
  bands wander with the resin instead of ruling straight across, which is most of what
  separates a hand-made board from a decal. Fins are a 2+1 — a big centre doing the holding
  and two small bites keeping it honest.
  Three things it took. The ramp colours have to go through `vivid()` like every other paint
  helper — set straight they come out of the lighting and the tone curve as pastel, which
  turned a sunrise into cream and lilac. A seven's diagonal falls from the far end of its
  top bar back toward the near one; run the other way it makes an L. And screen-left is
  NEGATIVE v with the deck toward you, so laid out the other way it came out as a
  seventy-one.
- **v2.12.0** — **The camera lift was a no-op.** It moved the camera up and then aimed it
  back at the object — which re-centres the very thing the lift was meant to shift, so the
  object stayed dead centre however the measured band moved, and a tail kept touching the
  stat plate. Looking *level* from the raised position is what actually puts it where the
  band is. It clears with margin now.
  **The header wash is heavier and carries past the type.** One that has faded out by the
  time it reaches the last line leaves the type sitting on nothing, which is what it was
  doing.
  **The brown water is gone.** The swell runs a unit and a third either side of the
  surface, so its troughs bare the seabed twice a wave — and a seabed painted the colour of
  dry beach came through as brown water washing in and out. Below the waterline the sand is
  pulled toward the sea's own colour, so what shows through a trough is shallows.
  **The shop is a rack, not forty-four beach photographs.** Every card had the whole beach
  in it — palms, surf, horizon — and a grid of those is forty-four competing horizons, with
  the one thing you came to look at the smallest object in the tile. Cards are shot against
  a cyclorama instead: the ground sweeps up into the backdrop with no seam and no horizon
  line, in the beach's own colours, so the board is the subject. It is far cheaper to
  render too, which matters when the shop photographs sixty-three of them.
- **v2.11.0** — **A surfer opens facing the other way.** Measured: at the old start angle
  the snout pointed screen RIGHT, and the position in the reference has it facing LEFT — so
  the opening angle was half a turn out. Characters now open in profile facing left and the
  snout swings toward you as the turn advances, which is the direction the arrow shows.
  Boards are unchanged: rail-on, deck to the left, fins to the right.
  **Nothing is covered by the stat plate.** The band is MEASURED off the chrome now rather
  than guessed at with a magic fraction — the name block is one line for some boards and
  three for others, and the plate is there for a board and absent for a surfer, so no fixed
  number could be right for all four combinations. That is how a tail ended up behind the
  bars. The object is fitted into the measured gap with margin, and centred in it.
  **The shop's pictures are mini versions of the preview.** They used to be their own thing
  — a three-quarter shot on transparency at its own zoom — so a card and the full-screen
  look showed the same object two different ways and nothing on a card told you what you
  would get when you opened it. Same standing-up, same beach, same framing maths. The one
  thing that differs is the angle: a card is a *still*, and rail-on a board is eight inches
  wide and comes out as a sliver on a landscape tile, so cards use three-quarters — mostly
  deck with enough rail to read the rocker.
  **And the shop is brighter.** It was a flat navy slab, the one screen in the game that did
  not look like the game; everything else is sea and sun. Now a sky-to-water wash with the
  cards as light glass on top of it rather than darker holes cut into it.
- **v2.10.0** — **The ocean was being tessellated flat.** The swell is carried in the
  vertex shader and its shortest component has a wavelength of about two and a half units,
  so the mesh has to be fine enough to hold it — the game's own ocean runs three quarters
  of a unit per segment. The preview's was at **six units a segment, eight times coarser**,
  which is why it read as a slab of blue that moved slightly rather than as water. But the
  water also has to run two thousand units to the horizon, and at three quarters of a unit
  that is a million vertices. So it is built row by row with the rows spaced
  **geometrically** — 0.85 apart at the shore, growing three quarters of a per cent each
  row until they are seventeen apart at the far edge. Four hundred rows cover the whole
  distance with the density where the waves are legible, at about the same vertex count as
  the game's own ocean. Colours were already the game's; the amplitude was already 1.35.
  Neither was ever the problem.
  **The sand was aliased too** — 230 x 210 over 760 x 460 is three units a vertex against
  a seven-unit ripple, two vertices per ripple. Finer mesh, longer ripples, and the grain
  is now colour rather than shape, because no mesh is ever fine enough for grains. Wet sand
  in the wash zone is much darker, so it stops reading as a dry stripe between wave lines.
  **The Back button is centred** — auto margins do nothing on an inline-level box, which is
  why setting them last time did not move it. It has to be block-level first.
  **Arrows are blue** with a deep navy underneath to hold an edge over pale sand, a cyan
  tube and a near-white filament. **A soft wash is back behind the name** — not the hard
  bar from before, and the bottom still has none. **Palm trunks curve properly**: a cubic
  term gives the reverse bend under the crown that a sine and a quadratic cannot.
  **Nugget has proper eyes**, white with a dark pupil, per the reference.
- **v2.9.0** — **Nugget the Chicken**, the twentieth rider, at 8,200 puka.
  A deep round body carried high on scaly yellow legs with almost no waist, and a small
  head on a *real* neck — the neck is the whole reason a chicken reads as a bird rather
  than as a ball. She uses the penguin's flat arms for wings, which folded against the
  body is exactly what a chicken's look like.
  Three new pieces of kit came with her. A **comb and wattles**: a row of soft lobes
  standing along the crown, tallest in the middle, and a pair hanging under the chin —
  nothing else in the roster has them, and without them a chicken is just a small round
  bird. A **fan tail**: a spray of flattened blades leaving the rump together and splaying
  up and back. And the **beak takes a length now**, because a penguin's bill is a long
  spike and a chicken's is a short stubby wedge and they are the same part; the black head
  patch that came with it is opt-out, since that is a penguin's mask and not a beak's.
  Two things the tail needed. `tailJ` carries a yaw of its own that the curled and roped
  tails are built around, so the fan undoes it first — splayed about a turned axis it comes
  out lopsided. And the whole spray stands clear of the rump before it splays: started at
  the joint, the feathers begin *inside* the body and only their tips ever emerge.
  All twenty riders rebuilt and checked after the beak change.
- **v2.8.0** — Nine things on the preview.
  **The sea is the game's sea again** — same colours, and its swell, drift and foam
  thresholds are copied over live each frame, so it moves exactly as the one you surf on.
  The preview had grown its own turquoise, which was a different ocean. Only the light
  level stays pinned, so it is a bright day rather than following the ride into dusk.
  **Darker sand with the dunes actually showing** — deeper relief, harder slope shading,
  and the base tone dropped again so none of it crushes at the top of the tone curve.
  **The palms are darker, shorter, closer and fully in frame**, with more rings on the
  trunk and warmth back in the bark. **More gulls**, smaller and lower, so there is
  something living in it.
  **Both scrims are gone.** Over a beach they read as a dark bar across the top and a grey
  wash across the bottom, fighting the picture they were meant to help. The type carries
  its own legibility now — a hard shadow rather than a panel.
  **The board and the Back button are centred.** A 0.06 camera lift tips the view down and
  pushes the object low: measured, its centre sat at 58% of the screen. And the button is
  an inline-flex box in a block container, so it was hard against the left edge with a
  hundred pixels of air on its right.
  **The arrows are a deep burnt orange.** A bright neon works over a dark sea and
  disappears over white sand and a pale sky — there is nothing for it to be brighter than.
- **v2.7.0** — **Two palms were standing in the sea.** The waterline is wherever the beach
  profile crosses zero — about −69 — and they were at −88 and −116, well past it, so their
  trunks came up out of the water. Placement is checked against the profile in code now
  rather than picked by eye: a palm is walked up the beach until there is at least half a
  unit of dry sand under it. The shoreline also moved back, so there is a deep enough beach
  to stand one on and still have it land inside the frame.
  **The sand has dunes you can see**, which took two fixes rather than one. The flat pad
  around the object was twenty-six units across and the near field IS about thirty units
  deep, so it was flattening everything visible — that is now four. And the dunes were
  there all along but *invisible*: at 0.80 luminance the sand sat right at the top of the
  tone curve where ACES compresses everything together, so no amount of relief could show.
  It renders in the middle of the curve now, with shading painted in from the surface slope
  because a white, near-Lambertian material under a hemisphere gives almost none of its
  own. The ripples are stretched along the shore rather than round: the camera is low, so
  anything square-ish flattens to nothing at that angle, while ripples running across the
  view present their profile over and over — which is why wind and tide ripples are the
  thing you actually see on a beach from a deck chair.
  **Palm bark and crowns.** The trunk carries the hard ring scars a coconut palm's old leaf
  bases leave, climbing in a slow spiral rather than stacking level, with fibre between them
  and three tones far enough apart to read as cut in. The crown was three greens within a
  few per cent of each other, so every frond was the same flat sheet; there is a deep shadow
  green and a sunlit one now, each frond carries its own tint, and leaflets darken as they
  droop into the crown's shade.
- **v2.6.0** — **Turning hard no longer ends the run, and cutbacks are gone as a trick.**
  Throwing the board back harder than 10.4 sideways used to blow the tail out and end the
  run — the reward and the wipeout were the same move a little further apart, which is a
  fine idea and a miserable one to be on the wrong side of. Both halves are gone: no
  points, no speed boost, no message, no wipeout. You can throw it about as much as you
  like and the only thing that comes of it is water. The spray stays, as feedback rather
  than as a trick; its timer is a rate limit on the particles now, not a cooldown on
  scoring, and it is named for what it does.
  `BS.blow` went with it — the per-board blowout threshold existed only to feed that one
  wipeout, so it is no longer computed or carried.
  Both halves verified against the previous build. Forcing sideways speed to 14.5 against
  the steer, ten times: **v2.5.0 dies, this does not.** Forcing it to 8.2 — inside the old
  cutback band — twelve times: **v2.5.0 scores 75, this scores 0.**
- **v2.5.0** — **The menu was bleeding through the shop.** The shop panel is 94% opaque,
  which is what lets the sea move behind it — but the MENU was behind it too, and six per
  cent of a 54px title is perfectly readable: *Surf*, the purse, *Play*, *Beach shop* and
  *Stats* were all ghosted through the cards. The menu is put away while the shop is up
  and brought back with it, the same as the full-screen viewer already did. The sea is
  still there behind both. Checked the whole round trip — menu → shop → preview → shop →
  menu — at every step.
- **v2.4.0** — **The beach to the reference.** White sand, turquoise water, deep blue sky
  with cumulus, palms to the sides, and the board **hovering** clear of the sand with a
  shadow under it rather than planted in it.
  **The shore runs square now**, not angled. The diagonal was a compromise to get dry land
  into a narrow frame and it read as a spit cutting across the view. Straight — sand in
  front, water behind, horizon above — is what standing on a beach looks like; the room
  for a palm comes from making the beach DEEP instead, with the waterline a long way back,
  so there is sand either side of the object that still lands inside the frame.
  **The bleed-through is gone.** That patch of water appearing out on the dry sand was the
  dune displacement dipping below sea level, so the sea plane came up through the beach.
  Dunes are now faded out as the sand approaches the water, and flattened entirely for
  twenty units around the object — which also means the ground under it is a known height,
  so its shadow lands on sand instead of inside a dune.
  Palms are placed against the frame rather than the world: the visible half-width sixty
  units back is about ten, so a palm at sixteen is off the side of the screen with only
  its frond tips showing. Fronds are denser and the crowns fuller.
- **v2.3.0** — **A proper beach.** Bright afternoon rather than dusk, with the game's own
  clouds and gulls in it, palms, and sand that reads as sand.
  The sky and sea are still the game's shaders but now on their **own** uniforms rather
  than the shared ones — sharing meant the preview inherited whatever hour the last run had
  wandered into, which is how it kept coming out at sunset. Only the wave clock is copied
  across, so the swell still rolls.
  **The band of sky under the water is gone**, and the fix was to stop pretending a beach
  is a floor. The sand was a flat sheet at the water's own level ending in a straight edge
  short of it: where they overlapped they fought for the same depth and tore into stripes,
  and where they did not the sky showed through the gap. It has a *profile* now — from well
  under the sea at the back, up through the waterline, to dunes inland — and the sea sheet
  carries over the submerged part. Above water you see sand, below it you see sea, and the
  shoreline is simply where they cross. And it runs **diagonally**: a shore square to the
  camera gives you either open water with nowhere to stand a palm or a beach that hides the
  sea, so the sand is a wedge coming in from the right and narrowing to a point.
  **Palms** are one buffer geometry each — trunk, frond spines and every leaflet in one set
  of arrays — because a palm is a few hundred blades and a few hundred draw calls for
  scenery is not worth it. Lofted trunk with the ring scars a palm actually has, fronds that
  stand up at the crown and droop as they run out, leaflets tapering along each spine, and
  coconuts tucked underneath.
  **The whole set scales with the framing distance.** At this field of view the frame is
  barely three units wide where the object stands, so a beach built once looked completely
  different depending on how far back the camera had gone — a longboard put the palms in a
  corner, a character had a dune filling half the screen. Scaled, it composes the same for
  everything.
  Sand colour was measured, not guessed: a plausible 0xbb9c66 rendered as **(152,150,140)**,
  all but neutral, because the scene's blue hemisphere and blue fill lift the blue channel
  nearly twice as hard as the red and ACES desaturates what is left. The chroma is put back
  in at the source.
  **Stat bars sit on a plate of their own now**, lifted clear of the button instead of
  sharing one dark slab with it, and **Back is cyan** — the menu's own colour.
- **v2.2.0** — **The preview stands on a beach**, and the fin faults I said were fixed
  were not.
  **Both of the earlier "0.0000" fin results were measuring the wrong thing.** The gap
  check probed the fin's centre — the one point the arithmetic puts it at — and the reach
  check compared against the board's widest point rather than its width at the fin's own
  station. A fin is not a point: its root is a straight chord up to two thirds of a metre
  long, and over that length the hull curves away underneath it and the outline narrows
  around it. Sampled properly: **roots standing up to 0.049 clear of the hull** on Riptide
  Wake, which is the daylight you could see, and **fins reaching 1.086 of the local
  half-width** on Midnight Gun. The root now goes to the highest the hull reaches anywhere
  along the chord, so every other point is buried in foam; the clamp is against the
  narrowest the board gets along the same span, base a fifth in from the rail. A swallow
  tail or an asymmetric cut also moves the surface forward of its own station, so the
  station is solved for rather than assumed — that closed the last 0.017. Now: **worst gap
  0.0008** (mesh faceting), **worst reach 0.885**, all 33 pairs still exact mirrors.
  **The full-screen look is on a beach** — the game's own sky and sea, the same shader
  instances on the same uniforms, so it sits at whatever time of day the last run reached
  and the swell keeps rolling; the swell is calmed while you are looking. The object stands
  ON the sand with the water behind it, because with the shoreline between camera and
  object how much beach you saw depended on how far back the camera had gone to frame that
  particular thing. `viewCam`'s far plane was 300 against a sky dome of radius 320, so the
  dome never drew at all: the "dark sky" was the clear colour and the "horizon" was the far
  plane cutting the sea off straight. Shop thumbnails are unchanged — they are photographed
  onto transparency and a beach baked into a 148px tile fights the board's graphics.
  **Arrows moved 12px in off the bezel** and the pulse has more range, in the tube as well
  as the halo.
- **v2.1.0** — **The preview opens side-on, and you can turn it with a finger.**
  Standing a board up maps its deck normal onto world +z, so at a yaw of zero you were
  looking straight at the deck. A quarter turn the other way puts that normal on −x: deck
  to screen-left, hull and fins to screen-right, and what you see is the **rail** — the
  profile that shows the rocker and the foil, which the flat-on view cannot. Turning from
  there is a reveal rather than a rotation of something already fully visible. Characters
  start at the same angle, in profile, and come round to face you.
  **Drag across and it follows your finger; let go and it carries on from exactly where
  you left it** — no snap back, no easing to a home angle. The idle turn is simply
  suspended while a finger is down, so there is no second angle to reconcile: the gesture
  writes the same number the idle rotation reads. A drag the full width of the phone is
  about three quarters of a turn. `#viewer` is `pointer-events:none` with its children
  opting back in, so there was nothing for a drag to land on; there is a surface for it
  now, under the arrows and the bottom bar but over the sea.
  **And it stays in the middle while it turns.** It rotates about the vertical through its
  own ORIGIN, but the framing was taken about the middle of its bounding box — fine for a
  board, wrong for a posed character, which does not sit centred on its origin and swung
  off to one side as it came round. That was invisible until everything started opening at
  a quarter turn. The camera now aims at the axis, and the horizontal reach is measured
  from the axis out to the furthest corner, which holds at every yaw. Measured across a
  full turn at eight angles: the silhouette's centre now wanders 0.076 at worst.
- **v2.0.0** — **The wipeout buttons are slimmer, lower, and in the menu's own colours.**
  They were green and blue-grey, two colours that appear nowhere on the main menu. The
  menu's palette is orange, cyan and yellow, so: *Surf again* keeps its orange (it is
  literally the Play button), *Watch a clip* takes the yellow off Stats, and *Main menu*
  takes the cyan off Beach shop. Pills are 40px tall instead of 52 — only on the wipeout
  card, which carries a five-line summary as well; the menu has the room and its buttons
  are the first thing you see, so they are untouched at 52.
  Getting them lower needed a layout change rather than a nudge. With a centred column
  the gap below is (screen − content)/2, so the only way down is to make the block
  TALLER — and thinning the buttons had made it shorter, which measured as the stack
  rising 48px. Padding it back out would have pushed the title up by half of whatever I
  added, into the notch. So the stack takes `margin-top:auto` instead: all the free space
  goes above it, pinning it to the bottom padding on a tall screen and collapsing to
  nothing on a short one. Checked at 430×930, 430×1100 and 375×667 — buttons 91px lower
  than they were, title within 17px of where it sat, and on the smallest screen the
  summary still clears them by 45px rather than being overlapped or clipped.
- **v1.99.0** — **The arrows are a drawn line rather than a bent bar.** Every segment is
  a bezier now, so there is no mitre at the point and no blunt cut at the ends: the band
  is widest where it turns and runs out to a sharp tip at either end. The two control
  points either side of the apex share the apex's own x, which makes the tangents through
  it collinear — so the curve genuinely *turns* instead of kinking, and turns tightly
  because those controls sit close to it. My first pass at this still had a 17° kink at
  the point, which is exactly the blockiness that was meant to go.
  Thinner again — strokes 3.0/1.35/0.44 against the previous 4.6/2.1/0.66 — and taller,
  46×240 at a 6.7:1 proportion where it was 5.5:1. The hollow is a slot rather than a gap.
- **v1.98.0** — **Thinner arrows.** The neon reads better as a fine line than as a fat
  tube: the three strokes come down from 10/4.8/1.05 to 4.6/2.1/0.66, the bloom behind
  them is pulled in to match, and the whole chevron is about 12% smaller — 54×189 rather
  than 62×224, still a comfortable tap. The hollow is untouched, so thinning the lines
  actually opens it up: more air inside, less ink around it. Same 5.54 proportion, same
  6px inset, still mirrored.
- **v1.97.0** — **The preview arrows are neon.** Hollow chevrons with a warm wash
  inside them, drawn the way a neon tube is drawn rather than with a glow filter bolted
  on: the *same closed outline* stroked three times, widest and deepest underneath, then
  the saturated tube, then a thin near-white filament on top. The falloff from a hot core
  out through orange happens across the width of the stroke itself, so it survives being
  scaled and never bands the way a blur does. The bloom breathes, and the filament
  breathes a beat out of step with it, so it reads as something powered rather than as a
  shape with a glow on it. Same proportion as before — 5.54 high to wide, symmetric, 6px
  in from each edge, mirrored exactly — and the same behaviour: still absent at the two
  ends, still walking the shop's order.
- **v1.96.0** — **The fins were still wrong, in three separate ways, and all three were
  measurable.**
  **Cant was never cant.** It is meant to lean a fin's tip out toward its rail, and it was
  applied as `rotation.z` under three.js's default XYZ order — which builds Rx·Ry·Rz, so
  it ran BEFORE the quarter turn that lays the fin across the board. That spun the fin
  inside its own plane, and the quarter turn then converted the spin into a fore-and-aft
  tilt. With opposite signs on the two rails, one fin leaned forward and the other back.
  Every one of the 33 boards carrying a side pair failed a mirror test, by up to 0.13 on
  Coco Egg, all of it in z. `ZYX` builds Rz·Ry·Rx, so the turn goes first and the cant then
  leans the tip outward. **All 33 mirror exactly now.**
  **The fins raked forward.** Rake sweeps a fin's tip toward the shape's +x, and a positive
  quarter turn put that at the nose. Measured on Razorline: tip at z 1.863 against a root
  at 2.055, so the tip LED the base by 0.18 where a real fin trails it. Turning the other
  way fixes it and is a rotation rather than a mirror, so winding and normals are untouched.
  **There was no toe-in.** A side fin is not set square to the stringer — its leading edge
  angles a couple of degrees in toward the nose, and that is half of why a cluster reads as
  a cluster instead of three fins that happen to be near each other. 2.73° now, mirrored.
  Cant is capped at 7.5°, the top of the range a shaper sets; the specs ran to nearly 15°,
  which nobody had ever calibrated because the number was never visible.
  **And the cluster is generated rather than typed.** Fins are grouped by station, and
  within a station every fin off the stringer is built from the first of them: same size,
  same profile, same rake, same distance out, mirrored. Three hand-typed numbers are no
  longer trusted to agree. A fin ON the stringer keeps its own size, so a big centre fin
  between smaller side bites — a real 2+1 — is still possible, it just has to be deliberate.
  That restores the 2+1s that v1.92 had flattened. Two setups that were not true to type
  are fixed: Fathom's side bites were under a third of its centre fin (now a little under
  half, which is the real ratio) and Asymmetric's sides were LARGER than its centre, which
  no thruster runs.
- **v1.95.0** — **The side arrows are chevrons now**, reshaped to the drawing: one long
  slender V about five and a half times as high as it is wide, running a quarter of the
  screen's height, rather than the stubby shaft-and-head arrow that was there. Symmetric
  about its own point, so the two arms are the same length, and mirrored exactly across
  the screen — measured, both sit 6px in from their edge and the apex is dead level with
  the middle. Round joins, or the point of a stroke this thick comes out as a spike.
- **v1.94.0** — **Arrows down each side of the preview.** Long thick orange arrows that
  pulse, one to a side, stepping to the previous or next item without going back to the
  grid — and they walk the *shop's* order, so what is next here is what was next in the
  list. There is nothing before the first or after the last, so at either end the arrow
  goes rather than sitting there greyed out inviting a tap that does nothing. No plate
  behind them: at this size the shape carries on its own, and a button would compete with
  the very thing it is there to let you look at.
  Stepping closes and reopens rather than swapping the model, because opening does a good
  deal more than load a mesh — it stands boards on their tail, measures the box it has
  just stood up in order to frame it, poses a character, and files away what to put back
  afterwards. Both halves run in one turn, so the shop never flashes up in between.
  Walked the whole board rack forward and back and both ends of both tabs: 58×110 hit
  areas, centred vertically, correct neighbour every time.
- **v1.93.0** — **Distance and tricks are two scores, each with its own record.** They
  were one: the score counted 0.6 a metre, which came to six hundred points over a
  kilometre — several times what the tricks themselves were worth — so the score was
  mostly a second, worse distance readout, and the single BEST was really a distance
  record wearing a trick score's name. Ground covered pays nothing into the score now.
  Distance still pays puka on its own, exactly as before, so the change costs about a
  seventh of a run's earnings rather than gutting them.
  Best distance was already kept (in `stats.bestDist`) and is untouched — distance is
  scored exactly as it always was, so that record still stands. Best *tricks* starts
  fresh under a new storage key, because every figure under the old one was banked when
  the score also counted distance and would have sat on the screen unbeatable.
  **The readout is off the top edge and rearranged.** It sat hard against the top, under
  the notch. It is 15px further down now, on a scrim that fades out rather than a bar
  with a line under it, with hairline dividers between the columns and tabular figures so
  the numbers stop jittering sideways as they change. BEST is no longer a column of its
  own — a best score means nothing sitting next to a distance — so each column carries
  its own record underneath, counting the run in progress and lighting up the moment you
  pass it rather than telling you on the wipeout screen. Four columns instead of five.
  **PUKA is this run's take, not the wallet.** It said "this run" and showed your whole
  balance. Verified with 4,321 in the bank: the readout showed 2. The wipeout screen
  banks it and the shop is where the balance lives.
- **v1.92.0** — **The nose you circled was not a board.** Every outline held its nose's
  own width for the first sixth of the board and only then blended out to the shape
  curve, which left a parallel-sided neck at each tip with a shoulder where it flared —
  a nipple on the end of a mini-mal. Measured on Tiki Mal, the rate at which the outline
  widened ran 19 → 5.6 → 2.8 → **1.2** a quarter-metre back, then climbed again to
  **4.9**: it stops widening, holds, and flares. A real plan outline widens fastest at
  the tip and slows all the way to the wide point; it never speeds up again. So the
  curve is now trimmed rather than blended — find the point at which it is already as
  wide as the nose, likewise the tail, and stretch that span over the board. Same widest
  point, same waist, no necks. Checked on all 43 boards by sampling the built mesh one
  ring at a time: **43 had the fault, none do now.**
  **And every fin on a board is the same fin.** Thirty of the thirty-six finned boards
  were mixing sizes — Tiki Mal ran a 0.58-deep centre fin between two 0.26-deep side
  bites, less than half its size, and Fathom's sides were under a third of its centre.
  One template now, taken from the largest in the cluster. Worst size spread within a
  cluster went from 68% to 0.8%. Only size and profile are shared: which way a fin rakes
  still belongs to the fin, so a twin-tip's back pair still trails backwards, and so
  does cant. Worth knowing: a big centre fin between small side bites is a *real* setup —
  that is what a 2+1 longboard runs — but at this scale it read as three fins that did
  not belong to each other rather than as a design.
  Board stats are computed from the spec numbers, not the loft, so no bar moved.
- **v1.91.0** — **An eye button on every shop tile.** The full-screen preview was only
  reachable by tapping the picture or the name, and nothing on the tile said so — you
  had to already know. There is now a small orange eye in the top-right corner of every
  card, boards and surfers alike, and tapping it opens the same preview. Tapping the
  picture still works; the eye is an addition, not a replacement. It sits over the
  picture rather than in the row of buttons so it never competes with **Ride it** or the
  price, and it stops the tap from reaching the picture underneath so one tap is one
  action. Checked on both tabs: 44 board tiles and 19 surfer tiles, every one with an
  eye, each opening its own card's preview.
- **v1.90.0** — Three things, all of them found by measuring rather than by looking.
  **He waves at you after a 180.** The head used to crane 2.30 radians back over his
  shoulder when he landed one, which screwed his neck away from the camera at the one
  moment you want to see his face — his body has already come round to face you, so the
  extra yaw was fighting it. That is gone. Instead he puts a hand up and waves for three
  seconds, eased up and eased back down, then goes back to surfing. There *was* a wave
  before, but it ran for as long as he rode switch, so it started on the landing and
  never stopped. Traced: four cycles at 1.4 Hz over 3.00 s, then the arm settles back to
  the planted stance exactly.
  **Fins sit on the board now.** Two separate faults, both on every board with more than
  one fin. Each fin was hung at the depth of the hull's *centreline* while standing out
  near the rail, where the hull is much shallower — on Razorline that left the root 0.017
  below the surface it is supposed to grow out of, about a quarter of the board's
  thickness there, and you could see daylight through the gap. And the sideways placement
  ignored both the fin's own thickness and its cant, which leans the tip outward again:
  seven boards had fins standing clear of the outline, Midnight Gun's by 13%. Fin roots
  now follow the hull's actual curve at their own offset, bases are held a tenth of the
  half-width in from the rail and tips are held at it. Audited across all 36 boards that
  carry fins: worst overhang 0.0000, worst root gap 0.0000.
  **And the clusters are laid out to a rule** — one fin exactly on the stringer if there
  is an odd one, the rest in mirrored pairs. Asymmetric had three fins at three different
  offsets with nothing in the middle, and Chrome Wake Pro's two centre fins were each a
  sixteenth off the stringer, one to either side.
  **Board ends are rounded, not pointed.** The closing arc was applied to the plan
  outline only, while the thickness ran out over a far longer span — so a narrow-nosed
  board finished as a thin flat blade: full width within 0.03 of the tip but only a
  third of its thickness, which face-on reads as a spike. The arc now takes the
  thickness with it, so the section scales down in proportion and the tip is a small
  round end rather than a point, and its radius will not go below the board's own
  thickness. On Midnight Gun the nose arc went from 0.030 to 0.123 board units.
- **v1.89.0** — **Boards stand up in the full-screen preview** and turn on their tail,
  which is how a board is looked at in a shop. Two details made it work. The Euler order
  matters: with the default XYZ the spin would be applied first and the stand-up second,
  so the board would tumble corner-over-corner instead of turning — YXZ stands it upright
  and then turns that about the world's vertical. And the fit is now taken against
  whichever axis binds, not the width, because an upright board is limited by its height;
  it fits the band BETWEEN the name block and the stat bars rather than the whole
  viewport, or the nose runs behind the title at one end and the tail through the bars at
  the other. Characters are unchanged — they still stand as they stand.
- **v1.88.0** — Four fixes.
  **Wick's tail fin** hung off the end of the tail joint like a paddle on a stick. A tail
  fin does not start somewhere behind an animal, it grows off the back of it — so it sits
  on the joint now with a peduncle filling the gap, swept back rather than standing square.
  **The helicopter can be worked.** heliAir is no longer a latch on the rotation: the roll
  is simply *airborne AND held*, so letting go stops it where it is and taking hold again
  starts it turning from there. Measured — roll froze at 4.90 on release and resumed to
  7.58 on the second press. You can work the button as many times as the air lasts.
  **ROLL and FLIP glyphs are swapped**, buttons unmoved.
  **A full-screen look at anything in the shop.** Tap a thumbnail or a name and the object
  fills the phone, turning once every fifteen seconds, with its name, its blurb and — for
  a board — its stat bars. It is rendered live rather than photographed, borrowing the
  scene the shop already photographs into and the renderer already running, so there is no
  second canvas and no second set of lights.
  One thing it needed: opening it cancels any thumbnail pass still
  running: that pass photographs one card per frame and every character shot rebuilds the
  shared rig, so a job landing a frame later would swap the model out underneath and leave
  you looking at your own surfer under somebody else's name.
- **v1.87.0** — The racing set is covered in sponsor decals, because a real GT car is not
  a paint job — it is a paint job under two hundred stickers, and the density of them is
  most of what makes one read as a race car rather than a coloured car.
  None of them say anything. At the size a board is ever actually seen, lettering IS a row
  of bars, so that is what they are: a panel, a margin, and bars inside it. No real marks
  and no real names anywhere. Each board gets a title bar down the deck, contingency
  strips running the flanks the way they do above a rear wheel arch, a scattered field of
  small decals placed off a hash so the board is the same board every time it is lofted,
  and a number roundel. Hydro and Acid carry a full grid car's worth; Laurel runs fewer
  and finer, because a privateer does.
- **v1.86.0** — A character of my own, a porcelain board, and a second racing set.
  **Wick the Anglerfish** (8,800). The whole cast is shapes; nothing in it *does*
  anything. She does: the bulb on the end of her stalk is dim in the afternoon and burns
  at dusk, driven off the same time-of-day figure the sky is lerped from, so a deep-sea
  fish carries her own light out into the sunset as the run goes long. All head and hinged
  jaw on almost no body, needle teeth too long to close over, pectoral flippers, a caudal
  fan. Least likely animal in the ocean to be on top of it.
  **Qinghua** (10,500) — blue-and-white porcelain. Sea-and-cliff scallops climbing the
  deck in rows, a key-fret meander down each rail, and a craze through the glaze. Cobalt
  is a wash rather than a flat ink, so every blue is laid down through a noise field: it
  pools and thins across a stroke the way a loaded brush does.
  **Hydro GT, Acid R, Laurel 11, Cup 22** — a second racing set off real GT liveries
  rather than invented ones. Works blue over white dissolving into pixels; matte black and
  a yellow that hurts, every panel line drawn with a straight edge; black lacquer with
  hand-laid gold coachlining and a laurel; and a one-make car in white with red bands and
  a number, which never needed anything else.
  All four of those came out blank white at first: `_patch` takes the VERTEX u and v as
  its second and third arguments, and every call was passing the patch's own centre into
  those slots, so the colour landed as undefined and painted the whole deck. Caught by
  probing the paint rather than staring at renders — the deck measured a flat 1.00
  luminance from nose to tail.
- **v1.85.0** — **Fathom** (6,800). Every other board in the rack imitates a style — a
  log, a fish, a spray job, a race livery. Not one of them is a READOUT. This one is: the
  deck carries a contour chart of the sea, built the way the sea in this game is actually
  built, out of a handful of sines crossing at angles, sampled across the board instead of
  across the water and quantised into depth bands. It samples its own field rather than
  calling waveH — the ocean's is tuned to metres of open water and would come out flat
  across two units of foam — but it is the same construction, scaled to the object.
  Everything else follows from that. Mid-length displacement hull, long and narrow with a
  pulled-in nose and a rounded pin, because it should look like an instrument and not a
  toy. Ink and bone with one cold accent, so nothing competes with the pattern. No deck
  pad, because a black rectangle in the middle of a chart is a hole in the chart. And
  depth graduations ticked down the rail, because a chart has a scale on it.
  Four things had to be measured rather than eyeballed to get there. The field was thirty
  cycles down the board and fell through the mesh into speckle — dropped to nine. Contour
  width was in band units, so lines thinned to dashes wherever the field steepened —
  normalised by the local gradient. The field only ever visited the middle third of its
  range, so nothing deep or shallow was ever painted: measured at 0.24–0.41 luminance
  across the whole deck, now 0.04–0.82. And the weave is far finer across the width than
  any other board needs, because contours mostly run that way and at the usual sixty
  divisions a hairline falls between two rings.
- **v1.84.0** — Two more on the helicopter, both about how it reads from where the camera
  actually is.
  **Dead level through the rolls.** Tipping it forward and up was right in principle and
  wrong on screen: the camera sits behind him, so any pitch turns the roll into a view of
  the board's underside swinging past rather than a turn. Level, the silhouette is exactly
  what the move is — the board going over and over.
  **He lies flat on the deck instead of perched on the nose.** At a body pitch of 0.95 he
  was still half upright, which read as leaning over the front of the board rather than
  lying on it. The body goes down flat (1.36) and the head comes up instead (0.66), which
  is how anyone actually lies on a board, and he sits forward without hanging off the end.
  Measured: body facing y -0.98, head facing z -0.72 and y -0.66 — chest on the deck, eyes
  over the nose.
- **v1.83.0** — Five corrections to the helicopter.
  **He was face-up and travelling tail-first.** A positive turn about X tips his top
  BACKWARDS, so he was lying on his back staring at the sky and going backwards up the
  board. Negative puts his chest on the deck. Measured rather than eyeballed: his facing
  vector was y +0.02 riding and is now y -0.91, z -0.38 prone — down, and down the board.
  Pitch is 0.95 rather than 1.22 so he looks over the nose instead of straight into it,
  and his chin comes up off the deck.
  **The dive is shallower in angle but far deeper.** He no longer spears in nose-first; he
  slides under at a gentler pitch and keeps going to 2.30 below the surface, board and
  all, then is fired back out on an eased-in curve — slow to leave, then all at once.
  **The rotation is yours again.** The wind-up is still scripted, but the rolling turns
  for as long as you hold and stops when you let go, so where it is pointing at release is
  the attitude he lands in. The release cue comes back with it.
  **The launch is lower** — 17.2 rather than 20.5, which is 1.72s of air against the 1.40s
  three turns take, so three is comfortably on and a fourth is not.
- **v1.82.0** — The controls are actual glass rather than an outline. From the
  references, four things together are what make a pane read as glass: a specular streak
  sitting across the top, an edge lit unevenly instead of a uniform ring, a cool bounce
  caught along the bottom inside, and enough blur behind it that you can tell there is
  thickness. All four, as one reusable `.glass` surface shared by every in-play control.
  The body stays almost clear — the first attempt had a broad bright cap and read as
  glossy plastic bubbles, so the streak came down to a thin sliver and the interior to
  almost nothing, leaving the crisp rim to do the work. That is what the references
  actually do. Pressing one lights the whole pane rather than filling it.
  White word and white glyph as before, and the sea still shows through all of it.
- **v1.81.0** — The helicopter is choreographed. Pressing it commits you to a run of
  beats, each finishing before the next starts, so it reads as one move he is performing
  rather than a rotation you are steering: he sinks down over the board and takes hold of
  it, hauls himself forward until he is prone on the nose, hops off the deck, puts the
  nose down and goes under, comes back up out of it, and is thrown up and slightly
  forward rolling three times over.
  The sequence owns his height outright while it runs, which is the only way he can go
  UNDER the water and come back — the ordinary ride physics exist to keep a board on the
  surface and would fight it the whole way down. He goes about a metre under at the
  bottom of the dive, with only the tail and the fins left showing.
  Roll rate eased from 2.6 to 2.15 turns a second and the launch raised to 20.5, which
  buys 2.05s of air against 1.40s of rolling — so the last two thirds of a second is a
  plain fall. That gap is deliberate: it makes the landing feel like the end of something
  rather than the moment the animation runs out. Three whole turns also finish square, so
  a helicopter lands itself. Measured end to end: it completes, lands clean, keeps the
  run going, and scored 3,612.
- **v1.80.0** — The foil flies, and the helicopter is a barrel roll.
  **The Mako rides up on its mast**, hull clear of the surface with only the wing in the
  water, the way the real thing does. The lift is VISUAL: the collision height is
  unchanged, because the mast and the wing are down there whatever the hull is doing, and
  a board that floated over hazards would be a different game. On top of it a slow bob —
  about four seconds to rise and settle, two sine terms slightly out of step so it never
  repeats cleanly. Measured: 0.62 of lift on the Mako and exactly zero on every other
  board.
  **HELICOPTER is a barrel roll now, not a flat spin**, and it is held and released like
  the other three rather than a committed move that plays itself out. It drives the roll
  axis, so the board turns over the way it is travelling. What it buys you is rate: a bit
  over twice the ROLL button's, for getting a lot of rotation into a short piece of air.
  Same rule as the rest — where he is pointing when you let go is the attitude he lands
  in, so it cues when it is square, and it takes the ROLL entry's name on the scoreboard
  with a 1.35x multiplier. He also lies right up on the nose now rather than over the
  middle of the board.
- **v1.79.0** — The version label sits clear of the iPhone home indicator. It was pinned
  at a flat `bottom:8px`, which put it right on the bar; it is 24px now, plus a
  `safe-area-inset-bottom` term so it tracks the indicator on any device that reports one.
  Worth noting for later: that env() term is inert until the viewport meta carries
  `viewport-fit=cover`, which this page does not set — so are the two safe-area rules
  already in the stylesheet, for the HUD's top inset and the menu buttons' bottom inset.
  Adding it would make all three work properly, but it also lets the canvas run under the
  notch and the indicator, so it wants testing on a real handset rather than being turned
  on blind.
- **v1.78.0** — **Bongo the Monkey** (2,100), and the reason the site was stuck on v1.74.
  The arms are the animal: longer than his legs and thin, so he looks built to hang off
  things. Long tail, long hands and feet, pale muzzle, and a new ear type — big flat
  discs low on the sides of the head, which is half of reading as a monkey at thumbnail
  size.
  **The deploy had dammed itself.** Three versions were sitting on main undeployed. The
  Pages workflow uses a concurrency group with `cancel-in-progress: false`, set a while
  back on the reasoning that queueing rather than cancelling meant two quick pushes would
  both land. That is wrong for a deploy: pushes to main are fast-forwards, so a later
  commit already contains the earlier one and publishing only the newest is exactly
  right. What the setting actually bought was a dam — the v1.75 run was never picked up
  by a runner and sat queued for twenty minutes, holding v1.76 and v1.77 behind it. The
  stuck run is cancelled and the group cancels in progress now, so a run nobody picks up
  can no longer hold the site three versions stale.
- **v1.77.0** — Three new surfers, and the kit pieces each needed.
  **Hazel the Squirrel** (1,600) — light and quick with the back legs of something that
  lives by jumping. Her tail is the whole animal, so it got its own type: a big plume
  built as a chain like the long tails but with a fat lobe on every link, arcing up off
  the rump and coming over her own back rather than curling into itself. New rodent face
  with the two front teeth a rodent never stops showing.
  **Quill the Porcupine** (3,900) — heavy and low, because everything he does he does
  slowly and on purpose. The quills are laid on the body using the same numbers the torso
  is built from rather than a guessed radius, so they sit ON a wide animal instead of
  floating off it, and each stands along its own outward normal raked back. Dark paws,
  pale tips.
  **Zorp the Alien** (13,500) — an enormous head on almost nothing, carried on the longest
  thinnest limbs in the game, with huge slanted black almonds for eyes and barely any face
  under them. Three fingers, three toes.
  One general fix came with him: kit parts hang off the head JOINT, so head size never
  reached them — his head is a third bigger than anyone's and his whole face was left
  buried inside it. The alien's eyes and mouth are placed against his head scale now.
- **v1.76.0** — An owner's key in the Records panel: one press unlocks every board.
  It lives behind the five-tap version door because that is already the entrance nobody
  finds by accident, and it stays out of the shop entirely — it is the only thing in the
  game that hands you something you did not ride for. It counts what is left ("Claim 37
  boards"), takes two presses like Reset does so a stray tap cannot quietly rewrite a
  save, greys out once everything is owned, and redraws the shop underneath if it happens
  to be open. It gets its own line in the button row; squeezed in as a fourth button its
  label wrapped onto two lines.
- **v1.75.0** — Less wash, a new trick, and an electric board.
  **The water round the board is calmer.** It was reading as a wall of white either side
  of the hull rather than a rail throwing spray — less of it, thrown less far, and gone
  sooner, on the carve fan, the bow spray and the wake ribbon alike. The board should be
  the thing you are looking at, not the wash.
  **HELICOPTER.** A fifth trick, on its own wide button under the four round ones because
  it is not a hold — you commit and it plays out. He turns to face down the board, goes
  flat over the deck, takes hold of the nose and hauls himself up it until his elbows fold
  and his face is at the tip, while the whole board spins three full turns underneath him
  and throws a corkscrew of spray. It launches itself higher than a normal jump rather
  than borrowing one: three turns at that rate needs 1.15 s and a jump only buys 1.08 s of
  air, so without its own pop it could never finish. Land it round and it pays 620.
  **Mako eFoil, 15,000.** An electric hydrofoil — a mast under the hull with a front wing
  and a rear stabiliser on a fuselage — and the halo board of the shop. 21 mph faster than
  anything else out there, and it leaves almost no wake, which is what a foil actually
  does. It is deliberately outside the balance curve on speed and pays for it with the
  thinnest hull in the game: it cracks in two hits, not three.
  Stats also learned two things while it was being added. A spec can now state a stat
  outright, because an electric board's speed is in its motor and not in its outline, and
  a stated stat is pinned so the balancing cannot quietly walk it back. And price now buys
  SPECIALISATION rather than a flat lift — spare points go to whatever a board is already
  best at — because spreading them evenly turned the dearest board in the game into four
  middling bars that told you nothing about riding it.
- **v1.74.0** — The in-play controls are glass and nothing else: a white ring, a white
  word, a white glyph, and the sea showing through. No fill, no colour, no sheen. They sit
  on top of the one thing you are trying to watch, so the less of it they cover the
  better — and colour was doing no work there anyway, since you learn which button is
  which by position within one run. The menus keep their colour; they do not have a wave
  behind them to keep an eye on. The one exception is the release cue on a trick button,
  which stays warm: it has to be caught out of the corner of an eye mid-rotation, and a
  white ring going slightly whiter would not be. Checked across the day cycle — dusk,
  night, deep red and full daylight — and against foam directly behind a button, which is
  the case white type is most at risk from.
- **v1.73.0** — Four fixes to the crash, from playtest screenshots.
  **He no longer leaves the shot.** The camera followed him for the first beat and then
  cut to a held wide position — and whenever the crash threw him far, which is exactly the
  throw worth watching, that held shot did not contain him. It now frames him and the
  board together for the whole crash, pulling back as they separate, and settles by
  damping rather than by cutting: it chases hard at first and barely moves by the end.
  **He drowns instead of lying down.** He used to settle onto his side and float on the
  surface. Now he comes upright, sinks to where only his head and shoulders are out,
  turns on the spot, bobs under and back up, kicks, keeps his chin up, and throws spray
  off both hands.
  **The board halves lie down.** They kept the rotation the break animation gave them and
  stayed there, so a half could stand on its end in open water — the one thing the sea
  would never allow. They stop turning when they hit and settle flat.
  **A crack could hover above the board.** A strand walking outward from the impact took
  whatever surface the ray hit next, so a segment that passed over a deck pad or a fin sat
  at a different height with the ribbon spanning up to it. Each strand is now locked to
  the one surface it started on and lifted along that face's own normal, so it stays cut
  into the hull however the hull curves.
- **v1.72.0** — Board stats, and three fixes to the damage system.

  **Stats are derived from the hull, not typed in.** Every board already carries its real
  shape — length, width, thickness, rocker — and that IS the physics, so a 9'6" log comes
  out fast, tough and unwilling to turn and a skimboard comes out loose and poppy,
  automatically, with no table to keep in sync with the art. Four of them, each wired to
  something the game already does: SPEED (top speed), TURN (carve response), AIR (pop off
  ramps and how fast you rotate) and TOUGH (cracks survived, 2 to 4, plus the spin-out
  threshold). Every board is then held to a budget set by its price — 18 points at the
  cheap end, 22 at the top — because without that the totals land wherever the geometry
  happens to put them and some cheap board is simply better than a dear one, which makes
  the shop a lottery instead of a choice. The shape decides the character; the price buys
  a small edge. Astro's board stays 5/5/5/5: the reference everything is read against.
  Shown in the shop GTA-style — label left, ten segments right, filled to level, one
  colour per stat — so a difference of one is countable rather than judged by eye.

  **Cracks are on both faces now.** They were on the deck only, which is under the rider
  from the one camera angle anybody actually plays from. Both surfaces come off a single
  raycast — hits arrive sorted, so the first is the deck and the last is the hull — and a
  crack goes through a board anyway. Also bolder and longer.

  **The board breaks where it was hit.** The third crack's position sets the split plane
  instead of it always parting down the middle, clamped off the very ends so neither half
  is a sliver.

  **The crash is worth watching again.** The shot used to cut straight to a wide hold on
  empty water while he was still in the air. It now rides in close for the first beat,
  framing him and the board together, and only then pulls back to the held shot.
- **v1.71.0** — The board takes damage. Coming down ON a hazard used to be the same as
  driving into its side, because the collision test only asked whether you were below its
  height — a landing you survived by a hair and one you missed by a hair were both instant
  death, and the "I was on top of it!" case did not exist at all. It does now: land on one
  and the board cracks instead of ending the run. Three cracks and it snaps in half.
  Head-on is unchanged and still kills, so reading the water still matters.
  The damage is worn rather than read off a bar. Each crack is a real fissure laid on the
  deck at the point of impact — raycast onto the hull so it follows the rocker, which also
  means it works on an imported model as well as on the lofted boards. Each crack makes
  the board wobble under you: a slow readable oscillation you counter-steer, never random
  jitter, so a damaged board is harder without being unfair. Each crack narrows the
  window for a CLEAN or PERFECT trick by a fifth. And every hit dumps your speed boost, so
  a mistake costs something immediately as well as slowly. Nothing carries between runs —
  nobody should have to pay to repair a board they already bought.
  The break splits the board down the middle and both halves ride the tumble the wipeout
  already runs. Anything that does not straddle the middle — the fins, the decal — moves
  across whole rather than being rebuilt, and what is rebuilt keeps every attribute it
  had, not just position and colour.
- **v1.70.0** — Allen is one body, hips and legs and all. A limb sweep starts at a control
  point pushed up inside the torso; how far up is now per character, and paired with a
  thick-topped leg profile it makes the thighs merge with the body and with each other
  inside the trunk. Allen's hips were narrowed so the belly no longer overhangs them, and
  his thighs flare to meet it: the trunk carries on down into the legs as one mass with a
  notch cut in it, rather than an oval with two sticks under it. The shoulders got the
  same treatment. Everyone else keeps the old blend, so nothing else moved.
- **v1.69.0** — Lily is Allen the Frog, and he is built like Kermit rather than like a
  round animal with a frog's face. Lanky is the whole read: a small oval body carried
  high on legs two and three-quarter times everyone else's, arms nearly twice as long and
  just as thin, and limb profiles that are deliberately near-uniform because a frog's arm
  has no bicep and no calf — it is the same thin all the way down. Big wide head sat
  straight on the shoulders with the eyes ON TOP of it, a wide dark-red mouth, flat feet
  with three toes and flat hands with four fingers. No collar.
  Two general things fell out of it. The frog's mouth is a child of the head JOINT, so
  head size never reached it and a widened skull kept a narrow little mouth — it scales
  with the head now. And the pelvis correction that keeps feet on the deck was computed
  from the knee angle, ignoring the hip's Y turn; that error is multiplied by the leg
  length, which is exactly zero error at the pug's proportions and a visible sink at the
  frog's. It is measured off the posed skeleton now, averaged over both legs. Feet across
  the whole cast sit within 0.021 of each other, measured in the rider's own space —
  which is also the fix to the measurement, since the old probe compared world heights
  while the board was rolled and reported differences that were not there.
- **v1.68.0** — The limbs are one mesh now, and the body is built per character.

  A limb made of a cylinder for the upper, a cylinder for the lower and a ball at the
  joint can never look like Human Fall Flat or Gang Beasts, because in those there is
  nothing AT the elbow to look wrong — the arm is a single continuous surface. Tuning
  ball sizes only moves the seam around, which is what the last two passes were doing.
  So the segments are gone. The joints still drive everything, but the visible limb is a
  tube swept along a smooth curve through hip, knee and ankle with a radius profile that
  thickens at the thigh, nips in at the knee and swells again at the calf. It rebuilds
  every frame — measured at 0.44 ms for all four limbs, in software rendering — so it
  bends smoothly at any angle and has no joint parts at all.

  The torso is rebuilt per character from a width profile rather than being one pug
  scaled three ways. Scaling only makes the same shape bigger; a profile says where the
  mass sits, and that is what separates animals: a pig is pear-shaped with almost no
  shoulder, an otter is one even tube, a frog is wide across the shoulders and narrow at
  the hip, a penguin is a teardrop standing on its heels. Every character also got a neck
  length and a head size, because a head sunk into the shoulders is not a head.

  Then per-animal detail: a panda's arms and legs are black and its body is white, a
  corgi wears white socks, a raccoon has dark hands, a sloth has hooked claws, a frog and
  an otter and a lizard have splayed webbed feet, a penguin's arms are flat paddles.
  Feet stay planted: front feet sit within 0.016 of each other across the cast.
- **v1.67.0** — The lump on the backside, which was three separate things. The seat laid
  between the hips was laid ONCE, at build time, along the line the hips sit on while
  riding — one foot in front of the other, down the board. Stood up for a portrait the
  hips move side by side, and the seat stayed where it was: a sausage bridging a stance
  the legs had left, hanging off the rump in every shop picture. It is now laid from
  wherever the hips actually are, and its end padding is cut to just enough to close over
  the haunch. The pug's curly tail had a teardrop hanging below and behind the joint
  instead of a root filling the rump, and its spiral was exactly side-on, so from behind
  the whole tail was edge-on and read as a stick — it is turned three-quarters now and
  reads as a curl from any angle. And every chain tail — cat, raccoon — had no base tilt,
  so it grew straight UP through the middle of the body and only its tip escaped out the
  top. They leave the rump backwards now, the way a tail does.
- **v1.66.0** — One body, not a pile of parts, the way Gang Beasts and Party Animals do
  it. The tube of every limb segment was built with its taper upside down against its own
  end caps: `CylinderGeometry` takes (radiusTop, radiusBottom) and the tube hangs down
  from the joint, so the wide end was landing at the ankle and the rim stood a full
  centimetre proud of the cap sphere — a hard bright ring at every knee, elbow and ankle.
  That one transposition is most of what was reading as blocky and bolted together.
  Then the trunk carries on: a rounded seat laid along the line between the hip sockets,
  overlapping the belly above and both haunches below, so the silhouette closes into a
  single shape with a crotch notch instead of a belly with two pipes hanging under it.
  Arms are now as heavy as the legs — in the references an arm and a leg are the same
  girth — and hang closer to the body with a softer elbow. Feet stay planted: measured
  across the cast, front feet now sit within 0.010 of each other relative to the board.
- **v1.65.0** — Stood up straight, welded together, and each animal built more like the
  animal. The shop portraits were photographing whatever pose the rig happened to be left
  in — and the rig is always mid-ride, hunched over the nose with its feet crossed along
  the board — which is why every character looked like it was falling forward. Portraits
  now take a neutral standing pose and put the riding one back afterwards, and they frame
  on height rather than on the bounding sphere, so a sloth's arms and a T. rex's tail stop
  shrinking the animal and the whole cast lines up at one size. Arms had daylight behind
  them because the shoulder sat on the silhouette edge: the sockets moved inside the chest
  and every limb grew a deltoid and a haunch that sink into the body, sized against the
  arm's thickness rather than its length so a T. rex's stubs still have shoulders. Leg
  LENGTH is now separate from leg thickness — a corgi is the same dog on a third less leg
  — and the pelvis drop that keeps the feet on the deck is measured from the actual hip
  and knee angles each frame rather than guessed, because how much height a short leg
  costs depends on how bent it is. Measured across the cast, feet sit within 1.4 cm of
  each other relative to the board. Then the animals themselves: a corgi is a long low
  barrel on stumps, an otter is narrow and long with a pale throat and muzzle instead of
  a bear's cream belly, a raccoon has a proper bandit mask with a pale brow over it, a
  penguin has flat flippers and orange feet, and a cat is the slimmest thing out there.
- **v1.64.0** — Soft-bodied, the way Party Animals and Fall Guys are. A limb that halves
  in width from hip to ankle reads as a carrot, so the taper is nearly gone: thigh 0.150 to
  0.140, shin 0.140 to 0.130, and both are a third shorter than they were. Hands are one
  mitten and feet are one stub — the moment a foot has separate toes it looks mechanical.
  The hips drop with the legs, or he floats above his own board.
  The per-character numbers were retuned against the new shapes rather than carried over:
  the old ones were set against thin limbs and gave either wires or clown feet. Nothing is
  thinner than 0.66 now, and that is an ant.
- **v1.63.0** — Arms and legs rebuilt, and every animal built to its own proportions.
  Each segment is a tapered tube capped by spheres of exactly the radius the tube has
  where they sit, so two segments meet without a seam — the old caps were a shade smaller
  than the tubes, which left the tube's cut edge showing as a ring at every joint and is
  what made them read as pipes threaded with beads. Feet have a heel and a toe now instead
  of a ball on a stick.
  The skeleton is still one skeleton and still drives every movement; what changes per
  character is how thick a limb is, how big a foot is and how long an arm is. A cat is slim
  with long limbs, a corgi is low and long, a bunny and a frog stand on enormous feet, a
  sloth's arms reach a third further than anyone's, a penguin's barely reach at all, and a
  T. rex's are less than half length — which is the entire joke of a T. rex. Shoulders now
  follow the body's width automatically, because a narrow animal wearing the pug's shoulder
  sockets has its arms hanging in the air beside it.
- **v1.62.0** — Twelve boards with real graphics on them, in four sets of three. Designer:
  a gold monogram lattice, a houndstooth check, a gold-stitched quilt. Street: a weathered
  boxcar stencil, a two-colour throw-up with drips, and a five-can wildstyle. Livery: a
  sponsor-plastered pit board, a hazard-yellow 44 with a checkered flash, and a matte works
  team in fluoro. Abstract: poured marble, a datamoshed glitch, and an aurora with a sky of
  stars behind it. All of it comes out of the same per-vertex paint callback the plain
  boards use — a monogram and a spray blob are both just functions of where you are on the
  deck — with value noise, fbm and a patch helper added for the job.
  Two things had to be got right. Patterns need square cells, and u spans the whole length
  of the board while v spans only its width, so equal frequencies gave cells two and a half
  times longer than they were wide; the check and the quilt both had to be re-proportioned.
  And a houndstooth's teeth have to push out of each dark cell into its pale neighbour —
  flipping whole corners instead joins the cells along the diagonal and the check collapses
  into stripes.
- **v1.61.0** — A half turn is a landing. Yaw only has to finish on a multiple of 180 now,
  not 360 — the board is just as square under him either way, he is simply facing the other
  way afterwards — and the window is wider than the trick's own timing window, which is the
  leniency. Land one and he rides switch: turned round to face you, waving, craning back
  over his shoulder to see where he is going, and he stays there until another 180 puts him
  back. Flip and roll still have to come all the way round; you cannot land upside down.
  The shell gets real relief too. Each rib is shaded across as well as along it, so it reads
  as a tube rather than a stripe, with dome shading turning the far side away and a cast
  shadow under the hinge.
- **v1.60.0** — The puka is a proper piece of nacre. Eleven ribs, each its own facet with
  its own gradient — near black where it meets the hinge, full colour at three quarters,
  blown to white at the rim — which is what makes a shell look lit from inside rather than
  printed flat. The hue runs teal to blue to a warm gold core and back to teal rather than
  all the way round the spectrum: a full rainbow reads as a paint fan, not a shell.
- **v1.59.0** — Every button gets a sheet of white inside it, underneath its colour. That
  is what a coloured glass actually is: the tint only looks vivid because there is light
  behind it. With the white doing that job the colour could come back down and still read,
  so the buttons ended up more glassy rather than less — and because the ground is now the
  button's own and not the sea, an orange button is the same orange over dark water as it
  is over a bright sky. The white runs brighter toward the foot, the way light passes
  through the base of a tumbler.
- **v1.58.0** — A brighter palette. Play is a hot tangerine rather than a burnt one, the
  shop went from navy blue to teal, Stats is yellow with dark type on it because white on
  yellow cannot be read, and Watch a clip moved to spring green so it is not a second teal.
  The round buttons follow: ROLL teal, FLIP orange, SPIN yellow, HAND violet, JUMP gold.
- **v1.57.0** — The ant's antennae no longer cross. A positive turn about Z tips the local
  up-axis toward negative X, so each one was leaning across to the far side of the head;
  they splay outward now.
- **v1.56.0** — Tricks are timed. Hold ROLL, FLIP or SPIN and he keeps turning for as long
  as the button is down; where he is pointing when you let go is the attitude he lands in.
  The button lights while that axis is squared up, which is what teaches the window: 115 ms
  either side of a whole turn to land CLEAN and take 1.4x, 41 ms for PERFECT and double.
  Come down outside it and he goes in crooked, as he always did. The per-trick payout decay
  goes with it — with a window to hit there is nothing left to spam.
- **v1.55.0** — Stats replaces Records on the menu: hero tiles for your bests, how far you
  have surfed measured in swimming pools, your favourite trick, the thing that ends most of
  your runs. Records — every count, every death, the whole difficulty curve — is a workshop
  tool, so it now lives only behind the five taps on the version number. The HUD belongs to
  the run and is hidden on the menu.
  Anthony's three cheeses are closed. Swing the board back and forth hard enough and the
  fins let go rather than paying out another cutback — the reward and the wipeout are the
  same move, a little further apart. The same trick over and over pays 1, 0.72, 0.52, 0.37,
  0.27 and then a floor of a quarter, recovering as you do other things. And a handstand
  stops paying after three and a half seconds, gets easier to fall out of the longer it is
  held, and past nine seconds his arms simply give out.
- **v1.54.0** — Bright colour back in the buttons. A translucent colour takes on whatever
  sits behind it, and behind these is a deep navy sea, so orange kept landing on brown.
  Brightening the backdrop does not fix it — multiplying a navy still leaves it navy — so
  the colour carries itself instead, sitting high enough to read as the colour it is while
  the sheen and the blur do the glass. Every round button in play got the same lift, so
  ROLL is blue, FLIP orange, SPIN gold, HAND purple and JUMP amber again.
- **v1.53.0** — HOLD comes off the trick pad, which is four buttons in a square now.
  Holding a finger on the water still grabs in the air and drags a paw on the wave — the
  gesture is unchanged, only the button is gone. The ant is reproportioned: a gaster is
  the biggest thing on an ant and a head is not, and having that the wrong way round is
  most of what made him read as a balloon. Smaller head, alert eyes rather than googly
  ones, and the abdomen given its proper mass.
- **v1.52.0** — The ant is De Soto now, and he is one animal rather than a set of parts in
  formation. The legs were the giveaway: the sockets stayed where a pug's are while the
  body shrank to half its width, so they hung in mid-air beside him — they come in with
  the body now. The thorax and the gaster are bridged by a real tapered waist instead of
  two floating nodes, a neck fills the space under the head, the gaster is ridged the way
  an abdomen is, and he has a big pair of eyes and a smile in place of the mandibles.
- **v1.51.0** — Glass with the colour still in it. The fill is let a long way down so the
  sea genuinely comes through, the blur behind is pushed harder so what comes through stays
  readable, and a sheen is laid across the top half — that highlight is what makes a
  surface look like glass rather than like a flat panel at low opacity. The round in-game
  buttons get the same treatment, so the whole game reads as one material.
- **v1.50.0** — The menu is three buttons: Play, Beach shop and Records. Surfers were a
  door of their own onto a room they already had a tab in, so they live inside the shop
  where the boards do. The title keeps the place it has always sat and the buttons drop to
  the bottom of the screen — out of the flow, so neither pushes the other around. The
  wipeout card keeps them together under the summary.
- **v1.49.0** — The buttons get their colours back. Glassing them meant a wash of white
  with a hint of tint in it, which in a game this bright read as a different, greyer game.
  Each one now keeps the shade it always had and is simply let down in opacity, with the
  blur behind doing the work — so the water moves through an orange button that is still
  orange.
- **v1.48.0** — The menu is open water and nothing else; the surfer is put on it when you
  press Play. Riders are surfers everywhere they are named. And there is an ant: Pili,
  glossy red, a narrow thorax with a heavy gaster slung behind it on a two-node waist,
  stick legs and jointed antennae that flick with the ride. Every character before her
  shared one body and that body is a fat pug, so the shared limbs and torso are now
  rescaled per character rather than fixed at the pug's proportions.
  The tail also throws a constant wash now, carving or not. A board going straight still
  shoves a wall of water out behind it — that is most of what you see in a real wake shot —
  and without it the sea read as glass under a board floating on top of it.
- **v1.47.0** — A main menu with departments — Play, Surfers, Beach shop, Records — and a
  way back to it from the wipeout card, which now also drops the purse line and the arrows.
  What each figure paid out is set beside it in gold rather than pointed at.
  Buttons are properly glass: the fill is a wash of white with the colour carried in a low
  tint, an edge and a soft glow, so the sea genuinely reads through them.
  The contact disc under the board is gone — open water has nothing for a shadow to fall on
  and it only ever read as a sticker tracking him about.
  The jet ski's swell now stands up a little from the instant he goes past instead of
  appearing out of flat water some way behind him; the build to the same wave is unchanged.
  And the whirlpool is nearly twice as wide. At its old size it sat inside the water you
  were already using and could be steered round without ever committing to it, which made
  it scenery: it now spans the whole lane, and the ramp and the jelly on its rim are the
  way out.
- **v1.46.0** — Both launches rebuilt, and the rider now looks like he means it.
  A kicker's exit speed comes from the ramp's own curve rather than from how far he
  happened to move between two frames — that measurement was frame-rate noise as much as
  anything, which is why the same ramp could throw a big jump or barely lift him — and it
  has a floor under it. Measured 1.75–2.0 s of air at every speed, which is two and a bit
  full rotations every time.
  The jet ski wake stopped dropping him. He used to come off the rising face before the
  crest, go ballistic under full gravity, and be falling at 13 units a second by the time
  the launch fired, so it read as a drop and then a bounce. He stays welded to the face all
  the way up now and the launch sets his speed rather than adding to it: one step from 3.7
  to 9.6 with no dip anywhere. Measured 4.75 s of air — near six full rotations.
  Through any rotation he pulls into a tuck: knees to the chest, spine curled, chin down,
  paws wrapped in — then opens out to spot the landing as the rotation runs out.
  FLIP and SPIN swap their symbols. A flip goes end over end so it is drawn as a loop with
  the sideways swipe marked either side of it, and a spin turns about the up axis so its
  loop is flattened into a horizontal ring. The old pair read exactly backwards.
- **v1.45.0** — The square is gone, and it was never a square. The menu never called the
  per-frame update, so its camera had never been placed and sat at the world origin — which
  is sea level. You were looking straight through the wave field edge-on: a slab of water
  across the middle of the screen with sky above it and more water below. It now gets the
  ride's own framing with a slow drift, and the sky work runs on the menu too, so the dome
  and the sun follow the camera instead of being left where they were built. Measured: a
  full-width edge one fifth of the way down the screen, a luminance step of 123 out of 255,
  and after the fix no full-width edge anywhere.
  Buttons are glass now — the colour is still theirs, but the water moves behind them, and
  the shop's buttons and tabs are the same material as the menu's. The surfer emoji is off
  the title. And the puka is a ribbed mother-of-pearl scallop rather than a holed disc,
  which at small sizes read as a CD.
- **v1.44.0** — A pass over the whole interface. The menu no longer lays a flat panel
  over the game — the sea keeps rolling behind it under a vignette with no edge anywhere —
  and the run summary is one block in the middle of the screen whose lines all start on the
  same left edge instead of each being centred on its own length. Every menu button is now
  the same width, height and shape, and only its colour changes; buttons were also falling
  back to the system font because they do not inherit one, so the shop's read differently
  from everything else. The puka is a proper shell: a pearl-to-aqua iridescent disc with a
  real hole and a specular highlight, rather than a flat beige coin. And the jet ski's
  glimmer is a slow warm bloom sitting behind the hull rather than drawn rays, which read
  as a sticker pasted on the water.
- **v1.43.0** — The trick buttons are back, and each one wears the finger movement that
  does the same thing: a vertical arrow on ROLL, a horizontal one on FLIP, a loop on SPIN,
  a dot in a dashed ring on HOLD, two dots on HAND. A button is not a second way of doing
  a trick — it makes exactly the same call the gesture does — so the button teaches the
  gesture rather than replacing it. The pad also stays put for the whole run instead of
  blinking out between leaving the water and reaching trick height.
  Tapping the version number five times on the menu opens a telemetry panel: how often the
  game is opened, day streaks, runs, best and average distance, time on the water, what
  ends each run, how far people get in 100 m steps, which tricks are thrown and whether by
  gesture or button, puka earned and spent, and what has been bought. It is all kept in
  this browser's own storage and never sent anywhere. The listener sits on the menu rather
  than on the version itself, so during a run a tap down there is still a tap on the water.
- **v1.42.0** — Renamed to Surf: the browser tab, the title on the menu and the readme.
  The save key is deliberately left as it was, so nobody's coins, boards or riders are
  lost to the rename.
- **v1.41.1** — Every lofted board finishes properly. The thickness envelope already ran
  to nothing at both tips, but the plan outline still finished at its full nose width, so
  the last ring of every board was a flat, zero-thickness flange the width of the nose —
  a lens of board hanging off each end. Both ends now round off over a span equal to their
  own half-width, which is a semicircle in plan: a wide nose finishes in a broad curve, a
  narrow one comes to a point, and either way the outline closes on the thickness instead
  of past it. Lengths and widths are unchanged, so nothing rides differently.
- **v1.41.0** — Seven more riders and ten more boards. The cast gains a frog with domed
  eyes standing off its skull, a sloth, a crested lizard, a T. rex with a hinged jaw and a
  spined tail that trails behind it as a counterweight, plus an otter, a masked raccoon and
  a penguin in a white bib — eyes, faces, ears and tails are all swappable kit on the one
  shared rig, so every one of them surfs exactly as well as Astro does. The rack gains two
  more wakeboards, two more skimboards, a bodyboard, a Hawaiian paipo, a kiteboard, a grom
  shortboard, a ten-foot noserider and a genuinely asymmetric board whose two halves are
  different widths with the tail cut forward on one side. Twin-tips now carry two bindings
  rather than one long strip, and the shop no longer stops to photograph twenty-five boards
  before it opens: the tiles go up at once and the pictures fill in a frame at a time.
- **v1.40.0** — The shop is a grid of tiles rather than a list with a stamp beside each
  line: previews are twice the size and rendered at half again the resolution, riders are
  turned three-quarters on so you see a face, and every tile carries its own button —
  amber to buy, teal to equip, a gold plate on whatever you are riding, and anything you
  cannot afford yet says how much is still to go.
- **v1.39.0** — Gestures remapped and a third axis added. Swipe up or down for a barrel
  roll, swipe left or right in the air for a real front or back flip — rotation about the
  board's length was all there ever was, so an end-over-end flip needed an axis of its own
  — and draw a circle with one finger to spin, which no longer needs a second finger.
  Straight swipes are settled when the finger comes off rather than as it passes: judged
  on the way past, the first quarter of a circle is indistinguishable from a flick and was
  throwing a roll before the loop had declared itself.
- **v1.38.0** — Astro's own board gets its livery: black rails and hull, a turquoise deck,
  turquoise fins and 17 laid across the tail in black with a white outline. The model
  arrives with its own colours and those are normally left alone, so this one board is
  deliberately repainted — and painted by which way each surface faces rather than by how
  far across it is, because a board narrows toward both ends and a fraction of the overall
  width only lands on the rail at the widest point. The number sits behind where the rider
  stands, which is the only place the chase camera can actually see it.
- **v1.37.0** — Tricks are gestures on the water instead of buttons. Tap to jump, tap
  again in the air to flip and keep tapping for more, swipe up or down for a front or back
  flip, press and hold to grab, tap with a second finger to spin and hold it for a
  handstand. Tapped rotations queue whole turns rather than spinning for as long as a
  button is down, which is what makes tapping land square. The trick bar and the jump
  button are gone. One thing that had to be handled: a phone fires a synthetic mouse click
  after every touch, so a single tap was jumping and then immediately flipping — once a
  real touch is seen the mouse path stands down.
- **v1.36.0** — Steering is side to side and nothing else: shifting weight fore and aft is
  gone from the swipe, the mouse drag and the keyboard, and everything that consulted it —
  trim, the board's pitch, how deep the rail digs, where the rider stands — now reads as
  neutral. Pearling went with it, since without a way to put your weight over the nose
  there is nothing left to bury it with. Jellyfish never end a run any more: hit one from
  above and it throws you as it always did, clip one on the water and it shoves you off
  instead of stinging.
- **v1.35.0** — The sea appeared to run backwards at speed. Every foam layer was drifting
  with the full water motion, and the finest one is nearly four cycles a unit — past about
  40 mph it was stepping more than a whole cycle between frames, which is exactly what
  makes a wagon wheel spin the wrong way. The coarse foam keeps the real drift so the
  water still travels; the detail is anchored in slower frames, which also settles it down
  at the start. The measured worst case falls from 203 cycles a second to 24, under the
  limit a 60 Hz screen can show. Carving buries the rail half as deep, so a turn wets it
  rather than swamping the deck. And puka shells have an icon — the cone-shell slice with
  the hole through it that the currency is named after.
- **v1.34.0** — Six riders, not just the pug: a grey cat with whiskers and a long tail, a
  pink pig with a snout and a curl, a corgi with enormous ears, a lop-eared bunny, and a
  panda. They share one skeleton — everything that moves belongs to the rig, and a
  character is only its colours and the pieces hanging off the head and tail joint — so
  every animal rides exactly as well as the pug and none of the animation is written
  twice. The shop is now two tabs, boards and riders, and every item is shown: each one is
  photographed once through the renderer that is already running, alone in a small scene
  and read back off an off-screen target, so the thumbnails are lit the way the game is.
- **v1.33.0** — Faster off the line and busier early: 10 mph is now the floor and it can
  never drop below it, 70 the ceiling, obstacles start 24 units apart rather than 30, and
  the whole cast is in play by 780 m instead of 1,500 — octopuses from 780, sharks from
  560, jellyfish from 340. The ramp is a curved kicker now rather than a wedge, and the
  jump comes out of its shape: the board runs up the surface you can see and the vertical
  speed it gains is simply how fast that surface is rising underneath it, so a fast
  approach launches harder, the lip angle sets the arc, and there is no scripted jump
  anywhere in it. Traced from a real run: nothing on the water, then 2.4, 3.8, 5.4, 6.9,
  8.5 up the face, and away off the lip carrying it.
- **v1.32.0** — Ten more boards, fifteen in the rack. Not only surfboards: a symmetrical
  twin-tip wakeboard, a finless skimboard, a finless koa alaia, a soft-top foamie, a tow
  board, plus a performance shortboard, a mid-length egg, a mini-mal, an electric twin and
  a step-up pintail. All lofted from the same spec, all around fifteen thousand triangles,
  and the rack now lists them cheapest first. Controls rebuilt: frosted glass with a
  gradient, an inner highlight and a press that actually presses, JUMP given its own amber
  so the main action reads at a glance. The instruction walls are gone from the menu and
  the wipeout screen.
- **v1.31.0** — Paced properly. It used to throw all six obstacle types at you 17 units
  apart from the first metre at 9.7 mph, which is a lot to meet at once. Now you start at
  5 mph on open water with nothing but buoys and logs 30 units apart, and the cast arrives
  a piece at a time — ramps at 220 m, jellyfish at 520, sharks at 950, the octopuses that
  throw things at 1,500 — while the gap closes to 16 units over about three kilometres.
  Spacing is held in metres rather than on a clock: timing the spawns meant that as the
  interval tightened while the speed was still low, things actually bunched up tighter
  mid-run than at the end, so difficulty went up, down, then up again. Speed starts at
  5 mph and climbs the whole way out to a 60 mph ceiling instead of being most of the way
  there by the first kilometre. The jet ski waits until 620 m and the whirlpool until 900.
- **v1.30.0** — Dawn, sunrise, sunset and dusk are four different things now rather than
  one long orange smear. Dawn is soft and pale with the sun still under the water and no
  gold in it; sunrise is the hot low disc that follows; sunset goes deep orange into red;
  dusk is navy to black. Stars come out whenever the sun is at or below the horizon, so
  they are there for the dark half of dawn as well as after sunset, and the sun and moon
  are clipped at sea level so they rise out of the horizon and sink back into it instead
  of floating past it — there is no ocean that far out to hide them otherwise. The
  whirlpool is rebuilt from the reference: a wide dish that turns into a steep throat in
  the last few metres, and its foam is fine drawn-out filaments with a collar of broken
  water round the hole, instead of five painted white arms. The jet ski's wake now swells
  from nothing over its whole run in — it was measured from a fixed line, so it stepped
  straight up to a third of full height the moment the ski went past. And the ski's trim
  is polished chrome with a hot line over it that catches in passes, rather than a flat
  lime stripe.
- **v1.29.1** — Whirlpools come round in about a third of the time. The first was on a
  55-95 second timer, which on the speed curve put it 840 to 1,590 m out — far enough
  that an ordinary run never met one. It is 26 to 42 seconds now, roughly 470 to 800 m,
  and the repeat drops from 75-130 seconds to 45-75. The 240 m floor stays, so one can
  never open in the first few seconds.
- **v1.29.0** — Two more things behind the wake launch, both found by measuring it.
  The wave was dying before it arrived: its lifetime dated from when the ski crossed
  forty units out, and over the new hundred-unit approach it faded on the way in and
  reached the rider a third of its proper height. And the face was throwing him off its
  front before the crest could — that path never flags him airborne, so the launch still
  fired, and it fired while he was dropping at thirteen units a second, replacing that
  with a small upward one. A seventeen-unit reversal in a single frame, which is the
  snap. While a wake is on him the hull now stays stuck to the face, and the takeoff
  adds to his speed instead of overwriting it, so there is nothing left to snap through.
- **v1.28.0** — The wake launch was throwing. It read the ride height from a variable
  that only exists inside the frame update, so the moment every other condition lined up
  it hit a ReferenceError and never fired at all — since v1.26. Fixed, and rebuilt: he
  now leaves at the top of the wave and nowhere else, keeping the speed he already has
  with only a nudge on top, and falls at a quarter gravity so the way down off a wall
  that size is long. The jet ski itself starts a hundred units out with a flash off its
  screen sized to hold its size on screen at any range, and the wake rolls in slowly from
  there, building the whole way. Shark fins can no longer step over you between frames —
  contact is swept sideways as well as forward — and the blade's full height counts. The
  day is a real one: first light with the sun only just clear of the water, up and over,
  down the other side and under, with the light level following it (no colour is tinted,
  there is simply less of it at either end). Buttons can no longer overlap on a narrow
  phone. Coins are puka shells now, with the run's distance and score shown converting
  into them, and the emoji are gone. The jet ski's rider is a pug. And going under once
  no longer ends it — there is one clip to watch to get back up, keeping everything you
  had, with the water ahead cleared so you are not handed straight back to it.
- **v1.27.0** — A whirlpool. It opens ahead of you, dished into the ocean's own height
  field so the water genuinely falls into it, with foam laid on the surface every frame
  and a swirl that turns. Everything afloat inside it is carried round and drawn in,
  turning to face the way it is being taken instead of sliding round sideways, and goes
  over the edge if it reaches the middle; you get pulled off your line too, and the
  centre ends the run. A ramp and a jellyfish are dropped on the near rim as it forms
  and held there rather than swallowed, so there is always a way out if you take it
  early enough. The octopus is calmer: arm motion is roughly a third of the speed, the
  sweep stops short of its own mantle instead of folding arms over its head, some arms
  curl under and some away, and the banded suckers are gone — the underside just runs a
  shade darker. Starfish come about a third less often, and the seaplane now takes 40 to
  78 seconds rather than 18 to 28.
- **v1.26.0** — The board banked the wrong way. Carving right tipped its right rail up,
  leaning out of the turn rather than into it the way a board — or a bike — actually
  does; the direction you travelled was right all along, only the roll was inverted.
  The rider's counter-lean flips with it, so he stays stacked over the rail he is on.
  The wake launch no longer snaps: it fired once the crest had already passed, by which
  point the wave was falling away and the rider was dropping, so setting an upward speed
  reversed him mid-fall and read as two separate events. It now goes off the face while
  the water is still lifting him, and the kick is added to the speed he already carries
  rather than replacing it.
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
