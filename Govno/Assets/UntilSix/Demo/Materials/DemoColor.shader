Shader "UntilSix/Demo Color"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            float4 Vert(float3 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position);
            }

            half4 Frag() : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
