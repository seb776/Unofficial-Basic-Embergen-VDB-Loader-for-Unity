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

        }
    }
}