using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Jump Flood Algorithm (JFA) based screen-space outline for URP (Unity 6.x / URP 17+).
///
/// Pipeline overview:
/// 1) Render selected objects into a float seed texture storing per-pixel screen coordinates.
/// 2) Run Jump Flood iterations in a compute shader to propagate nearest seed + squared distance.
/// 3) Composite outline on top of the camera color target.
/// </summary>
public class JFAOutlineRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private JFAOutlineRenderFeatureSettings settings = new();
    Material seedMaterial;
    Material outlineMaterial;
    private JFAOutlinePass pass;

    public override void Create()
    {
        ValidateSettings();

        seedMaterial = CoreUtils.CreateEngineMaterial(settings.seedShader);
        outlineMaterial = CoreUtils.CreateEngineMaterial(settings.outlineShader);

        pass = new JFAOutlinePass(settings, seedMaterial, outlineMaterial);
    }

    protected override void Dispose(bool disposing)
    {
        pass = null;
        CoreUtils.Destroy(outlineMaterial);
        CoreUtils.Destroy(seedMaterial);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogWarning("Jfa outline render feature is not supported on this hardware.");
            return;
        }

        var cameraData = renderingData.cameraData;

        if (!settings.renderInOverlayCameras && cameraData.renderType == CameraRenderType.Overlay)
        {
            return;
        }

        if (cameraData.isSceneViewCamera)
        {
            if (!settings.renderInSceneView)
            {
                return;
            }
        }
        else if (cameraData.cameraType == CameraType.Preview)
        {
            if (!settings.renderInPreviewCameras)
            {
                return;
            }
        }
        else if (cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        if (settings.outlineLayer.value == 0)
        {
            return;
        }

        renderer.EnqueuePass(pass);
    }

    private void ValidateSettings()
    {
        if (settings.seedShader == null)
        {
            throw new InvalidDataException(
                "You must supply a seed shader. If in doubt, drag JFAOutlineSeed.shader here that comes with this package."
            );
        }

        if (settings.outlineShader == null)
        {
            throw new InvalidDataException(
                "You must supply an outline shader. If in doubt, drag JFAOutline.shader here that comes with this package."
            );
        }

        if (settings.jfaComputeStepShader == null)
        {
            throw new InvalidDataException(
                "You must supply a jfaComputeStepShader. If in doubt, drag JFAComputeStep.compute here that comes with this package."
            );
        }
    }
}
