// Cumulus on a camera-facing quad. Each cloud is one tile of a baked atlas (SkyKit.CloudAtlas):
// R is how solid the cloud is, G how thick it is front to back, B and A the slope of its front
// surface. A heap of overlapping spheres, cut flat at the base and billowed with noise, is what
// the atlas was made from, so all this shader has to do is light it:
//   - the sun reaches a point through the cloud between it and the sun, found by four taps
//     marched toward the sun across the sprite on ever blurrier mips (a soft cone, not a ray);
//   - what survives that is the straight beam plus a floor for the light that has scattered
//     round inside a few times, which is why the shade side of a cumulus is grey, not black;
//   - every billow is turned to the sun by its own slope, wrapped so the terminator is soft;
//   - looking toward the sun, light comes through wherever the cloud is thin: the silver lining,
//     strong only near the sun (forward scattering), and hotter than white so the bloom finds it;
//   - the sky lights the rest, blue-white from above and darker off the sea, with a flat grey
//     base; when the sun is low the base burns with its glow; low clouds sink into the haze.
// Every colour is the web's raw sRGB number, converted at the end like the sky itself.
// Per vertex: uv0 the atlas, uv1 the tile's rect (u, v, width, height), colour alpha the opacity.
Shader "Surf/Cumulus"
{
    Properties
    {
        _Atlas ("Cloud atlas (linear)", 2D) = "black" {}
        _SunDir ("Light direction (world, toward the light)", Vector) = (0.3, 0.5, -0.8, 0)
        _SunCol ("Light colour (raw)", Vector) = (1, 0.97, 0.92, 1)
        _Top ("Sky zenith (raw)", Vector) = (0.25, 0.56, 0.84, 1)
        _Mid ("Sky mid (raw)", Vector) = (0.56, 0.82, 0.96, 1)
        _Hz ("Sky horizon (raw)", Vector) = (0.85, 0.93, 0.98, 1)
        _Under ("Underside glow (raw rgb, amount)", Vector) = (1, 0.7, 0.4, 0)
        _Light ("Light amount", Float) = 1
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
            float4 _Atlas_ST;
            float4 _SunDir, _SunCol, _Top, _Mid, _Hz, _Under;
            float _Light;
            CBUFFER_END
            TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 tile : TEXCOORD1; float4 col : COLOR; };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 tile : TEXCOORD1;
                float3 lb : TEXCOORD2;      // the light in the quad's own frame: right, up, toward the lens
                float3 wpos : TEXCOORD3;
                float op : TEXCOORD4;
            };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.wpos = TransformObjectToWorld(i.pos.xyz);
                o.uv = i.uv; o.tile = i.tile; o.op = i.col.a;
                float3 r = TransformObjectToWorldDir(float3(1, 0, 0));
                float3 u = TransformObjectToWorldDir(float3(0, 1, 0));
                float3 f = TransformObjectToWorldDir(float3(0, 0, 1));    // the quad is turned to the lens, so +z points away from it
                float3 L = normalize(_SunDir.xyz);
                o.lb = float3(dot(L, r), dot(L, u), -dot(L, f));
                return o;
            }

            float HG(float c, float g) { float g2 = g * g; return (1.0 - g2) / pow(max(1e-3, 1.0 + g2 - 2.0 * g * c), 1.5); }

            half4 frag(V i) : SV_Target
            {
                float2 uv = i.uv; float4 tile = i.tile;
                float4 t = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, uv);
                float a = t.r * i.op;
                if (a < 0.003) discard;
                float2 loc = (uv - tile.xy) / max(tile.zw, float2(1e-5, 1e-5));
                float thick = t.g;
                float3 n = float3(t.b * 2.0 - 1.0, t.a * 2.0 - 1.0, 0.0);
                n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
                float3 L = normalize(i.lb);
                // light crossing the cloud on its way here: four taps toward the sun, the later
                // ones on blurrier mips, clamped inside this cloud's own tile
                float2 st = L.xy * tile.zw * float2(44.0 / 256.0, 44.0 / 128.0);
                float2 lo = tile.xy + tile.zw * 0.004, hi = tile.xy + tile.zw * 0.996;
                float tau = 0.0;
                tau += SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_Atlas, clamp(uv + st * 0.12, lo, hi), 0.5).g * 0.55;
                tau += SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_Atlas, clamp(uv + st * 0.30, lo, hi), 1.5).g * 0.75;
                tau += SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_Atlas, clamp(uv + st * 0.58, lo, hi), 2.5).g * 0.95;
                tau += SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_Atlas, clamp(uv + st * 1.00, lo, hi), 3.5).g * 1.10;
                tau += thick * saturate(-L.z) * 1.3;          // and through its own depth when the sun is behind it
                // the straight beam, and under it the light scattered round inside a few times
                float shadow = max(exp(-tau * 0.85), exp(-tau * 0.22) * 0.42);
                // each billow turned to the sun, wrapped so the terminator is soft like vapour
                float wrap = saturate(dot(n, L) * 0.5 + 0.5);
                // powder: the thin rim facing the sun is a little darker than the body behind it
                float powder = 1.0 - 0.35 * exp(-thick * 9.0) * saturate(L.z + 0.2);
                // silver lining: looking toward the sun, light comes through where the cloud is thin
                float3 vdir = normalize(i.wpos - GetCameraPositionWS());
                float cs = dot(vdir, normalize(_SunDir.xyz));
                float phase = HG(cs, 0.55) * 0.16 + 0.10;
                float thru = exp(-thick * 3.5);
                float hgt = saturate((loc.y - 0.19) / 0.62);  // 0 at the flat base, 1 at the crowns
                float direct = (0.38 + 0.62 * wrap) * shadow * powder;
                float silver = thru * phase;
                // the sky's own light: blue-white from above, darker and warmer off the sea below,
                // and a flat grey base where the cloud sits on its own shadow
                float3 skyUp = lerp(_Mid.rgb, float3(1.0, 1.0, 1.0), 0.70) * 0.92;   // white-topped: the sky above a cumulus is mostly the sun's own scatter
                float3 skyDn = lerp(_Hz.rgb, _Mid.rgb, 0.35) * 0.52;
                float3 amb = lerp(skyDn, skyUp, saturate(n.y * 0.35 + 0.30 + hgt * 0.55));
                amb *= lerp(0.80, 1.0, smoothstep(0.0, 0.45, hgt));
                float3 col = amb * (0.45 + 0.55 * _Light) + _SunCol.rgb * (direct * 0.85 + silver);
                // the sun under the cloud base at either end of the day: the underside burns
                float under = (1.0 - smoothstep(0.0, 0.55, hgt)) * _Under.a * saturate(0.25 - n.y * 0.5 + 0.5 * thru + 0.4);
                col = lerp(col, _Under.rgb * (0.80 + 0.40 * wrap), under * 0.78);
                // the further down toward the horizon, the more of its haze is in front of it
                float haze = 1.0 - smoothstep(0.0, 0.20, saturate(vdir.y));
                col = lerp(col, _Hz.rgb, haze * 0.45);
                a *= 1.0 - haze * 0.25;
                col = pow(clamp(col, 0.0, 1.6), 2.2);
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
