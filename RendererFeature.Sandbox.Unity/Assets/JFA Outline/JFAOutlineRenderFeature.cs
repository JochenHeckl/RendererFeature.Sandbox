using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class JFAOutlineRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private JFAOutlineRenderFeatureSettings settings = new();
    Material seedMaterial;
    Material outlineMaterial;
    private JFAOutlinePass pass;

    public override void Create()
    {
        if (ValidateSettings())
        {
            seedMaterial = CoreUtils.CreateEngineMaterial(settings.seedShader);
            outlineMaterial = CoreUtils.CreateEngineMaterial(settings.outlineShader);

            pass = new JFAOutlinePass(settings, seedMaterial, outlineMaterial);
        }
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

        renderer.EnqueuePass(pass);
    }

    private bool ValidateSettings()
    {
        if (settings.seedShader == null)
        {
            Debug.LogError(
                "You must supply a seed shader. If in doubt, drag JFAOutlineSeed.shader here that comes with this package."
            );

            return false;
        }

        if (settings.outlineShader == null)
        {
            Debug.LogError(
                "You must supply an outline shader. If in doubt, drag JFAOutline.shader here that comes with this package."
            );

            return false;
        }

        if (settings.jfaComputeStepShader == null)
        {
            Debug.LogError(
                "You must supply a jfaComputeStepShader. If in doubt, drag JFAComputeStep.compute here that comes with this package."
            );

            return false;
        }

        if (settings.outlineLayer.value == 0)
        {
            Debug.LogError("JFA Outline layer mask is 0. No outlines will be drawn.");
            return false;
        }

        return true;
    }
}
