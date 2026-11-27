namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using NUnit.Framework;
    using UnityEngine.TestTools;

    /// <summary>Contract tests against a scripted backend; they do not exercise a native Steam client.</summary>
    public sealed class SteamProviderTests
    {
        private sealed class FakeBackend : ISteamBackend
        {
            public SteamStartResult StartResult = new SteamStartResult(SteamStartStatus.Ready, 480);
            public bool Online = true;
            public ulong User = 76561197960287930UL;
            public readonly Dictionary<string, bool> Achievements = new Dictionary<string, bool>();
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public readonly List<string> Calls = new List<string>();
            public UniTaskCompletionSource<int> Store;
            public int StoreResult = 1;
            public string Launch = string.Empty;
            public bool Cloud = true;
            public int Shutdowns;

            public string Name => "Fake";
            public event Action<ulong, string> JoinRequested;

            public void RaiseJoin(ulong friend, string connect) => JoinRequested?.Invoke(friend, connect);

            public SteamStartResult Start(uint expectedAppId, uint restartAppId)
            {
                Calls.Add($"start {expectedAppId} {restartAppId}");
                return StartResult;
            }

            public void Shutdown() => Shutdowns++;

            public bool LoggedOn => Online;
            public ulong LocalUser => User;

            public bool TryGetAchievement(string name, out bool unlocked) => Achievements.TryGetValue(name, out unlocked);

            public bool SetAchievement(string name)
            {
                Calls.Add("unlock " + name);
                Achievements[name] = true;
                return true;
            }

            public bool IndicateAchievementProgress(string name, uint current, uint maximum)
            {
                Calls.Add($"progress {name} {current}/{maximum}");
                return true;
            }

            public bool TryGetStat(string name, out int value) => Ints.TryGetValue(name, out value);

            public bool TryGetStat(string name, out float value) => Floats.TryGetValue(name, out value);

            public bool SetStat(string name, int value)
            {
                Calls.Add($"stat {name}={value}");
                Ints[name] = value;
                return true;
            }

            public bool SetStat(string name, float value)
            {
                Floats[name] = value;
                return true;
            }

            public UniTask<int> StoreStatsAsync(CancellationToken token)
            {
                Calls.Add("store");
                if (Store == null)
                    return UniTask.FromResult(StoreResult);
                token.Register(() => Store.TrySetCanceled());
                return Store.Task;
            }

            public UniTask<(int result, ulong board)> FindLeaderboardAsync(string name, CancellationToken token)
            {
                Calls.Add("find " + name);
                return UniTask.FromResult((1, name == "Missing" ? 0UL : 99UL));
            }

            public UniTask<(int result, SteamLeaderboardRow[] rows)> DownloadLeaderboardAsync(ulong board, int first, int last, CancellationToken token)
            {
                Calls.Add($"download {board} {first}-{last}");
                return UniTask.FromResult((1, new[] { new SteamLeaderboardRow(1, first, 500), new SteamLeaderboardRow(2, first + 1, 400) }));
            }

            public bool SetRichPresence(string key, string value)
            {
                Calls.Add($"presence {key}={value}");
                return true;
            }

            public ulong[] GetFriends() => new ulong[] { 11, 22 };

            public bool InviteUserToGame(ulong friend, string connect)
            {
                Calls.Add($"invite {friend} {connect}");
                return true;
            }

            public bool IsDlcInstalled(uint app) => app == 1001;

            public string GetLaunchCommandLine() => Launch;

            public bool CloudEnabled => Cloud;

            public bool RemoteFileExists(string name) => Files.ContainsKey(name);

            public byte[] RemoteFileRead(string name) => Files.TryGetValue(name, out var bytes) ? (byte[])bytes.Clone() : null;

            public bool RemoteFileWrite(string name, byte[] data)
            {
                Files[name] = (byte[])data.Clone();
                return true;
            }

            public void Dispose() { }
        }

        private static IEnumerator Run(Func<UniTask> action)
        {
            var task = action();
            while (!task.Status.IsCompleted())
                yield return null;
            task.GetAwaiter().GetResult();
        }

        private static (SteamProvider provider, FakeBackend backend, AccountSession accounts) Create(Action<FakeBackend> setup = null)
        {
            var backend = new FakeBackend();
            setup?.Invoke(backend);
            var accounts = new AccountSession();
            var options = new SteamOptions { AppId = 480 }
                .Achievement("first-win", "ACH_WIN_ONE_GAME")
                .Achievement("lanterns", "ACH_LANTERNS", "STAT_LANTERNS", 10)
                .Statistic("games", "NumGames", SteamStatType.Integer)
                .Statistic("distance", "FeetTraveled", SteamStatType.Float)
                .Leaderboard("speed", "Feet Traveled")
                .Leaderboard("missing", "Missing")
                .Dlc("expansion", 1001)
                .Dlc("soundtrack", 1002);
            return (new SteamProvider(backend, accounts, options), backend, accounts);
        }

        [Test]
        public void StartSelectsTheSteamAccountAndReportsCapabilityAvailability()
        {
            var (provider, backend, accounts) = Create(b => b.Launch = "+connect lobby-7");
            Assert.That(provider.Status(Capability.Achievements).Availability, Is.EqualTo(Availability.Initializing));
            Assert.That(provider.Status(Capability.Purchases).IsSupported, Is.False);
            var result = provider.Start(inPlayer: false);
            Assert.That(result.Status, Is.EqualTo(SteamStartStatus.Ready));
            Assert.That(backend.Calls[0], Is.EqualTo("start 480 0"), "Restart is never requested outside players or without opt-in.");
            Assert.That(accounts.Current, Is.EqualTo(new AccountIdentity("steam", "76561197960287930")));
            Assert.That(provider.Current, Is.EqualTo(accounts.Current));
            Assert.That(provider.Status(Capability.Achievements).Availability, Is.EqualTo(Availability.Ready));
            Assert.That(provider.JoinIntents.TryTake(out var launch), Is.True);
            Assert.That(launch.Payload, Is.EqualTo("+connect lobby-7"));
            backend.RaiseJoin(22, "+connect lobby-9");
            backend.RaiseJoin(22, "+connect lobby-9");
            Assert.That(provider.JoinIntents.Pending, Is.EqualTo(1), "Duplicate join requests are captured once.");
            Assert.That(provider.Start().Status, Is.EqualTo(SteamStartStatus.Ready));
            Assert.That(backend.Calls.FindAll(c => c.StartsWith("start")), Has.Count.EqualTo(1));
            provider.Dispose();
            Assert.That(backend.Shutdowns, Is.EqualTo(1));
            Assert.That(accounts.Current, Is.Null);
        }

        [Test]
        public void FailedOrOfflineStartsAreUnavailableNotFailedSilently()
        {
            var (missing, _, _) = Create(b => b.StartResult = new SteamStartResult(SteamStartStatus.ClientNotRunning));
            missing.Start();
            Assert.That(missing.Status(Capability.Friends).Availability, Is.EqualTo(Availability.TemporarilyUnavailable));
            var friends = ((IFriends)missing).ListAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(friends.Error, Is.EqualTo(PlatformError.Unavailable));
            missing.Dispose();

            var (offline, _, accounts) = Create(b => b.Online = false);
            offline.Start();
            Assert.That(accounts.Current, Is.Null);
            Assert.That(offline.Status(Capability.Presence).Availability, Is.EqualTo(Availability.Offline));

            var (other, _, otherAccounts) = Create();
            other.Start();
            otherAccounts.Select(new AccountIdentity("local", "Player 1"));
            Assert.That(other.Status(Capability.Statistics).Availability, Is.EqualTo(Availability.AccountRequired));
            Assert.That(((IStatistics)other).ReadAsync(new StatisticId("games"), CancellationToken.None).GetAwaiter().GetResult().Error, Is.EqualTo(PlatformError.AccountChanged));

            var (mismatch, _, _) = Create(b => b.StartResult = new SteamStartResult(SteamStartStatus.AppIdMismatch, 999, "Expected App ID 480."));
            mismatch.Start();
            Assert.That(mismatch.Status(Capability.Achievements).Availability, Is.EqualTo(Availability.Failed));
        }

        [UnityTest]
        public IEnumerator AchievementProgressUsesMappedStatisticsAndSteamStoreResult() =>
            Run(async () =>
            {
                var (provider, backend, _) = Create(b =>
                {
                    b.Achievements["ACH_LANTERNS"] = false;
                    b.Achievements["ACH_WIN_ONE_GAME"] = false;
                    b.Ints["STAT_LANTERNS"] = 2;
                });
                provider.Start();
                Assert.That((await ((IAchievements)provider).SetProgressAsync(new AchievementId("lanterns"), 0.5, CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Ints["STAT_LANTERNS"], Is.EqualTo(5));
                Assert.That(backend.Calls, Does.Contain("progress ACH_LANTERNS 5/10"));
                backend.Calls.Clear();
                Assert.That((await ((IAchievements)provider).SetProgressAsync(new AchievementId("lanterns"), 0.3, CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Calls, Is.Empty, "Progress never moves backwards and needs no store.");
                Assert.That((await ((IAchievements)provider).SetProgressAsync(new AchievementId("first-win"), 0.5, CancellationToken.None)).Error, Is.EqualTo(PlatformError.Unsupported));
                Assert.That((await ((IAchievements)provider).SetProgressAsync(new AchievementId("first-win"), 1, CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Achievements["ACH_WIN_ONE_GAME"], Is.True);
                Assert.That((await ((IAchievements)provider).SetProgressAsync(new AchievementId("unknown"), 1, CancellationToken.None)).Error, Is.EqualTo(PlatformError.NotFound));
                backend.StoreResult = 3;
                var offline = await ((IAchievements)provider).SetProgressAsync(new AchievementId("lanterns"), 1, CancellationToken.None);
                Assert.That(offline.Error, Is.EqualTo(PlatformError.Unavailable), "EResult NoConnection maps to Unavailable.");
            });

        [UnityTest]
        public IEnumerator CancelledStoreReportsPossibleCommitment() =>
            Run(async () =>
            {
                var (provider, backend, _) = Create(b => b.Store = new UniTaskCompletionSource<int>());
                provider.Start();
                using var cancellation = new CancellationTokenSource();
                var write = ((IStatistics)provider).WriteAsync(new StatisticId("games"), 3, cancellation.Token);
                cancellation.Cancel();
                var result = await write;
                Assert.That(result.Error, Is.EqualTo(PlatformError.Cancelled));
                Assert.That(result.MayHaveCommitted, Is.True);
                Assert.That(backend.Ints["NumGames"], Is.EqualTo(3));
            });

        [UnityTest]
        public IEnumerator StatisticsRejectValuesTheirSteamTypeCannotHold() =>
            Run(async () =>
            {
                var (provider, backend, _) = Create(b => b.Floats["FeetTraveled"] = 1.5f);
                provider.Start();
                Assert.That((await ((IStatistics)provider).ReadAsync(new StatisticId("distance"), CancellationToken.None)).Value, Is.EqualTo(1.5).Within(1e-6));
                Assert.That((await ((IStatistics)provider).WriteAsync(new StatisticId("games"), 2.5, CancellationToken.None)).Error, Is.EqualTo(PlatformError.InvalidData));
                Assert.That((await ((IStatistics)provider).WriteAsync(new StatisticId("games"), double.NaN, CancellationToken.None)).Error, Is.EqualTo(PlatformError.InvalidData));
                Assert.That((await ((IStatistics)provider).WriteAsync(new StatisticId("distance"), 7.25, CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Floats["FeetTraveled"], Is.EqualTo(7.25f));
            });

        [UnityTest]
        public IEnumerator SocialLeaderboardAndEntitlementCallsMapIdentities() =>
            Run(async () =>
            {
                var (provider, backend, _) = Create();
                provider.Start();
                ILeaderboards boards = provider;
                var rows = await boards.ReadAsync(new LeaderboardId("speed"), 0, 10, CancellationToken.None);
                Assert.That(rows.Value, Has.Length.EqualTo(2));
                Assert.That(rows.Value[0].Account, Is.EqualTo(new AccountIdentity("steam", "1")));
                Assert.That(backend.Calls, Does.Contain("download 99 1-10"));
                await boards.ReadAsync(new LeaderboardId("speed"), 10, 5, CancellationToken.None);
                Assert.That(backend.Calls.FindAll(c => c.StartsWith("find")), Has.Count.EqualTo(1), "Leaderboard handles are cached.");
                Assert.That((await boards.ReadAsync(new LeaderboardId("missing"), 0, 1, CancellationToken.None)).Error, Is.EqualTo(PlatformError.NotFound));
                Assert.That((await ((IPresence)provider).SetAsync("In the forest", CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Calls, Does.Contain("presence status=In the forest"));
                var friends = await ((IFriends)provider).ListAsync(CancellationToken.None);
                Assert.That(friends.Value[1], Is.EqualTo(new AccountIdentity("steam", "22")));
                Assert.That((await ((IInvites)provider).SendAsync(friends.Value[0], "+connect lobby-1", CancellationToken.None)).Succeeded, Is.True);
                Assert.That(backend.Calls, Does.Contain("invite 11 +connect lobby-1"));
                Assert.That((await ((IInvites)provider).SendAsync(new AccountIdentity("local", "x"), "a", CancellationToken.None)).Error, Is.EqualTo(PlatformError.InvalidData));
                Assert.That((await ((IEntitlements)provider).HasAsync(new ProductId("expansion"), CancellationToken.None)).Value, Is.True);
                Assert.That((await ((IEntitlements)provider).HasAsync(new ProductId("soundtrack"), CancellationToken.None)).Value, Is.False);
                Assert.That(provider.TryGet<ICommerce>(out _), Is.False);
                Assert.That(provider.TryGet<ICloudStorage>(out var cloud), Is.True);
                Assert.That(cloud, Is.SameAs(provider.Cloud));
            });

        [UnityTest]
        public IEnumerator CloudSlotsCompareAndPublishWithinTheSignedInAccount() =>
            Run(async () =>
            {
                var (provider, backend, accounts) = Create();
                provider.Start();
                var address = new CloudAddress(provider.Current.Value, 1);
                ICloudStorage cloud = provider.Cloud;
                Assert.That(cloud.ExposesDivergentVersions, Is.False);
                Assert.That((await cloud.ReadVersionsAsync(address, CancellationToken.None)).Value, Is.Empty);
                var revision = Guid.NewGuid();
                var first = await cloud.PublishAsync(address, new CloudSaveVersion("candidate", new byte[] { 1, 2, 3 }, revision, "Forest"), null, CancellationToken.None);
                Assert.That(first.Succeeded, Is.True);
                Assert.That(backend.Files.ContainsKey("threadlink_slot_1.tlc"), Is.True);
                var read = await cloud.ReadVersionsAsync(address, CancellationToken.None);
                Assert.That(read.Value[0].Version, Is.EqualTo(first.Value.Version));
                Assert.That(read.Value[0].Export(), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(read.Value[0].LocalRevision, Is.EqualTo(revision));
                Assert.That(read.Value[0].Title, Is.EqualTo("Forest"));
                var stale = await cloud.PublishAsync(address, new CloudSaveVersion("x", new byte[] { 9 }, Guid.NewGuid(), "Stale"), null, CancellationToken.None);
                Assert.That(stale.Error, Is.EqualTo(PlatformError.Conflict));
                var second = await cloud.PublishAsync(address, new CloudSaveVersion("x", new byte[] { 4 }, Guid.NewGuid(), "Grove"), first.Value.Version, CancellationToken.None);
                Assert.That(second.Succeeded, Is.True);
                Assert.That(second.Value.Version, Is.Not.EqualTo(first.Value.Version));
                backend.Files["threadlink_slot_2.tlc"] = new byte[] { 7, 7, 7 };
                Assert.That((await cloud.ReadVersionsAsync(new CloudAddress(provider.Current.Value, 2), CancellationToken.None)).Error, Is.EqualTo(PlatformError.InvalidData));
                var foreign = new CloudAddress(new AccountIdentity("steam", "1"), 1);
                Assert.That((await cloud.ReadVersionsAsync(foreign, CancellationToken.None)).Error, Is.EqualTo(PlatformError.AccountChanged));
                backend.Cloud = false;
                Assert.That((await cloud.ReadVersionsAsync(address, CancellationToken.None)).Error, Is.EqualTo(PlatformError.Unavailable));
            });

        [Test]
        public void ProviderComposesThroughSentinelSelection()
        {
            var (provider, _, _) = Create();
            provider.Start();
            Assert.That(provider.Status(Capability.Achievements).Availability, Is.EqualTo(Availability.Ready));
            Assert.That(provider.TryGet<IAchievements>(out _), Is.True);
            Assert.That(provider.TryGet<ICloudStorage>(out var cloud), Is.True);
            Assert.That(cloud, Is.SameAs(provider.Cloud));
            Assert.That(ProviderRequirements.ValidateSelection(provider, Capability.Purchases).Error, Is.EqualTo(PlatformError.Unsupported));
            Assert.That(ProviderRequirements.ValidateSelection(provider, Capability.Achievements | Capability.Leaderboards, requireReady: true).Succeeded, Is.True);
        }

        [ManualRegistration]
        public sealed class SteamWatcher : ThreadlinkSubsystem<SteamWatcher>
        {
            public static readonly List<string> Heard = new List<string>();
            private SubscriptionToken ready,
                account,
                join;

            protected override void OnSubsystemBoot()
            {
                ready = Iris.Subscribe<PlatformReady>(e => Heard.Add("ready " + e.Provider));
                account = Iris.Subscribe<AccountChanged>(e => Heard.Add("account " + e.Current));
                join = Iris.Subscribe<JoinIntentReceived>(e => Heard.Add("join " + e.Intent.Payload));
            }

            protected override void OnSubsystemShutdown()
            {
                ready.Dispose();
                account.Dispose();
                join.Dispose();
            }
        }

        [Test]
        public void SteamStartingInASessionIsAnnouncedToSubscribersThatBooted()
        {
            var settings = SteamSettings.Current;
            bool enabled = settings.Enabled;
            uint appId = settings.AppId;
            var factory = SteamPlatform.BackendFactory;
            settings.Enabled = true;
            settings.AppId = 480;
            SteamPlatform.BackendFactory = () => new FakeBackend { Launch = "+connect lobby-3" };
            SteamWatcher.Heard.Clear();
            try
            {
                using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Sentinel), typeof(SteamPlatform), typeof(SteamWatcher));
                Assert.That(SteamPlatform.Provider, Is.Not.Null);
                Assert.That(SteamWatcher.Heard, Has.Member("ready steam"));
                Assert.That(SteamWatcher.Heard, Has.Member("account steam:76561197960287930"));
                Assert.That(SteamWatcher.Heard, Has.Member("join +connect lobby-3"));
            }
            finally
            {
                settings.Enabled = enabled;
                settings.AppId = appId;
                SteamPlatform.BackendFactory = factory;
            }
        }
    }
}
