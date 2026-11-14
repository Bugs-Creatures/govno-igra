Shader "Govno/Lamp Outline"
{
    Properties
    {
        _OutlineColor("Color", Color) = (1, 0.86, 0.1, 1)
        _OutlineWidth("Width", Float) = 0.018
        [HideInInspector] _OutlineCenter("Mesh Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _OutlineCull("Cull", Float) = 1
        [HideInInspector] _OutlineZTest("Depth Test", Float) = 4
        [HideInInspector] _OutlineColorMask("Color Mask", Float) = 15
        [HideInInspector] _OutlineStencilRef("Stencil Ref", Float) = 0
        [HideInInspector] _OutlineStencilWriteMask("Stencil Write Mask", Float) = 0
        [HideInInspector] _OutlineStencilCompare("Stencil Compare", Float) = 8
        [HideInInspector] _OutlineStencilPass("Stencil Pass", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Cull [_OutlineCull]
        ColorMask [_OutlineColorMask]
        ZWrite Off
        ZTest [_OutlineZTest]
        Stencil
        {
            Ref [_OutlineStencilRef]
            ReadMask 1
            WriteMask [_OutlineStencilWriteMask]
            Comp [_OutlineStencilCompare]
            Pass [_OutlineStencilPass]
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ BOX_SILHOUETTE SILHOUETTE_MASK OBJECT_SILHOUETTE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineCenter;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(positionOS);
                // Mesh centers can differ from their pivots, especially on hinged parts.
                float3 radialWS = SafeNormalize(positionWS - TransformObjectToWorld(_OutlineCenter.xyz));
                #if !defined(SILHOUETTE_MASK)
                    positionWS += radialWS * _OutlineWidth;
                #endif

                #if defined(BOX_SILHOUETTE)
                    // Pull the outer edge toward the camera along the same sight line.
                    // This keeps its screen position and clears the floor at the base.
                    float3 view = GetWorldSpaceNormalizeViewDir(positionWS);
                    float bias = _OutlineWidth * max(1.0, rcp(max(abs(view.y), 0.12)));
                    positionWS += view * bias;
                #endif
                #if defined(OBJECT_SILHOUETTE)
                    // Keep the bottom edge visible where the object touches a surface.
                    positionWS += GetWorldSpaceNormalizeViewDir(positionWS) * _OutlineWidth;
                #endif

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionOS = TransformWorldToObject(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(BOX_SILHOUETTE)
                    // Remove rays through the original cube. Only the expanded silhouette remains.
                    float3 positionWS = TransformObjectToWorld(input.positionOS);
                    float3 ray = TransformWorldToObjectDir(-GetWorldSpaceNormalizeViewDir(positionWS), false);
                    float3 inverseRay = lerp(-1.0, 1.0, step(0.0, ray)) / max(abs(ray), 1e-6);
                    float3 a = (-0.5 - input.positionOS) * inverseRay;
                    float3 b = (0.5 - input.positionOS) * inverseRay;
                    float3 nearHit = min(a, b);
                    float3 farHit = max(a, b);
                    float enter = max(nearHit.x, max(nearHit.y, nearHit.z));
                    float leave = min(farHit.x, min(farHit.y, farHit.z));
                    if (enter <= leave)
                        discard;
                #endif

                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
