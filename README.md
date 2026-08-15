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

- **v2.86.0** — **The aeroplane goes down at 5 km.** The set wave — the plane crashing and
  the barrel that comes out of the impact — armed past 600 m, which put the biggest thing in
  the game inside the first minute of nearly every run. `SET_WAVE_AT` is now 5,000 m and it
  is one named constant rather than a literal buried in the update loop.
  The distance gate only ever *started* a 30–45 s countdown, so on its own it would have put
  the wave a kilometre or two past the mark rather than at it; crossing 5 km now arms a short
  fuse so it arrives where the number says. Later waves in the same run keep their old
  45–75 s spacing. The test hook that jumps a run past the gate took the constant too — it
  was a literal `dist=700`, which cleared a 600 m mark and would have silently stopped
  clearing a 5,000 m one.

- **v2.85.0** — **Hitboxes that match the obstacle, and a field of octopuses.**
  - **"I didn't hit it and I still died" was real, and it was the log.** Collision was a
    single circle of radius `r` in the ground plane. Measured against what the meshes
    actually are, a log is 5.1 m long and **1.0 m thin** — but carried a 2.35 m circle, so
    it killed you 2.4 m short of itself, while it was still visibly ahead. The shark fin was
    the same: a 0.32 m blade inside a 1.70 m circle. Collision is now an **ellipse** with
    separate half-extents across your line (`hx`) and along it (`hz`), and every obstacle's
    numbers were set from a measurement of its own mesh rather than by eye:

    | obstacle | old circle | new hx / hz | real halfX / halfZ |
    |---|---|---|---|
    | buoy | 0.85 | 0.85 / 0.85 | 0.79 / 0.80 |
    | log | 2.35 | 2.50 / **0.62** | 2.55 / 0.52 |
    | ramp | 2.40 | 1.70 / 2.55 | 1.62 / 2.56 |
    | jelly | 1.05 | 1.05 / 1.05 | 1.02 / 1.03 |
    | bigfin | 1.70 | 1.70 / **0.50** | 1.66 / 0.32 |
    | octopus | 1.15 | 1.25 / 1.25 | 2.34 / 3.42 |

    The octopus stays deliberately smaller than it looks — its arms wave a long way out and
    are not a wall. Obstacle shadows now take the same footprint, so a log lies down as a
    long thin smudge instead of a circle the size of its length.
  - **More octopuses, spread out.** They still arrive at 780 m, but their share of the field
    grows from 24 up to 38 with distance, and past 1,050 m a pick often brings a second one
    along in a different lane and further back — so they read as a field you thread between
    rather than one thrower at a time.
  - `__surf.obsBounds()` (debug builds only) reports every obstacle's measured bounding box
    against its declared collision, because "did I actually touch that" is a question about
    numbers.

- **v2.84.0** — **Three fairness fixes.**
  - **Chests no longer need a jump.** The catch used to demand you were clear of your own
    waterline, so every chest was a jump you had to spot and set up for — and riding straight
    through one and having it ignore you reads as a broken pickup, not a skill gate. The
    chest also floats lower out on the flat (1.9 → 1.25) and the catch box is wider in all
    three axes. Jumping for it still works; it is just no longer the only way.
  - **Session goals actually track now.** Two separate bugs. `goalEv()` opens with
    `if(!goals)return`, and goals were only built lazily when the run card first drew them —
    so on a fresh save every barrel, chest, close call, jelly and ramp of the *entire first
    run* was counted against nothing. And completing a goal replaced it with a new one on the
    spot, so the card that followed showed a brand new goal sitting at zero: finishing one
    looked identical to never having touched it. Goals are now built at load, and a finished
    goal stays on the card marked DONE, rolling over only when the next run starts.
  - **Nothing unavoidable in a ramp's landing zone.** A ramp hides whatever sits behind it,
    and once you are off the lip you cannot steer — so a buoy in the run-out was a death you
    never had a chance to see. Leaving the lip now slides anything in the flight corridor out
    to the side while it is still tens of metres away. Ramps and jellyfish are left where
    they are, because both are a bounce rather than a wall, and so is the big fin, which
    sweeps across your line in plain sight.

- **v2.83.0** — **One thumb per side.** All five trick buttons stack in a single column down
  the left; JUMP keeps the right on its own. The two-by-two pad put SPIN and HAND under the
  left thumb but left ROLL and FLIP out at arm's reach. Everything is smaller with it: trick
  buttons 64px to 54px, JUMP 94px to 84px — still clear of the 44pt minimum tap target. HELI
  loses its wide-pill shape, which spanned two grid columns that no longer exist and would
  have read as a different kind of control sitting alone under a column of circles.

- **v2.82.0** — **The bodies themselves, rebuilt.** v2.81 repainted twenty animals that were
  all the same shape underneath, which is why they still read as one toy in twenty colours.
  This changes the shape.
  - **The torso can now be a body.** It was one width profile scaled by `wide` and `deep` —
    an ellipse of *constant aspect* the whole way up, threaded on a single vertical axis. It
    could be fat or thin, tall or short, and nothing else. Now: `wProf` and `dProf` vary
    width and depth independently, `zProf` bends the centre line so a chest can sit forward
    of the hips, and `keel` lets the lower front jut past the flanks. Five shared builds
    (`barrel`, `deep`, `lean`, `squat`, `upright`) rather than twenty hand-written sets.
  - **Every animal now has a waist.** Measured across the roster it was 0.93 for all twenty —
    the widest point was simply the middle of the sphere, which is the definition of a
    balloon. It is 0.69–0.82 now.
  - **The rig follows the body it is wearing.** The shoulders were at a hard-coded x=0.44 and
    the head at a fixed height up a fixed neck, so widening a body swallowed its head and
    narrowing one left the arms hanging in daylight. Both are now derived from the torso's
    own half-width, and the head's scale moved onto its JOINT so ears, eyes and whiskers
    shrink with it instead of floating where the old skull used to be.
  - **Heads came down.** Sixteen of twenty had a head wider than 0.72 of the chest, six had
    heads wider than their whole chest. That ratio *is* the "two balls stacked up" look.
    Every mammal is now between 0.63 and 0.77; the alien, the ant and the anglerfish keep
    their big heads, because for those three it is the joke.
  - **Markings stopped sinking into the body.** The torso mesh is a 26-row sphere whose facets
    sag 0.73% inside the true sphere, while markings are cut from a 72-row copy that follows
    it exactly. Lifts of 1.004 therefore put the tabby's stripes *underneath* the cat,
    surfacing only where the coarse mesh happened to dip — which is what made them look
    dashed. The lift is now derived from the mesh instead of guessed.

- **v2.81.0** — **Every character repainted against the real animal, and perks on the shop card.**
  - **Markings are now measured, not guessed.** Every patch, stripe and band used to be
    sized with flat constants. Three things the constants ignored: the torso is a *sphere*
    pushed through a width profile, so its half-width falls to nothing at the hips and the
    shoulders; the profile is then scaled again by `build`; and the head is scaled by
    `headS`. Between them a marking could sit a fifth of the body outside the body. That is
    why the cat wore its stripes as a rib cage hanging in front of its chest, why the otter's
    throat patch floated by its shoulder as a detached cream egg, and why five animals had a
    white blob apparently stuck to one shoulder. `bodyRX/bodyRZ/bodyY` and `headRZ` now
    report where the mesh actually is, and every marking is placed off them.
  - **Patches are cut from the animal's own skin.** `bodyPatch()` lifts the triangles of the
    already-shaped torso between two heights (optionally only the front-facing ones),
    inflates them 0.7%, and hands back a mesh — so the pale front and the panda's saddle fit
    exactly and follow every taper. Stacked lenses got the taper right but seamed into a row
    of scallops down the chest; stacked tori wrapped, but ridged up into a car tyre.
  - **Tabby stripes lie flush.** The tube's centreline sits *on* the surface so half sinks in,
    and the arc is centred on the spine — a bar comes over the back and dies out on the
    flank, it never crosses the pale chest.
  - **Per-animal fixes:** pig ears set wide and rolled out (two cones near the crown had been
    converging into a single horn) with a longer neck and a bigger head so it stops reading
    as one balloon; corgi ears widened into spades instead of rabbit ears, and the body made
    long rather than round; monkey ears pushed clear of a skull they were buried inside;
    raccoon recoloured from blue-grey to grizzled warm brown, with the mask joined across the
    bridge of the nose; sloth given a face that separates from its fur and claws that are
    hooks rather than splinters; ant taken from traffic-cone orange to fire-ant red-brown
    with a near-black gaster.
  - **Character perks are visible.** Shop cards for riders now carry the same black stat bar
    the boards do — the trait the perk lifts, how far it lifts it, and the perk's own line
    underneath. A bonus you cannot see is a bonus nobody believes in.

- **v2.80.0** — **Sparkles removed.** The v2.79 sun-glitter sparkles come back out, cleanly:
  the ocean's own sun specular stays, the twinkle cells go.

