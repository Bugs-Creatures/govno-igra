Shader "UntilSix/Monster Footprint"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Footprint", 2D) = "white" {}
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend DstColor Zero
            ColorMask RGB
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
            CBUFFER_END

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.uv = uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 print = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half dirt = lerp(0.26h, 0.52h, saturate(print.r * 3.0h));
                half3 stain = dirt * half3(1.0h, 0.77h, 0.58h);
                return half4(lerp(half3(1, 1, 1), stain, print.a * _Opacity), 1);
            }
            ENDHLSL
        }
    }
}
