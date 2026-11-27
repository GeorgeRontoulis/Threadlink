namespace Threadforge.Threadlink.Tests
{
    using NUnit.Framework;

    public class LocalProviderTests
    {
        [Test]
        public void LocalSelectionRequiresNoSdkAndAdvertisesOnlyImplementedCapabilities()
        {
            using var session = new AccountSession();
            using var provider = new LocalProvider(session);
            using var composition = new SentinelComposition(provider);
            composition.Select<IAccounts>(provider.Identity, Capability.Accounts);
            Assert.That(composition.Resolve<IAccounts>().Succeeded, Is.False);
            provider.SelectProfile("profile-a");
            var first = session.Capture();
            Assert.That(
                composition.Resolve<IAccounts>().Value.Current.Value.Value,
                Is.EqualTo("profile-a")
            );
            provider.SelectProfile("profile-b");
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(provider.Status(Capability.CloudStorage).Supported, Is.False);
            Assert.That(provider.Status(Capability.Achievements).Supported, Is.False);
            Assert.That(
                provider.JoinIntents.Capture(new JoinIntent("local", "command-line-1", "session")),
                Is.True
            );
        }
    }
}
