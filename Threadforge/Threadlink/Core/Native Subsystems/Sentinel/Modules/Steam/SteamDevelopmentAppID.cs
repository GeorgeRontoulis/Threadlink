namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.IO;

    internal static class SteamDevelopmentAppID
    {
        internal static bool TryResolve(out uint appID)
        {
            if (TryParse(Environment.GetEnvironmentVariable("SteamAppId"), out appID))
                return true;

            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt"),
                Path.Combine(AppContext.BaseDirectory, "steam_appid.txt"),
            };

            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path) && TryParse(File.ReadAllText(path), out appID))
                        return true;
                }
                catch
                {
                    // Try the next canonical development location.
                }
            }

            appID = 0;
            return false;
        }

        internal static bool TryParse(string text, out uint appID) =>
            uint.TryParse(text?.Trim(), out appID) && appID != 0;
    }
}