- **v2.79.0** — **The feature batch: eight at once.**
  - **Haptics** (Android): tricks, wipeouts, close calls, chest catches, every ceremony tap,
    and the burst.
  - **The title screen is staged**: Astro bobs on his board in the swell and the treasure
    chest drifts across the middle distance — and the menu sea now runs on the same clock
    the game samples, which also removes the water-jump when a run started.
  - **Sun glitter**: pixel sparkles inside the sun's specular lobe, twinkling on their own
    clocks, strongest at low sun.
  - **Close calls**: shave past a buoy, log, jelly, shark or octopus inside a metre and a
    half without touching — +25 and a buzz. Flying clean over something does not count.
  - **Session goals**: three standing goals on the run card (barrels, chests, close calls,
    jellies, ramps, single-run distance/tricks), 100 puka each, auto-replaced from the pool.
  - **Chest in the barrel**: ~30% of tube rides hang the chest low over the pocket — a pipe
    jump grabs it, +150 on top. Its catch gate measures against the chest itself now; the
    old sea-height gate made it uncatchable on one break side, because the pocket slopes.
  - **Rider perks**: every animal but the pug carries one small legible bonus (trick points,
    pop, grip, float, speed, chest luck, an extra life), shown on the shop card, applied on
    top of the board.
  - **Share**: one button on the run card — fresh render, native share sheet with the
    screenshot and the line; clipboard fallback.

- **v2.78.0** — **The ramp's little wings clipped off.** The tabs sticking out of both top
  corners were the lit lip bar: 6 cm wider than the deck it sits on, floating 20 cm proud of
  the rails — which roll *down* at the edges — so its bright teal ends hung past the corners
  in mid-air. It now sits inside the deck's width, settled into the crown.

- **v2.77.0** — **Button polish.** "Watch a clip" becomes **Free life** (say what it gives,
  not what it costs), "Beach shop" becomes **Shop**, and the pills slim down (11px pad, 15px
  on the run card) with a brighter glass sheen. The uneven spacing was real and structural:
  per-button margin-tops of 9px in one rule and 10px in another, plus the play row's own
  margin, all fighting — one flex `gap` owns the spacing now, measured equal on every screen.

- **v2.76.0** — **The chest gets its gloss and its stage manners.** Phong materials instead
  of Lambert — lacquered wood and polished brass are specular highlights, which Lambert
  cannot do — plus plank grooves, gold corner posts, brass rivet lines down every strap and
  band, a lock plate with a hanging shackle, and a dark inlay so the open box has an inside.
  At the end of the run it spins while you tap, and the moment it opens it **turns to face
  the camera** — lock toward you, hinge behind — so the **lid swings away** and you look
  straight into the lit box. The "purse" caption is gone; a puka payout is just the number,
  and gear says only "View the shop to equip."

- **v2.75.0** — **The chest becomes a pirate chest, and the ceremony goes 3D.** Built to the
  reference photos: dark wood, domed lid on a real hinge, gold straps and lock, smaller than
  the old crate, spinning as it drifts, with a soft gold glow. The end-of-run ceremony now
  shows the **actual 3D chest** — placed in front of whatever camera the crash held, spinning
  slowly behind a mostly-clear overlay. Taps shake the real thing; on the twelfth the **lid
  swings open on its hinge** and light comes up out of the box, and only then does the
  preview say what you got. **Boards join the loot** (rare, ~3%), characters stay super-rare
  (~1%), and gear rewards say "View the shop to equip."

- **v2.74.0** — **The treasure chest, and a cleaner front door.**
  - A wooden crate drifts down the lane two metres off the water — riding flat passes under
    it, a jump catches it, which is what makes it a play rather than a pickup. Catching it
    banks it for the end of the run: when you fall, the chest comes up over the wipeout
    card. Tap it, tap it, tap it — it shakes harder with every tap — and on the twelfth it
    bursts and pays out: mostly 25/50/100 puka, sometimes 200–350, and very, very rarely a
    surfer you haven't unlocked (it lands in the Beach shop). Then Surf again, Beach shop,
    or Main menu.
  - The main screen loses the purse, the Stats button and the little wave-test icon. Stats
    still opens on five quick taps of the version number, and the wave test now lives in
    that secret panel with the other dev tools.
  - **A real bug the chest test caught:** the set wave was only cleared by the *revive*
    path, never by a normal reset — so Surf again after wiping out mid-wave started the new
    run with the old wave still standing, phase and all. Cleared with the run now.

- **v2.73.0** — **The foam is rideable, and the ride is a circle.** The puffs sit on the
  lip's own circle; now, wherever they have climbed, the ring's surface blends from the
  ray-marched water to that circle — so the rider surfs around and *up* the foam, and at
  full coverage the orbit is genuinely circular. The pacing-by-wall-distance from v2.56 has
  nothing left to smooth: dr/dθ goes to zero and going round is uniform. The whole history
  of this radius, measured: a **5.07 m cliff** at one angle (v2.55), a **4.9–7.5 m bulge**
  through the open sector (v2.56–v2.72), now **0.09 m of variation round the entire ring**,
  and a held turn rides it within 0.10 m. The perfect circle, closed by the foam that
  completes it visually.

- **v2.72.0** — **More foam in the broken corridor, and the rider stays visible inside it.**
  Density boost where the user pointed: seven strips instead of five, fuller puffs and wider
  scatter as the section breaks, so the stretch between the foot chain and the near corner
  fills in as one mass. That immediately buried the rider on one break side (measured at NDC
  0.66,−0.29: completely hidden), so the puffs now yield along the **camera-to-rider
  sightline** — not a spherical pocket around him, which cleared nothing, because the
  occluders are metres away from him sitting on the line to the lens. Foam everywhere the
  user asked, and a corridor of clarity that travels with the character.

- **v2.71.0** — **The foam becomes whipped cream.** The user supplied three stylised
  references — rounded cauliflower puffs, bright white tops, soft blue-grey crevices, crisp
  round silhouettes — and a mesh can't be that from every angle: three attempts rendered as
  shards, exposed polygons, or intersection creases, each a different failure of the same
  idea. The bank is **camera-facing puff sprites** now, the classic stylised-cloud
  technique: round by construction, fake-sphere shaded per puff, slightly irregular
  hand-drawn rims, each breathing on its own beat. Placement keeps the v2.69 circle logic
  but the arc **stops at the foot corner** — run through the circle's bottom, the puffs sat
  in the trough and the rider vanished into them with only his ears out; in every reference
  the surfer's own water is clear. Puffs also melt as they near the lens, so a close one is
  never a white screen.

- **v2.70.0** — **The foam stops being shards.** The phone showed the bank as jagged white
  triangles, and there were three separate causes, each invisible at the test rig's blurry
  3 fps: **per-vertex random lumps** (adjacent ring vertices with wildly different radii make
  a spiky star — replaced with two smooth harmonics whose phases drift along the tube);
  **a clean geometric rim** (a straight-edged alpha cutoff traces the polygons — the rim
  threshold is now eaten by the drifting mottle noise, so silhouettes dissolve like cloud);
  and **lobes embedded in the curtain** (anything interpenetrating the wall gets clipped
  along the depth test's triangles — a row of hard sails up the boot; the strips now start a
  little way down the gap and are pulled in off the circle, so the cloud sits in front of
  the water instead of inside it). Finer slicing along the tube as well.

- **v2.69.0** — **The foam completes the circle.** "Should create a perfect circle once the
  foam clouds are there" — the best spec of the whole barrel effort. The lobes now sit ON the
  tube's own cross-section circle (the same one the lip's arc is built on), filling the open
  sector from the curtain's tip round the bottom. And the fill is progressive along the tube,
  because the first cut completed the circle at every z and thereby walled off the view — the
  open sector at mid-distance *is* the hole you look through. Down the line the foam hugs the
  corner where flat meets face; it climbs the gap as the section approaches the break; beside
  and behind the rider the O is closed. Strips jittered off the parallel-rope grid.

- **v2.68.0** — **The foam spreads onto the floor you can actually see.** The user circled
  the flat between the rider and the near wall — and both previous spreads missed it, because
  they went *outward* past the curtain, where the mass is hidden behind the very wall it grew
  from. From the riding camera the visible flat is INBOARD of the foot line. A third strip of
  lobes now rolls from the wall's boot across the tube's own floor toward the rider, lying
  low on the water. Still one draw call, still arriving only when the lip lands.

- **v2.67.0** — **The foam bank becomes a body.** v2.66's mass read as a faint veil from the
  riding camera, and the reason was exactly backwards alpha: it keyed opacity UP on facing
  the eye, and from inside the tube the bank is seen edge-on — so precisely the surfaces
  that form its silhouette went to glass. A cloud is opaque through its body and soft only
  at the very rim; the alpha now says so. And the mass is the *shape the user drew*: a
  second strip of lower, wider lobes rolls out over the flat in front of the tall churn at
  the wall, so the bank spreads instead of lying along the foot like a rope.

- **v2.66.0** — **A body of foam, not more paint.** The circled areas in the reference are a
  thing no surface-whitening can be: a volumetric mass of whitewater billowing where the lip
  lands. It is real geometry now — a lumpy bank laid along the curtain's foot, baked lumps
  per vertex, breathing gently in the vertex shader, lit as aerated white with soft cloudy
  edges. It lives in the shell's own local frame and copies the curl's transform each frame,
  so formation, pitch-over and closeout come free, and it swells through the broken section
  behind the break. It **arrives with the landing**: faded in over the last quarter of the
  formation, because until then the curtain's tip is still in the air — the first cut of
  this floated at the mouth like a cloud on nothing, and the second drowned six metres under
  the swell margin before anyone saw it at all. One draw call; the same near-lens cut as the
  wall.

- **v2.65.0** — **The near section breaks.** The circled stretch — the near wall's boot,
  between the mouth and the camera — is past the break by the peel's own geometry, so it
  should read as whitewater *rolling*, not glass meeting a white floor. A second boil layer
  now climbs well up the fall there, keyed on the same interpolated peel constants the ocean
  uses (so "near the break" means the same thing everywhere), and the impact-line spray's
  range extends right up past the camera instead of stopping fourteen metres short of it.

