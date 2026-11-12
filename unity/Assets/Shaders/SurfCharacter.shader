// The riders, lit the way the web lights them and then tone mapped the way the web tone maps
// them. The web runs ACES (exposure 1.02) over everything but its sea; this port keeps the
// global curve off so the sea and sky stay the web's colour, which left the pug and the other
// riders raw: darker, more saturated and harder than the same model on the web. Like the
// board's shader, this one carries the curve itself. Textured, skinned (any shader is), real
// sun shadows on him, the sky's ambient, a soft sheen.
Shader "Surf/Character"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Sheen ("Sheen", Float) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float _Sheen;
            CBUFFER_END
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float2 uv : TEXCOORD2; };
            V vert(A i)
            {
                V o; o.w = TransformObjectToWorld(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.pos = TransformWorldToHClip(o.w); return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 N = normalize(i.n), Vw = normalize(_WorldSpaceCameraPos - i.w);
                float3 alb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                Light L = GetMainLight(TransformWorldToShadowCoord(i.w));
                float sh = lerp(L.shadowAttenuation, 1.0, GetMainLightShadowFade(i.w));
                float3 ld = normalize(L.direction);
                float diff = saturate(dot(N, ld));
                float3 col = alb * (SampleSH(N) + L.color * diff * sh);
                // a soft sheen, broad, the way fur and skin catch a low sun
                float3 H = normalize(ld + Vw);
                col += L.color * sh * pow(saturate(dot(N, H)), 24.0) * _Sheen;
                // the web's ACES at exposure 1.02 (three.js divides by 0.6 inside; this fit has it built in)
                col *= 1.02;
                col = saturate((col * (2.51 * col + 0.03)) / (col * (2.43 * col + 0.59) + 0.14));
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
