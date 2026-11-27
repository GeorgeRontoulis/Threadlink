namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Unity.Steamworks.Interop;

    /// <summary>Steam backend on Unity's <c>com.unity.steamworks-api</c> package (<c>Unity.Steamworks.Interop</c>).</summary>
    /// <remarks>
    /// The package delivers callbacks on the main thread with its default dispatch mode, so no pump is installed.
    /// Import the Steamworks SDK libraries through Project Settings ▸ Steamworks; this adapter never initializes on its own.
    /// The package cannot report initialization by other code, so the provider must be the only Steam API initializer;
    /// a second backend instance in the same process is refused.
    /// </remarks>
    public sealed class UnitySteamworksBackend : ISteamBackend
    {
        private static UnitySteamworksBackend owner;

        private UniTaskCompletionSource<int> store;
        private bool running;

        /// <inheritdoc />
        public string Name => "Unity Steamworks API";

        /// <inheritdoc />
        public event Action<ulong, string> JoinRequested;

        /// <inheritdoc />
        public SteamStartResult Start(uint expectedAppId, uint restartAppId)
        {
            try
            {
                if (owner != null && owner != this)
                    return new SteamStartResult(SteamStartStatus.AlreadyInitialized, 0, "Another backend instance owns the Steam API.");
                if (!SteamAPI.IsSteamRunning())
                    return new SteamStartResult(SteamStartStatus.ClientNotRunning);
#if !UNITY_EDITOR
                if (restartAppId != 0 && SteamAPI.RestartAppIfNecessary(new AppId_t(restartAppId)))
                    return new SteamStartResult(SteamStartStatus.RestartRequired, restartAppId);
#endif
                var init = SteamAPI.InitEx(out string error);
                if (init != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                    return new SteamStartResult(SteamStartStatus.Failed, 0, init + ": " + error);
                running = true;
                owner = this;
                uint app = (uint)SteamUtils.GetAppID();
                if (expectedAppId != 0 && app != expectedAppId)
                {
                    Shutdown();
                    return new SteamStartResult(SteamStartStatus.AppIdMismatch, app, "Expected App ID " + expectedAppId + ".");
                }
                SteamCallbacks.Subscribe<UserStatsStored_t>(OnStatsStored);
                SteamCallbacks.Subscribe<GameRichPresenceJoinRequested_t>(OnJoinRequested);
                return new SteamStartResult(SteamStartStatus.Ready, app);
            }
            catch (DllNotFoundException exception)
            {
                return new SteamStartResult(SteamStartStatus.Failed, 0, "Steam native library not found: " + exception.Message);
            }
            catch (Exception exception)
            {
                if (running)
                    Shutdown();
                return new SteamStartResult(SteamStartStatus.Failed, 0, exception.Message);
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (!running)
                return;
            running = false;
            SteamCallbacks.Unsubscribe<UserStatsStored_t>(OnStatsStored);
            SteamCallbacks.Unsubscribe<GameRichPresenceJoinRequested_t>(OnJoinRequested);
            store?.TrySetCanceled();
            if (owner == this)
                owner = null;
            SteamAPI.Shutdown();
        }

        /// <inheritdoc />
        public bool LoggedOn => running && SteamUser.BLoggedOn();

        /// <inheritdoc />
        public ulong LocalUser => running ? (ulong)SteamUser.GetSteamID() : 0;

        /// <inheritdoc />
        public bool TryGetAchievement(string name, out bool unlocked) => SteamUserStats.GetAchievement(name, out unlocked);

        /// <inheritdoc />
        public bool SetAchievement(string name) => SteamUserStats.SetAchievement(name);

        /// <inheritdoc />
        public bool IndicateAchievementProgress(string name, uint current, uint maximum) =>
            SteamUserStats.IndicateAchievementProgress(name, current, maximum);

        /// <inheritdoc />
        public bool TryGetStat(string name, out int value) => SteamUserStats.GetStat(name, out value);

        /// <inheritdoc />
        public bool TryGetStat(string name, out float value) => SteamUserStats.GetStat(name, out value);

        /// <inheritdoc />
        public bool SetStat(string name, int value) => SteamUserStats.SetStat(name, value);

        /// <inheritdoc />
        public bool SetStat(string name, float value) => SteamUserStats.SetStat(name, value);

        /// <inheritdoc />
        public async UniTask<int> StoreStatsAsync(CancellationToken token)
        {
            var completion = new UniTaskCompletionSource<int>();
            store = completion;
            if (!SteamUserStats.StoreStats())
            {
                store = null;
                return (int)EResult.k_EResultFail;
            }
            using (token.Register(() => completion.TrySetCanceled(token)))
                return await completion.Task;
        }

        private void OnStatsStored(UserStatsStored_t stored)
        {
            var pending = store;
            store = null;
            pending?.TrySetResult((int)stored.m_eResult);
        }

        private void OnJoinRequested(GameRichPresenceJoinRequested_t request) =>
            JoinRequested?.Invoke((ulong)request.m_steamIDFriend, request.m_rgchConnect);

        /// <inheritdoc />
        public async UniTask<(int result, ulong board)> FindLeaderboardAsync(string name, CancellationToken token)
        {
            try
            {
                var found = await WithCancellation(SteamCallbacks.CallResultAsync<LeaderboardFindResult_t>(() => SteamUserStats.FindLeaderboard(name)), token);
                return ((int)EResult.k_EResultOK, found.m_bLeaderboardFound != 0 ? (ulong)found.m_hSteamLeaderboard : 0UL);
            }
            catch (SteamCallResultException)
            {
                return ((int)EResult.k_EResultIOFailure, 0UL);
            }
        }

        /// <inheritdoc />
        public async UniTask<(int result, SteamLeaderboardRow[] rows)> DownloadLeaderboardAsync(ulong board, int first, int last, CancellationToken token)
        {
            try
            {
                var downloaded = await WithCancellation(
                    SteamCallbacks.CallResultAsync<LeaderboardScoresDownloaded_t>(
                        () => SteamUserStats.DownloadLeaderboardEntries((SteamLeaderboard_t)board, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, first, last)
                    ),
                    token
                );
                var rows = new SteamLeaderboardRow[Math.Max(0, downloaded.m_cEntryCount)];
                for (int i = 0; i < rows.Length; i++)
                {
                    if (!SteamUserStats.GetDownloadedLeaderboardEntry(downloaded.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t entry, null, 0))
                        return ((int)EResult.k_EResultFail, Array.Empty<SteamLeaderboardRow>());
                    rows[i] = new SteamLeaderboardRow((ulong)entry.m_steamIDUser, entry.m_nGlobalRank, entry.m_nScore);
                }
                return ((int)EResult.k_EResultOK, rows);
            }
            catch (SteamCallResultException)
            {
                return ((int)EResult.k_EResultIOFailure, Array.Empty<SteamLeaderboardRow>());
            }
        }

        /// <inheritdoc />
        public bool SetRichPresence(string key, string value) => SteamFriends.SetRichPresence(key, value);

        /// <inheritdoc />
        public ulong[] GetFriends()
        {
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            var friends = new ulong[Math.Max(0, count)];
            for (int i = 0; i < friends.Length; i++)
                friends[i] = (ulong)SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            return friends;
        }

        /// <inheritdoc />
        public bool InviteUserToGame(ulong friend, string connect) => SteamFriends.InviteUserToGame((CSteamID)friend, connect);

        /// <inheritdoc />
        public bool IsDlcInstalled(uint app) => SteamApps.BIsDlcInstalled(new AppId_t(app));

        /// <inheritdoc />
        public string GetLaunchCommandLine()
        {
            int length = SteamApps.GetLaunchCommandLine(out string line, 4096);
            return length > 0 ? line : string.Empty;
        }

        /// <inheritdoc />
        public bool CloudEnabled => SteamRemoteStorage.IsCloudEnabledForAccount() && SteamRemoteStorage.IsCloudEnabledForApp();

        /// <inheritdoc />
        public bool RemoteFileExists(string name) => SteamRemoteStorage.FileExists(name);

        /// <inheritdoc />
        public byte[] RemoteFileRead(string name)
        {
            int size = SteamRemoteStorage.GetFileSize(name);
            if (size <= 0)
                return null;
            var data = new byte[size];
            return SteamRemoteStorage.FileRead(name, data, size) == size ? data : null;
        }

        /// <inheritdoc />
        public bool RemoteFileWrite(string name, byte[] data) => SteamRemoteStorage.FileWrite(name, data, data.Length);

        /// <inheritdoc />
        public void Dispose() => Shutdown();

        // Steamworks call results arrive as Tasks; cancellation stops waiting, not the native call.
        private static UniTask<T> WithCancellation<T>(System.Threading.Tasks.Task<T> task, CancellationToken token) =>
            task.AsUniTask().AttachExternalCancellation(token);
    }

    internal static class UnitySteamworksBackendRegistration
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => SteamPlatform.BackendFactory ??= () => new UnitySteamworksBackend();
    }
}
