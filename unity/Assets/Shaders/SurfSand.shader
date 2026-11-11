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
        _Grade ("Grade (sRGB multiplier on the texture)", Color) = (1.0, 0.99, 0.97, 1)
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
                float s1 = h21(floor(g / 0.035)), s2 = vnoise(g / 0.11), s3 = vnoise(g / 0.55 + 7.3);
                float speck = (s1 - 0.5) * 0.16 * kFine + (s2 - 0.5) * 0.12 * kMid + (s3 - 0.5) * 0.10 * keep(0.55, fw);
                float dark = step(0.965, h21(floor(g / 0.05) + 3.1)) * 0.30 * keep(0.05, fw) * _Detail;   // the odd dark grain
                albedo *= 1.0 + speck - dark;
                // wind ripples: long low ridges a few inches apart, wandering
                float warp = vnoise(g * 0.35) * 2.6;
                float rip = sin((g.x * 0.62 + g.y * 0.78) / 0.30 * 6.2831 * 0.5 + warp * 3.0);
                float2 ripD = normalize(float2(0.62, 0.78));
                // the grains' own relief: the gradient of the fine noise, tilted into the normal
                float e = 0.045;
                float2 gr = float2(vnoise((g + float2(e, 0)) / 0.09) - vnoise((g - float2(e, 0)) / 0.09),
                                   vnoise((g + float2(0, e)) / 0.09) - vnoise((g - float2(0, e)) / 0.09));
                float2 tilt = gr * 0.55 * kMid + ripD * rip * 0.10 * kRip;
                N = normalize(N + float3(-tilt.x, 0.0, -tilt.y));

                // ---- the light ----
                float4 sc = TransformWorldToShadowCoord(i.w);
                Light L = GetMainLight(sc);
                float shadow = lerp(L.shadowAttenuation, 1.0, GetMainLightShadowFade(i.w));
                float3 Ld = normalize(L.direction);
                // raking light: the flat beach is the reference, slopes toward the sun lighter, away darker
                float k = saturate(dot(N, Ld) * 2.2 + 0.55);
                float shade = 0.70 + 0.40 * k;
                shade *= lerp(1.0 - _ShadowK, 1.0, shadow);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(i.sp.xy / max(1e-4, i.sp.w));
                    shade *= lerp(1.0, ao.indirectAmbientOcclusion, 0.8);
                #endif
                float3 col = albedo * _Grade.rgb * _Gain * shade;
                // the key's warmth on what it reaches, a little cooler in the shade
                float3 keyC = pow(max(L.color, 0.0), 1.0 / 2.2) / max(1e-3, max(L.color.r, max(L.color.g, L.color.b)));
                col *= lerp(float3(0.93, 0.96, 1.04), lerp(float3(1, 1, 1), keyC, 0.10), shadow);

                // the odd grain of quartz catching the sun
                float3 Vw = normalize(GetWorldSpaceViewDir(i.w));
                float gh = h21(floor(g / 0.04) + 11.7);
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
