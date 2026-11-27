namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using global::Steamworks;
    using UnityEngine;

    /// <summary>Steam backend on Steamworks.NET (<c>com.rlabrecque.steamworks.net</c>).</summary>
    /// <remarks>
    /// Steamworks.NET delivers callbacks only when <c>SteamAPI.RunCallbacks</c> runs, so this backend installs a hidden
    /// pump while Steam is running and removes it on shutdown. Steamworks.NET ships its own native libraries; do not also
    /// import the SDK through <c>com.unity.steamworks-api</c> in the same project.
    /// </remarks>
    public sealed class SteamworksNetBackend : ISteamBackend
    {
        private Callback<UserStatsStored_t> statsStored;
        private Callback<GameRichPresenceJoinRequested_t> joinRequested;
        private UniTaskCompletionSource<int> store;
        private SteamworksNetPump pump;
        private bool running;

        /// <inheritdoc />
        public string Name => "Steamworks.NET";

        /// <inheritdoc />
        public event Action<ulong, string> JoinRequested;

        /// <inheritdoc />
        public SteamStartResult Start(uint expectedAppId, uint restartAppId)
        {
            try
            {
                if (CallbackDispatcher.IsInitialized)
                    return new SteamStartResult(SteamStartStatus.AlreadyInitialized, 0, "Another owner initialized Steamworks.NET.");
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
                uint app = SteamUtils.GetAppID().m_AppId;
                if (expectedAppId != 0 && app != expectedAppId)
                {
                    Shutdown();
                    return new SteamStartResult(SteamStartStatus.AppIdMismatch, app, "Expected App ID " + expectedAppId + ".");
                }
                statsStored = Callback<UserStatsStored_t>.Create(OnStatsStored);
                joinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnJoinRequested);
                pump = SteamworksNetPump.Create();
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
            statsStored?.Dispose();
            joinRequested?.Dispose();
            statsStored = null;
            joinRequested = null;
            store?.TrySetCanceled();
            if (pump != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(pump.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(pump.gameObject);
                pump = null;
            }
            SteamAPI.Shutdown();
        }

        /// <inheritdoc />
        public bool LoggedOn => running && SteamUser.BLoggedOn();

        /// <inheritdoc />
        public ulong LocalUser => running ? SteamUser.GetSteamID().m_SteamID : 0;

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
            JoinRequested?.Invoke(request.m_steamIDFriend.m_SteamID, request.m_rgchConnect);

        /// <inheritdoc />
        public async UniTask<(int result, ulong board)> FindLeaderboardAsync(string name, CancellationToken token)
        {
            var (found, failed) = await CallAsync<LeaderboardFindResult_t>(SteamUserStats.FindLeaderboard(name), token);
            if (failed)
                return ((int)EResult.k_EResultIOFailure, 0UL);
            return ((int)EResult.k_EResultOK, found.m_bLeaderboardFound != 0 ? found.m_hSteamLeaderboard.m_SteamLeaderboard : 0UL);
        }

        /// <inheritdoc />
        public async UniTask<(int result, SteamLeaderboardRow[] rows)> DownloadLeaderboardAsync(ulong board, int first, int last, CancellationToken token)
        {
            var (downloaded, failed) = await CallAsync<LeaderboardScoresDownloaded_t>(
                SteamUserStats.DownloadLeaderboardEntries(new SteamLeaderboard_t(board), ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, first, last),
                token
            );
            if (failed)
                return ((int)EResult.k_EResultIOFailure, Array.Empty<SteamLeaderboardRow>());
            var rows = new SteamLeaderboardRow[Math.Max(0, downloaded.m_cEntryCount)];
            for (int i = 0; i < rows.Length; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(downloaded.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t entry, null, 0))
                    return ((int)EResult.k_EResultFail, Array.Empty<SteamLeaderboardRow>());
                rows[i] = new SteamLeaderboardRow(entry.m_steamIDUser.m_SteamID, entry.m_nGlobalRank, entry.m_nScore);
            }
            return ((int)EResult.k_EResultOK, rows);
        }

        private static async UniTask<(T value, bool ioFailure)> CallAsync<T>(SteamAPICall_t call, CancellationToken token)
            where T : struct
        {
            var completion = new UniTaskCompletionSource<(T, bool)>();
            var result = CallResult<T>.Create((value, ioFailure) => completion.TrySetResult((value, ioFailure)));
            try
            {
                result.Set(call);
                using (token.Register(() => completion.TrySetCanceled(token)))
                    return await completion.Task;
            }
            finally
            {
                result.Dispose();
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
                friends[i] = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate).m_SteamID;
            return friends;
        }

        /// <inheritdoc />
        public bool InviteUserToGame(ulong friend, string connect) => SteamFriends.InviteUserToGame(new CSteamID(friend), connect);

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
    }

    internal static class SteamworksNetBackendRegistration
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => SteamPlatform.BackendFactory ??= () => new SteamworksNetBackend();
    }
}
