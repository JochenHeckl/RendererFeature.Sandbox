Shader "Custom/NewUnlitUniversalRenderPipelineShader"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "JFA Outline - Seed"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct VSOut
            {
                float4 positionHCS : SV_Position;
            };

            VSOut vert(Attributes input)
            {
                VSOut output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(VSOut input) : SV_Target
            {
                float2 pixelPos = input.positionHCS.xy;

                float sdfDistance = 0.0;  // seed distance
                float idPlaceholder = 0.0; // future use (instance id, mask, etc.)

                return float4(
                    pixelPos.x,
                    pixelPos.y,
                    sdfDistance,
                    idPlaceholder
                );
            }

            ENDHLSL
        }
    }
}
