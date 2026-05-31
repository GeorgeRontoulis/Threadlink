namespace Threadlink.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;

    internal sealed class SteamAppIdBuildProcessor : IPostprocessBuildWithReport
    {
        // Execute first in the post-build queue
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            // Restrict this operation to standalone builds where Steamworks operates
            if (report.summary.platformGroup is not BuildTargetGroup.Standalone)
                return;

            const string fileName = "steam_appid.txt";

            // Directory.GetCurrentDirectory() reliably points to the Unity project root
            string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            // report.summary.outputPath returns the full path to the executable (e.g., /Build/Game.exe)
            string buildDirectory = Path.GetDirectoryName(report.summary.outputPath);
            string destinationPath = Path.Combine(buildDirectory, fileName);

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destinationPath, true);
                Debug.Log($"[Steamworks Setup] Successfully copied {fileName} to {destinationPath}");
            }
            else Debug.LogWarning($"[Steamworks Setup] Failed to locate {fileName} at {sourcePath}. The build will fail to initialize SteamAPI.");
        }
    }
}