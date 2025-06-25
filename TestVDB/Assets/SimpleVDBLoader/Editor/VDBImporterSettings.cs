using SimpleVDBLoader;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimpleVDBLoader
{
    [CustomEditor(typeof(VDBImporter))]
    public class VDBImporterSettings : ScriptedImporterEditor
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
            var propFileContent = serializedObject.FindProperty("VDBContent");
            var fileContent = propFileContent.boxedValue as VDBFileContent;
            var selectedIndex = EditorGUILayout.Popup("Max resolution", prop.intValue, availableMaxResolutions);
            prop.intValue = selectedIndex;
            //fileContent.VDBContent.Weight(); // TODO ? to get actual disk size
            // Also get in memory size ?
            base.ApplyRevertGUI();
        }
    }
}