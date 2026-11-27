namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    public enum IntegrationLevel
    {
        LocalBackend,
        ExternalAdapterRequired,
        ExtensionContractsOnly,
    }

    /// <summary>Integration prerequisites, not a declaration of implemented or verified native capabilities.</summary>
    public sealed class ProviderRequirement
    {
        public string Target { get; }
        public IntegrationLevel Level { get; }
        public string Prerequisites { get; }

        public ProviderRequirement(string target, IntegrationLevel level, string prerequisites)
        {
            Target = target;
            Level = level;
            Prerequisites = prerequisites;
        }
    }

    /// <summary>Public extension boundaries. Native adapters supply capability-specific evidence separately.</summary>
    public static class ProviderRequirements
    {
        public static IReadOnlyList<ProviderRequirement> Targets { get; } =
            Array.AsReadOnly(
                new[]
                {
                    new ProviderRequirement(
                        "Windows Local",
                        IntegrationLevel.LocalBackend,
                        "Private writable filesystem with atomic replacement; explicit local profile. Verify on the target filesystem."
                    ),
                    new ProviderRequirement(
                        "Linux Local",
                        IntegrationLevel.LocalBackend,
                        "Linux runtime/filesystem verification; private writable root and atomic replacement."
                    ),
                    new ProviderRequirement(
                        "macOS Local",
                        IntegrationLevel.LocalBackend,
                        "macOS runtime/filesystem verification; private writable root and atomic replacement."
                    ),
                    new ProviderRequirement(
                        "Steam",
                        IntegrationLevel.ExternalAdapterRequired,
                        "Steamworks adapter, authorized SDK/wrapper, native redistributables, client, App ID/account and per-capability backend configuration."
                    ),
                    new ProviderRequirement(
                        "Android Local",
                        IntegrationLevel.LocalBackend,
                        "Android build module/device verification and application-private storage; atomic replacement support."
                    ),
                    new ProviderRequirement(
                        "Google Play",
                        IntegrationLevel.ExternalAdapterRequired,
                        "Separate Play integration, Android SDK/device, configured application/account and capability-specific service setup."
                    ),
                    new ProviderRequirement(
                        "Apple Local",
                        IntegrationLevel.LocalBackend,
                        "Apple toolchain, target device and sandbox storage verification."
                    ),
                    new ProviderRequirement(
                        "Apple Services",
                        IntegrationLevel.ExternalAdapterRequired,
                        "Capability-specific Apple adapter, toolchain/device, identifiers, entitlements and service configuration."
                    ),
                    new ProviderRequirement(
                        "Microsoft Store Windows",
                        IntegrationLevel.ExternalAdapterRequired,
                        "Store adapter, relevant Windows SDK, configured package/product identity, account and service access."
                    ),
                    new ProviderRequirement(
                        "Public PC GDK",
                        IntegrationLevel.ExternalAdapterRequired,
                        "PC GDK adapter, supported Windows SDK/runtime, title configuration and account/backend access."
                    ),
                    new ProviderRequirement(
                        "GOG",
                        IntegrationLevel.ExternalAdapterRequired,
                        "Authorized Galaxy SDK/account access, adapter and configured game services."
                    ),
                    new ProviderRequirement(
                        "Xbox Console",
                        IntegrationLevel.ExtensionContractsOnly,
                        "Authorized console SDK/tooling, configured title, hardware and per-capability native verification."
                    ),
                    new ProviderRequirement(
                        "Nintendo",
                        IntegrationLevel.ExtensionContractsOnly,
                        "Authorized platform integration supplied separately; contracts/test doubles do not verify console support."
                    ),
                    new ProviderRequirement(
                        "Sony",
                        IntegrationLevel.ExtensionContractsOnly,
                        "Authorized platform integration supplied separately; contracts/test doubles do not verify console support."
                    ),
                }
            );

        /// <summary>Validates actual selected provider capabilities. Requiring readiness is a runtime policy, not an SDK-installation probe.</summary>
        public static PlatformResult<Unit> ValidateSelection(
            ISentinelProvider provider,
            Capability required,
            bool requireReady = false
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if ((provider.Supported & required) != required)
                return PlatformResult<Unit>.Failure(
                    PlatformError.Unsupported,
                    provider.Identity + " does not declare all required capabilities."
                );
            foreach (Capability value in Enum.GetValues(typeof(Capability)))
            {
                if (value == Capability.None || (required & value) == 0)
                    continue;
                var status = provider.Status(value);
                if (!status.Supported || (provider.Supported & value) == 0)
                    return PlatformResult<Unit>.Failure(
                        PlatformError.Unsupported,
                        provider.Identity + " does not implement " + value
                    );
                if (requireReady && status.Availability != Availability.Ready)
                    return PlatformResult<Unit>.Failure(
                        PlatformError.Unavailable,
                        provider.Identity + ": " + value + " is " + status.Availability
                    );
            }
            return PlatformResult<Unit>.Success(default);
        }
    }
}
