"""
Cut a seamless loop out of a recording, for the game's sustained sounds.

Two things here are easy to get wrong and both were got wrong first time:

EQUAL POWER, NOT EQUAL GAIN. A crossfade between two pieces of the SAME take is a crossfade
between uncorrelated signals, and uncorrelated signals add in power, not in amplitude. A
straight linear fade -- w and 1-w -- lands at 0.5+0.5 of amplitude but only sqrt(0.5) of
power in the middle, which is a 3 dB hole once per loop. Measured on the turn sound: the
level across the wrap came out at 0.760 of the level going into it, a 24% dip you would hear
as a pulse every 0.8 s. sqrt(w) and sqrt(1-w) sum to 1 in POWER and the dip disappears.

AND THE SEAM TEST HAS TO SUIT THE MATERIAL. A single-sample step across the join says
something useful about a tone and nothing at all about noise, where neighbouring samples
differ that much everywhere -- on the ride loop it read "nine times worse" for a join that
was perfectly fine. So the step is reported against the material's OWN median and p95, and
the tests that decide are the ones that survive contact with noise: does the level match
across the wrap, and does the timbre.

  python3 tools/mkloop.py <src.wav> <out.wav> START LENGTH XFADE
         [--swap] [--flatten] [--peak dBFS]
"""
import sys, wave, numpy as np


def load(p):
    w = wave.open(p)
    n, r, ch = w.getnframes(), w.getframerate(), w.getnchannels()
    a = np.frombuffer(w.readframes(n), dtype='<i2').astype(np.float32).reshape(n, ch) / 32768.0
    return a, r


def save(p, a, r):
    o = wave.open(p, 'wb')
    o.setnchannels(a.shape[1]); o.setframerate(r); o.setsampwidth(2)
    o.writeframes((np.clip(a, -1, 1) * 32767).astype('<i2').tobytes()); o.close()


def rms(x):
    return float(np.sqrt((x ** 2).mean()))


def flatten(src, r):
    """Divide out a long fade, keeping the texture and losing only the slope."""
    W = max(1, int(0.25 * r))
    env = np.array([rms(src[i:i + W]) for i in range(0, len(src) - W, W)])
    env = np.maximum(env, 1e-6)
    xs = np.linspace(0, 1, len(env))
    fit = np.polyval(np.polyfit(xs, env, 2), xs)
    g = np.interp(np.linspace(0, 1, len(src)), xs, fit.mean() / fit)
    out = src * g[:, None]
    after = np.array([rms(out[i:i + W]) for i in range(0, len(out) - W, W)])
    print(f"  flattened: envelope spread {env.max()/env.min():.2f}x -> {after.max()/after.min():.2f}x")
    return out


