using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using UnityEditor;

// The importer is registered with Unity's asset pipeline by placing the ScriptedImporter attribute on the
// CubeImporter class. The CubeImporter class implements the abstract ScriptedImporter base class.

namespace SimpleVDBLoader
{
    [ScriptedImporter(1, "vdb")]
    public class VDBImporter : ScriptedImporter
    {
        public float m_Scale = 1;
        public int MaxResolution = 2;

        // The ctx argument contains both input and output data for the import event

        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.DependsOnSourceAsset(ctx.assetPath);
            var gameObjectName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            //ctx.assetPath
            var gameObject = new GameObject(gameObjectName);
            var vdbRenderer = gameObject.AddComponent<VDBRenderer>();
            var assetPath = Path.Combine(Path.GetDirectoryName(ctx.assetPath), Path.GetFileNameWithoutExtension(ctx.assetPath));
            vdbRenderer.FileIn = ctx.GetReferenceToAssetMainObject(ctx.assetPath);
            vdbRenderer.Asset = ctx.assetPath;
            //// 'cube' is a GameObject and is automatically converted into a prefab.
            //// Only the 'Main Asset' is eligible to become a prefab.
            ctx.AddObjectToAsset("main obj", gameObject);
            ctx.SetMainObject(gameObject);

            //var material = new Material(Shader.Find("Standard"));
            //material.color = Color.red;
            var originalFile = System.IO.File.ReadAllBytes(ctx.assetPath);
            //// Assets must be assigned a unique identifier string consistent across imports.
            Debug.Log(ctx.assetPath);
            //ctx.AddObjectToAsset("originalAsset", );

            //// Assets that are not passed into the context as import outputs must be destroyed.
            //var tempMesh = new Mesh();
            //DestroyImmediate(tempMesh);
        }
    }
}