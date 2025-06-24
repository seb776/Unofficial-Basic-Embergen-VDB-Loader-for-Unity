using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;


[System.Serializable]
public struct UnityLight
{
    public Vector3 Position;
    public Vector3 Direction;
    public int Type;
    public Vector3 Col;
}

// TODO Separate AnimatedVDBRenderer?

public class VDBRenderer : MonoBehaviour
{
    public VDBFileContent Asset; // Equivalent of mesh in meshrenderer



    public float GlobalFogAdjustment = 1;
    public Vector3 FogColor = new Vector3(75 / 255.0f, 75 / 255.0f, 75 / 255.0f);
    public Vector3 BackgroundColor = new Vector3(0.1f, 0.1f, 0.1f);
    [Range(1, 10)]
    public int ShadowDistanceOffset = 1;

    // Temp
    public RenderTexture MainTex;
    public MeshRenderer DebugView;
    public RenderTexture TestOutputTex;
    public RenderTexture TestInputTex;
    int HasChangedInt = -1;
    float CurFrame = 0;

    // Internals
    ComputeShader VolumeShader;
    ComputeBuffer ShadowBuffer;
    ComputeBuffer[] ValidVoxelSitesBuffer; // Array are for animation (each represneting a frame)
    ComputeBuffer[] ValidVoxelSitesBuffer2;
    ComputeBuffer[] IndexBuffers;
    ComputeBuffer[] VertexBuffers;
    ComputeBuffer UnityLightBuffer;
    ComputeBuffer[] SHBuffer;
    ComputeBuffer ValidSDFSitesBuffer;
    ComputeBuffer SDFSHBuffer;
    ComputeBuffer CounterBuffer;
    ComputeBuffer SDFLocationBuffer;
    RenderTexture VolumeTex;

    MeshFilter[] MeshesToFollow;
    Mesh[] Meshes;
    Texture3D VolumeTex2;
    Texture3D SDFTex2;
    RenderTexture SDFTexture;
    Vector4[] NonZeroVoxels;
    Texture3D PlaceHolderSDF;

    UnityLight[] UnityLightData;
    Light[] UnityLights;
    Vector3[] Sizes;


    private void CreateRenderTexture(ref RenderTexture ThisTex)
    {
        ThisTex = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
        ThisTex.enableRandomWrite = true;
        ThisTex.Create();
    }

