using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

// TODO kernel indices may have changed due to removed a lot of code




// TODO this can be a first step to animation (simply storing each frame separately)
// Useless for now only a test idea
public class VDBRenderFrame
{

}

interface IVDBRenderer
{
    public int RenderOrder { get; set; }
    public VDBFileContent Asset { get; set; } // Equivalent of mesh in meshrenderer
}

public class VDBRenderer : MonoBehaviour, IVDBRenderer
{

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


    // Internals
    ComputeShader VolumeShader; // TODO go to renderer feature
    ComputeBuffer ShadowBuffer;
    ComputeBuffer[] ValidVoxelSitesBuffer; // Array are for animation (each represneting a frame)
    ComputeBuffer[] ValidVoxelSitesBuffer2;


    RenderTexture VolumeTex;

    Texture3D VolumeTex2; // Unsure but appears to stores indices for DDAAlgorithm

    Vector4[] NonZeroVoxels;
    Vector3[] Sizes; // TODO this can be "Size"

    int IVDBRenderer.RenderOrder { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    VDBFileContent IVDBRenderer.Asset { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

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
        VolumeTex.Release();
        ShadowBuffer.Release();
        if (ValidVoxelSitesBuffer != null) for(int i = 0; i < ValidVoxelSitesBuffer.Length; i++) ValidVoxelSitesBuffer[i].Release();
        if (ValidVoxelSitesBuffer2 != null) for(int i = 0; i < ValidVoxelSitesBuffer2.Length; i++) ValidVoxelSitesBuffer2[i]?.Release();
    }


}
