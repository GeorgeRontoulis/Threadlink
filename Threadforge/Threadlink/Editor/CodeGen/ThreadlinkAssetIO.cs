namespace Threadlink.Editor.CodeGen
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.Utilities.Strings;
    using UnityEditor;

    internal sealed class ThreadlinkAssetIO
    {
        private static readonly List<string> FailureBuffer = new(1);

        internal static void WriteText(string projectRelativePath, string contents)
        {
            EnsureParentFolder(projectRelativePath);
            File.WriteAllText(projectRelativePath.ToAbsolutePath(), contents);
            AssetDatabase.ImportAsset(projectRelativePath, ImportAssetOptions.ForceUpdate);
        }

        internal static void WriteLines(string projectRelativePath, IEnumerable<string> lines)
        {
            EnsureParentFolder(projectRelativePath);
            File.WriteAllLines(projectRelativePath.ToAbsolutePath(), lines);
            AssetDatabase.ImportAsset(projectRelativePath, ImportAssetOptions.ForceUpdate);
        }

        internal static void DeleteAssets(List<string> projectRelativePaths)
        {
            if (projectRelativePaths.Count <= 0)
                return;

            FailureBuffer.Clear();

            AssetDatabase.DeleteAssets(projectRelativePaths.ToArray(), FailureBuffer);

            int failureCount = FailureBuffer.Count;

            for (int i = 0; i < failureCount; i++)
            {
                Scribe.Send<ThreadlinkAssetIO>("Could not delete '", FailureBuffer[i],
                "'. A stale meta file may be left behind.").ToUnityConsole(DebugType.Warning);
            }

            FailureBuffer.Clear();
        }

        internal static void CollectAssetsInFolder(string projectRelativeFolder, string searchPattern, List<string> buffer)
        {
            var absoluteFolder = projectRelativeFolder.ToAbsolutePath();

            if (Directory.Exists(absoluteFolder) is false)
                return;

            var files = Directory.GetFiles(absoluteFolder, searchPattern, SearchOption.TopDirectoryOnly);
            int length = files.Length;

            for (int i = 0; i < length; i++)
                buffer.Add(files[i].ToProjectRelativePath());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsInsideAssets(string projectRelativePath)
        {
            return string.IsNullOrEmpty(projectRelativePath) is false
            && projectRelativePath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static void EnsureFolder(string projectRelativeFolder)
        {
            if (IsInsideAssets(projectRelativeFolder) is false || AssetDatabase.IsValidFolder(projectRelativeFolder))
                return;

            var segments = projectRelativeFolder.Split('/');
            var current = segments[0];
            int length = segments.Length;

            for (int i = 1; i < length; i++)
            {
                var next = string.Concat(current, "/", segments[i]);

                if (AssetDatabase.IsValidFolder(next) is false)
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureParentFolder(string projectRelativePath)
        {
            int separator = projectRelativePath.LastIndexOf('/');

            if (separator > 0)
                EnsureFolder(projectRelativePath[..separator]);
        }
    }
}
