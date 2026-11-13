// The beach. The web's sand tile texture and normal map, graded the way the web's title grades
// them (a warm tan, read off the web's own frame: about 170,150,115 on open sand), lit by the
// title's low key light raking across the normal map so every ripple in the texture stands up,
// with the real shadows of the palm, the pug, the chest and the signpost falling on it.
// On top of the texture, and only close to the lens where the texture runs out of pixels:
// grains (light and dark specks at three sizes, in world space so they never stretch with a
// tile), the tiny relief those grains give the surface in raking light, fine wind ripples, and
// the odd quartz grain catching the sun. Each fades out by its own screen-space size, so far
// sand stays calm instead of shimmering. The maths runs in plain sRGB numbers, as the web's
// does and as the sea's does here, and goes to linear once at the end.
Shader "Surf/Sand"
{
    Properties
    {
        _BaseTex ("Base", 2D) = "white" {}
        _NrmTex ("Normal", 2D) = "bump" {}
        _NrmK ("Normal strength", Float) = 1.35
        // a Vector, not a Color: in a linear project Unity converts a Color property to linear before
        // the shader sees it, and this is a plain multiplier on sRGB numbers (1.13 would arrive as 1.31)
        _Grade ("Grade (sRGB multiplier on the texture)", Vector) = (1.04, 1.07, 1.13, 1)
        _Gain ("Gain", Float) = 1.0
        _ShadowK ("Shadow darkness", Float) = 0.42
        _Detail ("Close-up detail", Float) = 1.0
        _Glint ("Quartz glints", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseTex); SAMPLER(sampler_BaseTex);
        TEXTURE2D(_NrmTex); SAMPLER(sampler_NrmTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseTex_ST; float4 _Grade; float _NrmK, _Gain, _ShadowK, _Detail, _Glint;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 v : POSITION; float3 n : NORMAL; float4 t : TANGENT; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 w : TEXCOORD1; float3 n : TEXCOORD2; float4 t : TEXCOORD3; float4 sp : TEXCOORD4; };

            V vert(A a)
            {
                V o;
                o.w = TransformObjectToWorld(a.v.xyz);
                o.pos = TransformWorldToHClip(o.w);
                o.uv = TRANSFORM_TEX(a.uv, _BaseTex);
                o.n = TransformObjectToWorldNormal(a.n);
                o.t = float4(TransformObjectToWorldDir(a.t.xyz), a.t.w * GetOddNegativeScale());
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }

            float h21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(h21(i), h21(i + float2(1, 0)), u.x), lerp(h21(i + float2(0, 1)), h21(i + float2(1, 1)), u.x), u.y);
            }
            // how much of a pattern of this size (feet) survives at this pixel: 1 while a cell spans
            // several pixels, 0 once it is smaller than one
            float keep(float cell, float fw) { return saturate(cell / max(1e-5, fw) * 0.5 - 0.6); }

            half4 frag(V i) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, i.uv).rgb;
                albedo = pow(max(albedo, 0.0), 1.0 / 2.2);                  // the texture's own sRGB numbers
                float3 tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_NrmTex, sampler_NrmTex, i.uv), _NrmK);
                float3 N0 = normalize(i.n), T = normalize(i.t.xyz - N0 * dot(i.t.xyz, N0)), B = cross(N0, T) * i.t.w;
                float3 N = normalize(T * tn.x + B * tn.y + N0 * max(0.05, tn.z));

                // ---- the close-up layer, in world feet ----
                float2 g = i.w.xz;
                float fw = max(fwidth(g.x), fwidth(g.y));
                float kFine = keep(0.035, fw) * _Detail, kMid = keep(0.11, fw) * _Detail, kRip = keep(0.30, fw) * _Detail;
                // grains: light and dark specks at three sizes
                // Grains are round, not cells: a hash per grid cell drew every speck as a little square,
                // and close to the lens those squares were the size of a fingernail. The fine layer is
                // smooth value noise; the odd dark grain is a soft dot at a jittered point in its cell.
                float s1 = vnoise(g / 0.035 + 3.7), s2 = vnoise(g / 0.11), s3 = vnoise(g / 0.55 + 7.3);
                // (measured against the web's title: the near sand there varies about half as much as
                // this did, so every layer of grain is about half what it was)
                float speck = (s1 - 0.5) * 0.08 * kFine + (s2 - 0.5) * 0.06 * kMid + (s3 - 0.5) * 0.06 * keep(0.55, fw);
                float2 dc = floor(g / 0.05), dfr = frac(g / 0.05);
                float2 dp = float2(h21(dc + 1.7), h21(dc + 4.3)) * 0.5 + 0.25;
                float dark = step(0.965, h21(dc + 3.1)) * smoothstep(0.30, 0.12, length(dfr - dp)) * 0.18 * keep(0.05, fw) * _Detail;   // the odd dark grain
                albedo *= 1.0 + speck - dark;
                // wind ripples: long low ridges a few inches apart, wandering
                float warp = vnoise(g * 0.35) * 2.6;
                float rip = sin((g.x * 0.62 + g.y * 0.78) / 0.30 * 6.2831 * 0.5 + warp * 3.0);
                float2 ripD = normalize(float2(0.62, 0.78));
                // the grains' own relief: the gradient of the fine noise, tilted into the normal
                float e = 0.045;
                float2 gr = float2(vnoise((g + float2(e, 0)) / 0.09) - vnoise((g - float2(e, 0)) / 0.09),
                                   vnoise((g + float2(0, e)) / 0.09) - vnoise((g - float2(0, e)) / 0.09));
                float2 tilt = gr * 0.25 * kMid + ripD * rip * 0.06 * kRip;
                N = normalize(N + float3(-tilt.x, 0.0, -tilt.y));

                // ---- the light ----
                // The shadow is looked up a little off the surface, along its normal and toward the
                // sun. With the title's sun only a few degrees up, a lookup on the surface itself
                // lands half inside the sand's own shadow-map depth and the beach shades itself in
                // speckles (acne), which read as dull, dirty sand.
                Light L0 = GetMainLight();
                float3 Ls = normalize(L0.direction);
                float4 sc = TransformWorldToShadowCoord(i.w + N0 * 0.12 + Ls * 0.15);
                Light L = GetMainLight(sc);
                float shadow = lerp(L.shadowAttenuation, 1.0, GetMainLightShadowFade(i.w));
                float3 Ld = normalize(L.direction);
                // raking light: the flat beach is the reference, slopes toward the sun lighter, away darker
                // the rake: slopes toward the sun lighter, away darker -- gentler than it was, which
                // with the sun this low turned every ripple of the normal map into hard light and dark
                float k = saturate(dot(N, Ld) * 1.5 + 0.60);
                float shade = 0.83 + 0.24 * k;                 // flat sand comes out where it was (it matched the web)
                shade *= lerp(1.0 - _ShadowK, 1.0, shadow);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(i.sp.xy / max(1e-4, i.sp.w));
                    shade *= lerp(1.0, ao.indirectAmbientOcclusion, 0.8);
                #endif
                float3 col = albedo * _Grade.rgb * _Gain * shade;
                // a little cooler in the shade, where only the sky lights it. (The grade above carries
                // the sky's blue-white fill the web's hemisphere light gives the whole beach,
                // 0xdfeeff at 0.62; with no sky term the sand came out orange and dull.)
                col *= lerp(float3(0.93, 0.96, 1.04), float3(1, 1, 1), shadow);

                // the odd grain of quartz catching the sun
                float3 Vw = normalize(GetWorldSpaceViewDir(i.w));
                float2 gc = floor(g / 0.04), gfr = frac(g / 0.04);
                float gh = h21(gc + 11.7) * smoothstep(0.32, 0.14, length(gfr - (float2(h21(gc + 2.9), h21(gc + 6.1)) * 0.5 + 0.25)));
                float3 gn = normalize(float3(gh - 0.5, 1.2, frac(gh * 7.31) - 0.5));
                float glint = step(0.975, gh) * pow(saturate(dot(gn, normalize(Ld + Vw))), 60.0) * shadow * keep(0.04, fw) * _Glint;

                col = pow(saturate(col), 2.2);
                col += float3(1.0, 0.92, 0.78) * glint * 1.6;               // past white, so the bloom finds them
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            float4 v(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half f() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            struct A2 { float4 p : POSITION; float3 n : NORMAL; };
            struct V2 { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
            V2 v(A2 a) { V2 o; o.pos = TransformObjectToHClip(a.p.xyz); o.n = TransformObjectToWorldNormal(a.n); return o; }
            half4 f(V2 i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
}
