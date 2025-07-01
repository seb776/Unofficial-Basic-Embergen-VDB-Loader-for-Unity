using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class VDBRendererFeature : ScriptableRendererFeature
{
    private VDBRenderPass _vdbPass;
    private ComputeShader _volumeShader;
    private int _renderVDBKernelIndex;
    private int _renderVDBShadowKernelIndex;

    public override void Create()
    {
        _volumeShader = Resources.Load<ComputeShader>("RenderVolume");
        _renderVDBKernelIndex = _volumeShader.FindKernel("RenderVolumetric");
        _renderVDBShadowKernelIndex = _volumeShader.FindKernel("ShadeComputation");
        _vdbPass = new VDBRenderPass(_volumeShader, _renderVDBKernelIndex, _renderVDBShadowKernelIndex);

    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_vdbPass == null)
        {
            return;
        }
        _vdbPass.RenderingDataObject = renderingData;
        renderer.EnqueuePass(_vdbPass);
    }
}