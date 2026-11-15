// Alpha-blended soft disc (no texture needed): a dark shadow pool on the ground or a puff of dark smoke.
// Colour = _Color x vertex colour (particles), alpha falls off smoothly from the centre of the quad.
Shader "GameJam/SoftShade"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.8)
        _Softness ("Softness", Range(0.5, 4)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Shade"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                float a = pow(smoothstep(0.0, 0.7, d), _Softness);      // a solid core, soft outer third
                return half4(i.color.rgb, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
