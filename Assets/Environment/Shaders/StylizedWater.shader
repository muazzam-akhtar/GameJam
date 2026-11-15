// Stylized stream water: turquoise with flowing light stripes and pale foam along the banks.
// The mesh is a ribbon: uv.x runs across the stream (0 = left bank, 1 = right bank), uv.y along it (in tiles).
Shader "GameJam/StylizedWater"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.08, 0.62, 0.72, 1)
        _ShallowColor ("Shallow Color", Color) = (0.32, 0.86, 0.88, 1)
        _FoamColor ("Foam / Highlight Color", Color) = (0.86, 1, 1, 1)
        _FlowSpeed ("Flow Speed (tiles / s)", Float) = 0.55
        _StripeDensity ("Stripe Density", Float) = 2.2
        _StripeAmount ("Stripe Amount", Range(0, 1)) = 0.55
        _FoamWidth ("Bank Foam Width", Range(0, 0.5)) = 0.16
        _Glow ("Self Brightness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _StripeDensity;
                float _StripeAmount;
                float _FoamWidth;
                float _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fogFactor : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = i.uv.x;
                float centre = 1.0 - abs(across * 2.0 - 1.0);                 // 0 at the banks, 1 in the middle
                float flow = i.uv.y - _Time.y * _FlowSpeed;

                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.05, 0.75, centre));

                // flowing streaks: stretched noise bands moving downstream
                float n1 = Noise(float2(across * 7.0, flow * _StripeDensity));
                float n2 = Noise(float2(across * 13.0 + 3.1, flow * _StripeDensity * 1.9 + 7.7));
                float streak = smoothstep(0.62, 0.78, n1 * 0.65 + n2 * 0.35);
                // ripple lines across the flow
                float ripple = smoothstep(0.86, 0.95, frac(flow * 1.6 + n2 * 0.7)) * smoothstep(0.15, 0.6, centre);
                // foam hugging the banks
                float foam = smoothstep(_FoamWidth, _FoamWidth * 0.25, min(across, 1.0 - across)) * (0.55 + 0.45 * n1);

                col = lerp(col, _FoamColor.rgb, saturate(max(streak * _StripeAmount, max(ripple * 0.5, foam))));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 n = normalize(i.normalWS);
                half lit = saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation;
                col *= SampleSH(n) * 0.75 + mainLight.color * (0.35 + 0.5 * lit) + _Glow;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
}
