using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;


// TODO kernel indices may have changed due to removed a lot of code

// Add a handle to the output buffer in your pass data
class PassData
{
    public Camera Camera;
    // TODO Rendertexture ?
    public BufferHandle output;
}

public class VDBRenderPass : ScriptableRenderPass
{
    public GraphicsBuffer outputBuffer; // This is useless from scriptable render pass implem

    public RenderingData RenderingDataObject;
    private ComputeShader _volumeShader;
    private int _renderVDBKernelIndex = 0;
    private int _renderVDBShadowKernelIndex = 0;

    ComputeBuffer UnityLightBuffer; // TODO can be common in renderer / pass
    Light[] UnityLights;
    UnityLight[] UnityLightData;


    private void _initLightData()
    {
        UnityLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        //Load Unity Lights
        UnityLightData = new UnityLight[UnityLights.Length];
        for (int i = 0; i < UnityLights.Length; i++)
        {
            Light ThisLight = UnityLights[i];
            Color col = ThisLight.color;
            UnityLightData[i].Position = ThisLight.transform.position;
            UnityLightData[i].Direction = ThisLight.transform.forward;
            UnityLightData[i].Type = (ThisLight.type == LightType.Point) ? 0 : (ThisLight.type == LightType.Directional) ? 1 : (ThisLight.type == LightType.Spot) ? 2 : 3;
            UnityLightData[i].Col = new Vector3(col[0], col[1], col[2]) * ThisLight.intensity;
        }
    }
    // Create the buffer in the render pass constructor
    public VDBRenderPass(ComputeShader volumeShader, int renderVDBKernelIndex, int renderVDBShadingKernelIndex)
    {
        _volumeShader = volumeShader;
        _renderVDBKernelIndex = renderVDBKernelIndex;
        _renderVDBShadowKernelIndex = renderVDBShadingKernelIndex;

        // Create the output buffer as a structured buffer
        // Create the buffer with a length of 5 integers, so the compute shader can output 5 values.
        outputBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 5, sizeof(int));


        UnityLightBuffer = new ComputeBuffer(UnityLights.Length, 40); // TODO automatic stride calculation

