// The sea's height field. The same seven-component dispersive swell the web game runs on,
// number for number: every component travels at its own speed (omega = sqrt(g k)) with an
// envelope at half that, so crests are born, peak and die and the pattern never freezes.
// Units are FEET throughout, as in the original, and the field coordinate along the lane is
// (dist * drift + z): the water scrolls past the rider rather than the rider moving.
using UnityEngine;

public static class WaveField
{
    public struct Wave { public float dx, dz, k, a, ek, ep, w; }

    public const float G = 9.8f;
    public const float EnvBase = 0.32f, EnvSwing = 0.68f;
    public static float Drift = 0.46f;     // climbs slightly with distance, see BoardController
    public static float Amp = 1.35f;       // the set's current size, 0.34 .. 2.6

    public static readonly Wave[] Waves = Build(new float[][] {
        new[]{ 0.00f, 1.00f, 0.190f, 1.000f, 0.38f, 0.00f },
        new[]{ 0.60f, 0.80f, 0.310f, 0.520f, 0.42f, 2.10f },
        new[]{-0.74f, 0.67f, 0.470f, 0.260f, 0.50f, 4.20f },
        new[]{ 0.40f,-0.92f, 0.720f, 0.110f, 0.58f, 1.30f },
        new[]{ 1.00f, 0.00f, 1.050f, 0.050f, 0.66f, 5.40f },
        new[]{ 0.86f, 0.51f, 1.700f, 0.030f, 0.72f, 3.10f },
        new[]{-0.35f, 0.94f, 2.600f, 0.016f, 0.80f, 0.70f },
    });

    static Wave[] Build(float[][] rows)
    {
        var o = new Wave[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            o[i] = new Wave { dx = r[0], dz = r[1], k = r[2], a = r[3], ek = r[4], ep = r[5],
                              w = Mathf.Sqrt(G * r[2]) };
        }
        return o;
    }

    static float Comp(in Wave W, float x, float z, float t)
    {
        float u = W.dx * x + W.dz * z;
        float env = EnvBase + EnvSwing * Mathf.Sin(u * W.k * W.ek - t * W.w * W.ek * 0.5f + W.ep);
        return W.a * env * Mathf.Sin(u * W.k - t * W.w);
    }

    /// full height, chop included: what the board rides
    public static float Height(float x, float fieldZ, float t)
    {
        float h = 0f;
        for (int i = 0; i < Waves.Length; i++) h += Comp(Waves[i], x, fieldZ, t);
        return h * Amp;
    }

    /// the long swell only: what a floating object rides, so buoys glide instead of vibrating
    public static float Swell(float x, float fieldZ, float t)
    {
        float h = 0f;
        for (int i = 0; i < 2; i++) h += Comp(Waves[i], x, fieldZ, t);
        return h * Amp * 1.06f;
    }

    /// surface normal by finite difference, for tilting things that sit on it
    public static Vector3 Normal(float x, float fieldZ, float t, float e = 0.6f)
    {
        float hx = Height(x + e, fieldZ, t) - Height(x - e, fieldZ, t);
        float hz = Height(x, fieldZ + e, t) - Height(x, fieldZ - e, t);
        return new Vector3(-hx / (2 * e), 1f, -hz / (2 * e)).normalized;
    }

    /// the wave list packed for the shader: (dx, dz, k, a) and (ek, ep, w, 0)
    public static void FillShaderArrays(Vector4[] a, Vector4[] b)
    {
        for (int i = 0; i < Waves.Length; i++)
        {
            var W = Waves[i];
            a[i] = new Vector4(W.dx, W.dz, W.k, W.a);
            b[i] = new Vector4(W.ek, W.ep, W.w, 0f);
        }
    }
}
