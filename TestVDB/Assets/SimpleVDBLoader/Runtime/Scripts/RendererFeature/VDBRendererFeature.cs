using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class VDBRendererFeature : ScriptableRendererFeature
{
    private VDBRenderPass _vdbPass;
    public override void Create()
    {
        _vdbPass = new VDBRenderPass();
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