// Blur + composite for SelectiveGlowFeature (dual-Kawase pyramid, like Bloom).
//  Pass 0  Down       - 5-tap dual-Kawase downsample
//  Pass 1  Up         - 8-tap dual-Kawase (tent) upsample
//  Pass 2  Composite  - tight + wide glow, dimmed over the object itself, added to the HDR camera colour
Shader "Hidden/GameJam/SelectiveGlow"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _Spread;
        float _Intensity;
        float _WideWeight;
        float _NarrowWeight;
        float _InnerDim;
        TEXTURE2D_X(_GlowNarrow);

        half4 S(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv); }

        // destination texel size in uv (constant over a fullscreen triangle)
        float2 DestTexel(float2 uv) { return abs(float2(ddx(uv.x), ddy(uv.y))); }
        ENDHLSL

        Pass
        {
            Name "Down"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float2 o = DestTexel(uv) * 0.5 * _Spread;
                half4 s = S(uv) * 4.0;
                s += S(uv - o);
                s += S(uv + o);
                s += S(uv + float2(o.x, -o.y));
                s += S(uv - float2(o.x, -o.y));
                return s * 0.125;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float2 o = DestTexel(uv) * _Spread;
                half4 s = S(uv + float2(-o.x * 2.0, 0.0));
                s += S(uv + float2(-o.x, o.y)) * 2.0;
                s += S(uv + float2(0.0, o.y * 2.0));
                s += S(uv + float2(o.x, o.y)) * 2.0;
                s += S(uv + float2(o.x * 2.0, 0.0));
                s += S(uv + float2(o.x, -o.y)) * 2.0;
                s += S(uv + float2(0.0, -o.y * 2.0));
                s += S(uv + float2(-o.x, -o.y)) * 2.0;
                return s * (1.0 / 12.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half3 wide = S(uv).rgb;
                half4 narrow = SAMPLE_TEXTURE2D_X(_GlowNarrow, sampler_LinearClamp, uv);
                // soft silhouette from the already-blurred tight level (alpha) -> no aliased edge
                half mask = smoothstep(0.25, 0.85, narrow.a);
                half3 glow = wide * _WideWeight + narrow.rgb * _NarrowWeight;
                glow *= lerp(1.0, 1.0 - _InnerDim, mask);          // light around the object, not over it
                return half4(glow * _Intensity, 0.0);
            }
            ENDHLSL
        }
    }
}