- **v2.64.0** — **The crash corner gets its pile.** Judged against the reference rather than
  the literal red circle: what the photo has at the impact line is not more coverage but a
  concentrated **mass** — brightest exactly where the curtain lands, dissolving soft in both
  directions. A near-white core band now sits on the sea at the landing line (only under a
  thrown lip), the lower curtain carries a touch more weight, and the mass spreads into the
  existing apron. Roughly a fifth of the frame in portrait, and the mouth stays the
  brightest, most readable thing in the shot.

- **v2.63.0** — **Frozen from the first barrel frame, no escaping, and the crash corner is a
  mass.**
  - **The camera no longer moves in the barrel at all — the centred view arrives before it.**
    v2.62 eased the locked aim onto the tube's axis while the barrel formed, and it was
    reported as the camera moving: it was — motion inside the barrel, where all motion is
    banned. The settle now rides on the *chase* camera, which is live and already tilting
    with the wave's rise, and it aims at where the axis *will be* at full form (predicted,
    not current, so the lock never inherits a moving target). The lock captures that
    finished pose: measured drift from the first locked frame, **0.0000**, aim included.
  - **You cannot steer out of the barrel.** The ring only ever cared about the bore's radius,
    not where the rider was — so before it engaged, the lane steering could carry him out
    through the mouth. A daylight-side tether now holds him in the bore from the moment the
    curl is up. Verified by steering hard away for the entire approach: still in the tube,
    1.5 m off the axis.
  - **The crash corner is as thick as the red circle.** Cloud over most of the fall at 0.78,
    churn over most of it at 0.97, the sea's apron wider (19 m reach) and denser, spray a
    third again heavier — a mass of white with glimpses of blue, not blue with white on it.

- **v2.62.0** — **The barrel view looks down the tube, mouth centred.** The user's composite
  said exactly what the shot should be, and it was geometrically impossible with the old aim:
  the ride camera's look-at converges on the sea far ahead, and from inside a tube that ray
  reaches sea level right where the mouth is — so the mouth always sat in the top of the
  frame with the roof cut off, whatever the wave did. The camera's **position** never moves
  (same lock as ever); its **aim** now settles onto the tube's own axis while the barrel is
  forming — anchored so the first locked frame is exactly the chase pose, no snap — and is
  frozen for good the moment the lip has fully thrown. Measured: the axis projects to screen
  centre within 0.002 on both break sides, and once formed the drift is 0.0000 through the
  orbit, the spit and the crash alike.

- **v2.61.0** — **The wave stays.** "The wave shows up and goes, then another one shows up" —
  measured, that was real and structural: the ride window (`swRideMax`, 6–16 s) was tuned when
  the barrel arrived already formed, and v2.54 made it *form* during the ride phase — four
  seconds at 0.26/s. Formation was quietly eating most of the window, so on a short draw the
  tube finished forming and closed out **two seconds later**, re-armed, and sent the next wave
  45–75 s on: a parade of barrels none of which stayed long enough to ride. The spit clock now
  waits for the barrel to exist (`swForm ≥ 0.95`) and the window is 10–18 s of *formed* tube —
  and circling still stops the clock entirely, so going round and round holds the wave open as
  long as you keep turning.

- **v2.60.0** — **The whole interior churns.** The user composited the 360-degree reference
  photo into the game frame, which settled what "like this" means: the entire inside of the
  barrel is textured whitewater — roof included — not glass with foam at the crash foot. The
  streak layers' thresholds come down and their weight doubles, a cloud-scale mottling layer
  covers the whole interior (faded at the weld so the crest still matches the sea), the sea's
  impact apron widens across the visible flat, and the depth-darkening eases so the texture
  survives into the deep tube. The mouth stays the brightest thing in frame.

- **v2.59.0** — **The crash corner goes cloudy.** Tuned against the 360-degree reference
  shot: the crisp boiling band alone read as paint, so it now sits inside a soft, low-contrast
  **cloud of aerated water** reaching well up the falling curtain — two noise layers at
  different scales, the cloud slow and wide, the churn fast and tight at the foot. Spray is
  half again denser and scatters taller, so it reads as splashing rather than fizz, and the
  sea's own impact band thickens to meet it. Alpha rises with both layers — aerated water is
  not glass-thin. (No sand: the reference's floor is a beach, ours stays water.)

- **v2.58.0** — **The crash zone. Where the curtain lands, the water now reacts.**
  - The impact line — the falling curtain meeting the sea — was clean glass on one side of
    the seam and painted foam on the other. It now carries the three things the reference
    photographs show: **churn wrapping up the falling water** (baked into the shell as a
    ragged, boiling band in the sea's own impact-band white, so both sides of the seam agree
    by construction), **spray off the line itself** (small on purpose — the pooled splash
    emitter, a handful a second, because the reference's crash corner is texture, not a
    plume, and a white cloud at the mouth would bury the brightest part of the shot), and
    the sea's own land band from v2.50 underneath.
  - It **forms with the wave**: the churn is keyed to the same fall geometry as the ring, so
    there is none of it on the unbroken wall down the line — no curtain, no impact, no foam —
    and it arrives as the lip throws. Verified mid-formation: zero splashes, zero band.
  - One measured lesson in it: the band was first *scaled* by the peel to keep it off the
    unbroken wall, and read as nothing — because the visible stretch of impact line runs
    through the middle of the peel, where a linear scale cut it to a third strength before
    the shader saw it. It is **gated, not scaled**; the curtain's own length already grows
    with the peel. Found by projecting the foot line into screen space rather than staring at
    coefficients.

- **v2.57.0** — **The barrel fills the screen, he rides closer to the lens, and a crash falls
  down the wave.**
  - **The wave covers the frame again.** The near wall was cut away to *the rider's own
    distance* — twenty-odd metres — which did not clear a wall in front of you, it deleted the
    entire tube around the camera, roof included. What showed through the hole was sky:
    measured in portrait, the top fifth of the screen was open air where the barrel should be,
    and the same frame with the cut switched off is water to every edge. It is a fixed three
    metres of clearance in front of the lens now. The comment on the old version claimed "what
    is behind the cut is more barrel" — it wasn't, and nothing had ever looked.
  - **He rides closer.** The trim down the line was −11.5 m, which put him twenty-seven metres
    from the lens and reading as a speck in the mouth of the wave. Most of what that bought is
    paid for by the cut fix — the tube stands around the camera now — so it comes back to −7.
    The trade is real and was measured rather than guessed: a phone in portrait is a
    31-degree horizontal keyhole, and closer makes the same orbit subtend more of it. At −7 he
    is a third bigger and stays in frame everywhere except the far side of a full loop.
  - **A crash off the top falls, instead of flying sideways.** Two of the three launch terms
    were wrong for a crash on the ring: `vx` is the *lane* velocity, which the ring overwrites
    every frame while it goes on integrating the steering underneath it — metres a second of
    something he is not doing — and the vertical term had a floor that threw him *upward* off
    a wall he was already twelve metres up. On top of that came a random 2–4 m/s sideways
    kick. He now leaves the wall with the ring's own tangential motion and gravity has the
    rest: measured off the roof, 12.4 m of fall and 0.00 m of sideways drift.
  - **The ruler was reading the wrong water.** `sampleWave`, which every "how far off the
    surface is he" check measures against, sampled the sea at `tNow` — while the ocean is
    *drawn* from `waveClock`, which advances at `1.0+0.55·sin+0.28·sin` and so drifts away
    from `tNow` without bound. Every such reading was taken against water from some other
    moment, out by as much as 1.4 m for reasons that had nothing to do with the rider. That is
    a measurement bug rather than a game one, but it is the one that sent two investigations
    the wrong way.
  - **Still open: the board against the water on the upper face.** With the clock fixed the
    hull measures 0.05–0.42 m *above* the surface everywhere it was checked, carving and
    straight, so the burial that reading appeared to show was the broken ruler. Whatever is
    being seen is not reproduced yet.

- **v2.56.0** — **The lip falls into the water, and going round is paced by the wall rather
  than by the clock.**
  - **The corner where the barrel breaks is gone**, and it needed both halves of the fix.
    Measured round the whole ring, it was one step: the radius went from 3.06 m straight to
    8.13 m at a single angle, because the lip's arc stopped about three metres above the sea
    and there was simply no water across that gap to ride. That is the "shoots you more to
    the right and then up, almost like a ninety degree angle" — the geometry really was a
    corner.
    - *The lip now falls a curtain into the water.* A thrown lip does not stop in mid-air;
      the water pours off it and lands in front, and that curtain is what closes a barrel's
      section. It hangs from the tip, scales with how far the lip has actually thrown — so a
      section down the line that has not broken yet still has an open mouth to see out of —
      and the ride's own surface function knows about it, so what you ride is what is drawn.
    - *And the weld at the other end is clamped.* The lip grows out of the crest, so that
      boundary is continuous by construction — but a ray fired just below it runs nearly
      tangent to the crest, and on a frame where the swell dips it passes clean over and
      finds the back of the wave: one angle read 8.18 m against an arc at 5.41. Inside that
      band the water cannot be further out than the lip welded to it.
    - Worst step round the whole circle: **5.07 m → 0.79 m**, and contact while moving
      **1.85 m → 0.82 m**. The suite walks all 64 angles now, so the cliff cannot come back
      quietly.
    - *And the angle is paced by distance along the wall.* This is the half that no amount of
      reshaping the water could have fixed. A barrel's section is a tall almond, not a circle
      — the roof is three metres from the axis and the trough under the mouth is five and a
      half, because that is where the water is. Anywhere the radius climbs steeply, a constant
      *angular* rate is a constant rate through a great deal more *wall*, and the rider is
      fired along it. Dividing by the local stretch turns that into a steady speed over the
      surface. Held-turn travel now measures ~0.5 m/s while the radius swings 6.4 → 7.5 →
      5.4 m through the handover that used to throw him.
  - **The surface march no longer skips grazing water.** Its step was 0.90 m, and a ray fired
    nearly parallel to the face could step clean over the sea and report it metres further
    out: single angles read 4.95 m between neighbours of 3.09 m. Those phantom spikes are why
    "stays on the wave while moving" kept drifting between 1.0 m and 1.9 m run to run.

- **v2.55.0** — **The lip pitches over instead of inflating, the ring is a circle, the near
  water is solid, and the shot really does not rise.**
  - **The camera no longer rises as you climb.** The lock was being re-clamped every frame to
    stay above the water under the *rider* — and the water under him rises the whole time he
    is going up the wall, so the shot rose with him. That is the last of the "camera still
    moves up". Inside the tube the sea is held glassy and the lock is taken at the chase
    camera's own height, metres clear of it, so there was nothing left for the clamp to save.
  - **A lip pitches, it does not inflate.** Scaling the shell alone grew the barrel like a
    tunnel opening out, which is the one thing it is not: a wave throws a ledge off the crest
    and that ledge swings over. The arc now rotates as well as grows — it starts laid back
    against the face, feathers off the crest as a line, and comes round over your head. The
    ring's own centre is carried with it, so the water and the physics are the same tube for
    the whole formation.
  - **The kink where the lip's arc meets the flat sea is NOT fixed.** *(Fixed in v2.56.0 —
    the four attempts below all treated it as one problem, and it was two.)* Three ways of
    rounding it were tried and all three measured worse than the corner they were removing,
    which is
    recorded in the code so the next attempt does not repeat them: a pure circle at the arc's
    radius leaves the board a metre and a half over the water at the bottom of the open sector;
    a circle shrunk to meet "the water below the axis" collapses, because the height field at
    the axis's own x is the *wave*, seven metres of it, not the trough; damping the radius in
    time rounds the corner and lags, so the board rides up to a metre off the surface whenever
    he is moving (worst contact 0.56 m → 1.58 m); and averaging the surface over a third of a
    radian either side rounds it without lag but smears the lip's radius toward the sea's near
    the boundary, which is a metre off the mesh he is supposedly riding (1.18 m). The ring is
    back on the surface at his own angle, which is where it measures best. The corner is real
    and still to do.
  - **Nothing near the lens is half-transparent.** The near wall was being *thinned* to 20%,
    which meant every foreground surface came out as a pale veil with the horizon showing
    through it — water that is not the colour of the water beside it. It is cut outright now:
    what is in the way simply is not drawn, and everything that is drawn is drawn at full
    strength. The frame edges hold because the tube runs full bore for its whole length —
    what is behind the cut is more barrel.

