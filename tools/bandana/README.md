# The paisley bandana

    python3 pattern.py                      # the print: a bordered square, not a tiled motif
    python3 measure_pug.py                  # find his neck: the waist between chest and jaw
    python3 cloth.py                        # build it ON him, against his own silhouette
    python3 shot_pug.py cloth.blend out.png # photograph him in it
    python3 cloth_export.py                 # normalise to a unit ring -> models/bandana.glb

It took three shapes to get one garment, and each wrong one fixed the previous one's fault
while introducing its own. Worth writing down, because both failures look reasonable going in.

**An open triangle draped as a bib.** It read as fabric -- soft, wide, folded -- which was
right. But it hung 2.30 neck-radii when mid-chest is 1.45 away, so it covered his whole front.
Reported as "it fits weird", and three rounds of fitting could not save it.

**A rolled band with tubular ends.** That fixed the size and the hugging and threw away the
whole point: tubes are not cloth. Reported, correctly, as horrible.

**Both at once, which is what a real bandana is:** a rolled band round the neck AND a short
folded triangle hanging from it. The photograph had been showing both the entire time.

Four things that only came out by building on the animal rather than on a stand-in cone:

- **His neck is findable without the rig.** Walk up the body in slices and it is the WAIST
  between two bulges. On pug.glb: z=0.88, radius 0.37 at the front and 0.39 at the side, chest
  bulging to 0.57 below and jaw to 0.49 above.
- **His cross-section is not a circle.** One radius per height puts the collar on a hoop
  standing out past his sides like a wire. Sample per ANGLE as well as per height.
- **Do not shrink-wrap.** Laying every row on the body gives a poncho. The drape takes the
  LARGER of his radius and its own fall, so it clears him where he is wide and hangs free
  where he is not.
- **Solidify only the open sheet.** The collar and knot are closed volumes already, and an
  even offset on a closed volume turns its caps inside out -- foot-long spikes out of the neck.
  A triangle narrowing to a single vertex does the same, so the tip stops at a short edge.

The collar's tilt -- high at the back, dipping at the front -- is built into the mesh. It was
briefly a runtime parameter, which was a patch over a shape that was wrong.
