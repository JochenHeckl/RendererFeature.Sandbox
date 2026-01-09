Shader "JFA Outline - Outline"
{
    Properties
    {
        outlineWidth("Outline Width (Pixels)", Float) = 1
        outlineColor("Outline Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "JFA Outline - Outline"

            // Fullscreen overlay
            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma enable_d3d11_debug_symbols
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(sdfBuffer);

            float outlineWidth;
            float4 outlineColor;

            struct VSInput
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PSInput
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            PSInput Vert(VSInput input)
            {
                PSInput output;

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            float4 Frag(PSInput input) : SV_Target
            {
                float4 sdfValue = SAMPLE_TEXTURE2D(sdfBuffer, sampler_PointClamp, input.texcoord);
                float distSq = sdfValue.z;
                float outlineWidthSquared = outlineWidth * outlineWidth;

                
                // (distSq == 0) is a seed pixel (so no outline)                
                if ((distSq > 0.0) && (distSq <= outlineWidthSquared))
                {
                    return outlineColor;
                }

                return float4(0.0, 0.0, 0.0, 0.0);
            }
            
            ENDHLSL
        }
    }
}
