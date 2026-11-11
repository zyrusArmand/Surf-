// The sky dome, the web game's shader line for line: horizon through mid to zenith by height,
// the sun's glow as a core, a wedge along the horizon and a ring, low bands near the sea, and
// the whole thing worked out in plain sRGB numbers and converted to linear at the end, which
// is how the web gets its colour. The sun disc itself is the one addition.
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
            float4 _Top, _Mid, _Horizon, _Glow, _SunDir;
            float _GlowAmt, _SunX, _SunSize, _Blend;
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 d : TEXCOORD0; };
            V vert(A i){ V o; o.pos = UnityObjectToClipPos(i.pos); o.d = i.pos.xyz; return o; }
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
                // the sun disc, cut at the horizon
                float3 L = normalize(_SunDir.xyz);
                float cosA = dot(vP, L);
                float disc = smoothstep(cos(_SunSize * 1.15), cos(_SunSize * 0.85), cosA);
                float rim = pow(max(cosA, 0.0), 180.0) * 0.45;
                float above = smoothstep(-0.02, 0.01, vP.y);
                c += (_Glow.rgb * rim + float3(1.3, 1.2, 1.05) * _Glow.rgb * disc) * above * saturate(_GlowAmt * 2.0 + 0.3);
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
