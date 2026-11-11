# Surf for Unity

The native version of Surf. Everything in this folder is a Unity project; the web game in the
folder above is untouched and still the live one.

## What is here so far (vertical slice)

- The sea, the sky and the time of day, ported shader for shader from the web game.
- The ride: the speed curve, the carve, the hop, held spins, rolls and flips, landings judged
  and scored the same way, boost, wipeouts and the run card.
- Buoys, logs, ramps and jellyfish, cast by distance and spaced on the water.
- The chase camera with the speed widening and the lean into a carve.
- HUD, trick buttons, JUMP, touch steering (drag anywhere that is not a button), keyboard
  (A/D or arrows to steer, Space to jump, J/K/L for spin/flip/roll).

Not here yet: the shop, riders and gear, the helicopter and handstand, the board tricks, the
dive, the barrel, the tow plane, the island, the UFO, sounds, quests. They come across one
system at a time, in the order they matter.

## Opening it the first time (about 15 minutes, mostly downloads)

1. Install **Unity Hub** from unity.com, then from the Hub install **Unity 2022.3 LTS**
   (any 2022.3.x). While installing, tick **iOS Build Support** and **Android Build Support**.
2. In the Hub, press **Add** and choose this `unity` folder. Open it. The first open takes a
   few minutes: it fetches the Universal Render Pipeline and the glTF importer.
3. A script in `Assets/Editor` runs on its own and sets the project up: the render pipeline,
   linear colour, portrait, and a `Main` scene. Watch the Console for "Surf setup" lines.
   If anything went wrong, use the menu **Surf > Set up project** to run it again.
4. Open `Assets/Scenes/Main.unity` and press **Play**. Steer by dragging in the Game view,
   Space to jump.

## Models

The slice's models are already in `Assets/Resources/Models/` (`pug`, `board`, `buoy`, `log`,
`jelly`), copied from `../models`. The glTF importer turns them into objects the game picks up
by name. A model that is not there is replaced by a plain shape of the right size, so the game
always runs; drop more in from `../models` as systems come across.

## Building for a phone

- **iPhone**: File > Build Settings, pick iOS, Build. Open the result in Xcode on a Mac,
  sign with your Apple developer account, run on your phone or upload to TestFlight.
- **Android**: File > Build Settings, pick Android, Build And Run with the phone plugged in.

## Where the numbers come from

Every constant in `Assets/Scripts` is lifted from `../index.html` so the feel carries over.
When something feels different from the web game, that file is the reference.
