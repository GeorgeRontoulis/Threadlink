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
            Assert.That(provider.Status(Capability.Accounts).Availability, Is.Not.EqualTo(Availability.Ready));
            provider.SelectProfile("profile-a");
            var first = session.Capture();
            Assert.That(provider.Status(Capability.Accounts).Availability, Is.EqualTo(Availability.Ready));
            Assert.That(provider.TryGet<IAccounts>(out var accounts), Is.True);
            Assert.That(accounts.Current.Value.Value, Is.EqualTo("profile-a"));
            provider.SelectProfile("profile-b");
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(provider.Status(Capability.CloudStorage).IsSupported, Is.False);
            Assert.That(provider.Status(Capability.Achievements).IsSupported, Is.False);
            Assert.That(
                provider.JoinIntents.Capture(new JoinIntent("local", "command-line-1", "session")),
                Is.True
            );
        }
    }
}
