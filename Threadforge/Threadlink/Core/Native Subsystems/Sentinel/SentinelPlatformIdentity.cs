namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using UnityEngine;

    public enum SentinelPlatformMarker : byte
    {
        Unknown = 0,

        Windows,
        MacOS,
        Linux,

        Xbox,
        PlayStation,
        Nintendo,

        Android,
        IOS,
        TvOS,
        VisionOS,

        WindowsStore,
        WebGL
    }

    public enum SentinelDistribution : byte
    {
        None = 0,

        /// <summary>
        /// Platform-native ecosystem where no storefront choice is meaningful
        /// (for example Xbox, PlayStation or Nintendo).
        /// </summary>
        Native,

        /// <summary>
        /// Threadlink's SDK-free local implementation.
        /// </summary>
        Local,

        Steam,
        MicrosoftStore,
        Epic,
        GOG,

        MacAppStore,
        AppleAppStore,
        GooglePlay,
        AmazonAppstore
    }

    public readonly struct SentinelModuleKey : IEquatable<SentinelModuleKey>
    {
        public SentinelPlatformMarker Platform { get; }
        public SentinelDistribution Distribution { get; }

        public SentinelModuleKey(SentinelPlatformMarker platform, SentinelDistribution distribution)
        {
            Platform = platform;
            Distribution = distribution;
        }

        public bool Equals(SentinelModuleKey other) => Platform == other.Platform && Distribution == other.Distribution;
        public override bool Equals(object obj) => obj is SentinelModuleKey other && Equals(other);
        public override int GetHashCode() => ((int)Platform * 397) ^ (int)Distribution;
        public override string ToString() => $"{Platform} / {Distribution}";
        public static bool operator ==(SentinelModuleKey left, SentinelModuleKey right) => left.Equals(right);
        public static bool operator !=(SentinelModuleKey left, SentinelModuleKey right) => !left.Equals(right);
    }

    public static class SentinelRuntimePlatformResolver
    {
        public static SentinelPlatformMarker Resolve()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsPlayer => SentinelPlatformMarker.Windows,
                RuntimePlatform.OSXPlayer => SentinelPlatformMarker.MacOS,
                RuntimePlatform.LinuxPlayer => SentinelPlatformMarker.Linux,

                RuntimePlatform.XboxOne => SentinelPlatformMarker.Xbox,
                RuntimePlatform.GameCoreXboxOne => SentinelPlatformMarker.Xbox,
                RuntimePlatform.GameCoreXboxSeries => SentinelPlatformMarker.Xbox,

                RuntimePlatform.PS4 => SentinelPlatformMarker.PlayStation,
                RuntimePlatform.PS5 => SentinelPlatformMarker.PlayStation,

                RuntimePlatform.Switch => SentinelPlatformMarker.Nintendo,

                RuntimePlatform.Android => SentinelPlatformMarker.Android,
                RuntimePlatform.IPhonePlayer => SentinelPlatformMarker.IOS,
                RuntimePlatform.tvOS => SentinelPlatformMarker.TvOS,
                RuntimePlatform.VisionOS => SentinelPlatformMarker.VisionOS,

                RuntimePlatform.WSAPlayerX86 => SentinelPlatformMarker.WindowsStore,
                RuntimePlatform.WSAPlayerX64 => SentinelPlatformMarker.WindowsStore,
                RuntimePlatform.WSAPlayerARM => SentinelPlatformMarker.WindowsStore,

                RuntimePlatform.WebGLPlayer => SentinelPlatformMarker.WebGL,

                // In Play Mode, resolve against the Editor host. The Player-build
                // pipeline independently resolves against the selected BuildTarget.
                RuntimePlatform.WindowsEditor => SentinelPlatformMarker.Windows,
                RuntimePlatform.OSXEditor => SentinelPlatformMarker.MacOS,
                RuntimePlatform.LinuxEditor => SentinelPlatformMarker.Linux,

                _ => SentinelPlatformMarker.Unknown
            };
        }
    }

    public static class SentinelDistributionPolicy
    {
        private static readonly SentinelDistribution[] Windows =
        {
            SentinelDistribution.Local,
            SentinelDistribution.Steam,
            SentinelDistribution.MicrosoftStore,
            SentinelDistribution.Epic,
            SentinelDistribution.GOG
        };

        private static readonly SentinelDistribution[] MacOS =
        {
            SentinelDistribution.Local,
            SentinelDistribution.Steam,
            SentinelDistribution.MacAppStore
        };

        private static readonly SentinelDistribution[] Linux =
        {
            SentinelDistribution.Local,
            SentinelDistribution.Steam,
            SentinelDistribution.GOG
        };

        private static readonly SentinelDistribution[] Android =
        {
            SentinelDistribution.Local,
            SentinelDistribution.GooglePlay,
            SentinelDistribution.AmazonAppstore
        };

        private static readonly SentinelDistribution[] IOS =
        {
            SentinelDistribution.Local,
            SentinelDistribution.AppleAppStore
        };

        private static readonly SentinelDistribution[] Native =
        {
            SentinelDistribution.Native
        };

        private static readonly SentinelDistribution[] WindowsStore =
        {
            SentinelDistribution.MicrosoftStore
        };

        private static readonly SentinelDistribution[] WebGL =
        {
            SentinelDistribution.Local
        };

        public static bool RequiresChoice(SentinelPlatformMarker platform) => GetAllowed(platform).Length > 1;

        public static SentinelDistribution GetDefault(SentinelPlatformMarker platform)
        {
            var allowed = GetAllowed(platform);
            return allowed.Length > 0 ? allowed[0] : SentinelDistribution.None;
        }

        public static bool IsAllowed(SentinelPlatformMarker platform, SentinelDistribution distribution)
        {
            var allowed = GetAllowed(platform);

            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == distribution)
                    return true;
            }

            return false;
        }

        public static SentinelDistribution[] GetAllowed(SentinelPlatformMarker platform)
        {
            return platform switch
            {
                SentinelPlatformMarker.Windows => Windows,
                SentinelPlatformMarker.MacOS => MacOS,
                SentinelPlatformMarker.Linux => Linux,

                SentinelPlatformMarker.Xbox => Native,
                SentinelPlatformMarker.PlayStation => Native,
                SentinelPlatformMarker.Nintendo => Native,

                SentinelPlatformMarker.Android => Android,
                SentinelPlatformMarker.IOS => IOS,
                SentinelPlatformMarker.TvOS => IOS,
                SentinelPlatformMarker.VisionOS => IOS,

                SentinelPlatformMarker.WindowsStore => WindowsStore,
                SentinelPlatformMarker.WebGL => WebGL,

                _ => Array.Empty<SentinelDistribution>()
            };
        }
    }
}
