namespace Threadlink.Editor
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    internal static class BinariesCleaner
    {
        [MenuItem("Threadlink/Clear all Binaries")]
        private static void ClearAllBinaries()
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Binaries Folder", Application.dataPath, string.Empty);

            if (string.IsNullOrEmpty(selectedPath))
                return;

            selectedPath = Path.GetFullPath(selectedPath);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            if (!selectedPath.StartsWith(projectRoot))
            {
                Scribe.Send<Threadlink>("Selected folder must be inside the Unity project.").ToUnityConsole(DebugType.Error);
                return;
            }

            if (!Directory.Exists(selectedPath))
            {
                Scribe.Send<Threadlink>("Selected folder does not exist.").ToUnityConsole(DebugType.Error);
                return;
            }

            var files = Directory.GetFiles(selectedPath, "*.bytes", SearchOption.AllDirectories);

            foreach (var file in files)
                File.WriteAllBytes(file, System.Array.Empty<byte>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scribe.Send<Threadlink>($"Cleared {files.Length} binaries.").ToUnityConsole();
        }
    }
}