        _updateLightData();
        _updateScreenData();
    }
    private void _updateScreenData()
    {
        _volumeShader.SetInt("ScreenWidth", Screen.width);
        _volumeShader.SetInt("ScreenHeight", Screen.height);
    }
    private void _updateLightData()
    {
        _volumeShader.SetBuffer(0, "UnityLights", UnityLightBuffer);
        _volumeShader.SetBuffer(2, "UnityLights", UnityLightBuffer);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        UnityLightBuffer.Release();

    }
    // TODO avoid doing this every frame => cache the VDBs in a list
    private AVDBRenderer[] _getAllActiveVDBInScene(PassData passData)
    {
        var objects = Object.FindObjectsByType<AVDBRenderer>(FindObjectsSortMode.None); // TODO be carefull this might yield objects that we don't want to render
        //var scene = RenderingDataObject.cameraData.camera.scene;
        return objects;
    }
    private void _handleLightData() // This has to be done in renderer feature / pass
    {
        VolumeShader.SetInt("LightCount", UnityLights.Length); // TODO this has to be passed on renderer feature level
        for (int i2 = 0; i2 < UnityLights.Length; i2++)
        {//If any unity lights have changed, reset the lighting data
            Light ThisLight = UnityLights[i2];
            Color col = ThisLight.color;
            if (ThisLight.transform.hasChanged)
            {
                HasChanged = true;
                ThisLight.transform.hasChanged = false;
                UnityLightData[i2].Position = ThisLight.transform.position;
                UnityLightData[i2].Direction = ThisLight.transform.forward;
            }
            int Type = (ThisLight.type == LightType.Point) ? 0 : (ThisLight.type == LightType.Directional) ? 1 : (ThisLight.type == LightType.Spot) ? 2 : 3;
            if (UnityLightData[i2].Type != Type)
            {
                HasChanged = true;
                UnityLightData[i2].Type = Type;
            }
            if (UnityLightData[i2].Type == 1) VolumeShader.SetVector("SunDir", UnityLightData[i2].Direction);
            Vector3 Col = new Vector3(col[0], col[1], col[2]) * ThisLight.intensity;
            if (!UnityLightData[i2].Col.Equals(Col))
            {
                HasChanged = true;
                UnityLightData[i2].Col = Col;
            }
        }
        if (HasChanged) UnityLightBuffer.SetData(UnityLightData);
    }
    private void ExecutePass(PassData data, ComputeGraphContext context)
    {
        var activeVDBs = _getAllActiveVDBInScene();
        // Eventualling frustum culling can be done here
        // TODO sorting of VDBs based on distance to camera ?
        foreach (var vdb in activeVDBs)
        {
            bool shouldRecomputeShading = false;

            context.cmd.SetComputeVectorParam(_volumeShader, "Size", vdb.GetSize());

            var currentVDBShadowBuffer = vdb.GetShadowBuffer();
            context.cmd.SetComputeBufferParam(
                computeShader: _volumeShader,
                kernelIndex: _renderVDBKernelIndex,
                name: "ShadowBuffer",
                buffer: currentVDBShadowBuffer
            );

            if (shouldRecomputeShading)
            {
                context.cmd.SetComputeBufferParam(
                    computeShader: _volumeShader,
                    kernelIndex: _renderVDBShadowKernelIndex,
                    name: "ShadowBuffer",
                    buffer: currentVDBShadowBuffer
                );

                //Calculate the Volume Shading
                context.cmd.DispatchCompute(
                    computeShader: _volumeShader,
                    kernelIndex: _renderVDBShadowKernelIndex,
                    threadGroupsX: Mathf.CeilToInt(ValidVoxelSitesBuffer[i].count / 1023.0f),
                    threadGroupsY: 1,
                    threadGroupsZ: 1
                );
            }

            // TODO pass preprocessed textures stored in VDBRenderer to the compute shader
            context.cmd.DispatchCompute(
                computeShader: _volumeShader,
                kernelIndex: _renderVDBKernelIndex,
                threadGroupsX: Mathf.CeilToInt((float)Screen.width / 8.0f),
                threadGroupsY: Mathf.CeilToInt((float)Screen.height / 8.0f),
                threadGroupsZ: 1
            );

            VolumeShader.SetTextureFromGlobal(0, "_CameraDepthTexture", "_CameraDepthTexture");
            if (Sizes.Length > 1 || CurFrame < 2 || HasChanged)
            {//Rebuild the Volume Texture
                VolumeShader.SetBool("Copy1", false); // TODO no longer in compute shader
                VolumeShader.SetVector("Size", Sizes[(int)Mathf.Floor(CurFrame) % (ValidVoxelSitesBuffer.Length)]);
                VolumeShader.SetBuffer(1, "NonZeroVoxels", ValidVoxelSitesBuffer[i]);
                VolumeShader.SetTexture(1, "DDATextureWrite", VolumeTex);
                VolumeShader.SetTexture(3, "DDATextureWrite", VolumeTex);
                VolumeShader.Dispatch(3, Mathf.CeilToInt(Sizes[i].x / 8.0f), Mathf.CeilToInt(Sizes[i].y / 8.0f), Mathf.CeilToInt(Sizes[i].z / 8.0f));

                VolumeShader.Dispatch(1, Mathf.CeilToInt(ValidVoxelSitesBuffer[i].count / 1023.0f), 1, 1);
                if (ValidVoxelSitesBuffer2[i] != null)
                {
                    VolumeShader.SetBuffer(1, "NonZeroVoxels", ValidVoxelSitesBuffer2[i]);
                    VolumeShader.SetBool("Copy1", true);  // TODO no longer in compute shader
                    VolumeShader.Dispatch(1, Mathf.CeilToInt(ValidVoxelSitesBuffer2[i].count / 1023.0f), 1, 1);
                }

                Graphics.CopyTexture(VolumeTex, VolumeTex2);
            }


            VolumeShader.SetTexture(0, "DDATexture", VolumeTex2);
            VolumeShader.SetTexture(2, "DDATexture", VolumeTex2);
            VolumeShader.SetBuffer(2, "NonZeroVoxels", ValidVoxelSitesBuffer[i]);
            VolumeShader.SetInt("ShadowDistanceOffset", ShadowDistanceOffset);

            if (CurFrame < 2 || HasChanged || Sizes.Length > 1 || true)
            {
                VolumeShader.Dispatch(2, Mathf.CeilToInt(ValidVoxelSitesBuffer[i].count / 1023.0f), 1, 1); //Calculate the Volume Shading
            }


            VolumeShader.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
            VolumeShader.SetMatrix("CameraToWorld", Camera.main.cameraToWorldMatrix);

            var camera = context.cmd.
            VolumeShader.SetFloat("_NearClip", camera.nearClipPlane);
            VolumeShader.SetFloat("_FarClip", camera.farClipPlane);
            this.gameObject.GetComponent<Camera>().Render();
            VolumeShader.SetTexture(0, "MainRenderTexture", TestInputTex);
            VolumeShader.SetTexture(0, "Result", MainTex);
            VolumeShader.SetFloat("_MyTime", Time.realtimeSinceStartup);
            VolumeShader.Dispatch(0, Mathf.CeilToInt((float)Screen.width / 8.0f), Mathf.CeilToInt((float)Screen.height / 8.0f), 1);//Dispatch the main renderer

        }
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextData)
    {
        var universalCameraData = contextData.Get<UniversalCameraData>();
        //var universalResourceData = contextData.Get<UniversalResourceData>();
        //universalCameraData.camera.scene
        // Eventually process light data here
        // Use AddComputePass instead of AddRasterRenderPass.
        using (var builder = renderGraph.AddComputePass("MyComputePass", out PassData data))
        {
            data.Camera = universalCameraData.camera;
            // Use ComputeGraphContext instead of RasterGraphContext.
            builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
        }
    }
}