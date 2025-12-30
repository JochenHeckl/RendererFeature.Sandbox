using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal class JFAOutlinePass : ScriptableRenderPass
{
    private const string seedPassName = "JFA Outline - Seed";
    private const string computeStepPassName = "JFA Outline - Jump Flood Compute Step";
    private const string outlinePassName = "JFA Outline - Outline";

    private sealed class SeedPassData
    {
        public RendererListHandle objectsToDraw;
        public Color clearColor;
    }

    private sealed class JFAStepPassData
    {
        public ComputeShader compute;
        public int kernel;
        public TextureHandle input;
        public TextureHandle output;
        public int width;
        public int height;
        public int jump;
        public int tgSizeX;
        public int tgSizeY;
    }

    private sealed class OutlinePassData
    {
        public TextureHandle sdfBufferTexture;
        public Material outlineMaterial;
        public float outlineWidth;
        public Color outlineColor;
    }

    private static readonly ProfilingSampler seedProfilingSampler = new ProfilingSampler(
        seedPassName
    );
    private static readonly ProfilingSampler outlineProfilingSampler = new ProfilingSampler(
        outlinePassName
    );

    private static readonly Color seedClearColor = new Color(-1f, -1f, 1e10f, 1f);

    private static readonly int inputBufferId = Shader.PropertyToID("inputBuffer");
    private static readonly int outputBufferId = Shader.PropertyToID("outputBuffer");
    private static readonly int bufferResolutionId = Shader.PropertyToID("bufferResolution");
    private static readonly int jumpDistanceId = Shader.PropertyToID("jumpDistance");
    private static readonly int sdfBufferId = Shader.PropertyToID("sdfBuffer");
    private static readonly int outlineWidthId = Shader.PropertyToID("outlineWidth");
    private static readonly int outlineColorId = Shader.PropertyToID("outlineColor");

    // Common URP shader tag set for drawing scene geometry.
    private static readonly List<ShaderTagId> shaderTagIds = new()
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
    };

    private readonly JFAOutlineRenderFeatureSettings settings;

    private Material outlineMaterial;
    private Material seedMaterial;

    private int jfaKernel;
    private int threadGroupSizeX = 8;
    private int threadGroupSizeY = 8;

    public JFAOutlinePass(
        JFAOutlineRenderFeatureSettings settings,
        Material seedMaterial,
        Material outlineMaterial
    )
    {
        this.settings = settings;
        this.seedMaterial = seedMaterial;
        this.outlineMaterial = outlineMaterial;

        renderPassEvent = settings.renderPassEvent;

        jfaKernel = settings.jfaComputeStepShader.FindKernel("JumpFlood");
        settings.jfaComputeStepShader.GetKernelThreadGroupSizes(
            jfaKernel,
            out uint x,
            out uint y,
            out _
        );

        threadGroupSizeX = Mathf.Max(1, (int)x);
        threadGroupSizeY = Mathf.Max(1, (int)y);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        var renderingData = frameData.Get<UniversalRenderingData>();
        var lightData = frameData.Get<UniversalLightData>();
        var resourceData = frameData.Get<UniversalResourceData>();

        TextureHandle cameraColor = resourceData.activeColorTexture;
        TextureHandle cameraDepth = resourceData.activeDepthTexture;

        // Derive our internal texture layout from the active camera color.
        // This ensures we match render scale and other per-camera target settings.
        TextureDesc sdfBufferDesc = renderGraph.GetTextureDesc(cameraColor);
        int width = sdfBufferDesc.width;
        int height = sdfBufferDesc.height;

        // We store seed coords + distSq as float4.
        sdfBufferDesc.depthBufferBits = DepthBits.None;
        sdfBufferDesc.msaaSamples = MSAASamples.None;
        sdfBufferDesc.format = GraphicsFormat.R32G32B32A32_SFloat;
        sdfBufferDesc.clearBuffer = false;
        sdfBufferDesc.enableRandomWrite = true;

        sdfBufferDesc.name = "JFA_Outline_sdfBuffer 0";
        var buffer0 = renderGraph.CreateTexture(sdfBufferDesc);
        sdfBufferDesc.name = "JFA_Outline_sdfBuffer 1";
        var buffer1 = renderGraph.CreateTexture(sdfBufferDesc);

        var sdfBuffers = new TextureHandle[2] { buffer0, buffer1 };

        using (
            var builder = renderGraph.AddRasterRenderPass<SeedPassData>(
                seedPassName,
                out var passData,
                seedProfilingSampler
            )
        )
        {
            passData.clearColor = seedClearColor;
            passData.objectsToDraw = CreateSeedRendererList(
                renderGraph,
                renderingData,
                cameraData,
                lightData
            );

            builder.UseRendererList(passData.objectsToDraw);
            builder.SetRenderAttachment(sdfBuffers[0], 0, AccessFlags.Write);

            // // Optional depth test against the scene depth. We can only bind depth if MSAA matches.
            // if (cameraDepth.IsValid())
            // {
            //     var depthDesc = renderGraph.GetTextureDesc(cameraDepth);
            //     if (depthDesc.msaaSamples == sdfBufferDesc.msaaSamples)
            //     {
            //         builder.SetRenderAttachmentDepth(cameraDepth, AccessFlags.Read);
            //     }
            // }

            builder.SetRenderFunc(
                static (SeedPassData data, RasterGraphContext context) =>
                {
                    // Clear only the seed color target.
                    context.cmd.ClearRenderTarget(false, true, data.clearColor);
                    context.cmd.DrawRendererList(data.objectsToDraw);
                }
            );
        }

        // Jump Flood iterations.
        // Start at the highest power-of-two <= OutlineWidth / 2.
        int jump = Mathf.NextPowerOfTwo(Mathf.CeilToInt(settings.outlineWidthPixels)) / 2;
        jump = Mathf.Max(1, jump);

        var nextCurrentIndex = 0;
        TextureHandle current = sdfBuffers[nextCurrentIndex];
        TextureHandle next = sdfBuffers[nextCurrentIndex == 0 ? 1 : 0];

        while (jump >= 1)
        {
            using (
                var builder = renderGraph.AddComputePass<JFAStepPassData>(
                    $"{computeStepPassName} (jump {jump})",
                    out var passData
                )
            )
            {
                passData.compute = settings.jfaComputeStepShader;
                passData.kernel = jfaKernel;
                passData.input = current;
                passData.output = next;
                passData.width = width;
                passData.height = height;
                passData.jump = jump;
                passData.tgSizeX = threadGroupSizeX;
                passData.tgSizeY = threadGroupSizeY;

                builder.UseTexture(passData.input, AccessFlags.Read);
                builder.UseTexture(passData.output, AccessFlags.Write);

                builder.SetRenderFunc(
                    static (JFAStepPassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeTextureParam(
                            data.compute,
                            data.kernel,
                            inputBufferId,
                            data.input,
                            0
                        );
                        context.cmd.SetComputeTextureParam(
                            data.compute,
                            data.kernel,
                            outputBufferId,
                            data.output,
                            0
                        );
                        context.cmd.SetComputeIntParams(
                            data.compute,
                            bufferResolutionId,
                            data.width,
                            data.height
                        );
                        context.cmd.SetComputeIntParam(data.compute, jumpDistanceId, data.jump);

                        int groupsX = (data.width + data.tgSizeX - 1) / data.tgSizeX;
                        int groupsY = (data.height + data.tgSizeY - 1) / data.tgSizeY;
                        context.cmd.DispatchCompute(data.compute, data.kernel, groupsX, groupsY, 1);
                    }
                );
            }

            jump /= 2;

            nextCurrentIndex = (nextCurrentIndex + 1) % 2;
            current = sdfBuffers[nextCurrentIndex];
            next = sdfBuffers[nextCurrentIndex == 0 ? 1 : 0];
        }

        using (
            var builder = renderGraph.AddRasterRenderPass<OutlinePassData>(
                outlinePassName,
                out var passData,
                outlineProfilingSampler
            )
        )
        {
            passData.sdfBufferTexture = current;
            passData.outlineMaterial = outlineMaterial;
            passData.outlineWidth = settings.outlineWidthPixels;
            passData.outlineColor = settings.outlineColor;

            builder.UseTexture(passData.sdfBufferTexture, AccessFlags.Read);

            // We blend on top of the existing camera color, so we must declare ReadWrite.
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);

            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc(
                static (OutlinePassData data, RasterGraphContext context) =>
                {
                    data.outlineMaterial.SetFloat(outlineWidthId, data.outlineWidth);
                    data.outlineMaterial.SetColor(outlineColorId, data.outlineColor);

                    // Bind the distance texture for sampling.
                    context.cmd.SetGlobalTexture(sdfBufferId, data.sdfBufferTexture);

                    // Fullscreen triangle (vertex shader uses SV_VertexID).
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.outlineMaterial,
                        0,
                        MeshTopology.Triangles,
                        3,
                        1
                    );
                }
            );
        }
    }

    private RendererListHandle CreateSeedRendererList(
        RenderGraph renderGraph,
        UniversalRenderingData renderingData,
        UniversalCameraData cameraData,
        UniversalLightData lightData
    )
    {
        var filtering = new FilteringSettings(RenderQueueRange.all, settings.outlineLayer);

        // Sorting doesn't matter much because we depth-test against the camera depth (when available).
        var sortingCriteria = SortingCriteria.CommonOpaque;
        var drawing = RenderingUtils.CreateDrawingSettings(
            shaderTagIds,
            renderingData,
            cameraData,
            lightData,
            sortingCriteria
        );
        drawing.overrideMaterial = seedMaterial;
        drawing.overrideMaterialPassIndex = 0;

        var rendererListParams = new RendererListParams(
            renderingData.cullResults,
            drawing,
            filtering
        );

        return renderGraph.CreateRendererList(rendererListParams);
    }
}
