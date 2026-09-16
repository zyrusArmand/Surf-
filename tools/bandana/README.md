# The paisley bandana

    python3 measure_pug.py                 # find his neck: the waist between chest and jaw
    python3 roll.py                        # build the band ON him, against his own silhouette
    python3 shot_pug.py roll.blend out.png # photograph him in it
    python3 roll_export.py                 # normalise to a unit ring -> models/bandana.glb

**The shape was wrong before the fit was.** Two versions modelled a square folded on its
diagonal and draped as an open triangular bib, and then tried to make that sit right -- first
by size, then by tilt. It never would: the reference photograph is not a bib. It is a ROLLED
BAND round the neck with two ends crossing down the chest, and no open triangle anywhere. Once
the object was the right object the fit took one sweep.

**Measure the animal, do not fractionate the skull.** His neck is findable without knowing
anything about the rig -- walk up the body in slices and it is the waist between two bulges.
On pug.glb: z=0.88, radius 0.40, chest bulging to 0.57 below and the jaw to 0.49 above. That
also said why the first drape read as a bib: it hung 2.30 neck-radii when mid-chest is 1.45.

**His cross-section is not a circle.** One radius per height put the band on a hoop standing
out past his sides like a collar on a wire. Sampled per ANGLE as well as per height it follows
the silhouette he actually has.

**The tilt belongs in the model.** A horizontal ring on a dog reads as a bar laid across his
chest. A collar sits high at the back and dips at the front. That lean was briefly a runtime
parameter, which was a patch over a shape that was wrong; it is built into the mesh now.

---

## The earlier, draped version

    python3 pattern.py     # draws bandana_col.png -- a bordered square, not a tiled motif
    python3 build.py       # the drape: a square folded on its diagonal, pleated by formula
    python3 finish.py      # knot, thickness, bevel, and the print
    python3 export.py      # hero render + a 5,200-tri GLB for models/bandana.glb
    python3 look.py bandana.blend out.png ghost    # three views, against a stand-in neck

Run them in that order; each reads the file the one before it wrote.

Two things here were got wrong first and are worth not repeating.

**A cloth SIMULATION does not survive this shape.** It was the first approach, twice. Once born
inside its own collider -- a solver handed an intersection does not resolve it, it panics -- and
once started clear, where seventy frames were nowhere near enough for a 1,500-vertex skirt with
self-collision to settle: it slid down the neck and hung off one side in a torn spiral. Cloth
tied at the top is not chaotic anyway. It pleats, and pleats radiate from the tie and deepen as
they fall because the same material has to cover more circumference the further down it goes.
That is a formula, and writing it as one gives folds that are even, symmetric and tunable by
looking.

**The pleats have to merge as the cloth narrows.** A fixed seven folds across a row that is
shrinking ends up with five vertices carrying seven cycles, which is not a pleat but aliasing --
it rendered as a fan of spikes at the point and survived the amplitude being faded out. The
frequency falls with the width, so the sampling never runs out.
