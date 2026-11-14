Shader "UntilSix/Monster Through Walls"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 0.17, 0.08, 1)
        _Width("Outline width", Float) = 0.035
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComparison("Stencil comparison", Float) = 6
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil pass", Float) = 0
        _ColorMask("Color mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" }
        Pass
        {
            ZTest Always
            ZWrite Off
            ColorMask [_ColorMask]
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp [_StencilComparison]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Width;
            CBUFFER_END

            float4 Vert(float3 position : POSITION, float3 normal : NORMAL) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(position);
                return TransformWorldToHClip(world + TransformObjectToWorldNormal(normal) * _Width);
            }

            half4 Frag() : SV_Target
            {
                return _Tint;
            }
            ENDHLSL
        }
    }
}
