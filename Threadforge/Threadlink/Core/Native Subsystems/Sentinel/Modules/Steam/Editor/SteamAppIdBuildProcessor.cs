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
    /// The project-root file is the canonical App ID source.
    /// It is copied only into Steam development builds and removed from release or
    /// non-Steam standalone builds. A release Steam build gets the App ID in its data folder
    /// instead, under a name the Steam client never reads (<see cref="RELEASE_FILE_NAME"/>), so that
    /// it can relaunch through Steam when started outside it.
    /// </summary>
    internal sealed class SteamAppIdBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        // Matches SteamReleaseAppID.FILE_NAME in the runtime module.
        private const string RELEASE_FILE_NAME = "threadlink_steam_appid.txt";

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup is not BuildTargetGroup.Standalone)
                return;

            var buildDirectory = Path.GetDirectoryName(report.summary.outputPath);

            if (string.IsNullOrEmpty(buildDirectory))
                return;

            var destination = Path.Combine(buildDirectory, "steam_appid.txt");
            var releaseDestination = GetReleaseAppIDPath(report.summary.outputPath, report.summary.platform);
            var platform = SentinelBuildPlatformResolver.Resolve(report.summary.platform);
            var distribution = ResolveDistribution(platform);
            var isSteam = distribution is SentinelDistribution.Steam;
            var isDevelopment = (report.summary.options & BuildOptions.Development) != 0;

            if (!isSteam || !isDevelopment)
                TryDelete(destination);

            if (!isSteam || isDevelopment)
                TryDelete(releaseDestination);

            if (!isSteam)
                return;

            var source = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");

            try
            {
                if (!File.Exists(source))
                {
                    Debug.LogError("[Sentinel Steam] steam_appid.txt is missing from the project root. "
                    + (isDevelopment ? "The development build cannot initialize Steam."
                    : "The release build cannot relaunch through Steam when started outside it."));
                    return;
                }

                if (isDevelopment)
                {
                    File.Copy(source, destination, true);
                    Debug.Log($"[Sentinel Steam] Copied steam_appid.txt to {destination}");
                    return;
                }

                if (!uint.TryParse(File.ReadAllText(source).Trim(), out var appID) || appID == 0)
                {
                    Debug.LogError("[Sentinel Steam] steam_appid.txt does not hold a valid App ID. "
                    + "The release build cannot relaunch through Steam when started outside it.");
                    return;
                }

                if (string.IsNullOrEmpty(releaseDestination))
                {
                    Debug.LogError("[Sentinel Steam] Could not find the release build's data folder for its App ID.");
                    return;
                }

                File.WriteAllText(releaseDestination, appID.ToString());
                Debug.Log($"[Sentinel Steam] Recorded App ID {appID} in {releaseDestination}");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Sentinel Steam] Could not stage steam_appid.txt: {exception.Message}");
            }
        }

        /// <summary>
        /// Where a standalone player's data folder (<c>Application.dataPath</c>) is in the build output.
        /// </summary>
        private static string GetReleaseAppIDPath(string outputPath, BuildTarget target)
        {
            if (string.IsNullOrEmpty(outputPath))
                return null;

            string dataFolder = target is BuildTarget.StandaloneOSX
            ? Path.Combine(outputPath, "Contents")
            : Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outputPath) + "_Data");

            return Directory.Exists(dataFolder) ? Path.Combine(dataFolder, RELEASE_FILE_NAME) : null;
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
