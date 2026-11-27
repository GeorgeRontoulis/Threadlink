namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using NUnit.Framework;

    public sealed class PlatformIdTests
    {
        // A platform that authors its own data: Sentinel hands it agnostic IDs and it resolves them itself.
        private sealed class Platform : ISentinelProvider, IAchievements, IStatistics, IEntitlements
        {
            internal readonly List<object> Received = new List<object>();
            private readonly Dictionary<string, string> authored = new Dictionary<string, string> { ["first-chest"] = "PLATFORM_FIRST_CHEST" };

            public string Identity => "platform";
            public Capability Supported => Capability.Achievements | Capability.Statistics | Capability.Entitlements;

            public CapabilityStatus Status(Capability capability) =>
                (Supported & capability) != 0 ? new CapabilityStatus(true, Availability.Ready) : new CapabilityStatus(false, Availability.Unsupported);

            public bool TryGet<T>(out T value)
                where T : class
            {
                value = this as T;
                return value != null;
            }

            public UniTask<PlatformResult<Unit>> SetProgressAsync(AchievementId achievement, double progress, CancellationToken token)
            {
                Received.Add(achievement);
                return UniTask.FromResult(
                    authored.ContainsKey(achievement.Name)
                        ? PlatformResult<Unit>.Success(default)
                        : PlatformResult<Unit>.Failure(PlatformError.NotFound, achievement + " is not authored here.")
                );
            }

            public UniTask<PlatformResult<double>> ReadAsync(StatisticId statistic, CancellationToken token)
            {
                Received.Add(statistic);
                return UniTask.FromResult(PlatformResult<double>.Success(3));
            }

            public UniTask<PlatformResult<Unit>> WriteAsync(StatisticId statistic, double value, CancellationToken token) =>
                UniTask.FromResult(PlatformResult<Unit>.Success(default));

            public UniTask<PlatformResult<bool>> HasAsync(ProductId product, CancellationToken token)
            {
                Received.Add(product);
                return UniTask.FromResult(PlatformResult<bool>.Success(product.Name == "expansion"));
            }

            public void Dispose() { }
        }

        [Test]
        public void SentinelPassesPlatformIdsThroughUnchanged()
        {
            using (var runtime = Threadlink.DeployIsolated())
            {
                runtime.Add<Sentinel>();
                var platform = new Platform();
                Sentinel.AddProvider(platform, platform.Supported);
                var chest = new AchievementId("first-chest");
                Assert.That(Sentinel.UnlockAchievementAsync(chest).GetAwaiter().GetResult().Succeeded, Is.True);
                var missing = Sentinel.UnlockAchievementAsync(new AchievementId("missing")).GetAwaiter().GetResult();
                Assert.That(missing.Error, Is.EqualTo(PlatformError.NotFound), "An ID the platform does not author is the platform's answer.");
                Assert.That(Sentinel.TryGet<IStatistics>(out var statistics), Is.True);
                Assert.That(statistics.ReadAsync(new StatisticId("lanterns-found"), CancellationToken.None).GetAwaiter().GetResult().Value, Is.EqualTo(3));
                Assert.That(Sentinel.TryGet<IEntitlements>(out var entitlements), Is.True);
                Assert.That(entitlements.HasAsync(new ProductId("expansion"), CancellationToken.None).GetAwaiter().GetResult().Value, Is.True);
                Assert.That(platform.Received[0], Is.EqualTo(chest), "Sentinel does not translate or wrap the ID.");
                Assert.That(platform.Received, Has.Some.EqualTo(new StatisticId("lanterns-found")));
            }
        }

        [Test]
        public void IdsCompareByNameAndRejectEmptyNames()
        {
            Assert.That(new AchievementId("a"), Is.EqualTo(new AchievementId("a")));
            Assert.That(new LeaderboardId("a").GetHashCode(), Is.EqualTo(new LeaderboardId("a").GetHashCode()));
            Assert.Throws<ArgumentException>(() => new ProductId(" "));
            Assert.Throws<ArgumentException>(() => new StatisticId(null));
        }
    }
}
