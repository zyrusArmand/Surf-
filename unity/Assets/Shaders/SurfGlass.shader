// A glassed surfboard: the paint lives in the vertex colours, lit by the sun with a tight
// resin highlight and a sky-tinted fresnel at the rails, so the pour reads as a finish on a
// shaped object rather than a flat decal.
Shader "Surf/Glass"
{
    Properties
    {
        _Gloss ("Gloss", Float) = 0.82
        _Spec ("Specular", Float) = 0.55
        _Fresnel ("Rail sheen", Float) = 0.35
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Gloss, _Spec, _Fresnel;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float4 col : COLOR; };

            V vert(A i)
            {
                V o;
                o.w = TransformObjectToWorld(i.pos.xyz);
                o.n = TransformObjectToWorldNormal(i.n);
                o.col = i.col;
                o.pos = TransformWorldToHClip(o.w);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 N = normalize(i.n);
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.w));
                float3 ld = normalize(L.direction);
                float diff = saturate(dot(N, ld));
                float3 amb = SampleSH(N);
                float3 col = i.col.rgb * (amb + L.color * diff * 1.1);   // no self-shadow: the caster pass has no bias of its own
                float3 H = normalize(ld + V);
                float sp = pow(saturate(dot(N, H)), lerp(16.0, 160.0, _Gloss)) * _Spec;
                float fr = pow(1.0 - saturate(dot(N, V)), 4.0) * _Fresnel;
                col += L.color * sp + amb * fr;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
