namespace Threadlink.Editor.Sentinel
{
    using Core.NativeSubsystems.Sentinel;
    using UnityEditor;

    public static class SentinelBuildPlatformResolver
    {
        public static SentinelPlatformMarker Resolve(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows => SentinelPlatformMarker.Windows,
                BuildTarget.StandaloneWindows64 => SentinelPlatformMarker.Windows,
                BuildTarget.StandaloneOSX => SentinelPlatformMarker.MacOS,
                BuildTarget.StandaloneLinux64 => SentinelPlatformMarker.Linux,

                BuildTarget.XboxOne => SentinelPlatformMarker.Xbox,
                BuildTarget.PS4 => SentinelPlatformMarker.PlayStation,
                BuildTarget.PS5 => SentinelPlatformMarker.PlayStation,
                BuildTarget.Switch => SentinelPlatformMarker.Nintendo,

                BuildTarget.Android => SentinelPlatformMarker.Android,
                BuildTarget.iOS => SentinelPlatformMarker.IOS,
                BuildTarget.tvOS => SentinelPlatformMarker.TvOS,
                BuildTarget.VisionOS => SentinelPlatformMarker.VisionOS,

                BuildTarget.WSAPlayer => SentinelPlatformMarker.WindowsStore,
                BuildTarget.WebGL => SentinelPlatformMarker.WebGL,

                _ => SentinelPlatformMarker.Unknown
            };
        }
    }
}
