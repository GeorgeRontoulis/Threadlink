namespace Threadlink.SentinelModules.Steam
{
    using Core.NativeSubsystems.Sentinel;
    using UnityEngine;

    internal static class SteamSentinelRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            Register(SentinelPlatformMarker.Windows);
            Register(SentinelPlatformMarker.MacOS);
            Register(SentinelPlatformMarker.Linux);
        }

        private static void Register(SentinelPlatformMarker platform)
        {
            SentinelModuleRegistry.Register(
                new SentinelModuleDescriptor(
                    platform,
                    SentinelDistribution.Steam,
                    SteamSentinelModuleInfo.ModuleID,
                    SteamSentinelModuleInfo.DisplayName,
                    static () => new SteamSentinelPlatform()
                )
            );
        }
    }
}