- **v2.54.0** — **The barrel builds around you, and the camera does not move at all.**
  - **The barrel forms instead of arriving finished.** The tube used to exist the moment the
    ride phase began: you reached the wave and a completed barrel was already waiting, which
    is not how any wave has ever worked. The lip grows out of the crest and arches over across
    about four seconds — the shell is scaled from almost nothing to full throw — and the ring
    only takes charge once there is a bore worth riding round, because it gates on its own
    radius. Measured over a real swell→ride: bore **0.5 m at the start of the ride → 5.7 m**
    once it is over you, with the open face to ride first.
  - **There is no barrel camera any more.** Every version of it cut to a chosen placement —
    pulled back to 25.5, re-centred on the tube's axis, aimed at a settled value — and every
    one of those is a snap at the instant the ring takes charge. The ride camera simply stops
    following and holds exactly where it already was. The new check measures *across the
    takeover*, which is the frame the snap lived in.
  - **The rider trims down the line instead.** That camera costs framing: a five-metre ring
    seen from where the chase camera stands subtends more than the lane is wide, so the orbit
    ran off the sides. Paid for by moving the thing that is allowed to move — he drives
    forward down the line over about a second, the orbit shrinks in frame, and the picture is
    untouched. Measured: in frame at every angle round the ring, x from 0.08 to 0.76.
  - **"IN THE BARREL!" is gone.** Nothing announces it; you can see it happening.
  - **Nothing is put in the tube to crash into.** Wreckage went in as the one thing that still
    bit in there and it was never a hazard you could play against: the ring owns your position
    while you are going round it, the drum arrives out of a wall you cannot see through, and
    the whole event is over in a frame. Reported twice as a wipeout out of nowhere, which is
    what it was. A blown trick is the risk in there now, judged exactly as it is everywhere
    else.

- **v2.53.0** — **Tricks turn about the board, the barrel stops zooming, no plane flash, and
  the tube plays by the same rules as everywhere else.**
  - **Tricks were scrambled once you were banked.** `player` is the parent and `rig` the
    child, so a trick rotation is applied in **world** axes to a board the ring has already
    banked. Out on the flat that is the same thing — a level board's axes *are* the world's.
    Round the tube it is not: banked ninety degrees, the board's transverse axis is world y,
    so a front flip came out as a flat spin and a spin came out as a flip; upside down in the
    roof they swap back. Conjugating the trick by the bank fixes it without moving anything
    in the hierarchy — the total becomes bank × trick × pose, i.e. pose the board, turn it
    about its own axes, then bank the result onto the wall. The barrel roll is about z, which
    the bank leaves alone, which is why that one always felt right and the other two didn't.
  - **Dropping into the barrel is no longer a zoom.** The barrel shot was pinned at z=25.5,
    well back from where the chase camera stands, to fit the ring into a 31-degree lane. It
    does fit — and arriving in the tube read as the picture pulling away, which is the one
    thing that announces a different camera has taken over. It holds the ride's own distance
    and height now. It fits anyway since the break moved: there is barrel around the lens
    wherever it stands.
  - **The plane flash on pressing play.** `startSetWave` made the aeroplane visible and left
    the FLY phase to move it — a frame later. So for one frame it was drawn wherever it had
    last been, which after the banner tow is close overhead, at the 2.6 scale set for the
    distant crash. It is placed at its k=0 pose before it is shown.
  - **The tube plays by the same rules as the rest of the game.** Both of these were softened
    for the barrel and both were worse for it: a drum to the chest cracked the board instead
    of ending the run, and a blown landing scored nothing and let you carry on. The result was
    that nothing in there ever ended a ride except a board quietly snapping several drums
    later, with nothing to connect it to. A blown landing now wipes you out on the same
    windows and with the same names as anywhere else, and wreckage ends the run and says
    WRECKAGE. *This reverses the softening added in v2.51.0, at the owner's request.* The ring
    itself still cannot throw you off — hanging in the roof is still free.

- **v2.52.0** — **No see-through patch following the rider, and the shot really does not move
  — through the crash included.**
  - **The near wall thins by depth alone.** Three versions of this now: removed outright,
    which opened the frame at its *edges* — out at the periphery that wall is the only thing
    between you and the sky. Bored toward the rider, which fixed the edges and put a hole
    around *him*, travelling with the board round the tube, which is the one thing water never
    does. And this: everything nearer the lens than he is goes translucent by the same amount
    everywhere, so it reads as looking *through* the near wall — which is what you are doing —
    and nothing about it tracks him. A source check now forbids the shader from referring to
    his position at all.
  - **The aim's depth was the last thing still moving.** Position, height and aim height were
    all locked; the aim *point* still rode `swCam` as it ramps 0→1 over the first second of
    the ride, sweeping from −14 to −24 in z and swinging the whole picture round while you go
    up the wall. Locked at the settled value, so the shot cuts to its final framing at ring
    entry and then holds. The new check samples from the first frame of the ride, not once
    everything has settled — sampling late would never have caught this.
  - **Crash in the barrel and the camera stays where it was.** Two things here, and the
    second is the one that mattered. The wipeout was written to cut to a wide chase that
    pulls back far enough to frame the rider and the board however far they are thrown — good
    on the open face, and on a barrel it reads as zooming out the moment you die. It holds the
    ride's own frame now instead: the wave still collapses, the whitewater still comes
    through, you just watch it from where you were watching the ride.
    But that crash camera has *never actually been seen*. `running` goes false the instant you
    wipe out, so the loop's `else` branch — the menu's slow drifting wide shot — has been
    running one line after `updateWipeout` placed the camera, every wipeout, overwriting it.
    Measured: the camera was landing at z=11.67 whatever the crash code asked for. So the
    real answer to "it zooms out when you die" was the menu camera snapping in. It is skipped
    while a crash is playing. Across a wipeout in the barrel: **13.87 m of movement → 0.000**.

