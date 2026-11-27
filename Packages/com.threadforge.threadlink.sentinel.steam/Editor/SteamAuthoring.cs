namespace Threadforge.Threadlink.Editor
{
    using System.Collections.Generic;
    using UnityEditor;

    /// <summary>What the game authors for Steam in Project Settings ▸ Threadlink ▸ Steam, reported to Sentinel's ID generation.</summary>
    public sealed class SteamAuthoring : IPlatformAuthoring
    {
        /// <inheritdoc />
        public string Platform => "Steam";

        /// <inheritdoc />
        public IEnumerable<AuthoredPlatformId> Authored
        {
            get
            {
                var settings = SteamSettings.Current;
                foreach (var achievement in settings.Achievements)
                    if (achievement != null)
                        yield return new AuthoredPlatformId(PlatformIdKind.Achievement, achievement.Name, achievement.ApiName);
                foreach (var statistic in settings.Statistics)
                    if (statistic != null)
                        yield return new AuthoredPlatformId(PlatformIdKind.Statistic, statistic.Name, statistic.ApiName);
                foreach (var board in settings.Leaderboards)
                    if (board != null)
                        yield return new AuthoredPlatformId(PlatformIdKind.Leaderboard, board.Name, board.ApiName);
                foreach (var dlc in settings.Dlc)
                    if (dlc != null)
                        yield return new AuthoredPlatformId(PlatformIdKind.Product, dlc.Name, dlc.AppId == 0 ? null : "DLC " + dlc.AppId);
            }
        }

        /// <inheritdoc />
        public void Open() => SettingsService.OpenProjectSettings("Project/Threadlink/Steam");
    }
}
