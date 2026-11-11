// The foam bank at the barrel's impact line: the web's puff sprites, round by construction
// and shaded as whipped cream, each breathing on its own beat. Quads faced to the lens in the
// vertex program (the web's point sprites, which not every phone draws at size). Vertex data:
// position = the puff's centre in the shell's frame, uv = (diameter, seed), uv2 = the corner.
Shader "Surf/FoamBank"
{
    Properties
    {
        _T ("Time", Float) = 0
        _Fade ("Fade", Float) = 0
        _Cut ("Cut", Float) = 0
        _Light ("Daylight", Float) = 1
        _Rider ("Rider", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _T, _Fade, _Cut, _Light;
            float4 _Rider;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct V { float4 pos : SV_POSITION; float2 q : TEXCOORD0; float seed : TEXCOORD1; float d : TEXCOORD2; float rd : TEXCOORD3; };

            V vert(A i)
            {
                V o;
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                // distance to the camera -> rider sightline, so the puffs that would bury him yield
                float3 cr = _Rider.xyz - _WorldSpaceCameraPos;
                float len = max(0.001, length(cr));
                float3 dir = cr / len;
                float t = clamp(dot(wp - _WorldSpaceCameraPos, dir), 0.0, len);
                o.rd = distance(wp, _WorldSpaceCameraPos + dir * t);
                float3 vp = TransformWorldToView(wp);
                o.d = length(vp);
                float b = 1.0 + 0.09 * sin(_T * 1.5 + i.uv.y * 39.0);
                float r = i.uv.x * b * 0.5;
                vp.xy += i.uv2 * r;
                o.pos = TransformWViewToHClip(vp);
                o.q = i.uv2; o.seed = i.uv.y;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float2 q = i.q;
                float d = length(q);
                float at = atan2(q.y, q.x);
                float edge = 0.84 + 0.10 * sin(at * 3.0 + i.seed * 40.0) + 0.06 * sin(at * 5.0 - i.seed * 23.0);
                clip(edge - d);
                float dn = d / edge;
                float shade = sqrt(max(0.0, 1.0 - dn * dn));
                float lit = saturate(0.42 + 0.50 * shade - 0.34 * q.y);
                float3 col = lerp(float3(0.70, 0.79, 0.90), float3(1, 1, 1), lit);
                float a = _Fade * 0.94 * smoothstep(1.0, 0.72, dn);
                a *= smoothstep(2.2, 5.5, i.d);                       // puffs melt as they reach the lens
                a *= 0.10 + 0.90 * smoothstep(1.3, 2.8, i.rd);        // and yield around the rider
                a *= 1.0 - (1.0 - smoothstep(1.30, 3.10, i.d)) * _Cut;
                col = pow(saturate(col), 2.2) * _Light;
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
