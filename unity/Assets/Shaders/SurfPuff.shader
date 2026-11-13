// Whitewater puffs on particle billboards: the spray a carve throws off the rail and the foam
// bubbling along each rail's track, drawn as little lit cumulus rather than flat white blotches.
//
// Each puff is one tile of a pair of baked textures (Resources/Clouds/puff_pos and puff_neg,
// unity/Tools~/cloudbake --puffs): a real 3D ball of foam, raymarched offline and lit from six
// directions in the sprite's own frame (right, up, toward the lens and their opposites), with
// self-shadowing, light diffusing through it and a through-light for thin parts lit from
// behind. A View-aligned particle billboard faces the lens with the camera's own right and up,
// so the sun turned into the camera's frame picks the blend of the six once per vertex, and each
// puff gets a sun side and a soft grey shade side wherever the sun is, for two texture reads.
// Then:
//   - the sky lights the shade, brighter from above than off the sea below;
//   - toward the sun, light glows through the thin parts (a slight forward-scatter);
//   - the foam's own albedo is a hair under white, so only the sun side reaches full white;
//   - it fades where it meets the water (the height of the sea under each puff comes in per
//     particle, so the quad's lower half thins out on the swell instead of being cut by it),
//     where it meets anything solid (soft particles, when the depth texture is there), just in
//     front of the lens, and into the horizon's haze far off, as the sea does.
// The rails' foam lies flat on the water instead (HorizontalBillboard, _Flat 1): its frame is then
// the world's -- right +X, up +Z, toward the viewer straight up -- so it is lit as foam seen from
// above, and a line of it running away from the lens stays a band on the sea rather than a stack
// of upright sprites piled on each other.
// Three knobs shape the outline and the shading (their defaults are the original look):
//   - _Edge.x: how far a fading puff erodes (its thin parts going first) rather than just thinning;
//     _Edge.y, .z: a blurrier mip of the outline, and how much of it to use, so overlapping puffs
//     melt into one mass instead of each showing its own rim; _Edge.w: a round falloff toward the
//     tile's border so no straight sprite edge can ever show (_Tiles gives the atlas grid);
//   - _Lift evens the sun and sky weights out toward light from all round: whitewater scatters so
//     much light inside itself that its shade side is far brighter than a cloud's.
// Per particle: vertex colour alpha the opacity (rgb a tint, white as a rule), uv the atlas
// tile from the texture sheet module, custom1.xy the sea's height under it and how tall a band
// over that height it fades through (0 there switches the fade off).
// Colours come in already converted to light (SkyKit.LightPuffs), once a frame. Output is
// premultiplied.
Shader "Surf/Puff"
{
    Properties
    {
        _PuffPos ("Light from +X +Y +Z, coverage (linear)", 2D) = "black" {}
        _PuffNeg ("Light from -X -Y -Z, through-light (linear)", 2D) = "black" {}
        _SunDir ("Light direction (world, toward the light)", Vector) = (0.3, 0.5, -0.8, 0)
        _SunLin ("Light colour (linear)", Vector) = (1, 0.94, 0.83, 1)
        _AmbUp ("Sky light from above (linear)", Vector) = (0.12, 0.2, 0.33, 1)
        _AmbDn ("Light off the sea below (linear)", Vector) = (0.03, 0.05, 0.07, 1)
        _HzLin ("Horizon haze (linear)", Vector) = (0.7, 0.85, 0.95, 1)
        _Albedo ("Foam albedo (linear)", Vector) = (0.93, 0.96, 1.0, 1)
        _Fade ("Soft distance, near fade start, near fade length, glow", Vector) = (0.6, 0.5, 1.5, 1)
        _Flat ("Lies flat on the water (0: faces the lens)", Float) = 0
        _Edge ("Erosion, outline mip bias, its share, round falloff", Vector) = (0.75, 0, 0, 0)
        _Lift ("Shading evened toward all-round light", Float) = 0
        _Tiles ("Atlas tiles across, down", Vector) = (4, 2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
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
            #pragma multi_compile_local _ _PUFF_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _PuffPos_ST, _PuffNeg_ST;
            float4 _SunDir, _SunLin, _AmbUp, _AmbDn, _HzLin, _Albedo, _Fade, _Edge, _Tiles;
            float _Flat, _Lift;
            CBUFFER_END
            TEXTURE2D(_PuffPos); SAMPLER(sampler_PuffPos);
            TEXTURE2D(_PuffNeg); SAMPLER(sampler_PuffNeg);

            // the particle streams: position, colour, uv (xy) and custom1 (zw), set in Fx.cs
            struct A { float4 pos : POSITION; float4 col : COLOR; float4 uv : TEXCOORD0; };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 wp : TEXCOORD1;      // six-way weights for +X +Y +Z; w: how far off the line of sight the sun stands
                float4 wn : TEXCOORD2;      // for -X -Y -Z; w: the opacity
                float4 wl : TEXCOORD3;      // the sun's direction across the sprite alone, as weights of +X +Y -X -Y
                float4 tint : TEXCOORD4;    // rgb the particle's tint; a: the haze in front of it
                float4 sp : TEXCOORD5;      // screen position; z the eye depth
                float4 misc : TEXCOORD6;    // x height over the sea, y the sea fade's band, z forward-scatter narrow, w broad
            };

            // the light in the sprite's own frame (right, up, toward the lens) as the weights of
            // the six baked directions, each taking the share the light leans its way (as Surf/Cumulus)
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

            float HG(float c, float g) { float g2 = g * g; return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * c), 1.5); }

            V vert(A i)
            {
                V o;
                float3 wpos = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(wpos);
                o.uv = i.uv.xy;
                // the billboard's frame is the camera's: its right, its up, and back toward it;
                // or, lying on the water, the world's: east, north, and up out of the sea
                float3 cr = lerp(UNITY_MATRIX_V[0].xyz, float3(1.0, 0.0, 0.0), _Flat);
                float3 cu = lerp(UNITY_MATRIX_V[1].xyz, float3(0.0, 0.0, 1.0), _Flat);
                float3 cb = lerp(UNITY_MATRIX_V[2].xyz, float3(0.0, 1.0, 0.0), _Flat);
                float3 L = normalize(_SunDir.xyz);
                Basis(float3(dot(L, cr), dot(L, cu), dot(L, cb)), i.col.a, o.wp, o.wn, o.wl);
                float3 even = float3(1.0 / 6.0, 1.0 / 6.0, 1.0 / 6.0);
                o.wp.xyz = lerp(o.wp.xyz, even, _Lift);
                o.wn.xyz = lerp(o.wn.xyz, even, _Lift);
                float3 toP = wpos - GetCameraPositionWS();
                float dist = length(toP);
                float cs = dot(toP / max(dist, 1e-4), L);
                // as the sea fogs: into the horizon's colour from 70 to 230 feet out
                o.tint = float4(i.col.rgb, smoothstep(70.0, 230.0, dist));
                o.sp = ComputeScreenPos(o.pos);
                o.sp.z = -TransformWorldToView(wpos).z;
                o.misc = float4(wpos.y - i.uv.z, i.uv.w, HG(cs, 0.72), HG(cs, 0.35));
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float4 p = SAMPLE_TEXTURE2D(_PuffPos, sampler_PuffPos, i.uv);
                float4 q = SAMPLE_TEXTURE2D(_PuffNeg, sampler_PuffNeg, i.uv);
                // the outline, partly from a blurrier mip so where puffs overlap they run together
                float cov = lerp(p.a, SAMPLE_TEXTURE2D_BIAS(_PuffPos, sampler_PuffPos, i.uv, _Edge.y).a, _Edge.z);
                // and a round falloff toward the tile's border (the tile's own -1..1 square)
                float2 lq = frac(i.uv * _Tiles.xy) * 2.0 - 1.0;
                float rim = 1.0 - smoothstep(0.55, 1.0, length(lq));
                cov *= lerp(1.0, rim, _Edge.w);
                // fading, the thin parts can go first (the foam tearing into lace) or the whole
                // puff can just thin out evenly, by _Edge.x
                float er = (1.0 - i.wn.w) * _Edge.x;
                float a = saturate((cov - er) / max(1.0 - er, 1e-3)) * lerp(i.wn.w, sqrt(i.wn.w), saturate(_Edge.x / 0.75));
                // the six lightmaps, back to radiance (stored square-rooted over 2.4)
                float3 P = p.rgb * p.rgb * 2.4;
                float3 Q = q.rgb * q.rgb * 2.4;
                float sun = dot(P, i.wp.xyz) + dot(Q, i.wn.xyz);
                // the sky: the dome above is half straight down and half round the sides, the sea
                // below gives back a little of the same
                float ring = (P.r + Q.r + P.b + Q.b) * 0.125;
                float upL = P.g * 0.5 + ring;
                float dnL = Q.g * 0.5 + ring;
                // toward the sun the thin parts glow (the baked through-light, only on the side
                // turned to it so a back-lit puff is not ringed by an outline), and the body a
                // little with them: a slight forward-scatter
                float side = dot(i.wl, float4(P.r, P.g, Q.r, Q.g));
                float facing = lerp(1.0, smoothstep(0.30, 0.85, side), i.wp.w);
                float avg = (P.r + P.g + P.b + Q.r + Q.g + Q.b) * (1.0 / 6.0);
                float glow = (i.misc.z * q.a * facing * 0.16 + i.misc.w * avg * 0.18) * _Fade.w;
                float3 col = _Albedo.rgb * (_SunLin.rgb * (sun + glow) + _AmbUp.rgb * upL + _AmbDn.rgb * dnL) * i.tint.rgb;
                col = lerp(col, _HzLin.rgb, i.tint.a * 0.6);
                // thinning where it sits in the sea: from the water line up through its band
                float band = i.misc.y;
                float sea = saturate((i.misc.x - 0.02) / max(band, 1e-3));
                a *= lerp(1.0, sea * sea * (3.0 - 2.0 * sea), step(1e-4, band));
                // and just in front of the lens, rather than a white sheet over the whole frame
                float eye = i.sp.z;
                a *= saturate((eye - _Fade.y) / max(_Fade.z, 1e-3));
                #if defined(_PUFF_SOFT)
                {
                    float2 suv = i.sp.xy / max(i.sp.w, 1e-4);
                    float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    a *= saturate((scene - eye) / max(_Fade.x, 1e-3));
                }
                #endif
                col = min(col, float3(2.0, 2.0, 2.0));
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
