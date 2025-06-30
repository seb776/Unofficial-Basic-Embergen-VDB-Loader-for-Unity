using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;


// TODO kernel indices may have changed due to removed a lot of code

VolumeShader = Resources.Load<ComputeShader>("RenderVolume");
var kernel = VolumeShader.FindKernel("RenderVolumetric");
ComputeBuffer UnityLightBuffer; // TODO can be common in renderer / pass
Light[] UnityLights;
UnityLight[] UnityLightData;

UnityLightBuffer = new ComputeBuffer(UnityLights.Length, 40); // TODO automatic stride calculation
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
UnityLightBuffer.Release();
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

VolumeShader.SetBuffer(2, "ShadowBuffer", ShadowBuffer);
VolumeShader.SetBuffer(0, "ShadowBuffer", ShadowBuffer);
VolumeShader.SetBuffer(1, "ShadowBuffer", ShadowBuffer);
VolumeShader.SetBuffer(3, "ShadowBuffer", ShadowBuffer);
VolumeShader.SetBuffer(2, "UnityLights", UnityLightBuffer);
VolumeShader.SetBuffer(0, "UnityLights", UnityLightBuffer);
VolumeShader.SetInt("ScreenWidth", Screen.width);
VolumeShader.SetInt("ScreenHeight", Screen.height);

VolumeShader.SetVector("Size", VDBFile.Size);
// Add a handle to the output buffer in your pass data
class PassData
{
    // TODO Rendertexture ?
    public BufferHandle output;
}
public class VDBRenderPass : ScriptableRenderPass
{
    public GraphicsBuffer outputBuffer;
    public RenderingData RenderingDataObject;

    // Create the buffer in the render pass constructor
    public VDBRenderPass()
    {
        // Create the output buffer as a structured buffer
        // Create the buffer with a length of 5 integers, so the compute shader can output 5 values.
        outputBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 5, sizeof(int));
    }

    // TODO avoid doing this every frame => cache the VDBs in a list
    private VDBRenderer[] _getAllActiveVDBInScene()
    {
        var objects = Object.FindObjectsByType<VDBRenderer>(FindObjectsSortMode.None);
        var scene = RenderingDataObject.cameraData.camera.scene;
        return objects;
    }

    private void ExecutePass(PassData data, ComputeGraphContext context)
    {
        var activeVDBs = _getAllActiveVDBInScene();
        // TODO sorting of VDBs based on distance to camera ?
        foreach (var vdb in activeVDBs)
        {
            // TODO pass preprocessed textures stored in VDBRenderer to the compute shader
            context.cmd.DispatchCompute(
            computeShader: context.renderGraph.GetComputeShader("MyComputeShader"), // Replace with your compute shader
            kernelIndex: 0, // Replace with the appropriate kernel index
            threadGroupsX: 1, // Adjust based on your compute shader requirements
            threadGroupsY: 1,
            threadGroupsZ: 1
        );
        }
    }
    void OnRenderObject()
    {
        // Solution comes from here
        // https://discussions.unity.com/t/unity-6-urp-depth-texture-is-black-not-available/1560743/3
        // Global texture no lkonger works the same since unity 6, but it's still accessible there
        VolumeShader.SetTextureFromGlobal(0, "_CameraDepthTexture", "_CameraDepthTexture");
    }
    // This has to be completely redeigned to fit ExecutePass
    public void Render()
    {
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

        var camera = this.gameObject.GetComponent<Camera>();
        VolumeShader.SetFloat("_NearClip", camera.nearClipPlane);
        VolumeShader.SetFloat("_FarClip", camera.farClipPlane);
        this.gameObject.GetComponent<Camera>().Render();
        VolumeShader.SetTexture(0, "MainRenderTexture", TestInputTex);
        VolumeShader.SetTexture(0, "Result", MainTex);
        VolumeShader.SetFloat("_MyTime", Time.realtimeSinceStartup);
        VolumeShader.Dispatch(0, Mathf.CeilToInt((float)Screen.width / 8.0f), Mathf.CeilToInt((float)Screen.height / 8.0f), 1);//Dispatch the main renderer

    }


    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextData)
    {
        // Use AddComputePass instead of AddRasterRenderPass.
        using (var builder = renderGraph.AddComputePass("MyComputePass", out PassData data))
        {
            
            // Use ComputeGraphContext instead of RasterGraphContext.
            builder.SetRenderFunc((PassData data, ComputeGraphContext context) => ExecutePass(data, context));
        }
    }
}