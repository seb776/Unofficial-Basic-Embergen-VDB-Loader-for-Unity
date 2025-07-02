using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;
using JetBrains.Annotations;



// TODO this can be a first step to animation (simply storing each frame separately)
// Useless for now only a test idea
public class VDBRenderFrame
{

}

[Serializable]
public class VDBMaterial
{
    public float GlobalFogAdjustment = 1;
    public Vector3 FogColor = new Vector3(75 / 255.0f, 75 / 255.0f, 75 / 255.0f);
    public Vector3 BackgroundColor = new Vector3(0.1f, 0.1f, 0.1f);
    [Range(1, 10)]
    public int ShadowDistanceOffset = 1;
}

public class VDBRenderer : AVDBRenderer
{
    public override int RenderOrder { get; set; }
    public override VDBFileContent Asset { get; set; }


    // Internals
    ComputeBuffer ShadowBuffer;
    ComputeBuffer ValidVoxelSitesBuffer; 
    ComputeBuffer ValidVoxelSitesBuffer2;
    //Vector4[] NonZeroVoxels; // TODO This can go to importer instead of here ?
    RenderTexture VolumeTex; // DDATexture write // This appears only used once for generating the VolumeTex2, can be moved to importer ?
    Texture3D VolumeTex2; // DDATexture read // Unsure but appears to stores indices for DDAAlgorithm, can be moved to importer ?


    void Start()
    {
        //Load VDB Files and Parse
        // Asset.VDBContent Already parsed
        // TODO perhaps this could be done in the importer instead of here
        OpenVDBReader VDBFile = Asset.VDBContent;
        
        ValidVoxelSitesBuffer = new ComputeBuffer(0, 16); //new ComputeBuffer((int)CurOffset, 16); // TODO Why here ? we appear to jsut ignore previous values and leak memory
        ValidVoxelSitesBuffer.SetData(Asset.NonZeroVoxels);

        //Initialize Textures
        VolumeTex2 = new Texture3D((int)Asset.VDBContent.Size.x, (int)Asset.VDBContent.Size.y, (int)Asset.VDBContent.Size.z, TextureFormat.RGFloat, false);

        Debug.Log("Active Voxels: " + Asset.NonZeroVoxels.Length + ", Inactive Voxels: " + (VolumeTex2.width * VolumeTex2.height * VolumeTex2.depth - Asset.NonZeroVoxels.Length));
        
        VolumeTex = new RenderTexture((int)Asset.VDBContent.Size.x, (int)Asset.VDBContent.Size.y, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.sRGB);
        VolumeTex.enableRandomWrite = true;
        VolumeTex.volumeDepth = (int)Asset.VDBContent.Size.z;
        VolumeTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        VolumeTex.Create();


        ShadowBuffer = new ComputeBuffer(VolumeTex2.width * VolumeTex2.height * VolumeTex2.depth, 8);
    }

    void OnDestroy()
    {
        VolumeTex.Release();
        ShadowBuffer.Release();
        ValidVoxelSitesBuffer.Release();
        ValidVoxelSitesBuffer2.Release();
    }

    public override ComputeBuffer GetShadowBuffer()
    {
        return ShadowBuffer;
    }

    public override Vector3 GetSize()
    {
        return Asset.VDBContent.Size;
    }
}
