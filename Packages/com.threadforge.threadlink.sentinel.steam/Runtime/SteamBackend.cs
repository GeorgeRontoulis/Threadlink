namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;

    /// <summary>Outcome of starting a Steam backend.</summary>
    public enum SteamStartStatus
    {
        /// <summary>The Steam API is initialized for a signed-in user.</summary>
        Ready,

        /// <summary>No Steam client is running for the current user.</summary>
        ClientNotRunning,

        /// <summary>Steam is relaunching the game through the client; this process should exit.</summary>
        RestartRequired,

        /// <summary>Another owner already initialized the Steam API in this process.</summary>
        AlreadyInitialized,

        /// <summary>The initialized App ID differs from the configured one.</summary>
        AppIdMismatch,

        /// <summary>The native library is missing or initialization failed for another reason.</summary>
        Failed,
    }

    /// <summary>Result of <see cref="ISteamBackend.Start"/>.</summary>
    public readonly struct SteamStartResult
    {
        /// <summary>Status.</summary>
        public SteamStartStatus Status { get; }

        /// <summary>Initialized App ID, or zero.</summary>
        public uint AppId { get; }

        /// <summary>Native explanation, if any.</summary>
        public string Detail { get; }

        /// <summary>Creates a result.</summary>
        public SteamStartResult(SteamStartStatus status, uint appId = 0, string detail = null)
        {
            Status = status;
            AppId = appId;
            Detail = detail;
        }
    }

    /// <summary>One downloaded leaderboard row.</summary>
    public readonly struct SteamLeaderboardRow
    {
        /// <summary>64-bit Steam ID.</summary>
        public ulong User { get; }

        /// <summary>Global rank, starting at 1.</summary>
        public int Rank { get; }

        /// <summary>Score.</summary>
        public int Score { get; }

        /// <summary>Creates a row.</summary>
        public SteamLeaderboardRow(ulong user, int rank, int score)
        {
            User = user;
            Rank = rank;
            Score = score;
        }
    }

    /// <summary>Thin, wrapper-specific access to the Steamworks calls the provider uses.</summary>
    /// <remarks>
    /// Implemented by the optional <c>Sentinel.Steam.Unity</c> adapter (Unity's <c>com.unity.steamworks-api</c>) and the
    /// <c>Sentinel.Steam.SteamworksNet</c> adapter (Steamworks.NET), or by test doubles. Call it on Unity's main thread.
    /// Asynchronous members return Steam <c>EResult</c> codes; the provider maps them to Sentinel outcomes.
    /// </remarks>
    public interface ISteamBackend : IDisposable
    {
        /// <summary>Wrapper name, such as "Unity Steamworks API".</summary>
        string Name { get; }

        /// <summary>Initializes the Steam API. When <paramref name="restartAppId"/> is non-zero in a player, Steam may relaunch the game.</summary>
        SteamStartResult Start(uint expectedAppId, uint restartAppId);

        /// <summary>Shuts the Steam API down. Called once by the provider's owner.</summary>
        void Shutdown();

        /// <summary>Whether the current user is signed in to Steam.</summary>
        bool LoggedOn { get; }

        /// <summary>64-bit Steam ID of the current user.</summary>
        ulong LocalUser { get; }

        /// <summary>Reads an achievement's unlocked state.</summary>
        bool TryGetAchievement(string name, out bool unlocked);

        /// <summary>Unlocks an achievement locally; persisted by <see cref="StoreStatsAsync"/>.</summary>
        bool SetAchievement(string name);

        /// <summary>Shows Steam's progress notification for an achievement.</summary>
        bool IndicateAchievementProgress(string name, uint current, uint maximum);

        /// <summary>Reads an integer statistic.</summary>
        bool TryGetStat(string name, out int value);

        /// <summary>Reads a float statistic.</summary>
        bool TryGetStat(string name, out float value);

        /// <summary>Writes an integer statistic locally.</summary>
        bool SetStat(string name, int value);

        /// <summary>Writes a float statistic locally.</summary>
        bool SetStat(string name, float value);

        /// <summary>Stores pending statistics and achievements; completes with the <c>UserStatsStored_t</c> result.</summary>
        UniTask<int> StoreStatsAsync(CancellationToken token);

        /// <summary>Finds a leaderboard; completes with (EResult, handle). A missing board returns a zero handle.</summary>
        UniTask<(int result, ulong board)> FindLeaderboardAsync(string name, CancellationToken token);

        /// <summary>Downloads global entries for an inclusive 1-based rank range.</summary>
        UniTask<(int result, SteamLeaderboardRow[] rows)> DownloadLeaderboardAsync(ulong board, int first, int last, CancellationToken token);

        /// <summary>Sets a rich presence key; an empty value clears it.</summary>
        bool SetRichPresence(string key, string value);

        /// <summary>Steam IDs of the user's immediate friends.</summary>
        ulong[] GetFriends();

        /// <summary>Invites a friend with a connect string delivered to their game.</summary>
        bool InviteUserToGame(ulong friend, string connect);

        /// <summary>Whether a DLC app is installed and owned.</summary>
        bool IsDlcInstalled(uint app);

        /// <summary>Connect data the game was launched with (for example from an invite), or empty.</summary>
        string GetLaunchCommandLine();

        /// <summary>Raised on the main thread when the user accepts an invite while the game runs.</summary>
        event Action<ulong, string> JoinRequested;

        /// <summary>Whether Steam Cloud is enabled for both the account and the app.</summary>
        bool CloudEnabled { get; }

        /// <summary>Whether a Steam Cloud file exists.</summary>
        bool RemoteFileExists(string name);

        /// <summary>Reads a whole Steam Cloud file, or null when missing or unreadable.</summary>
        byte[] RemoteFileRead(string name);

        /// <summary>Writes a whole Steam Cloud file.</summary>
        bool RemoteFileWrite(string name, byte[] data);
    }
}
