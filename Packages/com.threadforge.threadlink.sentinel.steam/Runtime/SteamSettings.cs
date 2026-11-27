namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;
    using UnityEngine.Serialization;

    /// <summary>
    /// Engineering settings for Steam: whether it starts, the App ID, and the game's Steam authoring. Achievements are
    /// authored here; each becomes a platform-agnostic <c>Achievements.Name</c> ID that Sentinel passes back to Steam.
    /// </summary>
    public sealed class SteamSettings : ThreadlinkSettings<SteamSettings>
    {
        /// <summary>A Steam achievement and the platform-agnostic name game code uses for it.</summary>
        [Serializable]
        public sealed class SteamAchievement
        {
            /// <summary>Name game code uses; shared with other platforms that author the same achievement.</summary>
            [FormerlySerializedAs("Id")]
            [Tooltip("Name game code uses: generates Achievements.<Name>. Use the same name on every platform.")]
            public string Name;

            /// <summary>Steam API name; empty uses <see cref="Name"/>.</summary>
            [Tooltip("API name in Steamworks. Empty uses the name.")]
            public string ApiName;

            /// <summary>Optional integer statistic that carries progress.</summary>
            [Tooltip("Optional integer stat that tracks progress.")]
            public string ProgressStat;

            /// <summary>Progress maximum for <see cref="ProgressStat"/>.</summary>
            [Tooltip("Stat value that completes the achievement.")]
            public int ProgressMaximum;
        }

        /// <summary>A Steam statistic and the platform-agnostic name game code uses for it.</summary>
        [Serializable]
        public sealed class SteamStatistic
        {
            /// <summary>Name game code uses; shared with other platforms that author the same statistic.</summary>
            [FormerlySerializedAs("Id")]
            [Tooltip("Name game code uses: generates Statistics.<Name>. Use the same name on every platform.")]
            public string Name;

            /// <summary>Steam API name; empty uses <see cref="Name"/>.</summary>
            [Tooltip("API name in Steamworks. Empty uses the name.")]
            public string ApiName;

            /// <summary>Integer or float.</summary>
            [Tooltip("Integer or float, as defined in Steamworks.")]
            public SteamStatType Type;
        }

        /// <summary>A Steam leaderboard and the platform-agnostic name game code uses for it.</summary>
        [Serializable]
        public sealed class SteamLeaderboard
        {
            /// <summary>Name game code uses; shared with other platforms that author the same leaderboard.</summary>
            [FormerlySerializedAs("Id")]
            [Tooltip("Name game code uses: generates Leaderboards.<Name>. Use the same name on every platform.")]
            public string Name;

            /// <summary>Steam leaderboard name; empty uses <see cref="Name"/>.</summary>
            [Tooltip("Leaderboard name in Steamworks. Empty uses the name.")]
            [FormerlySerializedAs("SteamName")]
            public string ApiName;
        }

        /// <summary>A Steam DLC and the platform-agnostic product name game code uses for it.</summary>
        [Serializable]
        public sealed class SteamDlc
        {
            /// <summary>Name game code uses; shared with other platforms that author the same product.</summary>
            [FormerlySerializedAs("Product")]
            [Tooltip("Name game code uses: generates Products.<Name>. Use the same name on every platform.")]
            public string Name;

            /// <summary>DLC App ID.</summary>
            [Tooltip("The DLC's Steam App ID.")]
            public uint AppId;
        }

        /// <summary>Start Steam with the session. Off by default: tests and the Editor never touch Steam unless enabled.</summary>
        [Tooltip("Start Steam with the session. Off keeps the Editor and tests away from Steam.")]
        public bool Enabled;

        /// <summary>The App ID. Zero accepts the App ID Steam starts with (for example from steam_appid.txt).</summary>
        [Tooltip("Steam App ID. 0 uses the one Steam starts with (steam_appid.txt).")]
        public uint AppId;

        /// <summary>Ask Steam to relaunch a player started outside the client.</summary>
        [Tooltip("Relaunch through Steam when started outside the Steam client.")]
        public bool RestartThroughSteam;

        /// <summary>The game's Steam achievements.</summary>
        [Tooltip("Achievements authored for Steam. Each becomes an Achievements ID.")]
        public SteamAchievement[] Achievements = Array.Empty<SteamAchievement>();

        /// <summary>The game's Steam statistics.</summary>
        [Tooltip("Statistics authored for Steam. Each becomes a Statistics ID.")]
        public SteamStatistic[] Statistics = Array.Empty<SteamStatistic>();

        /// <summary>The game's Steam leaderboards.</summary>
        [Tooltip("Leaderboards authored for Steam. Each becomes a Leaderboards ID.")]
        public SteamLeaderboard[] Leaderboards = Array.Empty<SteamLeaderboard>();

        /// <summary>The game's Steam DLC.</summary>
        [Tooltip("DLC authored for Steam. Each becomes a Products ID.")]
        public SteamDlc[] Dlc = Array.Empty<SteamDlc>();

        internal SteamOptions CreateOptions()
        {
            var options = new SteamOptions { AppId = AppId, RestartThroughSteam = RestartThroughSteam };
            foreach (var achievement in Achievements)
                if (achievement != null && !string.IsNullOrWhiteSpace(achievement.Name))
                    options.Achievement(achievement.Name, string.IsNullOrWhiteSpace(achievement.ApiName) ? achievement.Name : achievement.ApiName, string.IsNullOrWhiteSpace(achievement.ProgressStat) ? null : achievement.ProgressStat, achievement.ProgressMaximum);
            foreach (var statistic in Statistics)
                if (statistic != null && !string.IsNullOrWhiteSpace(statistic.Name))
                    options.Statistic(statistic.Name, string.IsNullOrWhiteSpace(statistic.ApiName) ? statistic.Name : statistic.ApiName, statistic.Type);
            foreach (var board in Leaderboards)
                if (board != null && !string.IsNullOrWhiteSpace(board.Name))
                    options.Leaderboard(board.Name, string.IsNullOrWhiteSpace(board.ApiName) ? board.Name : board.ApiName);
            foreach (var dlc in Dlc)
                if (dlc != null && !string.IsNullOrWhiteSpace(dlc.Name) && dlc.AppId != 0)
                    options.Dlc(dlc.Name, dlc.AppId);
            return options;
        }

        /// <inheritdoc />
        public override string Validate() =>
            Enabled && SteamPlatform.BackendFactory == null && Type.GetType("Threadforge.Threadlink.UnitySteamworksBackend, Threadforge.Threadlink.Sentinel.Steam.Unity") == null
                && Type.GetType("Threadforge.Threadlink.SteamworksNetBackend, Threadforge.Threadlink.Sentinel.Steam.SteamworksNet") == null
                ? "Steam is enabled but neither Unity's Steamworks API nor Steamworks.NET is installed."
                : null;
    }

    /// <summary>
    /// Starts Steam with the session when enabled in Project Settings ▸ Threadlink ▸ Steam, and routes Sentinel's
    /// accounts, achievements, statistics, leaderboards, presence, friends, invites, entitlements and cloud saves to it.
    /// </summary>
    public sealed class SteamPlatform : ThreadlinkSubsystem<SteamPlatform>
    {
        /// <summary>Creates the Steam wrapper. The installed wrapper's assembly sets it; tests may replace it.</summary>
        public static Func<ISteamBackend> BackendFactory { get; set; }

        private SteamProvider provider;

        /// <summary>The provider, or null while Steam is disabled or failed to start.</summary>
        public static SteamProvider Provider => Instance.provider;

        // Steam signs in through Sentinel's accounts and routes Sentinel's features, so it starts once every subsystem has
        // booted. Sentinel announces PlatformReady when the provider is added.
        /// <inheritdoc />
        protected override void OnSubsystemInit()
        {
            var settings = SteamSettings.Current;
            if (!settings.Enabled)
                return;
            if (BackendFactory == null)
                throw new InvalidOperationException("Steam is enabled but no Steamworks wrapper is installed.");
            var created = new SteamProvider(BackendFactory(), Sentinel.Accounts, settings.CreateOptions());
            var result = created.Start(!Application.isEditor);
            if (result.Status != SteamStartStatus.Ready)
            {
                created.Dispose();
                Debug.LogWarning("Threadlink: Steam did not start (" + result.Status + "). " + result.Detail);
                return;
            }
            provider = created;
            Sentinel.AddProvider(provider, provider.Supported);
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(provider != null ? "Started" : SteamSettings.Current.Enabled ? "Not started" : "Disabled", SteamSettings.Current.Enabled && provider == null);
            if (report.Detailed)
                report.Section("Steam").Field("Wrapper", provider?.Backend.Name ?? (BackendFactory != null ? "Installed" : "None")).Field("App ID", (long)SteamSettings.Current.AppId);
        }
    }
}
