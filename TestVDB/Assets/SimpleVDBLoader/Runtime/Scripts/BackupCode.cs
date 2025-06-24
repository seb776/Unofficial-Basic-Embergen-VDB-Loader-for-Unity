////public bool DoMeshes = true; // This has to move
////public bool DoIndirect = true; // This has to move also (dependent on DoMeshes which has nothing to do directly with rendering the VDB)

//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.XR;

//MeshesToFollow = Object.FindObjectsOfType<MeshFilter>();
//Meshes = new Mesh[MeshesToFollow.Length];
//IndexBuffers = new ComputeBuffer[MeshesToFollow.Length];
//VertexBuffers = new ComputeBuffer[MeshesToFollow.Length];
////Load Meshes to be voxelized
//for (int i = 0; i < MeshesToFollow.Length; i++)
//{
//    List<Vector3> Vertexes = new List<Vector3>();
//    Meshes[i] = MeshesToFollow[i].sharedMesh;
//    for (int i2 = 0; i2 < Meshes[i].subMeshCount; i2++)
//    {
//        List<Vector3> E = new List<Vector3>();
//        Meshes[i].GetVertices(E);
//        Vertexes.AddRange(E);
//    }
//    int[] Indexes = Meshes[i].triangles;
//    IndexBuffers[i] = new ComputeBuffer(Indexes.Length, 4);
//    IndexBuffers[i].SetData(Indexes);
//    VertexBuffers[i] = new ComputeBuffer(Vertexes.Count, 12);
//    VertexBuffers[i].SetData(Vertexes.ToArray());
//}


//if (MeshesToFollow.Length == 0) DoMeshes = false;
//VolumeShader.SetBool("DoMeshes", DoMeshes);
//VolumeShader.SetBool("UseIndirect", DoIndirect);

//VolumeShader.SetFloat("FogAdjustment", GlobalFogAdjustment);
//VolumeShader.SetVector("BackgroundColor", BackgroundColor);
//VolumeShader.SetVector("FogColor", FogColor);

//bool HasChanged = HasChangedInt != -1;
////Calculate minimum for bounding boxes
//Vector3 Min = new Vector3(98999, 99999, 99999); // TODO why 98999,99999,99999 ?
//for (int i2 = 0; i2 < Meshes.Length; i2++)
//{
//    if (MeshesToFollow[i2].gameObject.transform.hasChanged)
//    {
//        MeshesToFollow[i2].gameObject.transform.hasChanged = false;
//        HasChanged = true;
//    }
//    Bounds bounds = MeshesToFollow[i2].sharedMesh.bounds;
//    bounds.min = Vector3.Scale(bounds.min, MeshesToFollow[i2].gameObject.transform.lossyScale) + MeshesToFollow[i2].gameObject.transform.position;
//    Min = new Vector3(Mathf.Min(Min.x, bounds.min.x), Mathf.Min(Min.y, bounds.min.y), Mathf.Min(Min.z, bounds.min.z));
//}
//if (DoMeshes) VolumeShader.SetTexture(5, "SDFWrite", SDFTexture);
//if (DoMeshes && HasChanged && CurFrame > MeshesToFollow.Length - 1)
//{//Reinitialize SDF data if meshes moved
//    int[] H = new int[2];
//    CounterBuffer.GetData(H);
//    H[1] = 0;
//    CounterBuffer.SetData(H);
//    // TODO 512 ? dependent on textures used
//    VolumeShader.Dispatch(7, Mathf.CeilToInt(512 / 8.0f), Mathf.CeilToInt(512 / 8.0f), Mathf.CeilToInt(512 / 8.0f));
//}