    void Start()
    {
        CreateRenderTexture(ref MainTex);
        this.gameObject.GetComponent<Camera>().targetTexture = TestInputTex;
        DebugView.material.SetTexture("_BaseMap", MainTex);
        VolumeShader = Resources.Load<ComputeShader>("RenderVolume");
        var kernel = VolumeShader.FindKernel("RenderVolumetric");
        Debug.Log("Dispatch ran: " + kernel);
        Debug.Log("OutputTex valid: " + MainTex.IsCreated());


        UnityLights = Object.FindObjectsOfType<Light>();
        //Load Unity Lights
        UnityLightData = new UnityLight[UnityLights.Length];
        for(int i = 0; i < UnityLights.Length; i++) {
            Light ThisLight = UnityLights[i];
            Color col = ThisLight.color; 
            UnityLightData[i].Position = ThisLight.transform.position;
            UnityLightData[i].Direction = ThisLight.transform.forward;
            UnityLightData[i].Type = (ThisLight.type == LightType.Point) ? 0 : (ThisLight.type == LightType.Directional) ? 1 : (ThisLight.type == LightType.Spot) ? 2 : 3;
            UnityLightData[i].Col = new Vector3(col[0], col[1], col[2]) * ThisLight.intensity;
        }

        //Load VDB Files and Parse
        // Asset.VDBContent Already parsed
        // TODO perhaps this could be done in the importer instead of here
        //Convert to own voxel format
        OpenVDBReader VDBFile = Asset.VDBContent;
        for (int i3 = 0; i3 < Mathf.Min(VDBFile.Grids.Length, 2); i3++)
        {
            var CurGrid = i3;
            Vector3 OrigionalSize = new Vector3(VDBFile.Grids[CurGrid].Size.x, VDBFile.Grids[CurGrid].Size.z, VDBFile.Grids[CurGrid].Size.y);
            NonZeroVoxels = new Vector4[VDBFile.Grids[CurGrid].Centers.Count];
            VDBFile.Size = OrigionalSize;

            int RepCount = 0;
            OpenVDBReader.Node4 CurNode;
            OpenVDBReader.Node3 CurNode2;
            OpenVDBReader.Voxel Vox;
            Vector3Int ijk = new Vector3Int(0, 0, 0);
            Vector3 location2 = Vector3.zero;
            uint CurOffset = 0;
            for (int i = 0; i < VDBFile.Grids[CurGrid].Centers.Count; i++)
            {
                ulong BitIndex1 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 4095) >> 7) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 4095) >> 7) << 5) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 4095) >> 7) << 10));
                ulong BitIndex2 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 127) >> 3) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 127) >> 3) << 4) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 127) >> 3) << 8));
                ulong BitIndex3 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 7) >> 0) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 7) >> 0) << 3) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 7) >> 0) << 6));

                if (VDBFile.Grids[CurGrid].RootNode.Children.TryGetValue(BitIndex1, out CurNode))
                {
                    if (CurNode.Children.TryGetValue(BitIndex2, out CurNode2))
                    {
                        if (CurNode2.Children.TryGetValue(BitIndex3, out Vox))
                        {
                            location2 = new Vector3(VDBFile.Grids[CurGrid].Centers[i].z, VDBFile.Grids[CurGrid].Centers[i].x, VDBFile.Grids[CurGrid].Centers[i].y);
                            float Val = System.BitConverter.ToSingle(System.BitConverter.GetBytes((uint)Vox.Density)) * 100000000000000000000000000000000000000.0f * 50.0f;
                            if (Val > 0.01f)
                            {
                                NonZeroVoxels[CurOffset] = new Vector4(location2.x, location2.y, location2.z, Val);
                                CurOffset++;
                            }
                        }
                    }
                }
            }
            if (i3 == 0)
            {
                ValidVoxelSitesBuffer[i2] = new ComputeBuffer((int)CurOffset, 16);
                ValidVoxelSitesBuffer[i2].SetData(NonZeroVoxels);
                SHBuffer[i2] = new ComputeBuffer((int)CurOffset, 28);
            }
            else
            {
                ValidVoxelSitesBuffer2[i2] = new ComputeBuffer((int)CurOffset, 16);
                ValidVoxelSitesBuffer2[i2].SetData(NonZeroVoxels);
            }
        }
        VolumeShader.SetVector("Size", VDBFile.Size);

        //Initialize Textures
        VolumeTex2 = new Texture3D((int)Sizes[0].x, (int)Sizes[0].y, (int)Sizes[0].z, TextureFormat.RGFloat, false);
        Debug.Log("Active Voxels: " + NonZeroVoxels.Length + ", Inactive Voxels: " + (VolumeTex2.width * VolumeTex2.height * VolumeTex2.depth - NonZeroVoxels.Length));
        VolumeTex = new RenderTexture((int)Sizes[0].x, (int)Sizes[0].y, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.sRGB);
        VolumeTex.enableRandomWrite = true;
        VolumeTex.volumeDepth = (int)Sizes[0].z;
        VolumeTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        VolumeTex.Create();


        ShadowBuffer = new ComputeBuffer(VolumeTex2.width * VolumeTex2.height * VolumeTex2.depth, 8);
        UnityLightBuffer = new ComputeBuffer(UnityLights.Length, 40);
        VolumeShader.SetBuffer(2, "ShadowBuffer", ShadowBuffer);
        VolumeShader.SetBuffer(0, "ShadowBuffer", ShadowBuffer);
        VolumeShader.SetBuffer(1, "ShadowBuffer", ShadowBuffer);
        VolumeShader.SetBuffer(3, "ShadowBuffer", ShadowBuffer);
        VolumeShader.SetBuffer(2, "UnityLights", UnityLightBuffer);
        VolumeShader.SetBuffer(0, "UnityLights", UnityLightBuffer);
        VolumeShader.SetInt("ScreenWidth", Screen.width);
        VolumeShader.SetInt("ScreenHeight", Screen.height);
    }

    void OnApplicationQuit()
    {
        UnityLightBuffer.Release();
        VolumeTex.Release();
        ShadowBuffer.Release();
        if (ValidVoxelSitesBuffer != null) for(int i = 0; i < ValidVoxelSitesBuffer.Length; i++) ValidVoxelSitesBuffer[i].Release();
        if (ValidVoxelSitesBuffer2 != null) for(int i = 0; i < ValidVoxelSitesBuffer2.Length; i++) ValidVoxelSitesBuffer2[i]?.Release();
        if (SHBuffer != null) for(int i = 0; i < SHBuffer.Length; i++) SHBuffer[i].Release();
        if (IndexBuffers != null) for(int i = 0; i < IndexBuffers.Length; i++) IndexBuffers[i].Release();
        if (VertexBuffers != null) for(int i = 0; i < VertexBuffers.Length; i++) VertexBuffers[i].Release();
        SDFTexture.Release();
        CounterBuffer.Release();
        ValidSDFSitesBuffer.Release();
        SDFSHBuffer.Release();
        SDFLocationBuffer?.Release();
    }

    private void LateUpdate()
    {
        _OnRenderImage(TestInputTex, TestOutputTex); // TODO this has to be cleaned (useless parameters...)
    }
    private void _OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        VolumeShader.SetInt("CurFrame", (int)Mathf.Floor(CurFrame));
        VolumeShader.SetInt("LightCount", UnityLights.Length);
        int i = (int)Mathf.Floor(CurFrame) % (ValidVoxelSitesBuffer.Length);
        for(int i2 = 0; i2 < UnityLights.Length; i2++) {//If any unity lights have changed, reset the lighting data
            Light ThisLight = UnityLights[i2];
            Color col = ThisLight.color;
            if(ThisLight.transform.hasChanged) {
                HasChanged = true;
                ThisLight.transform.hasChanged = false;
                UnityLightData[i2].Position = ThisLight.transform.position;
                UnityLightData[i2].Direction = ThisLight.transform.forward;
            } 
            int Type = (ThisLight.type == LightType.Point) ? 0 : (ThisLight.type == LightType.Directional) ? 1 : (ThisLight.type == LightType.Spot) ? 2 : 3;
            if(UnityLightData[i2].Type != Type) {
                HasChanged = true;
                UnityLightData[i2].Type = Type;
            }
            if(UnityLightData[i2].Type == 1) VolumeShader.SetVector("SunDir", UnityLightData[i2].Direction);
            Vector3 Col = new Vector3(col[0], col[1], col[2]) * ThisLight.intensity;
            if(!UnityLightData[i2].Col.Equals(Col)) {
                HasChanged = true;
                UnityLightData[i2].Col = Col;
            }
        }
        if(HasChanged) UnityLightBuffer.SetData(UnityLightData);

        VolumeShader.SetBool("ResetHistory", HasChanged);
        VolumeShader.SetBuffer(2, "SH", SHBuffer[i]);
        VolumeShader.SetBuffer(0, "SH", SHBuffer[i]);
        VolumeShader.SetBuffer(3, "SH", SHBuffer[i]);
        if(Sizes.Length > 1 || CurFrame < 2 || HasChanged) {//Rebuild the Volume Texture
        	VolumeShader.SetBool("Copy1", false);
            VolumeShader.SetVector("Size", Sizes[(int)Mathf.Floor(CurFrame) % (ValidVoxelSitesBuffer.Length)]);
            VolumeShader.SetBuffer(1, "NonZeroVoxels", ValidVoxelSitesBuffer[i]);
            VolumeShader.SetTexture(1, "DDATextureWrite", VolumeTex);
            VolumeShader.SetTexture(3, "DDATextureWrite", VolumeTex);
            VolumeShader.Dispatch(3, Mathf.CeilToInt(Sizes[i].x / 8.0f), Mathf.CeilToInt(Sizes[i].y / 8.0f), Mathf.CeilToInt(Sizes[i].z / 8.0f));
    
            VolumeShader.Dispatch(1, Mathf.CeilToInt(ValidVoxelSitesBuffer[i].count / 1023.0f), 1, 1);
            if(ValidVoxelSitesBuffer2[i] != null) {
                VolumeShader.SetBuffer(1, "NonZeroVoxels", ValidVoxelSitesBuffer2[i]);
            	VolumeShader.SetBool("Copy1", true);
                VolumeShader.Dispatch(1, Mathf.CeilToInt(ValidVoxelSitesBuffer2[i].count / 1023.0f), 1, 1);
            }

            Graphics.CopyTexture(VolumeTex, VolumeTex2);
        }
        if(HasChangedInt != -1) {
            if(HasChangedInt == i) {
                HasChangedInt = -1;
            }
        } else if(HasChanged) HasChangedInt = i;

        VolumeShader.SetTexture(0, "DDATexture", VolumeTex2);
        VolumeShader.SetTexture(2, "DDATexture", VolumeTex2);
        VolumeShader.SetBuffer(2, "NonZeroVoxels", ValidVoxelSitesBuffer[i]);
        
        VolumeShader.SetInt("ShadowDistanceOffset", ShadowDistanceOffset);

        if(CurFrame < 2 || HasChanged || Sizes.Length > 1 || true) {VolumeShader.Dispatch(2, Mathf.CeilToInt(ValidVoxelSitesBuffer[i].count / 1023.0f), 1, 1);}//Calculate the Volume Shading


        VolumeShader.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        VolumeShader.SetMatrix("CameraToWorld", Camera.main.cameraToWorldMatrix);
        CurFrame += 1.0f;
        var camera = this.gameObject.GetComponent<Camera>();
        VolumeShader.SetFloat("_NearClip", camera.nearClipPlane);
        VolumeShader.SetFloat("_FarClip", camera.farClipPlane);
        this.gameObject.GetComponent<Camera>().Render();
        VolumeShader.SetTexture(0, "MainRenderTexture", TestInputTex);
        VolumeShader.SetTexture(0, "Result", MainTex);
        VolumeShader.SetFloat("_MyTime", Time.realtimeSinceStartup);
        VolumeShader.Dispatch(0, Mathf.CeilToInt((float)Screen.width / 8.0f), Mathf.CeilToInt((float)Screen.height / 8.0f), 1);//Dispatch the main renderer
    }

    void OnRenderObject()
    {
        // Solution comes from here
        // https://discussions.unity.com/t/unity-6-urp-depth-texture-is-black-not-available/1560743/3
        // Global texture no lkonger works the same since unity 6, but it's still accessible there
        VolumeShader.SetTextureFromGlobal(0, "_CameraDepthTexture", "_CameraDepthTexture");
    }
}
