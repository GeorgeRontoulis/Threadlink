namespace Threadforge.Threadlink.Tests
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using NUnit.Framework;

    public class SentinelContractsTests
    {
        private sealed class Provider : ISentinelProvider, IPresence
        {
            internal bool Online;
            internal int Disposals;
            public string Identity => "fixture";
            public Capability Supported => Capability.Presence;

            public CapabilityStatus Status(Capability capability) =>
                capability == Capability.Presence
                    ? new CapabilityStatus(true, Online ? Availability.Ready : Availability.Offline)
                    : new CapabilityStatus(false, Availability.Unsupported);

            public bool TryGet<T>(out T value)
                where T : class
            {
                value = typeof(T) == typeof(IPresence) ? this as T : null;
                return value != null;
            }

            public UniTask<PlatformResult<Unit>> SetAsync(string state, CancellationToken token) =>
                UniTask.FromResult(PlatformResult<Unit>.Success(default));

            public void Dispose()
            {
                Disposals++;
            }
        }

        [Test]
        public void SupportedOfflineAndUnsupportedAreDistinctAndCompositionIsExplicit()
        {
            var provider = new Provider();
            using var composition = new SentinelComposition(provider);
            Assert.That(
                composition.Resolve<IPresence>().Error,
                Is.EqualTo(PlatformError.Unsupported)
            );
            composition.Select<IPresence>(provider.Identity, Capability.Presence);
            Assert.That(provider.Status(Capability.Presence).Supported, Is.True);
            Assert.That(
                composition.Resolve<IPresence>().Error,
                Is.EqualTo(PlatformError.Unavailable)
            );
            provider.Online = true;
            Assert.That(composition.Resolve<IPresence>().Value, Is.SameAs(provider));
            Assert.That(provider.Status(Capability.Purchases).Supported, Is.False);
            composition.Dispose();
            composition.Dispose();
            Assert.That(provider.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void AccountEpochAndExplicitIdentityLinksNeverEquateProviderStrings()
        {
            using var session = new AccountSession();
            var a = new AccountIdentity("local", "7");
            var b = new AccountIdentity("steam", "7");
            Assert.That(a, Is.Not.EqualTo(b));
            session.Select(a);
            var lease = session.Capture();
            session.Select(a);
            Assert.That(lease.IsCurrent, Is.True);
            session.Select(b);
            Assert.That(lease.IsCurrent, Is.False);
            Assert.That(lease.Token.IsCancellationRequested, Is.True);
            var links = new IdentityLinks();
            Assert.That(links.TryResolve(b, out _), Is.False);
            links.Link(a, b);
            Assert.That(links.TryResolve(b, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(a));
            Assert.Throws<InvalidOperationException>(
                () => links.Link(new AccountIdentity("local", "8"), b)
            );
        }

        [Test]
        public void StartupJoinIntentsAreBoundedDeduplicatedAndConsumedExplicitly()
        {
            var buffer = new JoinIntentBuffer(1);
            var intent = new JoinIntent("test", "invite-1", "join-session");
            Assert.That(buffer.Capture(intent), Is.True);
            Assert.That(buffer.Capture(intent), Is.False);
            Assert.That(buffer.Capture(new JoinIntent("test", "invite-2", "other")), Is.False);
            Assert.That(buffer.Pending, Is.EqualTo(1));
            Assert.That(buffer.Rejected, Is.EqualTo(1));
            Assert.That(buffer.TryTake(out var taken), Is.True);
            Assert.That(taken, Is.SameAs(intent));
            Assert.That(buffer.Capture(intent), Is.False);
        }

        [Test]
        public void PurchaseIdentitySeparatesAccountsAndRestorationRetainsStableKey()
        {
            var account = new AccountIdentity("test", "player");
            var first = new PurchaseReceipt(account, "native-id", new ProductId("coins"), Acknowledgment.Pending);
            var restored = new PurchaseReceipt(
                account,
                "native-id",
                new ProductId("coins"),
                Acknowledgment.Pending,
                true
            );
            Assert.That(restored.StableKey, Is.EqualTo(first.StableKey));
            Assert.That(
                new PurchaseReceipt(
                    new AccountIdentity("other", "player"),
                    "native-id",
                    new ProductId("coins"),
                    Acknowledgment.Pending
                ).StableKey,
                Is.Not.EqualTo(first.StableKey)
            );
        }
    }
}
