namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using UnityEngine;

    /// <summary>
    /// Sentinel's single deployment-policy asset.
    ///
    /// Unity determines the hardware/platform. This asset stores exactly one managed
    /// distribution choice for targets where Unity's BuildTarget cannot express the
    /// storefront/ecosystem (for example Windows: Steam vs GOG).
    ///
    /// Targets with one valid distribution ignore this field and resolve automatically.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SentinelConfig.asset",
        menuName = "Threadlink/Subsystem Dependencies/Sentinel Config"
    )]
    public sealed class SentinelConfig : ScriptableObject
    {
#if !ODIN_INSPECTOR
        [SerializeReferenceButton]
#endif
        [SerializeReference]
        private SentinelDistributionConfiguration distribution = new SentinelDistributions.Local();

        public SentinelDistribution GetDistribution(SentinelPlatformMarker platform)
        {
            var allowed = SentinelDistributionPolicy.GetAllowed(platform);

            if (allowed.Length == 0)
                return SentinelDistribution.None;

            // No configuration should be required when the target itself fully identifies
            // the ecosystem (Xbox/PlayStation/Nintendo/etc.).
            if (allowed.Length == 1)
                return allowed[0];

            if (distribution == null || !distribution.Supports(platform))
            {
                return SentinelDistribution.None;
            }

            return distribution.Distribution;
        }

#if UNITY_EDITOR
        public const string EditorOnly_DistributionPropertyName = nameof(distribution);
#endif
    }
}
