# The paisley bandana

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
