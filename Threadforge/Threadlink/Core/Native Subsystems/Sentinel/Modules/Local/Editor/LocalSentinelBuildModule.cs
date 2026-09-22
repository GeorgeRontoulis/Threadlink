namespace Threadlink.SentinelModules.Local.Editor
{
    using Core.NativeSubsystems.Sentinel;
    using System.Collections.Generic;
    using Threadlink.Editor.Sentinel;

    public sealed class LocalSentinelBuildModule : ISentinelBuildModule
    {
        public string ModuleID => LocalSentinelModuleInfo.ModuleID;
        public string DisplayName => LocalSentinelModuleInfo.DisplayName;

        public bool Implements(in SentinelModuleKey key)
        {
            if (key.Distribution is not SentinelDistribution.Local)
                return false;

            return key.Platform is
            SentinelPlatformMarker.Windows or
            SentinelPlatformMarker.MacOS or
            SentinelPlatformMarker.Linux;
        }

        public void ConfigureBuild(in SentinelBuildContext context, List<SentinelBuildDiagnostic> diagnostics)
        {
            // Local is configuration-free.
        }

        public void ValidateBuild(in SentinelBuildContext context, List<SentinelBuildDiagnostic> diagnostics)
        {
            diagnostics.Add(SentinelBuildDiagnostic.Info("Local Sentinel requires no platform SDK or storefront configuration."));
        }
    }
}
