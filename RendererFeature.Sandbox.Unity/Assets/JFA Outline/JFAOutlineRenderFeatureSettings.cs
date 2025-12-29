using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[Serializable]
public class JFAOutlineRenderFeatureSettings
{
    [Header("Filters")]
    public bool renderInSceneView = false;
    public bool renderInPreviewCameras = false;
    public bool renderInOverlayCameras = false;

    [Header("Outline Attributes")]
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    public LayerMask outlineLayer = 0;

    [Min(1f)]
    public float outlineWidthPixels = 4f;
    public Color outlineColor = Color.blue;

    [Header("Shaders")]
    public Shader seedShader;
    public ComputeShader jfaComputeStepShader;
    public Shader outlineShader;
}
