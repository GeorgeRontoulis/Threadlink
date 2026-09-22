namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Serialized deployment-distribution choice for targets where Unity's BuildTarget
    /// does not uniquely identify the storefront/ecosystem.
    ///
    /// Concrete implementations are intentionally stateless. Native SDK identifiers and
    /// credentials should remain in each platform SDK's canonical configuration source
    /// rather than being duplicated into Sentinel.
    /// </summary>
    [Serializable]
    public abstract class SentinelDistributionConfiguration
    {
        public abstract SentinelDistribution Distribution { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Supports(SentinelPlatformMarker platform) => SentinelDistributionPolicy.IsAllowed(platform, Distribution);
    }

    /// <summary>
    /// Managed-reference choices shown by Threadlink's SerializeReference UI.
    /// Nested class names intentionally map directly to concise Inspector labels.
    /// </summary>
    public static class SentinelDistributions
    {
        [Serializable]
        public sealed class Local : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.Local;
        }

        [Serializable]
        public sealed class Steam : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.Steam;
        }

        [Serializable]
        public sealed class MicrosoftStore : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.MicrosoftStore;
        }

        [Serializable]
        public sealed class Epic : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.Epic;
        }

        [Serializable]
        public sealed class GOG : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.GOG;
        }

        [Serializable]
        public sealed class MacAppStore : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.MacAppStore;
        }

        [Serializable]
        public sealed class AppleAppStore : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.AppleAppStore;
        }

        [Serializable]
        public sealed class GooglePlay : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.GooglePlay;
        }

        [Serializable]
        public sealed class AmazonAppstore : SentinelDistributionConfiguration
        {
            public override SentinelDistribution Distribution => SentinelDistribution.AmazonAppstore;
        }
    }
}
