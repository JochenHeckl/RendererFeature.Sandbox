Shader "JFA Outline - Seed"
{
    SubShader
    {
        // Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "JFA Outline - Seed"
            // Tags { "LightMode" = "UniversalForward" }

            // We only need depth testing (when a depth attachment is bound),
            // but we do NOT want to modify the camera depth.
            ZWrite Off
            ZTest Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertexShader
            #pragma fragment PixelShader
            #pragma multi_compile_instancing

            struct VSInput
            {
                float3 positionOS : POSITION;
            };

            struct PSInput
            {
                float2 position : SV_Position;
            };

            struct PSOutput
            {
                float2 position;
                float distance;
                float instanceId; // reserved for future use in instanceId
            };

            PSInput VertexShader(VSInput input)
            {
                PSInput output;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                return output;
            }

            PSOutput PixelShader(SeedData input) : SV_Target
            {
                return PSOutput(input.position.xy, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
