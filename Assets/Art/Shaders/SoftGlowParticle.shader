// Additive soft glowing dot for particles (no texture needed). Uses the particle vertex colour.
Shader "GameJam/SoftGlowParticle"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                float a = d * d * (3.0 - 2.0 * d);
                a *= a;                                         // soft core, no hard edge
                return half4(i.color.rgb * (i.color.a * a * _Intensity), 1);
            }
            ENDHLSL
        }
    }
}