//VolumeShader.SetVector("SDFOffset", Min);
//if (CurFrame < MeshesToFollow.Length && DoMeshes)
//{//Voxelize One Mesh Per Frame
//    int i2 = (int)Mathf.Floor(CurFrame) % (MeshesToFollow.Length);
//    Mesh mesh = Meshes[i2];
//    VolumeShader.SetVector("Scale", MeshesToFollow[i2].gameObject.transform.lossyScale);
//    VolumeShader.SetVector("Position", MeshesToFollow[i2].gameObject.transform.position - Min);
//    VolumeShader.SetBuffer(4, "Index", IndexBuffers[i2]);
//    VolumeShader.SetBuffer(4, "Vertices", VertexBuffers[i2]);
//    VolumeShader.SetBuffer(4, "Counter", CounterBuffer);
//    VolumeShader.SetTexture(4, "SDFWrite", SDFTexture);
//    VolumeShader.SetInt("MaxIndex", IndexBuffers[i2].count / 3);
//    VolumeShader.Dispatch(4, (int)Mathf.CeilToInt(IndexBuffers[i2].count / 64.0f / 3.0f), 1, 1);
//    Graphics.CopyTexture(SDFTexture, SDFTex2);
//}
//if (CurFrame == MeshesToFollow.Length - 1 && DoMeshes)
//{//When all meshes are Voxelized, build SDF Data
//    int[] H = new int[2];
//    CounterBuffer.GetData(H);
//    if (SDFSHBuffer != null) SDFSHBuffer.Release();
//    if (SDFLocationBuffer != null) SDFLocationBuffer.Release();
//    SDFLocationBuffer = new ComputeBuffer(H[0], 12);
//    SDFSHBuffer = new ComputeBuffer(H[0], 28);
//    VolumeShader.SetTexture(7, "SDF", SDFTex2);
//    VolumeShader.SetBuffer(7, "Counter", CounterBuffer);
//    VolumeShader.SetBuffer(7, "SDFLocations", SDFLocationBuffer);
//    VolumeShader.SetBuffer(7, "SDFIndexes", ValidSDFSitesBuffer);
//    VolumeShader.SetBuffer(0, "SDFIndexes", ValidSDFSitesBuffer);
//    VolumeShader.SetBuffer(0, "SDFVoxels", SDFSHBuffer);
//    VolumeShader.SetBuffer(7, "SDFVoxels", SDFSHBuffer);
//    VolumeShader.Dispatch(7, Mathf.CeilToInt(512 / 8.0f), Mathf.CeilToInt(512 / 8.0f), Mathf.CeilToInt(512 / 8.0f));
//}

//if (DoMeshes && CurFrame > MeshesToFollow.Length - 1)
//{//When all meshes are voxelized and built, calculate lighting, 1 pass per frame
//    VolumeShader.SetTexture(6, "SDF", SDFTex2);
//    VolumeShader.SetTexture(6, "DDATexture", VolumeTex2);
//    VolumeShader.SetBuffer(6, "UnityLights", UnityLightBuffer);
//    VolumeShader.SetBuffer(6, "SDFLocations", SDFLocationBuffer);
//    VolumeShader.SetBuffer(6, "SDFIndexes", ValidSDFSitesBuffer);
//    VolumeShader.SetBuffer(6, "SDFVoxels", SDFSHBuffer);
//    VolumeShader.Dispatch(6, (int)Mathf.CeilToInt(SDFLocationBuffer.count / 1023.0f), 1, 1);
//}
//if (DoMeshes)
//{
//    VolumeShader.SetTexture(2, "SDF", SDFTex2);
//    VolumeShader.SetTexture(0, "SDF", SDFTex2);
//}
//else
//{
//    VolumeShader.SetTexture(2, "SDF", PlaceHolderSDF);
//    VolumeShader.SetTexture(0, "SDF", PlaceHolderSDF);
//}

//PlaceHolderSDF = new Texture3D(1, 1, 1, TextureFormat.RFloat, false);
//if (MeshesToFollow.Length != 0)
//{
//    SDFTex2 = new Texture3D(512, 512, 512, TextureFormat.RFloat, false);
//    SDFTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.sRGB);
//    SDFTexture.enableRandomWrite = true;
//    SDFTexture.volumeDepth = 512;
//    SDFTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
//    SDFTexture.Create();
//    CounterBuffer = new ComputeBuffer(2, 4);
//    int[] H = new int[2];
//    CounterBuffer.SetData(H);
//    ValidSDFSitesBuffer = new ComputeBuffer(512 * 512 * 512, 4);
//}
//else
//{
//    ValidSDFSitesBuffer = new ComputeBuffer(1, 4);
//    SDFSHBuffer = new ComputeBuffer(1, 28);
//    VolumeShader.SetBuffer(0, "SDFIndexes", ValidSDFSitesBuffer);
//    VolumeShader.SetBuffer(0, "SDFVoxels", SDFSHBuffer);
//}