using SimpleVDBLoader;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace SimpleVDBLoader
{
    [CustomEditor(typeof(VDBImporter))]
    public class VDBImporterSettiings : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            var availableMaxResolutions = new string[]
            {
            "16",
            "32",
            "64",
            "128",
            "256",
            "512",
            "1024",
            "No maximum",
            };
            // Compression (as an encoded video ?) As a texture 3D ? 2D array ?
            // To power of 2 ?
            // Filter (none, bilinear, trilinear)
            // Import animation (fixed time ?)
            // scale ?
            // Mips maps ? (is this relevant ?)
            // Tiny volumetric Renderer in GUI ?

            var resolution = new GUIContent("Max Resolution");
            var prop = serializedObject.FindProperty("MaxResolution");
            var selectedIndex = EditorGUILayout.Popup("Max resolution", prop.intValue, availableMaxResolutions);
            prop.intValue = selectedIndex;
            base.ApplyRevertGUI();
        }
    }
}