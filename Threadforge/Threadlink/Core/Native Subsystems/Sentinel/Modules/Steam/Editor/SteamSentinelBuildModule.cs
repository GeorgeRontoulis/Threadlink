namespace Threadlink.SentinelModules.Steam.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Core.NativeSubsystems.Sentinel;
    using Threadlink.Editor.Sentinel;
    using UnityEditor.PackageManager;

    public sealed class SteamSentinelBuildModule : ISentinelBuildModule
    {
        public string ModuleID => SteamSentinelModuleInfo.ModuleID;
        public string DisplayName => SteamSentinelModuleInfo.DisplayName;

        public bool Implements(in SentinelModuleKey key)
        {
            if (key.Distribution is not SentinelDistribution.Steam)
                return false;

            return key.Platform
                is SentinelPlatformMarker.Windows
                    or SentinelPlatformMarker.MacOS
                    or SentinelPlatformMarker.Linux;
        }

        public void ConfigureBuild(
            in SentinelBuildContext context,
            List<SentinelBuildDiagnostic> diagnostics
        )
        {
            // SteamAppIdBuildProcessor owns build-output staging after a successful build.
        }

        public void ValidateBuild(
            in SentinelBuildContext context,
            List<SentinelBuildDiagnostic> diagnostics
        )
        {
            var package = PackageInfo
                .GetAllRegisteredPackages()
                .FirstOrDefault(x => x.name == SteamSentinelModuleInfo.SteamworksPackageName);

            if (package == null)
            {
                diagnostics.Add(
                    SentinelBuildDiagnostic.Error(
                        "Steamworks.NET is not installed. Install package "
                            + "'com.rlabrecque.steamworks.net' before building the Steam distribution."
                    )
                );
            }
            else
            {
                diagnostics.Add(
                    SentinelBuildDiagnostic.Info(
                        $"Steamworks.NET {package.version} detected; "
                            + $"module validated against {SteamSentinelModuleInfo.ValidatedSteamworksVersion}."
                    )
                );
            }

            var appIDPath = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");

            if (!TryReadAppID(appIDPath, out var appID, out var appIDError))
            {
                diagnostics.Add(SentinelBuildDiagnostic.Error(appIDError));
            }
            else if (appID == 480)
            {
                diagnostics.Add(
                    SentinelBuildDiagnostic.Warning(
                        "Steam App ID 480 is Valve's Spacewar test App ID. Replace it before shipping your own title."
                    )
                );
            }
            else
            {
                diagnostics.Add(
                    SentinelBuildDiagnostic.Info($"Steam development App ID {appID} validated.")
                );
            }

            diagnostics.Add(
                SentinelBuildDiagnostic.Info(
                    "Achievement IDs default to Steam API names TL_ACH_<8-digit uint hex>. "
                        + "Use SteamSentinelSettings.RegisterAchievement for explicit names and progress-stat mappings."
                )
            );
        }

        private static bool TryReadAppID(string path, out uint appID, out string error)
        {
            appID = 0;
            error = null;

            if (!File.Exists(path))
            {
                error =
                    "Project-root steam_appid.txt is missing. Sentinel Steam owns this development App ID file "
                    + "and requires it for Editor/development Steam initialization.";

                return false;
            }

            try
            {
                var text = File.ReadAllText(path).Trim();

                if (uint.TryParse(text, out appID) && appID != 0)
                    return true;

                error = "steam_appid.txt must contain one non-zero numeric Steam App ID.";
                return false;
            }
            catch (System.Exception exception)
            {
                error = $"Could not read steam_appid.txt: {exception.Message}";
                return false;
            }
        }
    }
}