- **v2.51.0** — **The barrel wraps the whole frame, the mystery wipeout is gone, tricks stay
  in the wave, and the shot really is fixed.**
  - **You could see out of the barrel at the edges, because the break was in the wrong
    place.** The camera rides at z=25.5 and the collapse started at z=4 — so every barrel shot
    was taken from twenty metres *inside* the section that has already closed out, where the
    shell has shrunk to half its bore and dropped three metres. Correct physics, useless
    cinematography: the tube was not around the lens any more and the top of the frame was
    open sky. The break moves back behind the camera, and a new `SW_HOLD` gives the wave a
    26 m stretch of fully-thrown tube in front of it — previously the throw peaked *at* the
    break and nowhere else, so nothing could be both fully barrelled and ahead of the
    collapse, which is the one place the shot needs to be.
  - **The near-wall cut is a window now, not a missing wall.** Taking the whole near wall away
    is what opened the corners: out at the periphery that wall is the only thing between you
    and the sky. It measures how far each point sits off the line from the camera to the
    rider and only removes water near that line.
  - **Wreckage in the tube costs you the board, not the run.** This was the mystery death: a
    drum in the pocket at the bottom of the ring, `startWipeout` on contact, no warning. The
    ring owns your position while you are going round it, so it was never something you could
    steer away from — measured on a real ride, two loops in. It cracks the board and knocks
    you off the wall instead, which says CRACKED! and leaves you a count; three still snap the
    board and end it, the same as anywhere else. The spawn interval goes **0.8–2.0 s →
    2.6–5.6 s**.
  - **A trick is a hop, not a launch.** The pop off the wall carried three metres toward the
    axis of a tube six and a half across — from the bottom of the ring that is straight up,
    and it read as being fired out of the wave. Now about a metre and a half, held for a
    second, which is enough to throw a trick without leaving the wall behind.
  - **The camera's aim holds still too.** The position was locked and the aim was not — it
    tracked a fifth of the climb, which on a seven-metre wave is the whole picture drifting
    every time you go round. The anti-drowning clamp was also re-applied every frame against a
    swell that moves, feeding that movement into a shot that is meant to be nailed down; it
    ratchets into the lock once now.
  - The peel checks sample **relative to the break** instead of at hard-coded z. When the break
    moved, "z=30" quietly stopped meaning "behind the break" — the collapse check would have
    passed on a wave that no longer collapsed anywhere in view.

- **v2.50.0** — **The lip and the sea are the same water, the break throws whitewater, and
  the tube stops looking like a pipe.** Worked against surf photography rather than by eye.
  - **The wave face is lit through, the way every photograph of one is.** A wall about to
    barrel is the brightest, greenest water in the picture — you are looking at daylight
    coming through several metres of it — and the flat sea in front is duller and bluer.
    Ours had it exactly backwards: a navy wall against a bright sea, with a translucent teal
    lip growing out of it. The face's transmission now covers the whole standing wall instead
    of a band along the crest (`smoothstep(0.46,0.98)` → `smoothstep(0.14,0.86)`) and the
    darkening that sat over it is nearly halved. The lip's own glow climbs more slowly into
    the tube, because it was putting 27 more green on the roof than on the water underneath.
    Both coefficients at the weld are untouched, so the join still meets exactly.
    Measured across the crest line: channel biases **R +20 / G −30 / B −21 → R +3 / G −3 /
    B −8**, worst single sample 43 → 26, and the green-over-blue artefact at the ceiling
    is gone (3.1% → 0%). Lip against open water from inside the barrel: **+23 luminance → −3**.
  - **The break now throws whitewater.** There was none — the lip landed on clean blue glass,
    which no barrel has ever done. A churned band runs from about a metre and a half down the
    face out to fourteen metres in *front* of the wave, only under a section whose lip has
    actually thrown, thickening into the collapse. Keyed on metres from the crest, not on the
    face profile: the profile is a cubic and crushes everything past seven metres into a
    sliver of its range, so the first attempt rendered as a few pixels.
  - **The corrugated tube.** The lip's grain came from world xz, which sounds right — same
    grain as the sea — and is wrong on a cylinder about z, because world x barely changes
    across the whole roof. The noise came out constant around the arc and varying down the
    length, and constant-around-the-arc seen down a tube is a ring. Water in a barrel runs up
    the face and over, so its grain lies along that path: long wandering streaks, about eight
    across the visible length rather than dozens, domain-warped so they never close into
    concentric circles. And the tube darkens away from you now, which is most of what stops
    an evenly lit bore reading as a pipe.
  - **It moves.** The whole streak field is dragged around the arc, and the geometry carries a
    fourth wave — a surge travelling round the throw, weighted to the outer half of the arc
    where the sheet has left the face. A lip being thrown should not hold still while paint
    slides over it.
  - `water.mjs` was comparing foam on one side against glass on the other once the wave grew
    whitewater, which moved its reading twenty points on a change that improved the seam. It
    tests both samples the same way now, and takes `PHASE=RIDE` to look from inside the barrel.

- **v2.49.0** — **The board lies down the line in the tube, and tricks work in there.**
  - **The board sat across the barrel at an angle.** Going round the ring was yawing it up to
    0.55 rad *and* pitching it 0.55 rad, both driven by how fast he was travelling round —
    and the wall is a cylinder about z, so the only heading that lies flat on it is along the
    tube. Turning the board into the direction of travel takes it off that line and stands it
    on a corner. Both are now a token amount, so a hard turn still reads without cocking the
    board: measured mid-turn at 0.05 rad of each, down from 0.55.
  - **You can throw a trick inside the barrel.** Every trick calls `doJump` when you are not
    already airborne, and the ring pinned `airborne` to false on every frame — so nothing
    ever left the water and `trickReady` never came up. Jumping in the tube now pushes you
    off the wall toward the axis instead of trying to jump "up", which in a barrel is not a
    direction. A pipe has its own gravity — whichever way is out — so you coast across and
    get pulled back to the wall, which at the roof means dropping toward the middle and
    rising back into it. You cannot leave the wave doing it: all that changes is your
    distance from the axis. Landing scores the trick the usual way, and because nothing in
    the tube wipes you out, a crooked one simply scores nothing rather than ending the run.
  - Three new checks: the board lies down the line while circling, jumping puts you in pipe
    air with tricks live, and it drops you back on the ring still in the wave.

- **v2.48.0** — **Right is right, the board stops flipping at the lip's edge, and the wave
  waits while you keep going round.**
  - **Steering in the tube was mirrored on half the waves.** Increasing the ring angle
    carries you toward `+swS` in x, and the input was not multiplied by `swS` — so on a wave
    breaking the other way, dragging right sent the rider left. The takeover a few lines
    above already read the angular velocity out of `vx` *with* the `swS` factor, so the input
    was the odd one out. Measured on both sides now: right moves you right either way.
  - **Crossing off the lip flipped the board.** The ring's radius is ray-marched to whatever
    is out there — lip or open sea — precisely so there is no seam, but the *roll* was still
    switching between two unrelated targets at the edge of the lip's arc: the ring's angle on
    one side, the flat sea's carve on the other. Easing across that gap unwound the board
    through most of a revolution, which is the whole flip you see coming from the top of the
    wave back down to the bottom. One expression covers the whole circle now. The roll is
    also wrapped into (−π, π], so the three-times-round unwind when the wave finally lets go
    is gone too.
  - **The clock stops while you are going round.** "You can only go in circles around the
    wave" was the rule the tube was built for, and a fixed 6–16 s ride was quietly overriding
    it — mid-loop, with everything going right, the wave spat you out. Keep it turning and
    the wave waits; park on one spot and the clock runs and it closes on you as before.
    0.7 rad/s is the bar: a full loop in nine seconds, so a drift will not hold it open.
  - Riding past the clock is free but does not pay forever — the tube's score rate tapers
    over the first 45 s of held loops, so an endless barrel is not also an endless score.
    Loops keep their 450 each.
  - Four new checks: right goes right on both sides, the board stays welded to the wall
    across the lip's edge, the clock freezes while circling, and it runs again when you stop.

- **v2.47.0** — **The sun stops shining through the wave, and you can see out of the barrel
  from the bottom of the ring.**
  - **The sky is depth-tested.** The sun, its three glow shells, the moon, the stars, the
    cloud and the gulls all ran with `depthTest:false` and a negative render order, on the
    stated theory that this painted them *before* the world so the world would draw over
    them. It does not. They are transparent materials, and three.js draws the entire opaque
    pass first — a negative render order only sorts them against each other, and with no
    depth test they landed on top of the ocean. Out on the flat that is invisible, because
    the sun sits in empty sky with no water in front of it; inside a barrel, where water
    fills the frame, the disc was painted straight across the inside of the wave, hard-edged
    silhouette and all. They now test against depth and draw after the lip, so the water
    hides them the way water does. The sun still rises, sets and clips into the horizon
    exactly as before.
  - **The near wall of the tube is cut away for the whole ride.** The camera sits at z=25.5
    and the shell runs out to z=34, so once the wave is up the camera is *inside* the barrel
    and its near wall wraps round the lens. That wall was being faded out only as the rider
    climbed above 60% of the wave's height — which meant that at the bottom of the ring,
    where most of a ride is spent, nothing was cut and the frame was a flat blue wall. It is
    cut whenever he is in the tube. Only water nearer the camera than the rider goes; the far
    wall, the barrel you are actually looking down, is untouched.
  - Two new checks: the cut holds above 0.9 at every angle round the ring, and nothing in the
    sky layer skips the depth test.

