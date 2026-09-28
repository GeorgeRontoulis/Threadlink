namespace Threadlink.SentinelModules.Steam
{
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// A release build's Steam App ID. Release builds ship without <c>steam_appid.txt</c>, so the build copies the
    /// project's App ID into the data folder under a name the Steam client never reads (see
    /// <c>SteamAppIdBuildProcessor</c>). With it, a release build started outside Steam can relaunch through Steam.
    /// </summary>
    internal static class SteamReleaseAppID
    {
        internal const string FILE_NAME = "threadlink_steam_appid.txt";

        internal static bool TryResolve(out uint appID)
        {
            try
            {
                var path = Path.Combine(Application.dataPath, FILE_NAME);

                if (File.Exists(path) && SteamDevelopmentAppID.TryParse(File.ReadAllText(path), out appID))
                    return true;
            }
            catch
            {
                // No readable release App ID: Steam can't be asked to relaunch the game.
            }

            appID = 0;
            return false;
        }
    }
}
