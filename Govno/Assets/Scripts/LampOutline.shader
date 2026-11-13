Shader "Govno/Lamp Outline"
{
    Properties
    {
        _OutlineColor("Color", Color) = (1, 0.86, 0.1, 1)
        _OutlineWidth("Width", Float) = 0.018
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Cull Front
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                // Built-in lamp primitives are centered. Radial expansion keeps hard edges joined.
                float3 radialWS = SafeNormalize(positionWS - TransformObjectToWorld(float3(0, 0, 0)));
                return TransformWorldToHClip(positionWS + radialWS * _OutlineWidth);
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
