Shader "PixelFlow/OutlineCube"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness (world units)", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 smoothNormalOS = normalize(IN.positionOS.xyz);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(smoothNormalOS);
                float3 expandedWS = worldPos + worldNormal * _OutlineThickness;

                OUT.positionHCS = TransformWorldToHClip(expandedWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
