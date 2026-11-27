namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Globalization;
    using System.Threading;

    /// <summary>Steam capabilities through an explicitly selected wrapper backend.</summary>
    /// <remarks>
    /// The provider is the only owner of the Steam API lifecycle: call <see cref="Start"/> once at a game-chosen point
    /// and dispose the provider at shutdown. Nothing initializes on installation. Use on Unity's main thread.
    /// Achievements and statistics are stored through Steam's own confirmation; cancellation after a store request
    /// reports <see cref="PlatformResult{T}.MayHaveCommitted"/> because Steam may still persist it. No call retries.
    /// Commerce is unsupported: Steam microtransactions require a trusted server.
    /// </remarks>
    public sealed class SteamProvider
        : ISentinelProvider,
            IAccounts,
            IAchievements,
            IStatistics,
            ILeaderboards,
            IPresence,
            IFriends,
            IInvites,
            IEntitlements
    {
        /// <summary>Provider identity used in account identities.</summary>
        public const string ProviderIdentity = "steam";

        private readonly ISteamBackend backend;
        private readonly AccountSession accounts;
        private readonly SemaphoreSlim storeGate = new SemaphoreSlim(1, 1);
        private readonly System.Collections.Generic.Dictionary<string, ulong> boards =
            new System.Collections.Generic.Dictionary<string, ulong>(StringComparer.Ordinal);
        private SteamStartResult? started;
        private bool disposed;

        /// <summary>Selected wrapper backend.</summary>
        public ISteamBackend Backend => backend;

        /// <summary>Configuration.</summary>
        public SteamOptions Options { get; }

        /// <summary>Invites and launch connect data captured for game-controlled consumption.</summary>
        public JoinIntentBuffer JoinIntents { get; }

        /// <summary>Last start result, or null before <see cref="Start"/>.</summary>
        public SteamStartResult? StartResult => started;

        /// <inheritdoc />
        public string Identity => ProviderIdentity;

        /// <inheritdoc />
        public Capability Supported =>
            Capability.Accounts
            | Capability.Achievements
            | Capability.Statistics
            | Capability.Leaderboards
            | Capability.Presence
            | Capability.Friends
            | Capability.Invites
            | Capability.Entitlements
            | Capability.JoinIntents
            | Capability.CloudStorage;

        /// <inheritdoc />
        public AccountIdentity? Current =>
            !disposed && accounts.Current.HasValue && accounts.Current.Value.Provider == ProviderIdentity
                ? accounts.Current
                : null;

        /// <summary>Steam Cloud storage for this provider's account.</summary>
        public SteamCloudStorage Cloud { get; }

        /// <summary>Creates a provider. The caller owns the account session and chooses the backend explicitly.</summary>
        public SteamProvider(ISteamBackend backend, AccountSession accounts, SteamOptions options, JoinIntentBuffer joinIntents = null)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            JoinIntents = joinIntents ?? new JoinIntentBuffer();
            Cloud = new SteamCloudStorage(this);
        }

        /// <summary>Initializes Steam and selects the signed-in Steam account. Repeated calls return the first result.</summary>
        /// <param name="inPlayer">Allow Steam to relaunch a player started outside the client when configured.</param>
        public SteamStartResult Start(bool inPlayer = false)
        {
            CheckOpen();
            if (started.HasValue)
                return started.Value;
            var result = backend.Start(Options.AppId, inPlayer && Options.RestartThroughSteam ? Options.AppId : 0);
            started = result;
            if (result.Status != SteamStartStatus.Ready)
                return result;
            if (backend.LoggedOn && backend.LocalUser != 0)
                accounts.Select(new AccountIdentity(ProviderIdentity, backend.LocalUser.ToString(CultureInfo.InvariantCulture)));
            backend.JoinRequested += OnJoinRequested;
            string launch = backend.GetLaunchCommandLine();
            if (!string.IsNullOrWhiteSpace(launch) && launch.Length <= 4096)
                JoinIntents.Capture(new JoinIntent(ProviderIdentity, "launch:" + launch, launch));
            return result;
        }

        private void OnJoinRequested(ulong friend, string connect)
        {
            if (disposed || connect == null || connect.Length > 4096)
                return;
            JoinIntents.Capture(new JoinIntent(ProviderIdentity, friend.ToString(CultureInfo.InvariantCulture) + ":" + connect, connect));
        }

        /// <inheritdoc />
        public CapabilityStatus Status(Capability capability)
        {
            if (capability == Capability.None || (Supported & capability) != capability)
                return new CapabilityStatus(false, Availability.Unsupported);
            if (disposed)
                return new CapabilityStatus(true, Availability.Failed, "Provider is disposed.");
            if (!started.HasValue)
                return new CapabilityStatus(true, Availability.Initializing, "Call Start first.");
            var start = started.Value;
            switch (start.Status)
            {
                case SteamStartStatus.Ready:
                    break;
                case SteamStartStatus.ClientNotRunning:
                    return new CapabilityStatus(true, Availability.TemporarilyUnavailable, "Steam client is not running.");
                default:
                    return new CapabilityStatus(true, Availability.Failed, start.Status + ": " + start.Detail);
            }
            if (!backend.LoggedOn)
                return new CapabilityStatus(true, Availability.Offline, "Steam user is offline.");
            if (Current == null)
                return new CapabilityStatus(true, Availability.AccountRequired, "Another account is selected.");
            if (capability == Capability.CloudStorage && !backend.CloudEnabled)
                return new CapabilityStatus(true, Availability.TemporarilyUnavailable, "Steam Cloud is disabled for this account or app.");
            return new CapabilityStatus(true, Availability.Ready);
        }

        /// <inheritdoc />
        public bool TryGet<T>(out T capability)
            where T : class
        {
            capability =
                disposed ? null
                : typeof(T) == typeof(ICloudStorage) ? Cloud as T
                : typeof(T) == typeof(IJoinIntents) ? JoinIntents as T
                : this as T;
            return capability != null;
        }

        /// <inheritdoc />
        public async UniTask<PlatformResult<Unit>> SetProgressAsync(AchievementId achievement, double progress, CancellationToken token)
        {
            if (!Ready(Capability.Achievements, token, out PlatformResult<Unit> failure))
                return failure;
            if (!Options.TryAchievement(achievement, out var binding))
                return PlatformResult<Unit>.Failure(PlatformError.NotFound, "Achievement " + achievement + " is not authored for Steam (Project Settings ▸ Threadlink ▸ Steam).");
            if (double.IsNaN(progress))
                return PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Progress is NaN.");
            progress = Math.Max(0, Math.Min(1, progress));
            if (!backend.TryGetAchievement(binding.api, out bool unlocked))
                return PlatformResult<Unit>.Failure(PlatformError.NotFound, "Steam has no achievement " + binding.api);
            if (unlocked)
                return PlatformResult<Unit>.Success(default);
            if (progress >= 1)
            {
                if (!backend.SetAchievement(binding.api))
                    return PlatformResult<Unit>.Failure(PlatformError.NativeFailure, "Steam rejected achievement " + binding.api);
                if (binding.progressStat != null && !backend.SetStat(binding.progressStat, binding.maximum))
                    return PlatformResult<Unit>.Failure(PlatformError.NativeFailure, "Steam rejected statistic " + binding.progressStat);
                return await StoreAsync(token);
            }
            if (binding.progressStat == null)
                return PlatformResult<Unit>.Failure(PlatformError.Unsupported, "Partial progress needs a progress statistic for " + binding.api);
            if (!backend.TryGetStat(binding.progressStat, out int existing))
                return PlatformResult<Unit>.Failure(PlatformError.NotFound, "Steam has no statistic " + binding.progressStat);
            int requested = (int)Math.Round(progress * binding.maximum, MidpointRounding.AwayFromZero);
            if (requested <= existing)
                return PlatformResult<Unit>.Success(default);
            if (!backend.SetStat(binding.progressStat, requested))
                return PlatformResult<Unit>.Failure(PlatformError.NativeFailure, "Steam rejected statistic " + binding.progressStat);
            backend.IndicateAchievementProgress(binding.api, (uint)requested, (uint)binding.maximum);
            return await StoreAsync(token);
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<double>> ReadAsync(StatisticId statistic, CancellationToken token)
        {
            if (!Ready(Capability.Statistics, token, out PlatformResult<double> failure))
                return UniTask.FromResult(failure);
            if (!Options.TryStatistic(statistic, out var binding))
                return UniTask.FromResult(PlatformResult<double>.Failure(PlatformError.NotFound, "Statistic " + statistic + " is not authored for Steam (Project Settings ▸ Threadlink ▸ Steam)."));
            bool read;
            double value;
            if (binding.type == SteamStatType.Integer)
            {
                read = backend.TryGetStat(binding.api, out int integer);
                value = integer;
            }
            else
            {
                read = backend.TryGetStat(binding.api, out float single);
                value = single;
            }
            return UniTask.FromResult(
                read
                    ? PlatformResult<double>.Success(value)
                    : PlatformResult<double>.Failure(PlatformError.NotFound, "Steam has no statistic " + binding.api)
            );
        }

        /// <inheritdoc />
        public async UniTask<PlatformResult<Unit>> WriteAsync(StatisticId statistic, double value, CancellationToken token)
        {
            if (!Ready(Capability.Statistics, token, out PlatformResult<Unit> failure))
                return failure;
            if (!Options.TryStatistic(statistic, out var binding))
                return PlatformResult<Unit>.Failure(PlatformError.NotFound, "Statistic " + statistic + " is not authored for Steam (Project Settings ▸ Threadlink ▸ Steam).");
            if (double.IsNaN(value) || double.IsInfinity(value))
                return PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Statistic value is not finite.");
            bool written =
                binding.type == SteamStatType.Integer
                    ? value >= int.MinValue && value <= int.MaxValue && Math.Abs(value - Math.Round(value)) < 1e-9 && backend.SetStat(binding.api, (int)Math.Round(value))
                    : backend.SetStat(binding.api, (float)value);
            if (!written)
                return PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Steam rejected statistic " + binding.api + " = " + value);
            return await StoreAsync(token);
        }

        private async UniTask<PlatformResult<Unit>> StoreAsync(CancellationToken token)
        {
            await storeGate.WaitAsync(token);
            try
            {
                int result;
                try
                {
                    result = await backend.StoreStatsAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return PlatformResult<Unit>.Failure(PlatformError.Cancelled, "Store requested; Steam may still persist it.", mayHaveCommitted: true);
                }
                return result == SteamResults.Ok
                    ? PlatformResult<Unit>.Success(default)
                    : PlatformResult<Unit>.Failure(SteamResults.Map(result), "UserStatsStored: EResult " + result);
            }
            finally
            {
                storeGate.Release();
            }
        }

        /// <inheritdoc />
        async UniTask<PlatformResult<LeaderboardEntry[]>> ILeaderboards.ReadAsync(LeaderboardId leaderboard, long start, int count, CancellationToken token)
        {
            if (!Ready(Capability.Leaderboards, token, out PlatformResult<LeaderboardEntry[]> failure))
                return failure;
            if (!Options.TryLeaderboard(leaderboard, out var name))
                return PlatformResult<LeaderboardEntry[]>.Failure(PlatformError.NotFound, "Leaderboard " + leaderboard + " is not authored for Steam (Project Settings ▸ Threadlink ▸ Steam).");
            if (start < 0 || count < 1 || start + count > int.MaxValue)
                return PlatformResult<LeaderboardEntry[]>.Failure(PlatformError.InvalidData, "Invalid leaderboard range.");
            if (!boards.TryGetValue(name, out ulong board))
            {
                var found = await backend.FindLeaderboardAsync(name, token);
                if (found.result != SteamResults.Ok)
                    return PlatformResult<LeaderboardEntry[]>.Failure(SteamResults.Map(found.result), "FindLeaderboard: EResult " + found.result);
                if (found.board == 0)
                    return PlatformResult<LeaderboardEntry[]>.Failure(PlatformError.NotFound, "Steam has no leaderboard " + name);
                board = boards[name] = found.board;
            }
            var download = await backend.DownloadLeaderboardAsync(board, (int)start + 1, (int)(start + count), token);
            if (download.result != SteamResults.Ok)
                return PlatformResult<LeaderboardEntry[]>.Failure(SteamResults.Map(download.result), "DownloadLeaderboardEntries: EResult " + download.result);
            var rows = download.rows ?? Array.Empty<SteamLeaderboardRow>();
            var entries = new LeaderboardEntry[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                entries[i] = new LeaderboardEntry(Steam(rows[i].User), rows[i].Rank, rows[i].Score);
            return PlatformResult<LeaderboardEntry[]>.Success(entries);
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<Unit>> SetAsync(string state, CancellationToken token)
        {
            if (!Ready(Capability.Presence, token, out PlatformResult<Unit> failure))
                return UniTask.FromResult(failure);
            return UniTask.FromResult(
                backend.SetRichPresence(Options.PresenceKey, state ?? string.Empty)
                    ? PlatformResult<Unit>.Success(default)
                    : PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Steam rejected rich presence.")
            );
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<AccountIdentity[]>> ListAsync(CancellationToken token)
        {
            if (!Ready(Capability.Friends, token, out PlatformResult<AccountIdentity[]> failure))
                return UniTask.FromResult(failure);
            var friends = backend.GetFriends() ?? Array.Empty<ulong>();
            var result = new AccountIdentity[friends.Length];
            for (int i = 0; i < friends.Length; i++)
                result[i] = Steam(friends[i]);
            return UniTask.FromResult(PlatformResult<AccountIdentity[]>.Success(result));
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<Unit>> SendAsync(AccountIdentity recipient, string joinData, CancellationToken token)
        {
            if (!Ready(Capability.Invites, token, out PlatformResult<Unit> failure))
                return UniTask.FromResult(failure);
            if (recipient.Provider != ProviderIdentity || !ulong.TryParse(recipient.Value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong friend))
                return UniTask.FromResult(PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Recipient is not a Steam account."));
            if (joinData == null || joinData.Length > 4096)
                return UniTask.FromResult(PlatformResult<Unit>.Failure(PlatformError.InvalidData, "Join data is missing or too long."));
            return UniTask.FromResult(
                backend.InviteUserToGame(friend, joinData)
                    ? PlatformResult<Unit>.Success(default)
                    : PlatformResult<Unit>.Failure(PlatformError.NativeFailure, "Steam rejected the invite.")
            );
        }

        /// <inheritdoc />
        public UniTask<PlatformResult<bool>> HasAsync(ProductId product, CancellationToken token)
        {
            if (!Ready(Capability.Entitlements, token, out PlatformResult<bool> failure))
                return UniTask.FromResult(failure);
            return UniTask.FromResult(
                Options.TryDlc(product, out uint app)
                    ? PlatformResult<bool>.Success(backend.IsDlcInstalled(app))
                    : PlatformResult<bool>.Failure(PlatformError.NotFound, "Product " + product + " is not authored for Steam (Project Settings ▸ Threadlink ▸ Steam).")
            );
        }

        /// <summary>Shuts the Steam API down if this provider started it. Account selection is cleared.</summary>
        public void Dispose()
        {
            if (disposed)
                return;
            bool ownsSelection = Current.HasValue;
            disposed = true;
            backend.JoinRequested -= OnJoinRequested;
            if (ownsSelection)
                accounts.Select(null);
            if (started.HasValue && started.Value.Status == SteamStartStatus.Ready)
                backend.Shutdown();
            backend.Dispose();
            storeGate.Dispose();
        }

        internal bool Ready<T>(Capability capability, CancellationToken token, out PlatformResult<T> failure)
        {
            if (token.IsCancellationRequested)
            {
                failure = PlatformResult<T>.Failure(PlatformError.Cancelled);
                return false;
            }
            var status = Status(capability);
            if (status.Availability == Availability.Ready)
            {
                failure = default;
                return true;
            }
            failure = PlatformResult<T>.Failure(
                status.Availability == Availability.AccountRequired ? PlatformError.AccountChanged
                : status.Availability == Availability.Unsupported ? PlatformError.Unsupported
                : PlatformError.Unavailable,
                status.Detail
            );
            return false;
        }

        private static AccountIdentity Steam(ulong user) =>
            new AccountIdentity(ProviderIdentity, user.ToString(CultureInfo.InvariantCulture));

        private void CheckOpen()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SteamProvider));
        }
    }
}
