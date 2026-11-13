// Cumulus on a camera-facing quad, lit like a volume from two baked textures (SkyKit.CloudMaps).
//
// Each cloud is a real 3D density field, raymarched offline (unity/Tools~/cloudbake) and lit
// from six directions in the sprite's own frame -- right, up and toward the lens, and their
// opposites -- with Beer-Lambert extinction, the octave approximation of multiple scattering,
// light that diffuses through the cloud rather than stopping dead, and a powder term that keeps
// thin wisps from glowing as if they were solid. What the viewer sees of each is stored per
// pixel: the "pos" texture holds the light from +X +Y +Z and the cloud's coverage, the "neg"
// texture the light from -X -Y -Z and the single-scattered light from straight behind.
//
// Here the sun's direction, turned into the quad's frame, picks its blend of the six (six-way
// lighting: each axis takes the share the light leans its way), so the sun side is lit and the
// far side is in its own shadow, wherever the sun is. Then:
//   - the sky lights the rest: blue-white from the dome above, a little off the sea below;
//   - the silver lining: light pouring forward through the thin fringes, sharply peaked round
//     the sun (Henyey-Greenstein, nothing added when looking away from it) and only on the
//     fringes turned toward it, so a back-lit cloud is never ringed by an outline;
//   - the undersides burn with the low sun's glow at either end of the day;
//   - low clouds sink into the horizon's haze.
// Colours come in already converted to light by SkyKit.LightClouds, once a frame rather than
// once a pixel. Two texture reads a pixel; everything else is a handful of multiplies.
// Per vertex: uv0 the atlas, uv1 the tile's rect (u, v, width, height; a negative width marks
// a mirrored tile), colour alpha the opacity.
Shader "Surf/Cumulus"
{
    Properties
    {
        _CloudPos ("Light from +X +Y +Z, coverage (linear)", 2D) = "black" {}
        _CloudNeg ("Light from -X -Y -Z, through-light (linear)", 2D) = "black" {}
        _SunDir ("Light direction (world, toward the light)", Vector) = (0.3, 0.5, -0.8, 0)
        _SunLin ("Light colour (linear)", Vector) = (1, 0.94, 0.83, 1)
        _AmbUp ("Sky light from above (linear)", Vector) = (0.12, 0.2, 0.33, 1)
        _AmbDn ("Light off the sea below (linear)", Vector) = (0.03, 0.05, 0.07, 1)
        _UnderLin ("Underside glow (linear rgb, amount)", Vector) = (1, 0.5, 0.2, 0)
        _HzLin ("Horizon haze (linear)", Vector) = (0.7, 0.85, 0.95, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _SunDir, _SunLin, _AmbUp, _AmbDn, _UnderLin, _HzLin;
            CBUFFER_END
            TEXTURE2D(_CloudPos); SAMPLER(sampler_CloudPos);
            TEXTURE2D(_CloudNeg); SAMPLER(sampler_CloudNeg);

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 tile : TEXCOORD1; float4 col : COLOR; };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 wp : TEXCOORD1;      // six-way weights for +X +Y +Z; w: how far off the line of sight the sun stands
                float4 wn : TEXCOORD2;      // for -X -Y -Z; w: the opacity
                float4 wl : TEXCOORD3;      // the sun's direction across the sprite alone, as weights of +X +Y -X -Y
                float3 wpos : TEXCOORD4;
            };

            // ---- shared vertex (mirrored in the browser mock) ----
            // The light in the sprite's own frame (right, up, toward the lens) as the weights of
            // the six baked directions: each takes the share the light leans its way. A billboard
            // faces the lens whole, so these are the same at every corner and cost nothing per pixel.
            void Basis(float3 Lb, float op, out float4 wp, out float4 wn, out float4 wl)
            {
                float3 L = normalize(Lb);
                float3 lp = max(L, float3(0.0, 0.0, 0.0));
                float3 ln = max(-L, float3(0.0, 0.0, 0.0));
                float s = 1.0 / max(lp.x + lp.y + lp.z + ln.x + ln.y + ln.z, 1e-4);
                float l2 = length(L.xy);
                float2 lt = L.xy / max(l2, 1e-4);
                float2 tp = max(lt, float2(0.0, 0.0));
                float2 tn = max(-lt, float2(0.0, 0.0));
                wp = float4(lp * s, saturate(l2 * 6.0));
                wn = float4(ln * s, op);
                wl = float4(tp * tp, tn * tn);
            }

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.wpos = TransformObjectToWorld(i.pos.xyz);
                o.uv = i.uv;
                float3 r = TransformObjectToWorldDir(float3(1, 0, 0));
                float3 u = TransformObjectToWorldDir(float3(0, 1, 0));
                float3 f = TransformObjectToWorldDir(float3(0, 0, 1));    // the quad is turned to the lens, so +z points away from it
                float3 L = normalize(_SunDir.xyz);
                // a mirrored tile has its right and left swapped
                float mirror = i.tile.z < 0.0 ? -1.0 : 1.0;
                float3 lb = float3(dot(L, r) * mirror, dot(L, u), -dot(L, f));
                Basis(lb, i.col.a, o.wp, o.wn, o.wl);
                return o;
            }

            float HG(float c, float g) { float g2 = g * g; return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * c), 1.5); }

            // ---- shared fragment (mirrored in the browser mock) ----
            float4 Shade(float4 p, float4 q, float4 wp, float4 wn, float4 wl, float3 wpos)
            {
                float a = p.a * wn.w;
                // the six lightmaps, back to radiance (stored square-rooted over 2.4)
                float3 P = p.rgb * p.rgb * 2.4;
                float3 Q = q.rgb * q.rgb * 2.4;
                // the sun: its direction is a blend of the six baked ones
                float sun = dot(P, wp.xyz) + dot(Q, wn.xyz);
                // the sky's light: the dome above is half straight down and half round the sides,
                // and the sea below gives back a little of the same
                float ring = (P.r + Q.r + P.b + Q.b) * 0.125;
                float upL = P.g * 0.5 + ring;
                float dnL = Q.g * 0.5 + ring;
                // silver lining: looking toward the sun, light pours through where the cloud is
                // thin (the baked single scattering from behind), sharply peaked round the sun and
                // only on the fringes turned toward it; with the sun straight behind, the whole rim
                float3 vdir = normalize(wpos - GetCameraPositionWS());
                float cs = dot(vdir, _SunDir.xyz);
                float hg = HG(cs, 0.75);
                float side = dot(wl, float4(P.r, P.g, Q.r, Q.g));
                float facing = lerp(1.0, smoothstep(0.30, 0.85, side), wp.w);
                float silver = hg * q.a * facing;
                // how low and golden the sun is: next to nothing at noon, most at either end of the day
                float low = _UnderLin.a;
                // the light that has scattered through the body still leans forward, so a cloud seen
                // toward a low sun glows right through rather than going grey (a broad lobe, where
                // the silver lining is the narrow one); and a low sun lights a cloud from the side
                // and below and off the bright sky round it, tinting the whole body
                float hgb = HG(cs, 0.45);
                float avg = (P.r + P.g + P.b + Q.r + Q.g + Q.b) * (1.0 / 6.0);
                float3 col = _SunLin.rgb * (sun * (0.66 + 0.30 * hgb * low) + silver * 0.12 + avg * low * (0.38 + 0.12 * hgb))
                           + _AmbUp.rgb * upL + _AmbDn.rgb * dnL;
                // the sun under the cloud base at either end of the day: the undersides burn with
                // its glow, spreading a little way up the body, hottest on the clouds nearest the sun
                col += _UnderLin.rgb * (low * (Q.g * 0.55 + avg * 0.22) * (1.0 + 0.05 * hg));
                // the further down toward the horizon, the more of its haze is in front of it
                float haze = 1.0 - smoothstep(0.0, 0.13, saturate(vdir.y));
                col = lerp(col, _HzLin.rgb, haze * 0.35);
                a *= 1.0 - haze * 0.25;
                // the silver lining may run past white, so the bloom finds it and nothing else
                col = min(col, float3(2.8, 2.8, 2.8));
                return float4(col * a, a);
            }

            half4 frag(V i) : SV_Target
            {
                float4 p = SAMPLE_TEXTURE2D(_CloudPos, sampler_CloudPos, i.uv);
                float4 q = SAMPLE_TEXTURE2D(_CloudNeg, sampler_CloudNeg, i.uv);
                float4 c = Shade(p, q, i.wp, i.wn, i.wl, i.wpos);
                if (c.a < 0.002) discard;
                return half4(c);
            }
            ENDHLSL
        }
    }
}
