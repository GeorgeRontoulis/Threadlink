namespace Threadforge.Threadlink.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using NUnit.Framework;

    public sealed class SentinelEventsTests
    {
        [ManualRegistration]
        public sealed class Watcher : ThreadlinkSubsystem<Watcher>
        {
            public static readonly List<AccountChanged> Accounts = new List<AccountChanged>();
            private SubscriptionToken token;

            protected override void OnSubsystemBoot() => token = Iris.Subscribe<AccountChanged>(Accounts.Add);

            protected override void OnSubsystemShutdown() => token.Dispose();
        }

        private sealed class Platform : ISentinelProvider, IAchievements
        {
            public string Identity => "events-platform";
            public Capability Supported => Capability.Achievements;

            public CapabilityStatus Status(Capability capability) =>
                (Supported & capability) != 0 ? new CapabilityStatus(true, Availability.Ready) : new CapabilityStatus(false, Availability.Unsupported);

            public bool TryGet<T>(out T value)
                where T : class
            {
                value = this as T;
                return value != null;
            }

            public UniTask<PlatformResult<Unit>> SetProgressAsync(AchievementId achievement, double progress, CancellationToken token) =>
                UniTask.FromResult(PlatformResult<Unit>.Success(default));

            public void Dispose() { }
        }

        [Test]
        public void TheStartupAccountAndLaterChangesAreAnnounced()
        {
            Watcher.Accounts.Clear();
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Sentinel), typeof(Watcher));
            var startup = Sentinel.Account;
            Assert.That(Watcher.Accounts.Count, Is.EqualTo(startup.HasValue ? 1 : 0), "The startup account is announced once, during initialization.");
            Sentinel.SelectProfile("events-test-profile");
            var last = Watcher.Accounts[Watcher.Accounts.Count - 1];
            Assert.That(last.Previous, Is.EqualTo(startup));
            Assert.That(last.Current, Is.EqualTo(Sentinel.Account));
            Assert.That(last.Current, Is.Not.EqualTo(startup));
        }

        [Test]
        public void PlatformsJoinIntentsAndAchievementsAreAnnounced()
        {
            using var runtime = Threadlink.DeployIsolated(typeof(Iris), typeof(Sentinel));
            var heard = new List<string>();
            Iris.Subscribe<PlatformReady>(e => heard.Add("ready " + e.Provider + " " + e.Capabilities));
            Iris.Subscribe<AchievementUnlocked>(e => heard.Add("unlocked " + e.Achievement.Name));
            Iris.Subscribe<JoinIntentReceived>(e => heard.Add("join " + e.Intent.Payload));
            var platform = new Platform();
            Sentinel.AddProvider(platform, platform.Supported);
            Sentinel.SetAchievementProgressAsync(new AchievementId("halfway"), 0.5).GetAwaiter().GetResult();
            Sentinel.UnlockAchievementAsync(new AchievementId("first-chest")).GetAwaiter().GetResult();
            var buffer = new JoinIntentBuffer();
            Assert.That(buffer.Capture(new JoinIntent("events-platform", "friend:1", "lobby-7")), Is.True);
            Assert.That(buffer.Capture(new JoinIntent("events-platform", "friend:1", "lobby-7")), Is.False, "A duplicate is neither queued nor announced.");
            Assert.That(heard, Is.EqualTo(new[] { "ready events-platform Achievements", "unlocked first-chest", "join lobby-7" }));
            Assert.That(buffer.Pending, Is.EqualTo(1), "An announced intent stays queued until taken.");
        }
    }
}
