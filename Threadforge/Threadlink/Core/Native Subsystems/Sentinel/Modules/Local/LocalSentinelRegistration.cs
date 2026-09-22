namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using UnityEngine;

    internal static class LocalSentinelRegistration
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
            SentinelModuleRegistry.Register(new SentinelModuleDescriptor
            (
                platform,
                SentinelDistribution.Local,
                LocalSentinelModuleInfo.ModuleID,
                LocalSentinelModuleInfo.DisplayName,
                static () => new LocalSentinelPlatform())
            );
        }
    }
}
