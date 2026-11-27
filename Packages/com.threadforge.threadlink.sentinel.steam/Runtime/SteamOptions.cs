namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>Steam statistic storage type.</summary>
    public enum SteamStatType
    {
        /// <summary>32-bit integer statistic.</summary>
        Integer,

        /// <summary>Float statistic.</summary>
        Float,
    }

    /// <summary>Explicit mapping between game identifiers and Steamworks API names.</summary>
    public sealed class SteamOptions
    {
        private readonly Dictionary<string, (string api, string progressStat, int maximum)> achievements =
            new Dictionary<string, (string, string, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, (string api, SteamStatType type)> statistics =
            new Dictionary<string, (string, SteamStatType)>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> leaderboards = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> dlc = new Dictionary<string, uint>(StringComparer.Ordinal);

        /// <summary>Configured App ID. Zero accepts the App ID Steam initializes with (for example from steam_appid.txt).</summary>
        public uint AppId { get; set; }

        /// <summary>Ask Steam to relaunch a player started outside the client. Ignored in the Editor.</summary>
        public bool RestartThroughSteam { get; set; }

        /// <summary>Rich presence key that carries <see cref="IPresence"/> state.</summary>
        public string PresenceKey { get; set; } = "status";

        /// <summary>Steam Cloud file name prefix for <see cref="ICloudStorage"/> slots.</summary>
        public string CloudFilePrefix { get; set; } = "threadlink_slot_";

        /// <summary>
        /// Authors a Steam achievement: the name game code uses (its <see cref="AchievementId"/>), the Steam API name, and
        /// optionally an integer statistic that carries progress.
        /// </summary>
        public SteamOptions Achievement(string id, string apiName, string progressStat = null, int progressMaximum = 0)
        {
            Require(id, apiName);
            if (progressStat != null && progressMaximum < 1)
                throw new ArgumentOutOfRangeException(nameof(progressMaximum));
            achievements[id] = (apiName, progressStat, progressMaximum);
            return this;
        }

        /// <summary>Authors a Steam statistic: the name game code uses (its <see cref="StatisticId"/>), the Steam API name and type.</summary>
        public SteamOptions Statistic(string id, string apiName, SteamStatType type)
        {
            Require(id, apiName);
            statistics[id] = (apiName, type);
            return this;
        }

        /// <summary>Authors a Steam leaderboard: the name game code uses (its <see cref="LeaderboardId"/>) and the Steam leaderboard name.</summary>
        public SteamOptions Leaderboard(string id, string apiName)
        {
            Require(id, apiName);
            leaderboards[id] = apiName;
            return this;
        }

        /// <summary>Authors a Steam DLC: the name game code uses (its <see cref="ProductId"/>) and the DLC App ID.</summary>
        public SteamOptions Dlc(string product, uint app)
        {
            Require(product, app == 0 ? null : "app");
            dlc[product] = app;
            return this;
        }

        internal bool TryAchievement(AchievementId id, out (string api, string progressStat, int maximum) value) =>
            achievements.TryGetValue(id.Name ?? string.Empty, out value);

        // Translation from Sentinel's platform-agnostic IDs to this platform's own names.
        internal bool TryStatistic(StatisticId id, out (string api, SteamStatType type) value) =>
            statistics.TryGetValue(id.Name ?? string.Empty, out value);

        internal bool TryLeaderboard(LeaderboardId id, out string value) => leaderboards.TryGetValue(id.Name ?? string.Empty, out value);

        internal bool TryDlc(ProductId id, out uint value) => dlc.TryGetValue(id.Name ?? string.Empty, out value);

        private static void Require(string id, string value)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Game identifiers and Steam names are required.");
        }
    }

    /// <summary>Maps Steam EResult codes to Sentinel errors.</summary>
    internal static class SteamResults
    {
        /// <summary>EResult k_EResultOK.</summary>
        public const int Ok = 1;

        /// <summary>Maps a non-OK EResult (values from the Steamworks SDK's EResult enumeration).</summary>
        public static PlatformError Map(int result)
        {
            switch (result)
            {
                case 3: // NoConnection
                case 10: // Busy
                case 16: // Timeout
                case 20: // ServiceUnavailable
                case 35: // ConnectFailed
                case 37: // IOFailure
                case 38: // RemoteDisconnect
                case 84: // RateLimitExceeded
                    return PlatformError.Unavailable;
                case 8: // InvalidParam
                    return PlatformError.InvalidData;
                case 9: // FileNotFound
                    return PlatformError.NotFound;
                case 15: // AccessDenied
                case 24: // InsufficientPrivilege
                    return PlatformError.PermissionDenied;
                case 18: // AccountNotFound
                case 21: // NotLoggedOn
                    return PlatformError.AccountChanged;
                case 25: // LimitExceeded
                case 54: // DiskFull
                    return PlatformError.QuotaExceeded;
                case 52: // Cancelled
                    return PlatformError.Cancelled;
                case 60: // RemoteFileConflict
                    return PlatformError.Conflict;
                default:
                    return PlatformError.NativeFailure;
            }
        }
    }
}
