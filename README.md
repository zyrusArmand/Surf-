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