- **v2.46.0** — **No jet skis during the set wave, and the camera in the barrel is properly
  fixed at last.**
  - **The ski stays away while the wave is up**, at every stage of it, not just the ride. One
    crossing the lane puts a wake through the one part of the game where the water is meant to
    be glassy, and the launch fires while the ring owns the rider's position — throwing him out
    of a wave the rules say he cannot fall out of. The wake cannot lift him off the ring
    either. The clock keeps running, so waiting does not cost the encounter, and the interval
    is **40–64 s → 62–100 s**: the old spacing was tight enough that two skis could bracket a
    single set wave, one on the way in and one on the way out.
  - **The camera holds still — height included.** This was tried in v2.44 and rejected, and
    that rejection was worthless: every one of those attempts ran while `Z1=13` was ending the
    shell in front of the camera, so the frame was filled by the tube's exterior no matter what
    the camera did. Retested against a shell that renders, a held camera frames the barrel
    fine. It is centred on the tube's **axis** rather than wherever he entered — he orbits that
    axis, so a camera parked off it watches an orbit that runs off one side — and set back to
    25.5, because a 6.6 m ring does not fit a 31° lane from closer. Measured round the ring:
    **all four quarters in frame** (x 0.06–0.97), where at the chase distance two of four fell
    outside it.
  - Twice now a conclusion has been drawn from a test run while something else was broken. Both
    times it was the shell. If the barrel looks wrong, check the curl renders BEFORE concluding
    anything about the camera.
- **v2.45.0** — **You have to actually reach the jellyfish now.** The obstacle test is a
  footprint on the water — x and z only — and every other obstacle pays for that with a height
  gate of its own (`py < waterY + h`). The jellyfish never had one, so its footprint alone
  fired: pass metres above one at the top of a jump and it still threw you, a bounce arriving
  out of clear air. It is gated on its own top plus a board's thickness, so landing ON it still
  counts and flying over it no longer does. Measured over a spawned jelly: **1 m over it
  bounces, 4 m and 8 m pass clean**, where before every height bounced.
  - The gate deliberately sits above the generic obstacle gate below it, so a jellyfish out of
    reach cannot fall through the chain and be treated as something lethal instead.
  - The first version of the test watched the score for the bounce's +60 and passed a broken
    build, because it ran while a barrel was paying out 240 a second. Bounces are counted now
    rather than inferred from a number that has other contributors.
- **v2.44.0** — **The barrel's inside goes glassy, and the camera stops sliding sideways.**
  - **Chop no longer rolls through the tube.** A swell running through the barrel was picking
    the rider off the wall and throwing him at the roof, which is the crash that had nothing to
    do with riding. One factor, `swCalm`, eases the ordinary swell down to 22% while he is in
    there and back up when he leaves. It multiplies `waveAmp`, which is also what feeds the
    shader's `uAmp`, so **the water you see calms with the water you ride** and the two cannot
    disagree.
  - **Only the sideways chase is frozen** — and that is the honest limit, reached by measuring
    four fully fixed placements that each lost the barrel: on the tube's axis the lip is a
    metre away and unreadable; well back you see the outside of the collapsed tail; just past
    the shell the camera is inside the wave; frozen at entry it holds at axis height aimed down
    and the shell fills the frame. Hiding the lip proved that last one — the same frame renders
    perfectly without the curl, so it was the tube filling the screen, not the water. Height
    and aim must keep following, because what makes a barrel legible is looking *into* it.
  - Two of my own mistakes, recorded because both looked like the reported bug: the anti-drown
    clamp was sampling `waveH` at the camera, which near the crest is the seven-metre set wave,
    so it shoved the camera to nine metres and aimed it down into the face. And shortening the
    shell to `Z1=13` to "get the camera out of the tail" was backwards — **the camera sits
    inside that shell on purpose**, and that is what makes the shot a barrel rather than a view
    of the back of a wave.
- **v2.43.0** — **He was standing on the wall with the board on the inside of him.** A
  skateboarder in a full pipe has the BOARD against the wall and his head toward the middle.
  The sign of the bank was inverted, so it was the other way round — which is exactly what the
  reports of "the board is in the middle" were describing, and it was a minus sign.
  - A roll of φ about +z carries local up to `(−sin φ, cos φ)`, and inward is
    `(−swS·sin θ, cos θ)`, so φ is **swS·θ — not minus**. Measured off the rig's real world
    quaternion rather than the Euler angles meant to produce it, the rider's up ran between
    **−0.23 and +0.09** against the inward direction: anywhere but at the axis. It is **1.000**
    all the way round the arc now.
  - **And the bank was damped**, which is the same bug as the position easing one version ago
    in a second place: on the wall the bank is not a response to anything, it IS where he is —
    the same angle that places him on the ring. Damping lagged it behind the turn and dropped
    alignment from 1.00 to **0.61 while actually moving**. Applied straight now, like the angle.
  - Both checks are in the suite, taken from the transform rather than from the inputs, and
    both sample while turning rather than parked.
- **v2.42.0** — **The floating was the easing, and only a moving ride could show it.**
  - Easing the position toward a target that is travelling round a circle is a **first-order
    lag**, and a follower chasing a point around a circle always cuts inward — the faster the
    point goes, the more it cuts. Measured mid-ride: the ring's radius was 6.60, the lip sat at
    6.9–7.2, and the rider was orbiting at **3.4–4.7** — three metres inside the wall, in open
    air. That is every "floating in the middle of the tube" report, and it survived four rounds
    of fixes because **an angle you set and let settle has no angular velocity to cut against**.
    Static checks read 0.6 m while real play was 3.5 m out.
  - The angle is his input now and is applied exactly. Only the RADIUS is smoothed, which is
    all the smoothing was ever for — the surface is sampled from a moving sea and breathes with
    the swell, and that should not reach the board as a shake.
  - **The radius is measured to whatever is actually out there**, by firing a ray from the
    tube's axis and stopping at the first surface: the lip inside its arc, the sea outside it.
    One rule, continuous across the boundary, so there is no step to ease across.
  - The sea is asked about the point he ENDS UP at rather than the one the ray aimed for —
    while the radius catches up those are different places, and the wrong one left him a metre
    and a half over the water at the bottom of the ring.
  - **Over a real ride: frames more than a metre from any surface 24% → 2%, worst 3.50 m →
    1.12 m.** The suite now includes a moving check as well as the parked ones, because the
    parked ones passed throughout.
- **v2.41.0** — **The board was riding a circle 1.7 m inside the lip, and every test passed.**
  The shell is built by running out along the arc and coming back offset by its thickness —
  and that offset goes AWAY from the axis: measured on the cross-section, the return face
  reaches y=5.68 where the arc reaches 4.63. So the face bounding the tube's interior is the
  arc itself, welded to the crest, and pulling the ring in by the slab's thickness put the
  board a constant **1.67–1.78 m off the mesh at every angle it was supposedly riding**. That
  is the board hanging in the middle of the tube, touching nothing. Only the board's own draft
  comes off now: **worst distance to any surface 1.78 m → 0.66 m**.
  - **The test was the real failure.** `contact.mjs` proved that OFF the lip he was on the
    water, and simply assumed the ring's radius matched the lip's surface. It never measured
    the distance to the mesh, so a board riding open air inside the tube passed cleanly. The
    suite now measures distance to the actual geometry and fails past 0.85 m — the check that
    would have caught this three versions ago.
  - The settle time matters and cost a false failure: the rider is EASED onto the ring, and at
    a tenth of wall clock that takes a couple of real seconds. At 1400 ms the reading was 0.95
    m — mid-ease, not a gap.
- **v2.40.0** — **The ring is not a full circle, and pretending it was left him hanging in
  mid-air.** The lip's arc leaves the crest and sweeps **232° at the break — and as little as
  102° down the line**, where the wave has not thrown yet. Everything outside that is the
  barrel's open mouth: no lip, no water at that radius, just the hole you look out of. A ring
  point in there is a point in nothing, which is exactly the board floating in the middle of
  the tube touching no surface at all.
  - The arc's ring angle is `π − SW_ALPHA + th`, so the covered sector is known exactly. Inside
    it the ring IS the surface; outside it the surface is the sea, and that is where he goes —
    which is also the honest answer, since you cannot ride an opening.
  - The board stops banking to a wall the moment there is no wall: out in the mouth the roll
    comes off and the hull lies on the water like anywhere else.
  - **`contact.mjs` measures it**, because "floating" is a number: height above the sea at each
    angle, against whether the lip covers that angle. Round the ring now — on the water through
    the open sector (0.04–0.26 m above it), on the lip through its span. **Nothing in mid-air**,
    and the suite fails if any angle is both off the lip and off the water.
- **v2.39.0** — **The gap was the swell, not the peel.** Two attempts fixed the wrong thing
  because both were reasoned rather than measured. `holes.mjs` settles it: the sky and
  everything in it is hidden, the background set to a colour water cannot produce, and every
  pixel of that colour inside the wave is somewhere you can see straight through it.
  - At the distance the reports came from — **sunset, around 1400 m — 5461 see-through
    pixels**, with the sun behind them. At 710 m, where every earlier test ran: zero. That is
    why it looked intermittent and why nothing geometric ever matched it.
  - **The lip is welded to the set wave's crest, and the water is the set wave PLUS the swell.**
    The swell scrolls as you travel, so wherever it dips below the crest line the water falls
    away from the lip's edge and daylight comes through. Chasing it exactly would mean
    rebuilding the shell every frame against a surface it otherwise ignores; a skirt hanging
    3.2 m below the weld costs nothing and cannot be got wrong — buried in the water where the
    swell is high, covering the gap where it is low.
  - Swept across eight distances from 900 m to 2 km: **zero see-through pixels at seven of
    them**, and the eighth is sky at the frame's edge above the wave, outside it.
  - Recorded for next time: a single sample of this returned both 5461 and 0 at the same
    odometer. Anything that depends on where the swell happens to be has to be swept, not
    sampled.
