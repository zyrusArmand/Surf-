# Quest icons

Hand-drawn line art, one PNG per icon, used as **CSS alpha masks** rather than as pictures.

## Why masks

The same icon has to sit on a brown paper note pinned to a board in the sun, on white text over
a dark blue panel, and turn gold when the quest is done. Painted as an image it can only ever be
one of those. Masked — `background: currentColor` through `mask-image` — the ink is whatever
colour the text beside it is, everywhere, including states nobody thought about when the drawing
was made. So the colour in the file is thrown away; only the **alpha** matters.

## Adding one

Drop the drawing in and run:

```
python3 icons/make.py turtle ~/turtle.jpg  seal ~/seal.jpg
```

It finds the ink (the paper is the brightest thing in a photograph of a drawing, so the
threshold comes from the image's own histogram, and the alpha ramps rather than snaps so thin
strokes survive), crops to the artwork, squares it, and writes a 160px PNG.

Then add the name to `QART` in `index.html` and point a quest at it:

```js
{id:'turtle5', text:'Pass 5 turtles', target:5, ev:'turtle', icon:'turtle'},
```

`qIcon(name)` renders it for the quest list and `qIcon(name,'qbi')` for the sheets on the board;
both read the same file.

## What is here

`wave` `shell` `fin` `star` `dolphin` `sunset` `sunrays` `boards` are spoken for by the eight
quests in `GOAL_POOL`. `tower` `turtle` `palms` `island` `board` are drawn and available —
`tower` is already used on the two sheets that are waiting for a quest rather than tracking one.
