namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;

    /// <summary>Checks Steam wrapper and native library setup. Read-only.</summary>
    public sealed class SteamValidator : IWorkspaceValidator
    {
        private const string UnityPackage = "com.unity.steamworks-api";
        private const string NetPackage = "com.rlabrecque.steamworks.net";

        /// <inheritdoc />
        public string Name => "Steam";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            var installed = new HashSet<string>(PackageInfo.GetAllRegisteredPackages().Select(package => package.name));
            bool unity = installed.Contains(UnityPackage);
            bool net = installed.Contains(NetPackage);
            if (!unity && !net)
            {
                issues.Add(new ValidationIssue(Name, IssueSeverity.Info, "No Steam wrapper is installed; SteamProvider has no backend adapter."));
                return;
            }
            if (unity && net)
                issues.Add(
                    new ValidationIssue(
                        Name,
                        IssueSeverity.Warning,
                        "Both com.unity.steamworks-api and Steamworks.NET are installed. Each brings steam_api native libraries; keep one wrapper per project."
                    )
                );
            if (unity && !File.Exists("Assets/Plugins/Steamworks/win64/steam_api64.dll") && !File.Exists("Assets/Plugins/Steamworks/linux64/libsteam_api.so") && !File.Exists("Assets/Plugins/Steamworks/osx/libsteam_api.dylib"))
                issues.Add(
                    new ValidationIssue(Name, IssueSeverity.Warning, "Steam native libraries are not imported. Use Project Settings ▸ Steamworks ▸ Import Steamworks SDK.")
                );
            foreach (string guid in AssetDatabase.FindAssets("redistributable_bin", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path))
                    continue;
                string sdk = Path.GetDirectoryName(Path.GetDirectoryName(path))?.Replace('\\', '/');
                issues.Add(
                    new ValidationIssue(
                        Name,
                        IssueSeverity.Error,
                        $"A raw Steamworks SDK is inside Assets ({sdk}). Unity imports its sample C++ as IL2CPP plugins and duplicates steam_api. Move it outside Assets and import libraries through the wrapper.",
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sdk)
                    )
                );
            }
        }
    }
}
