using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
using System.Threading.Tasks;
using System.Threading;

public class DDAVolume : MonoBehaviour
{
    public Object FileIn;
    public bool DoMeshes = true;
    public bool DoIndirect = true;
    public float GlobalFogAdjustment = 1;
    public Vector3 FogColor = new Vector3(75 / 255.0f, 75 / 255.0f, 75 / 255.0f);
    public Vector3 BackgroundColor = new Vector3(0.1f, 0.1f, 0.1f);
    ComputeShader VolumeShader;
    ComputeBuffer ShadowBuffer;
    ComputeBuffer[] ValidVoxelSitesBuffer;
    ComputeBuffer[] ValidVoxelSitesBuffer2;
    ComputeBuffer[] IndexBuffers;
    ComputeBuffer[] VertexBuffers;
    ComputeBuffer UnityLightBuffer;
    ComputeBuffer[] SHBuffer;
    ComputeBuffer ValidSDFSitesBuffer;
    ComputeBuffer SDFSHBuffer;
    ComputeBuffer CounterBuffer;
    ComputeBuffer SDFLocationBuffer;
    public RenderTexture MainTex;
    RenderTexture VolumeTex;
    Mesh[] Meshes;
    Texture3D VolumeTex2;
    Texture3D SDFTex2;
    RenderTexture SDFTexture;
    Vector4[] NonZeroVoxels;
    Texture3D PlaceHolderSDF;

