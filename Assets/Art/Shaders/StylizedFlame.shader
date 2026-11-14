// Stylized fire for flame-shaped meshes (URP).
// Reads per-vertex data baked in Blender (vertex colour):
//   R = 0 at the flame's base -> 1 at its tip, G = random flicker phase, B = 1 for sparks/embers.
// Colours are HDR so the scene's Bloom makes the flames glow.
Shader "GameJam/Stylized Flame"
{
    Properties
    {
        [HDR] _CoreColor ("Core (hot base)", Color) = (2.4, 1.35, 0.32, 1)
        [HDR] _MidColor ("Middle", Color) = (2.0, 0.42, 0.05, 1)
        [HDR] _TipColor ("Tip (cooler)", Color) = (1.05, 0.07, 0.02, 1)
        [HDR] _EmberColor ("Sparks / embers", Color) = (3.5, 1.6, 0.4, 1)
        _NoiseScale ("Noise Scale", Float) = 6
        _RiseSpeed ("Rise Speed", Float) = 1.6
        _Turbulence ("Turbulence", Range(0, 1)) = 0.5
        _TipFade ("Tip Fade", Range(0, 1)) = 0.5
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.65
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Additive ("Additive Glow (0 = solid, 1 = pure glow)", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha      // premultiplied: _Additive slides between alpha-blend and additive glow
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _MidColor, _TipColor, _EmberColor;
                float _NoiseScale, _RiseSpeed, _Turbulence, _TipFade, _EdgeSoftness, _Opacity, _Additive;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                return 0.55 * ValueNoise(p) + 0.3 * ValueNoise(p * 2.07 + 7.3) + 0.15 * ValueNoise(p * 4.13 + 1.7);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = i.color.r;                     // base -> tip
                float phase = i.color.g * 37.0;          // per-flame offset
                float ember = i.color.b;
                float time = _Time.y;

                // rising, turbulent fire noise
                float3 p = i.positionWS * _NoiseScale + phase;
                float n = Fbm(p + float3(0, -time * _RiseSpeed * 2.0, 0));
                float pulse = 0.85 + 0.15 * sin(time * 11.0 + phase * 6.2831);
                float tt = saturate(t + (n - 0.5) * _Turbulence);

                // colour ramp: hot core -> orange -> red tips
                half3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.5, tt));
                col = lerp(col, _TipColor.rgb, smoothstep(0.45, 1.0, tt));
                col *= (0.75 + 0.5 * n) * pulse;

                // soft, glowing silhouettes: hotter where the surface faces you, fading edge-on
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float ndv = abs(dot(N, V));
                col *= lerp(0.8, 1.25, ndv);
                float edge = lerp(1.0, smoothstep(0.0, 0.6, ndv), _EdgeSoftness);

                // tips burn away, eaten by the noise so they lick and flicker
                float tipCut = 1.0 - smoothstep(1.0 - _TipFade, 1.05, tt + (n - 0.5) * 0.35);
                float a = _Opacity * edge * tipCut;

                // sparks: small, very bright, flickering embers
                float twinkle = 0.55 + 0.45 * sin(time * 17.0 + phase * 9.0);
                col = lerp(col, _EmberColor.rgb * twinkle, ember);
                a = lerp(a, _Opacity, ember);

                return half4(col * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
