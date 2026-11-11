// The sky dome, the web game's shader line for line: horizon through mid to zenith by height,
// the sun's glow as a core, a wedge along the horizon and a ring, low bands near the sea, and
// the whole thing worked out in plain sRGB numbers and converted to linear at the end, which
// is how the web gets its colour. The sun disc itself is the one addition.
//
// The night, as the web draws it in its sky layer: a star field (460 points at 2.1px, laid
// out by azimuth and elevation and faded with the dark), and the moon on its own arc -- a
// baked disc, pale warm grey and never white, with the seas laid out where everybody knows
// they go and a few craters, feathered by a tight halo and lit into a faint wide haze, the
// disc cut at the horizon and the haze not. The web builds these as meshes riding with the
// camera; here they are painted by direction, which is the same picture.
Shader "Surf/Sky"
{
    Properties
    {
        _Top ("Zenith", Vector) = (0.25, 0.56, 0.84, 1)
        _Mid ("Mid", Vector) = (0.56, 0.82, 0.96, 1)
        _Horizon ("Horizon", Vector) = (0.85, 0.93, 0.98, 1)
        _Glow ("Glow", Vector) = (1, 1, 1, 1)
        _GlowAmt ("Glow amount", Float) = 0.1
        _SunX ("Sun x (web)", Float) = 0
        _SunDir ("Sun direction", Vector) = (-0.45, 0.75, -0.5, 0)
        _SunSize ("Sun size", Float) = 0.045
        _Blend ("Horizon blend", Float) = 0.30
        _MoonDir ("Moon direction", Vector) = (0, -1, 0, 0)
        _MoonUp ("Moon up (0..1)", Float) = 0
        _MoonSize ("Moon size (rad)", Float) = 0.030
        _Night ("Stars (0..1)", Float) = 0
        _StarSize ("Star size (rad)", Float) = 0.0032
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Top, _Mid, _Horizon, _Glow, _SunDir, _MoonDir;
            float _GlowAmt, _SunX, _SunSize, _Blend, _MoonUp, _MoonSize, _Night, _StarSize;
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
            V vert(A i){ V o; o.pos = UnityObjectToClipPos(i.pos); o.d = i.pos.xyz; return o; }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            // the seas, placed by hand: Imbrium, Serenitatis, Tranquillitatis, Fecunditatis,
            // Crisium, Procellarum, Nubium, Humorum (u, v, rw, rh of the web's moon map)
            static const float4 SEAS[8] = {
                float4(0.44, 0.30, 0.115, 0.085), float4(0.545, 0.33, 0.075, 0.062),
                float4(0.575, 0.44, 0.080, 0.058), float4(0.645, 0.47, 0.052, 0.048),
                float4(0.665, 0.33, 0.040, 0.040), float4(0.355, 0.44, 0.125, 0.115),
                float4(0.475, 0.55, 0.058, 0.045), float4(0.400, 0.62, 0.048, 0.036) };

            // ---- the stars: web STAR_N=460, az uniform, el = pow(rand,0.75) * 0.47pi ----
            // One candidate per cell of an azimuth/elevation grid, kept with the probability
            // that gives the web's elevation law (the count per band grows as el^(1/3)) and the
            // web's total: 44 x 14 cells at a mean keep of 0.75 is about 460 stars.
            float stars(float3 vP)
            {
                if (vP.y <= 0.0) return 0.0;
                const float NA = 44.0, NE = 14.0, ELMAX = 1.4765;      // 0.47 pi
                float az = atan2(vP.z, vP.x);
                float el = asin(saturate(vP.y));
                if (el >= ELMAX) return 0.0;
                float2 cell = float2(floor((az / 6.2831853 + 0.5) * NA), floor(el / ELMAX * NE));
                float keep = pow((cell.y + 0.5) / NE, 0.3333);
                if (hash21(cell) > keep) return 0.0;
                float2 j = float2(hash21(cell + 17.1), hash21(cell + 31.7));
                float saz = ((cell.x + 0.15 + 0.7 * j.x) / NA - 0.5) * 6.2831853;
                float sel = (cell.y + 0.15 + 0.7 * j.y) / NE * ELMAX;
                float3 s = float3(cos(saz) * cos(sel), sin(sel), sin(saz) * cos(sel));
                float d = length(vP - s);
                return smoothstep(_StarSize, _StarSize * 0.45, d);
            }

            // ---- the moon's face: the web's moonTexture, the parts of it that read at this size ----
            // tu/tv are the web's canvas fractions (the middle faces the camera, v runs down).
            float3 moonFace(float2 tuv)
            {
                float3 col = float3(0.812, 0.792, 0.733);                 // the regolith, #cfcabb
                // mottle, so nothing is a flat field
                float2 g = tuv * float2(40.0, 20.0);
                float m = hash21(floor(g)) * 0.5 + hash21(floor(g * 2.3)) * 0.5;
                col *= 0.94 + 0.12 * m;
                float dark = 0.0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float2 q = (tuv - SEAS[i].xy) / SEAS[i].zw;
                    float d = length(q) + (m - 0.5) * 0.35;              // a torn edge, not a stamp
                    dark = max(dark, smoothstep(1.15, 0.30, d));
                }
                col = lerp(col, float3(0.478, 0.471, 0.455), dark * 0.80);   // toward (122,120,116)
                // craters: a dark floor with a bright rim, many small and a few large
                float2 cg = tuv * float2(48.0, 24.0);
                float2 cc = floor(cg); float2 cf = frac(cg) - 0.5;
                float ch = hash21(cc + 7.3);
                float cr = 0.10 + pow(ch, 3.4) * 0.38;
                float cd = length(cf + (float2(hash21(cc + 1.1), hash21(cc + 2.2)) - 0.5) * 0.4) / cr;
                float floorK = smoothstep(1.0, 0.6, cd) * 0.22, rimK = smoothstep(0.25, 0.0, abs(cd - 0.95)) * 0.18;
                col *= 1.0 - floorK + rimK;
                // Tycho and Copernicus, the two everybody can point at
                float ty = length((tuv - float2(0.435, 0.735)) * float2(1.0, 0.5)) / 0.017;
                float cp = length((tuv - float2(0.585, 0.615)) * float2(1.0, 0.5)) / 0.013;
                col *= 1.0 - smoothstep(1.0, 0.5, ty) * 0.30 + smoothstep(0.3, 0.0, abs(ty - 1.0)) * 0.34;
                col *= 1.0 - smoothstep(1.0, 0.5, cp) * 0.26 + smoothstep(0.3, 0.0, abs(cp - 1.0)) * 0.28;
                return col;
            }

            fixed4 frag(V i) : SV_Target
            {
                float3 vP = normalize(i.d);
                float h = saturate(vP.y);
                float3 c = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, _Blend, h));
                c = lerp(c, _Top.rgb, smoothstep(_Blend * 0.83, 0.85, h));
                float3 sd = normalize(float3(_SunX, 0.0, -1.0));
                float3 fw = float3(vP.x, 0.0, vP.z);
                float az = dot(normalize(fw + float3(1e-5, 0.0, 0.0)), sd);
                float d = distance(float2(vP.x, vP.y * 1.7), float2(_SunX, 0.10));
                float core = smoothstep(0.42, 0.02, d);
                float wedge = smoothstep(-0.15, 1.0, az) * smoothstep(0.40, 0.0, h);
                float ring = smoothstep(0.22, 0.0, h) * 0.35;
                c = lerp(c, _Glow.rgb, saturate(_GlowAmt * (core * 0.95 + wedge * 0.45 + ring)));
                float band = sin(vP.y * 46.0) * 0.5 + 0.5;
                float lowband = smoothstep(0.34, 0.02, h) * band * _GlowAmt * 0.22;
                c = lerp(c, _Glow.rgb, lowband);
                // the stars: behind the sun's glow, in front of the dome (web renderOrder 4)
                c += float3(0.992, 0.984, 0.941) * stars(vP) * _Night;
                // the moon: the web's two shells (19 wide at 0.50, pow 1.3; 46 wide at 0.16, pow 3.2,
                // additive as SrcAlpha/One, so colour * a * a * opacity) and the disc over them
                float above = smoothstep(-0.02, 0.01, vP.y);
                if (_MoonUp > 0.001)
                {
                    float3 M = normalize(_MoonDir.xyz);
                    float cosM = dot(vP, M);
                    float angM = acos(clamp(cosM, -1.0, 1.0));
                    float aG = pow(max(0.0, 1.0 - angM / 0.0442), 1.3);
                    float aH = pow(max(0.0, 1.0 - angM / 0.1070), 3.2);
                    c += float3(0.737, 0.816, 0.957) * aG * aG * 0.50 * _MoonUp;
                    c += float3(0.561, 0.659, 0.847) * aH * aH * 0.16 * _MoonUp;
                    float disc = smoothstep(cos(_MoonSize * 1.04), cos(_MoonSize * 0.96), cosM);
                    if (disc > 0.0 && cosM > 0.5)
                    {
                        // onto the disc, then onto the sphere behind it: longitude and latitude
                        float3 ex = normalize(cross(float3(0, 1, 0), M)); float3 ey = cross(M, ex);
                        float3 p = vP / cosM - M;
                        float tn = tan(_MoonSize);
                        float u = dot(p, ex) / tn, v = dot(p, ey) / tn;
                        float zz = sqrt(saturate(1.0 - u * u - v * v));
                        float lon = atan2(u, zz), lat = asin(clamp(v, -1.0, 1.0));
                        float2 tuv = float2(0.5 + lon / 6.2831853, 0.5 - lat / 3.14159265);
                        c = lerp(c, moonFace(tuv), disc * 0.92 * _MoonUp * above);
                    }
                }
                // the sun disc, cut at the horizon
                float3 L = normalize(_SunDir.xyz);
                float cosA = dot(vP, L);
                float discS = smoothstep(cos(_SunSize * 1.15), cos(_SunSize * 0.85), cosA);
                float rim = pow(max(cosA, 0.0), 180.0) * 0.45;
                c += (_Glow.rgb * rim + float3(1.3, 1.2, 1.05) * _Glow.rgb * discS) * above * saturate(_GlowAmt * 2.0 + 0.3);
                // sRGB numbers to linear light, as the web does, and a dither against banding
                c = pow(saturate(c), 2.2);
                float dz = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                c += (dz - 0.5) / 255.0;
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
