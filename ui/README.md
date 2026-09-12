# ui/ — the painted furniture on the run card

Five pictures were delivered as finished paintings on a flat studio ground. `make.py` cuts them
into what the page can actually use:

    python3 ui/make.py <folder holding the source jpgs>

| file | what it is | used by |
| --- | --- | --- |
| `btn_dbl.png` | DOUBLE COINS (Watch Ad) | `#dblBtn` |
| `btn_life.png` | SECOND LIFE (Watch Ad) | `#lifeAdBtn` |
| `plank.png` | a quest plank with nothing written on it | `.over #ovText .tr.goal` |
| `knob.png` | the shell that rides the bar | `.tr.goal .bar i.on::after` |
| `plaque_life.png` | the LIFE plaque | `.over #overRow .ob-life` |
| `plaque_share.png` | the SHARE plaque | `.over #overRow .ob-share` |
| `plaque_menu.png` | the MAIN MENU plaque | `.over #overRow .ob-menu` |
| `sign_school.png` | BOARD SCHOOL | `#dSlotSchool` |
| `sign_boards.png` | SURFBOARDS | `#dSlotBoards` |
| `sign_riders.png` | RIDERS | `#dSlotSurfer` |

Most of them ship whole: the ground is keyed off and that is the asset, lettering included,
so the buttons carry no text of their own and their labels are only there for anything that
cannot see a picture. The plaques are the same, and all three are cut from **one shared crop
box** with no trimming to each plaque's own outline afterwards: they are shot in identical
frames and they sit side by side in a row, so what matters is that they come out at the same
scale in the same place. Tightened to itself, a plaque whose shadow reaches a few pixels
further renders a few per cent smaller than the one beside it.

**The plank is the one that is cut apart**, because three things on it change from quest to
quest: the carving in the dish on the left, the words along the top and the count in the trough.
All three come out and the row draws its own, so one painting serves all eight goals in
`GOAL_POOL` instead of needing eight paintings. Two planks were delivered and both are used —
the roundel is taken from *Ride a Barrel*, whose dish is a clean circle, and the trough from
*Shave 3 close calls*, whose bar has no shell sitting on it.

Filling the three holes back in is the only place a pixel is invented, and even there the rule
is that a hole is filled from its **own** material:

* the title band and the trough are filled straight **across**, row by row, from the wood either
  side of the words. Both are shaded top to bottom and blurring instead flattens that shading,
  which leaves the patch sitting visibly proud of everything around it.
* the dish is filled by pushing its own wood inward — a normalised blur, colour and coverage
  blurred together and then divided — so the rim shadow survives and only the carving leaves.
* neither fill is allowed to see the plank's outline or the trough's rope. Those are far bigger
  departures from flat wood than any letter is, and a fill that can reach one drags it into the
  middle of the board.

The geometry the stylesheet uses is this painting's own, as a percentage of the cropped plank
(844 × 188 before scaling): dish centre 12.86% × 51.9%, trough interior 29.9% from the left,
64.2% wide, 62.2% down, 21.8% tall. Change the crop in `make.py` and those five numbers in
`index.html` have to move with it.

The three hut signs are photographs rather than flat paintings, and the linen behind them is a
woven texture with no single colour to key out. What separates them is that the block is wood
and the cloth is not — 75 points of red over blue on one and 18 on the other, with nothing in
between. The burnt-in engraving is dark enough in places to fall out of that key, so the outline
is closed by taking the block's own row and column extents: a photographed block is convex, so
its extents *are* its outline, and the engraving is inside them.

They are painted at the lit quad's real projected size by `layoutShopSlots`, not at the button's.
The button is floored at 44px so a thumb can find it, and a sign painted across that floor hangs
over the edges of the square it is nailed to.