- **v2.38.0** — **The sun was shining through a hole, and the foil was lifting the wrong way.**
  - **The gap was still there, one factor short.** v2.35.0 dropped each lip slice onto the
    crest the peel leaves, but that offset is a LOCAL one and the shell's y scale multiplies it
    again afterwards — so the correction came out short by exactly the fold factor. Computed:
    0.07 m at z=+12, 0.45 at +18, 1.09 at +24, **1.46 by +30**. That is the daylight between
    lip and water, and at the right time of day the sun itself. Dividing the offset by fold
    before baking it takes the gap to **0.000 at every station**.
  - **The camera inside the barrel is fixed.** Not damped, not eased — set once when he joins
    the ring and not moved again until he leaves it. It sits a little further back than the
    chase camera, because a five-metre ring does not fit a 31° lane from where the chase camera
    stands; measured round a full turn, he now stays in frame the whole way (x 0.02 → 0.84).
    Outside the barrel nothing changes.
  - **The foil lifts along the wave, not along the sky.** On a foil the hull stands off the
    water on its mast, and that lift was being added to world +y — right in the pocket and
    wrong everywhere else on the ring. At the top of the tube it drove the board a mast's
    length further INTO the roof rather than hanging it beneath, which is the board floating
    free of the wave. It now lifts toward the tube's axis, which is what "up" means to someone
    riding the inside of one.
  - Worth recording: the camera framing experiments in v2.37.0 were run while the lip's shader
    was failing to compile, so "pulling back loses the barrel" was never actually tested
    against a barrel. It was retested here against one that renders.
- **v2.37.0** — **The camera stops chasing you round the barrel, the lip comes alive, and the
  board lies on the wall it is riding.**
  - **A near-still shot inside the tube.** The camera no longer slides sideways to keep you
    centred and barely rises as you climb, so you travel through the frame instead of hanging
    in the middle of it while the world turns. It is not welded, and that is deliberate: both
    fully locked versions were tried and both lost the barrel. Locked where it stood, the rider
    leaves the side of the screen a third of the way round (measured, x=−0.26). Moved onto the
    tube's axis to centre the orbit, the camera ends up *inside* a five-metre tube with the lip
    too close to read. A barrel is only legible from outside its mouth and a little below,
    which is where the chase camera already sits — so what is removed is the chasing.
  - **The lip breathes.** It was a rigid shell with moving paint on it, which is most of why
    the top read as scenery while the bottom read as water. Travelling waves now run along its
    length and around its arc, faded to nothing at the crest on both faces so the weld cannot
    be shaken loose.
  - **The board lies on the ring, not on the sea beneath it.** Its pitch was still coming from
    hull samples taken off the flat water underneath — which, up in the tube, is metres below
    and says nothing about the surface being ridden. That is what made it look like it was
    floating over the wave rather than carving it. The nose now lifts as he climbs and drops
    coming down the far side, and the board turns into the direction it is actually travelling.
  - **A guard for the failure that hides.** A shader compile error does not throw: the program
    fails, three.js writes it to `console.error`, and the mesh simply stops being drawn — state
    still reports it visible, the geometry is still right, and there is nothing on screen. The
    lip's new ripple used `uT` in the vertex shader where it was only declared in the fragment
    one, the whole barrel vanished, and it read as a camera bug for several rounds. The suite
    now fails on any shader that does not compile.
- **v2.36.0** — **The ring was a circle around the wrong point.** It was centred on the pocket
  plus a metre, and the tube's real axis — the centre of the arc the lip is built on — is
  **3.47 m** away from that. A circle around the wrong centre cannot lie on the wave: it
  crossed the surface twice and spent the rest of the way either through the water or out in
  the air beside it, which is exactly the board floating clear of the face instead of carving
  it.
  - The axis is now read out of the lip's own geometry. The arc is a circle centred at
    `(sin α, −cos α)·rad` in the shell's frame, so the ring is that same circle put through the
    shell's transform and pulled in by the lip's thickness — the rider travels the *inner*
    face, the surface actually there to touch.
  - **And the sea gets the last word on contact.** Round the top the ring IS the lip's inner
    face, so the circle is the surface; round the bottom it only approximates a face the height
    field defines exactly, so down there the water wins and the board rides on it. Measured
    around the ring: 0.13–0.17 m of clearance through the lower quarter, reaching 11.11 at the
    roof against a lip underside of 11.37. Contact is asked of the sea every frame rather than
    trusted to the circle.
- **v2.35.0** — **The gap along the weld was a hole, not a colour.**
  - **The lip was welded to a crest the peel had already lowered.** Every slice started at
    local y=0 — the shell's origin at `swA`, which is right where the wave stands full height
    and wrong everywhere behind the break, because the peel drops the crest there while the
    lip stayed up at the old level. Computed along the wave: 0.06 m of daylight at z=+6,
    0.87 m at +12, **3.85 m by +30**. Each slice now sits on the crest the peel actually
    leaves.
  - **The top wears the sea's own surface texture.** The lip carried a texture of its own in
    (arc, length) space, which is why it never quite passed as the same water however well the
    colour matched — the grain was a different size and ran in a different direction on the two
    sides of the join. It uses the ocean's exact noise layers now: same world coordinates, same
    frequencies, same thresholds, same drift. The one thing deliberately left out is the
    forward travel — the ocean scrolls its noise by `uDist` so the swell rolls past you, and
    this water is standing still while you ride along it.
  - **The smoke is a string, not beads.** One puff per frame leaves a dotted line whose gaps
    grow as the frame rate drops. Puffs are laid ALONG the segment the plane covered since the
    last frame, spaced by distance rather than by time, so the trail is continuous at any frame
    rate and comes out the same on a fast phone and a slow one. Smaller, and it hangs for about
    nine seconds instead of three.
  - **The plane grew to suit its new distance.** Moving the crash to z=-168 so it fits a
    portrait frame left it three pixels across: in shot, and unreadable. The distance is what
    the framing needs, so the aeroplane is 2.6× instead — nothing sits beside it out there to
    give away the scale, and a legible crash beats a correct wingspan.
  - Two load-order bugs found on the way: `SW_PEAK` was declared below the lip's geometry,
    which is built at load time, so the whole page died in the temporal dead zone; and warping
    a test straight into RIDE skipped the transition that locks the pocket onto the rider, so
    every warped capture was of a wave standing at x=0.
- **v2.34.0** — **The lip was measured against the wrong thing, and four other fixes.**
  - **The tube read as lit plastic stuck on the ocean.** v2.33.0 matched the lip to the wave's
    *crest* and passed its own test — but the crest is the brightest line on the whole wave, so
    everything got dragged up to it, and from outside the barrel the lip was a bright cyan slab
    against a navy sea. `water.mjs` measures the comparison that actually matters: it hides and
    shows the lip, so the pixels that change are exactly the lip and the untouched ones are
    sea. It came in **120.8 luminance** brighter than the water it grows out of. Now **+42**.
  - **The lighting was the real problem, not the palette.** The lip and the sea were lit by two
    different formulas — the lip's ambient floor was 0.72 against the ocean's 0.38, and inside
    a tube the direct term is zero everywhere, so that one number nearly doubled its brightness
    before anything else was added. Tuning constants to match at one place kept breaking
    another. The lip now uses the ocean's own colours, floor, diffuse weight and sky term, and
    transmission — looking *through* the water rather than at it — is the only term that
    departs from the sea's recipe. The weld is continuous by construction rather than by tuning.
  - **Joining the ring no longer jumps.** The angle is read back out of the rider's current
    position on the first frame, so the ring takes over exactly where he already is, and he is
    eased onto it rather than pinned — the ring's floor is sampled from the moving sea, so hard
    placement was passing every ripple straight into the board as a shake.
  - **The plane crash is in frame.** It went in at z=-24, which in portrait is off the side of
    the screen: the crash that starts the whole event happened where you could not see it. The
    lane is only about 31° wide, so something 46 across has to be ~166 away to be in shot. It
    comes down at -168 now, with the splash scaled to stay readable at that range.
  - **And it flies the way it is pointing.** The model's nose is its -z, so the yaw matching a
    velocity is `atan2(-dx,-dz)`; the old fixed expression turned the wrong way, subtracting the
    side-slip the flight path was adding, so it crabbed across the sky pointing off its own line
    of travel. Heading now comes from where it is actually going, and a smoke trail hangs on the
    line it came down — at that distance the airframe is a few pixels, the smoke is not.
- **v2.33.0** — **The roof and the face are the same water now, measured rather than eyeballed.**
  `seam.mjs` projects the crest line — where the lip is welded to the sea — to screen
  coordinates from the wave's own numbers and samples the capture either side of it, so the
  join is a number instead of an opinion. It reports the *bias* (a systematic step) separately
  from the spread, because the foam texture alone swings a single reading by twenty.
  - Start: the tube came in duller and redder (108,202,203) than the sea directly beneath it
    (76,254,255) — biases of about R +44, G −40, B −40. End: **R −10.3, G +1.6, B −0.8**, all
    inside the texture's own noise.
  - **The seam was structural, not a palette choice.** The lip's lit-through glow is keyed on
    `t`, the distance around the arc, and fell to ZERO at `t=0` — exactly where the water's
    own glow, keyed on height up the face, is at its maximum. The two are welded along that
    line, so the wave shone brightest precisely where the lip growing out of it went dark. No
    recolouring fixes a discontinuity built into the parameterisation; the lip now starts at
    the value the face has at the crest.
  - **And the thickness ramp was inverted.** The face's "thick water" darkening rose all the
    way with height, so it was heaviest at the crest — where the water is thinnest and about
    to throw. That was the last systematic step across the weld, worth 33 in green alone.
  - **A clipping guard came out of it.** Matching the two sides is only half the job: a channel
    sitting at its ceiling matches everything and shows nothing. The first version that matched
    perfectly did it by pinning green and blue at 255 across the whole tube — the old white-out
    in cyan. The suite now measures that too: 47.7% → **21.6%** of the wave at the ceiling.