def main():
    src_p, dst_p = sys.argv[1], sys.argv[2]
    START, LEN, XF = (float(sys.argv[i]) for i in (3, 4, 5))
    PEAK = float(sys.argv[sys.argv.index('--peak') + 1]) if '--peak' in sys.argv else -3.0

    a, r = load(src_p)
    S, N, X = int(START * r), int(LEN * r), int(XF * r)
    if S + N + X > len(a):
        sys.exit(f"need {(S+N+X)/r:.2f}s of source, file is {len(a)/r:.2f}s")
    src = a[S:S + N + X].copy()
    if '--flatten' in sys.argv:
        src = flatten(src, r)

    if '--swap' in sys.argv:
        # ---- SLICE AND SWAP THE HALVES ----
        # Cut the segment in two and put the second half first. The join that lands in the
        # MIDDLE of the result is the old end-meets-start seam, and it gets the crossfade --
        # while the new wrap, end-of-A back to start-of-B, is the segment's own midpoint and
        # was continuous all along. So the loop point needs no treatment at all, which is
        # better than treating it well: nothing is blended where the ear comes round.
        # B runs to N+X, not to N. A crossfade always EATS its overlap, so a B that stopped at
        # the requested end left the result exactly XFADE short -- asked for 7.30 s, got 6.95 s.
        # Harmless to listen to and a quiet lie to work with, and the trailing X samples were
        # already loaded for precisely this purpose, so B simply takes them.
        M = N // 2
        A, B = src[:M], src[M:N + X]
        w = (np.arange(X, dtype=np.float32) / X)[:, None]
        joint = B[-X:] * np.sqrt(1.0 - w) + A[:X] * np.sqrt(w)
        out = np.concatenate([B[:len(B) - X], joint, A[X:]])
        assert len(out) == N, f"swap produced {len(out)} samples, wanted {N}"
        print(f"  halves swapped at {M/r:.2f}s; the seam is now mid-file and crossfaded, "
              f"the wrap is untouched original")
    else:
        # The join is made in the middle of a continuous passage: the material that WOULD have
        # played next is faded over the opening, so the wrap never lands on a cut.
        out = src[:N].copy()
        w = (np.arange(X, dtype=np.float32) / X)[:, None]
        out[:X] = src[:X] * np.sqrt(w) + src[N:N + X] * np.sqrt(1.0 - w)

    pk = float(np.abs(out).max())
    out = np.clip(out * ((10 ** (PEAK / 20)) / max(pk, 1e-9)), -1, 1)
    save(dst_p, out, r)

    # ---- and now prove it, with tests that suit noise ----
    d = np.abs(np.diff(out, axis=0)).max(axis=1)
    seam = float(np.abs(out[-1] - out[0]).max())
    W = max(1, int(0.05 * r))
    e1, e2 = rms(out[-W:]), rms(out[:W])
    mid = len(out) // 2

    def spec(x):
        m = x.mean(axis=1) * np.hanning(len(x))
        s = np.abs(np.fft.rfft(m)); return s / max(s.sum(), 1e-12)
    dw = float(np.abs(spec(out[-W:]) - spec(out[:W])).sum() * 100)
    # THE CONTROL MUST NOT BE THE SEAM. Taking it at the midpoint was fine in the default
    # mode, but under --swap the midpoint IS the crossfade, and a crossfade is two takes
    # averaged together -- artificially smooth. Judged against that, the wind loop's wrap
    # read 62.1% against a 49.1% "control" and looked like a defect, when the wrap is
    # literally uncut original samples and cannot be one. So the control is now the MEDIAN
    # of several interior joins, none of them the seam: what this material does unaided.
    probes = [int(len(out) * f) for f in (0.14, 0.27, 0.40, 0.60, 0.73, 0.86)]
    dcs = [float(np.abs(spec(out[p - W:p]) - spec(out[p:p + W])).sum() * 100)
           for p in probes if W <= p <= len(out) - W and abs(p - mid) > 2 * W]
    dc = float(np.median(dcs)) if dcs else float(
        np.abs(spec(out[mid - W:mid]) - spec(out[mid:mid + W])).sum() * 100)
    print(f"  {LEN:.2f}s loop from {START:.2f}s, {XF:.2f}s equal-power crossfade, peak x{(10**(PEAK/20))/max(pk,1e-9):.1f}")
    print(f"  seam step {seam:.5f}  (this material: median {np.median(d):.5f}, p95 {np.percentile(d,95):.5f})"
          f" -> {'ordinary' if seam < np.percentile(d,95) else 'ABOVE p95'}")
    # A level ratio means nothing until you know what this material does ANYWAY: ambience
    # breathes, and on the underwater take neighbouring 50 ms windows swing by half either way
    # all on their own. So the wrap is judged against the spread of every other window pair.
    rr = np.array([rms(out[i + W:i + 2 * W]) / max(rms(out[i:i + W]), 1e-9)
                   for i in range(0, len(out) - 2 * W, W)])
    lo, hi = np.percentile(rr, 5), np.percentile(rr, 95)
    ok = lo <= e2 / e1 <= hi
    print(f"  level across the wrap {e2/e1:.3f}   (this material's own 5-95% spread: {lo:.3f}-{hi:.3f})"
          f" -> {'ordinary' if ok else 'OUTSIDE NORMAL'}")
    print(f"  timbre across the wrap {dw:.1f}%  (this material's own interior joins: {dc:.1f}%)"
          f" -> {'ordinary' if dw < dc * 1.35 else 'ABOVE NORMAL'}")


main()
