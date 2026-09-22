namespace Threadlink.SentinelModules.Steam.Editor
{
    using Core.NativeSubsystems.Sentinel;
    using System.IO;
    using Threadlink.Editor.Sentinel;
    using Threadlink.Shared;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;

    /// <summary>
    /// The project-root file is the canonical Editor/development App ID source.
    /// It is copied only into Steam development builds and removed from release or
    /// non-Steam standalone builds.
    /// </summary>
    internal sealed class SteamAppIdBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup is not BuildTargetGroup.Standalone)
                return;

            var buildDirectory = Path.GetDirectoryName(report.summary.outputPath);

            if (string.IsNullOrEmpty(buildDirectory))
                return;

            var destination = Path.Combine(buildDirectory, "steam_appid.txt");
            var platform = SentinelBuildPlatformResolver.Resolve(report.summary.platform);
            var distribution = ResolveDistribution(platform);
            var isSteam = distribution is SentinelDistribution.Steam;
            var isDevelopment = (report.summary.options & BuildOptions.Development) != 0;

            if (!isSteam || !isDevelopment)
            {
                TryDelete(destination);
                return;
            }

            var source = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");

            try
            {
                if (!File.Exists(source))
                {
                    Debug.LogError("[Sentinel Steam] steam_appid.txt is missing from the project root. "
                    + "The development build cannot initialize Steam.");
                    return;
                }

                File.Copy(source, destination, true);
                Debug.Log($"[Sentinel Steam] Copied steam_appid.txt to {destination}");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Sentinel Steam] Could not stage steam_appid.txt: {exception.Message}");
            }
        }

        private static SentinelDistribution ResolveDistribution(SentinelPlatformMarker platform)
        {
            if (!ThreadlinkConfigFinder.TryGetConfig(out SentinelConfig config))
                return SentinelDistribution.None;

            return config != null ? config.GetDistribution(platform) : SentinelDistribution.None;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Sentinel Steam] Could not remove steam_appid.txt from build output: {exception.Message}");
            }
        }
    }
}