- **v2.32.0** — **The camera stays at the ride angle all the way round the tube.** v2.31.0 rolled
  it with the ring; that kept the tube wrapped around the view, but the whole picture turning
  over is disorienting and it throws away the horizon — the one stable reference telling you
  where you are on the wave. The rider turns upside down now; the shot does not.
  - **The lip gets out of the way instead.** Measured with the plain camera: at the top of the
    ring the rider projects into frame and is drawn *completely hidden* behind the back of his
    own barrel, which is why the loop camera existed. So the water gives way rather than the
    camera moving — only the part of the lip nearer the camera than the rider fades, and only
    while he is up out of the pocket. The far wall, which is the tube you look down, is
    untouched, and in the pocket nothing fades at all: that shot is unchanged.
- **v2.31.0** — **The barrel is a place you ride around, not a lane you sit in.** Three changes
  that only make sense together.
  - **The wave forms around you.** It used to stand up at a fixed x and leave you to go and
    find the pocket, which meant the biggest thing in the game could arrive and pass you by.
    The pocket now tracks the rider as the swell comes in, so by the time it stands up it has
    closed around him. It locks where it landed at the moment the ride starts — left tracking,
    it would follow him round the ring for ever and he could never move inside his own barrel.
  - **Steering carries you around the tube.** Inside, the lane is the tube's circumference:
    up the face, across the roof upside down, down the falling curtain and back to the water.
    The ring is derived from the wave rather than tuned — its floor is the height field in the
    pocket, its roof is `swRoofY()` at the rider's own z — so it inherits the peel for free.
    Out along the unbroken wall there is no thrown lip, the ring closes to nothing, and there
    is nothing to circle. You can only loop where the wave is genuinely hollow. A full turn
    pays 450, counted on the crossing so rocking over the top cannot farm it.
  - **Nothing in the ring can throw you off.** Being pitched, hitting the lip and running out
    onto the shoulder all ended the ride before; all three are gone. The clock is the only way
    out and debris is the only thing that still bites, which is what keeps the ring worth
    steering rather than a place to park.
  - **The camera belongs to the loop.** The chase camera stays upright and level, so at the
    roof it ended up outside the wave looking at the back of the lip with the rider nowhere on
    screen. Inside the ring it now sits inboard on the same radius and rolls with him, so the
    tube stays wrapped around the view and it is the world that turns over.
  - The float test is skipped while on the ring: up in the roof he genuinely *is* above the
    water, and the ordinary code would call that air and drop him out of the wave.
- **v2.30.0** — **The sun stops being a sticker.** A flat-shaded sphere ends exactly where its
  silhouette does, and against the sky that hard cut reads as pasted on — worst seen down the
  barrel, where the mouth frames it small and the cut is the only edge in view. The obvious
  fix, a shader with a soft rim, is the wrong one: that material carries the horizon clipping
  plane, and a raw `ShaderMaterial` in r128 never receives the clipping chunks, so the sun
  would stop sinking into the sea. A third glow shell hugging the disc feathers the same edge
  and leaves the clip alone. Over the barrel frame, measured against v2.27.0: near-white
  6.3% → 2.7%, contrast (sd) 56.3 → 62.2, local detail 3.43 → 3.84, mean 170 → 110.
- **v2.29.0** — **The set wave breaks along its length instead of everywhere at once.** It
  presented the same cross-section down its entire length — a tunnel, not a wave that is
  breaking. Now the throw is a function of position along the wave: far down the line the lip
  has barely left the crest and the section is unbroken green wall, it pitches out as it comes
  toward the break, stands fully over your head at the break itself, and collapses behind.
  Measured along the wave at full height: lip reach 0.94 m at z=-120, 9.89 m at z=-30, 15.0 m
  at z=0, back to 6.79 m at z=+30, with the crest falling 6.95 m to 3.13 m behind the break.
  - **The break line does not move, and does not need to.** The rider sits at z~0 while the
    world scrolls past him, so a break fixed in this frame *is* a break travelling along the
    water — which makes the whole peel a spatial pattern and lets it be baked into the
    geometry rather than rebuilt every frame.
  - **A whitewater trail behind it**, where the crest has already dropped out of the height
    field: a churned sheet over the whole standing section rather than a line along its top.
  - **The wave contributes to `dhz` for the first time.** Every earlier version was constant
    along z, so its normal never tilted down the line at all; without this the collapse would
    light as though it were flat.
  - **The trap, recorded because it will be back:** `waveH`'s `z` is the *swell's* scrolling
    frame — `dist*WAVE_DRIFT + worldZ` — not world z. The shader takes true world z. Feeding
    the scrolled one to the peel put the rider permanently past the break by 700 m, so the
    wave rendered perfectly while the water under the board collapsed to 45% and the rider
    sank through his own board. The scroll is taken back off inside `setWaveH` now, and a
    test asserts the crest under the rider stands at full height.
  - `swRoofY()` takes the z it is asked about and derives the radius from the same peel the
    geometry uses. The note left on it last version predicted exactly this.
  - The lip's slices are doubled to 104: the wall-to-barrel transition spanned about three
    slices at 52, which is where features in this codebase start rendering as facets.
- **v2.28.0** — **The ocean was shading its biggest wave with thresholds meant for ripples.**
  The set wave's face rendered as one flat white sheet with a knife edge along its foot, and
  the obvious suspect — the lip mesh — turned out to be innocent: hiding the lip left the
  white wall exactly where it was. `uPeak` sits near 1.2, tuned to a swell about a metre
  tall, and the set wave stands at seven, so in `vH` it drove the colour ramp and every foam
  term against their limits at once. `clamp(7*0.28+0.5)` is 1, so the whole face took the
  shallow colour; `crest` and `lip` both saturated, so foam came out at a flat 0.85 white
  everywhere, with the knife edge falling on the contour where it crossed.
  - **The sea is now shaded by its own height**, with the set wave subtracted back out, and
    the set wave carries its own treatment: thick and dark out of the trough, lit through
    where it thins toward the lip — the tube roof's transmission colour, so face and roof
    read as one body of water — and foam only in a band along the crest, where it is
    actually tearing. Measured over the wave region: near-white 6.3% → 3.8%, contrast
    (sd) 56.3 → 61.2, mean 170 → 122.
  - **The tube's rings were the leaf's veins again.** The curl's foam ran 46 cycles along the
    length against 7 around the arc, so the noise formed forty-odd bands stacked down the
    tube — and bands seen down a tube are rings. It read as ribbed pipe. The fast axis is the
    one around the arc now. Swapping it wholesale (26 against 9) traded rings for a
    starburst, since streaks that long all converge on the vanishing point and read as drawn
    speed lines; near-square is what breaks it up.
  - **The weld shows in colour, not just geometry.** With the ocean's crest no longer white,
    the lip's own white leading edge drew the very line it exists to hide. The join is
    narrower and dimmer, and the wave's crest foam reaches up to meet it.
- **v2.27.0** — **A play button beside Play that summons the set wave.** The wave was the
  hardest thing in the game to look at: it is only eligible past 600 m, then waits out its own
  30–45 s clock, and a wipeout on the way means starting the whole approach again. The green
  play icon next to **Play** starts a run with the wave already on a two-second fuse — two
  seconds to the plane going down, and the barrel reaches you about twelve seconds later once
  the crash and the swell have run. It is a one-off: the wave clears the flag as it starts, so
  the run goes back to its normal rhythm rather than becoming a wave machine. The row is
  exactly as wide as a lone button was, so Play gives up the width the icon takes and the
  stack below it does not move.
- **v2.26.0** — **The test harness is back, and it can reach the set wave.** `test.mjs` had
  been lost with an old container, so nothing was checked before shipping. It is rebuilt and
  self-contained — it serves the repo itself rather than assuming a server is already up,
  and it drives the pre-installed Chromium rather than downloading one.
  - **A door for the tests.** The game is wrapped in an IIFE, so a harness could not ask it
    anything, and the set wave only fires past 600 m and runs ~25 s of simulated time — which
    under swiftshader is four minutes of wall clock per attempt, with no way to see what state
    it reached. Loading with `#debug` now exposes `window.__surf`: read the state, arm the
    wave, warp into SWELL/RIDE/EXIT, sample the height field, and measure the lip mesh
    directly. A normal page never defines it.
  - **A guard on the wave's two copies.** The wave exists twice — `setWaveH()` in JS, which
    the board rides, and the block in the ocean's vertex shader, which is what you see — and
    only discipline keeps them equal. The suite now fails if the shader's literals drift from
    `SW_FACE`, `SW_BACK` or the face exponent.
  - The eight absent `models/*.glb` 404s are the documented fallback path, not faults, so the
    harness whitelists exactly those and treats any other missing asset as a failure.
  - No game behaviour changed. `swRoofY()` was measured against the lip mesh on suspicion that
    v2.25.0's taper had left it stale, and it is correct where the rider actually sits — 11.41
    against a measured 11.43.
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
