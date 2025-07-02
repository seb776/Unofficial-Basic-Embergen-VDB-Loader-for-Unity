using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// The importer is registered with Unity's asset pipeline by placing the ScriptedImporter attribute on the
// CubeImporter class. The CubeImporter class implements the abstract ScriptedImporter base class.



namespace SimpleVDBLoader
{
    [ScriptedImporter(1, "vdb")]
    public class VDBImporter : ScriptedImporter
    {
        public VDBFileContent VDBContent;
        public float ScaleFactor = 1;
        public int MaxResolution = 2;
        public bool ImportAnimation;
        public int SelectedFrame; // When Import animation false and model contains animation

        // The ctx argument contains both input and output data for the import event
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var gameObjectName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var gameObject = new GameObject(gameObjectName);
            var vdbRenderer = gameObject.AddComponent<VDBRenderer>();
            var assetPath = Path.Combine(Path.GetDirectoryName(ctx.assetPath), Path.GetFileNameWithoutExtension(ctx.assetPath));

            ctx.AddObjectToAsset("main obj", gameObject);
            ctx.SetMainObject(gameObject);

            var originalFile = System.IO.File.ReadAllBytes(ctx.assetPath);
            VDBContent = new VDBFileContent();
            VDBContent.FileContent = originalFile;
            vdbRenderer.Asset = VDBContent;

            var VDBFile = new OpenVDBReader();
            Task t1 = Task.Run(() => { VDBFile.ParseVDB(ctx.assetPath, 1234); }); // TODO Check utility of instanceNumber
            t1.Wait(); // TODO wait for task without deadlocking

            //Convert to own voxel format
            for (int i3 = 0; i3 < Mathf.Min(VDBContent.VDBContent.Grids.Length, 2); i3++) // TODO Unsure why we iterate ?
            {
                var CurGrid = i3;
                Vector3 OrigionalSize = new Vector3(VDBContent.VDBContent.Grids[CurGrid].Size.x, VDBContent.VDBContent.Grids[CurGrid].Size.z, VDBContent.VDBContent.Grids[CurGrid].Size.y);
                VDBContent.NonZeroVoxels = new Vector4[VDBContent.VDBContent.Grids[CurGrid].Centers.Count];
                VDBContent.VDBContent.Size = OrigionalSize;

                int RepCount = 0;
                Node4 CurNode;
                Node3 CurNode2;
                Voxel Vox;
                Vector3Int ijk = new Vector3Int(0, 0, 0);
                Vector3 location2 = Vector3.zero;
                uint CurOffset = 0;
                for (int i = 0; i < VDBContent.VDBContent.Grids[CurGrid].Centers.Count; i++)
                {
                    ulong BitIndex1 = (ulong)((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].x & 4095) >> 7) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].y & 4095) >> 7) << 5) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].z & 4095) >> 7) << 10));
                    ulong BitIndex2 = (ulong)((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].x & 127) >> 3) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].y & 127) >> 3) << 4) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].z & 127) >> 3) << 8));
                    ulong BitIndex3 = (ulong)((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].x & 7) >> 0) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].y & 7) >> 0) << 3) | ((((int)VDBContent.VDBContent.Grids[CurGrid].Centers[i].z & 7) >> 0) << 6));

                    if (VDBContent.VDBContent.Grids[CurGrid].RootNode.Children.TryGetValue(BitIndex1, out CurNode))
                    {
                        if (CurNode.Children.TryGetValue(BitIndex2, out CurNode2))
                        {
                            if (CurNode2.Children.TryGetValue(BitIndex3, out Vox))
                            {
                                location2 = new Vector3(VDBContent.VDBContent.Grids[CurGrid].Centers[i].z, VDBContent.VDBContent.Grids[CurGrid].Centers[i].x, VDBContent.VDBContent.Grids[CurGrid].Centers[i].y);
                                float Val = System.BitConverter.ToSingle(System.BitConverter.GetBytes((uint)Vox.Density)) * 100000000000000000000000000000000000000.0f * 50.0f; // TODO constant, why ?
                                if (Val > 0.01f)
                                {
                                    VDBContent.NonZeroVoxels[CurOffset] = new Vector4(location2.x, location2.y, location2.z, Val);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}