    [Range(1, 10)]
    public int ShadowDistanceOffset = 1;
    [System.Serializable]
    public struct UnityLight {
        public Vector3 Position;
        public Vector3 Direction;
        public int Type;
        public Vector3 Col;
    }
    UnityLight[] UnityLightData;
    Light[] UnityLights;
    OpenVDBReader[] VDBFileArray;
    Vector3[] Sizes;

    
    private void CreateRenderTexture(ref RenderTexture ThisTex)
    {
        ThisTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        ThisTex.enableRandomWrite = true;
        ThisTex.Create();
    }
    MeshFilter[] MeshesToFollow;
    public MeshRenderer DebugView;
    void Start()
    {
        CreateRenderTexture(ref MainTex);
        DebugView.material.SetTexture("_BaseMap", MainTex);
        VolumeShader = Resources.Load<ComputeShader>("RenderVolume");
        var kernel = VolumeShader.FindKernel("CSMain");
        Debug.Log("Dispatch ran: " + kernel);
        Debug.Log("OutputTex valid: " + MainTex.IsCreated());


        UnityLights = Object.FindObjectsOfType<Light>();
        MeshesToFollow = Object.FindObjectsOfType<MeshFilter>();
        Meshes = new Mesh[MeshesToFollow.Length];
        IndexBuffers = new ComputeBuffer[MeshesToFollow.Length];
        VertexBuffers = new ComputeBuffer[MeshesToFollow.Length];
        //Load Meshes to be voxelized
        for(int i = 0; i < MeshesToFollow.Length; i++) {
            List<Vector3> Vertexes = new List<Vector3>();
            Meshes[i] = MeshesToFollow[i].sharedMesh;
            for(int i2 = 0; i2 < Meshes[i].subMeshCount; i2++) {
                List<Vector3> E = new List<Vector3>();
                Meshes[i].GetVertices(E);
                Vertexes.AddRange(E);
            }
            int[] Indexes = Meshes[i].triangles;
            IndexBuffers[i] = new ComputeBuffer(Indexes.Length, 4);
            IndexBuffers[i].SetData(Indexes);
            VertexBuffers[i] = new ComputeBuffer(Vertexes.Count, 12);
            VertexBuffers[i].SetData(Vertexes.ToArray());
        }
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

        string CachedString = AssetDatabase.GetAssetPath(FileIn);
        uint CurVox = 0;
        string[] Materials;
        if(CachedString.Contains(".vdb")) {
            Materials = new string[]{Application.dataPath + CachedString.Replace("Assets", "").Replace("/" + FileIn.name, "\\" + FileIn.name)};            
        } else {
            Materials = System.IO.Directory.GetFiles(Application.dataPath + CachedString.Replace("Assets", ""));
        }
        VDBFileArray = new OpenVDBReader[Materials.Length];
        List<string> Material3 = new List<string>();
        for(int i2 = 0; i2 < Materials.Length; i2++) {
            if(Materials[i2].Contains("meta")) continue;
            if(!Materials[i2].Contains("vdb")) continue;
            Material3.Add(Materials[i2]);
        }
        Materials = Material3.ToArray();
        Sizes = new Vector3[Materials.Length];
        ValidVoxelSitesBuffer = new ComputeBuffer[Materials.Length];
        ValidVoxelSitesBuffer2 = new ComputeBuffer[Materials.Length];
        SHBuffer = new ComputeBuffer[Materials.Length];
        List<Task> RunningTasks = new List<Task>();
        for(int i2 = 0; i2 < Materials.Length; i2++) {
            var A = i2;
            VDBFileArray[A] = new OpenVDBReader();
            Task t1 = Task.Run(() => { VDBFileArray[A].ParseVDB(Materials[A], A);});
            RunningTasks.Add(t1);
        }

        while(RunningTasks.Count != 0) {
            int TaskCount = RunningTasks.Count;
            for(int i = TaskCount - 1; i >= 0; i--) {
                if (RunningTasks[i].IsFaulted) {
                    Debug.Log(RunningTasks[i].Exception);
                    RunningTasks.RemoveAt(i);
                } else if(RunningTasks[i].Status == TaskStatus.RanToCompletion) {
                    RunningTasks.RemoveAt(i);
                }
            }
        }

        //Convert to own voxel format
        int CurGrid = 0;
        for(int i2 = 0; i2 < Materials.Length; i2++) {
            CurGrid = 0;
            OpenVDBReader VDBFile = VDBFileArray[i2];
            for(int i3 = 0; i3 < Mathf.Min(VDBFile.Grids.Length, 2); i3++) {
                CurGrid = i3;
                Vector3 OrigionalSize = new Vector3(VDBFile.Grids[CurGrid].Size.x, VDBFile.Grids[CurGrid].Size.z, VDBFile.Grids[CurGrid].Size.y);
                NonZeroVoxels = new Vector4[VDBFile.Grids[CurGrid].Centers.Count]; 
                VDBFile.Size = OrigionalSize;

                int RepCount = 0;
                OpenVDBReader.Node4 CurNode;
                OpenVDBReader.Node3 CurNode2;
                OpenVDBReader.Voxel Vox;
                Vector3Int ijk = new Vector3Int(0,0,0);
                Vector3 location2 = Vector3.zero;
                uint CurOffset = 0;
                for(int i = 0; i < VDBFile.Grids[CurGrid].Centers.Count; i++) {
                    ulong BitIndex1 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 4095) >> 7) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 4095) >> 7) << 5) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 4095) >> 7) << 10));
                    ulong BitIndex2 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 127) >> 3) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 127) >> 3) << 4) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 127) >> 3) << 8));
                    ulong BitIndex3 = (ulong)((((int)VDBFile.Grids[CurGrid].Centers[i].x & 7) >> 0) | ((((int)VDBFile.Grids[CurGrid].Centers[i].y & 7) >> 0) << 3) | ((((int)VDBFile.Grids[CurGrid].Centers[i].z & 7) >> 0) << 6));

                    if(VDBFile.Grids[CurGrid].RootNode.Children.TryGetValue(BitIndex1, out CurNode)) {
                        if(CurNode.Children.TryGetValue(BitIndex2, out CurNode2)) {
                            if(CurNode2.Children.TryGetValue(BitIndex3, out Vox)) {
                                location2 = new Vector3(VDBFile.Grids[CurGrid].Centers[i].z, VDBFile.Grids[CurGrid].Centers[i].x, VDBFile.Grids[CurGrid].Centers[i].y);
                                float Val = System.BitConverter.ToSingle(System.BitConverter.GetBytes((uint)Vox.Density)) * 100000000000000000000000000000000000000.0f * 50.0f;
                                if(Val > 0.01f) {
                                    NonZeroVoxels[CurOffset] = new Vector4(location2.x, location2.y, location2.z, Val);
                                    CurOffset++;
                                }
                            }
                        }
                    }
                }
			if(i3 == 0) {
	            ValidVoxelSitesBuffer[i2] = new ComputeBuffer((int)CurOffset, 16);
    	        ValidVoxelSitesBuffer[i2].SetData(NonZeroVoxels);
            	SHBuffer[i2] = new ComputeBuffer((int)CurOffset, 28);
            } else {
	            ValidVoxelSitesBuffer2[i2] = new ComputeBuffer((int)CurOffset, 16);
    	        ValidVoxelSitesBuffer2[i2].SetData(NonZeroVoxels);
        	}
            }
            VolumeShader.SetVector("Size", VDBFile.Size);
            Sizes[i2] = VDBFile.Size;
            VDBFileArray[i2] = null;
        }


        //Initialize Textures
        VolumeTex2 = new Texture3D((int)Sizes[0].x, (int)Sizes[0].y, (int)Sizes[0].z, TextureFormat.RGFloat, false);
        Debug.Log("Active Voxels: " + NonZeroVoxels.Length + ", Inactive Voxels: " + (VolumeTex2.width * VolumeTex2.height * VolumeTex2.depth - NonZeroVoxels.Length));
        VolumeTex = new RenderTexture((int)Sizes[0].x, (int)Sizes[0].y, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.sRGB);
        VolumeTex.enableRandomWrite = true;
        VolumeTex.volumeDepth = (int)Sizes[0].z;
        VolumeTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        VolumeTex.Create();

        PlaceHolderSDF = new Texture3D(1, 1, 1, TextureFormat.RFloat, false);
        if(MeshesToFollow.Length != 0) {
            SDFTex2 = new Texture3D(512, 512, 512, TextureFormat.RFloat, false);
            SDFTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.sRGB);
            SDFTexture.enableRandomWrite = true;
            SDFTexture.volumeDepth = 512;
            SDFTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            SDFTexture.Create();
            CounterBuffer = new ComputeBuffer(2, 4);
            int[] H = new int[2];
            CounterBuffer.SetData(H);
            ValidSDFSitesBuffer = new ComputeBuffer(512 * 512 * 512, 4);
        } else {
            ValidSDFSitesBuffer = new ComputeBuffer(1, 4);
            SDFSHBuffer = new ComputeBuffer(1, 28);
            VolumeShader.SetBuffer(0, "SDFIndexes", ValidSDFSitesBuffer);
            VolumeShader.SetBuffer(0, "SDFVoxels", SDFSHBuffer);
        }

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
    int HasChangedInt = -1;
    float CurFrame = 0;
    public RenderTexture TestOutputTex;
    public RenderTexture TestInputTex;
    private void LateUpdate()
    {
        _OnRenderImage(TestInputTex, TestOutputTex);
    }
    private void _OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(MeshesToFollow.Length == 0) DoMeshes = false;
        VolumeShader.SetBool("DoMeshes", DoMeshes);
        VolumeShader.SetBool("UseIndirect", DoIndirect);
        VolumeShader.SetFloat("FogAdjustment", GlobalFogAdjustment);
        VolumeShader.SetVector("BackgroundColor", BackgroundColor);
        VolumeShader.SetVector("FogColor", FogColor);
        
        bool HasChanged = HasChangedInt != -1;
        //Calculate minimum for bounding boxes
        Vector3 Min = new Vector3(98999,99999,99999);
        for(int i2 = 0; i2 < Meshes.Length; i2++) {
            if(MeshesToFollow[i2].gameObject.transform.hasChanged) {
                MeshesToFollow[i2].gameObject.transform.hasChanged = false;
                HasChanged = true;
            }
            Bounds bounds = MeshesToFollow[i2].sharedMesh.bounds;
            bounds.min = Vector3.Scale(bounds.min, MeshesToFollow[i2].gameObject.transform.lossyScale) + MeshesToFollow[i2].gameObject.transform.position;
            Min = new Vector3(Mathf.Min(Min.x, bounds.min.x), Mathf.Min(Min.y, bounds.min.y), Mathf.Min(Min.z, bounds.min.z));
        }
        if(DoMeshes) VolumeShader.SetTexture(5, "SDFWrite", SDFTexture);
        if(DoMeshes && HasChanged && CurFrame > MeshesToFollow.Length - 1) {//Reinitialize SDF data if meshes moved
            int[] H = new int[2];
            CounterBuffer.GetData(H);
            H[1] = 0;
            CounterBuffer.SetData(H);
            VolumeShader.Dispatch(7, Mathf.CeilToInt(512 / 8.0f),Mathf.CeilToInt(512 / 8.0f),Mathf.CeilToInt(512 / 8.0f));
        }

        VolumeShader.SetVector("SDFOffset", Min);
        if(CurFrame < MeshesToFollow.Length && DoMeshes) {//Voxelize One Mesh Per Frame
            int i2 = (int)Mathf.Floor(CurFrame) % (MeshesToFollow.Length);
            Mesh mesh = Meshes[i2];
            VolumeShader.SetVector("Scale", MeshesToFollow[i2].gameObject.transform.lossyScale);
            VolumeShader.SetVector("Position", MeshesToFollow[i2].gameObject.transform.position - Min);
            VolumeShader.SetBuffer(4, "Index", IndexBuffers[i2]);
            VolumeShader.SetBuffer(4, "Vertices", VertexBuffers[i2]);
            VolumeShader.SetBuffer(4, "Counter", CounterBuffer);
            VolumeShader.SetTexture(4, "SDFWrite", SDFTexture);
            VolumeShader.SetInt("MaxIndex", IndexBuffers[i2].count / 3);
            VolumeShader.Dispatch(4, (int)Mathf.CeilToInt(IndexBuffers[i2].count / 64.0f / 3.0f), 1, 1);
            Graphics.CopyTexture(SDFTexture, SDFTex2);
        }
        if(CurFrame == MeshesToFollow.Length - 1 && DoMeshes) {//When all meshes are Voxelized, build SDF Data
            int[] H = new int[2];
            CounterBuffer.GetData(H);
            if(SDFSHBuffer != null) SDFSHBuffer.Release();
            if(SDFLocationBuffer != null) SDFLocationBuffer.Release();
            SDFLocationBuffer = new ComputeBuffer(H[0], 12);
            SDFSHBuffer = new ComputeBuffer(H[0], 28);
            VolumeShader.SetTexture(7, "SDF", SDFTex2);
            VolumeShader.SetBuffer(7, "Counter", CounterBuffer);
            VolumeShader.SetBuffer(7, "SDFLocations", SDFLocationBuffer);
            VolumeShader.SetBuffer(7, "SDFIndexes", ValidSDFSitesBuffer);
            VolumeShader.SetBuffer(0, "SDFIndexes", ValidSDFSitesBuffer);
            VolumeShader.SetBuffer(0, "SDFVoxels", SDFSHBuffer);
            VolumeShader.SetBuffer(7, "SDFVoxels", SDFSHBuffer);
            VolumeShader.Dispatch(7, Mathf.CeilToInt(512 / 8.0f),Mathf.CeilToInt(512 / 8.0f),Mathf.CeilToInt(512 / 8.0f));
        }

        if(DoMeshes && CurFrame > MeshesToFollow.Length - 1) {//When all meshes are voxelized and built, calculate lighting, 1 pass per frame
            VolumeShader.SetTexture(6, "SDF", SDFTex2);
            VolumeShader.SetTexture(6, "DDATexture", VolumeTex2);
            VolumeShader.SetBuffer(6, "UnityLights", UnityLightBuffer);
            VolumeShader.SetBuffer(6, "SDFLocations", SDFLocationBuffer);
            VolumeShader.SetBuffer(6, "SDFIndexes", ValidSDFSitesBuffer);
            VolumeShader.SetBuffer(6, "SDFVoxels", SDFSHBuffer);
            VolumeShader.Dispatch(6, (int)Mathf.CeilToInt(SDFLocationBuffer.count / 1023.0f), 1, 1);
        }
        if(DoMeshes) {
            VolumeShader.SetTexture(2, "SDF", SDFTex2);
            VolumeShader.SetTexture(0, "SDF", SDFTex2);
        } else {
            VolumeShader.SetTexture(2, "SDF", PlaceHolderSDF);
            VolumeShader.SetTexture(0, "SDF", PlaceHolderSDF);
        }


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
        VolumeShader.SetTexture(0, "Result", MainTex);
        VolumeShader.Dispatch(0, Mathf.CeilToInt((float)Screen.width / 8.0f), Mathf.CeilToInt((float)Screen.height / 8.0f), 1);//Dispatch the main renderer
        //RenderTexture.active = destination;
        //GL.Clear(true, true, Color.red);
        //Graphics.Blit(MainTex, destination);
    }


}
