namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    public readonly struct SentinelModuleDescriptor
    {
        public SentinelModuleKey Key { get; }
        public string ModuleID { get; }
        public string DisplayName { get; }

        private Func<ISentinelPlatform> Factory { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SentinelModuleDescriptor(SentinelPlatformMarker platform, SentinelDistribution distribution,
        string moduleID, string displayName, Func<ISentinelPlatform> factory)
        {
            Key = new SentinelModuleKey(platform, distribution);
            ModuleID = moduleID;
            DisplayName = displayName;
            Factory = factory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISentinelPlatform CreatePlatform() => Factory?.Invoke();
    }

    public static class SentinelModuleRegistry
    {
        private static readonly Dictionary<SentinelModuleKey, SentinelModuleDescriptor> Modules = new(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Modules.Clear();

        public static SentinelResult Register(in SentinelModuleDescriptor descriptor)
        {
            if (descriptor.Key.Platform is SentinelPlatformMarker.Unknown
            || descriptor.Key.Distribution is SentinelDistribution.None)
            {
                return SentinelResult.Failure(SentinelError.InvalidArgument,
                "Sentinel modules must register a concrete Platform / Distribution key.");
            }

            if (!SentinelDistributionPolicy.IsAllowed(descriptor.Key.Platform, descriptor.Key.Distribution))
            {
                return SentinelResult.Failure(SentinelError.InvalidArgument,
                $"Sentinel module '{descriptor.ModuleID}' registered invalid key '{descriptor.Key}'.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.ModuleID))
            {
                return SentinelResult.Failure(SentinelError.InvalidArgument, "Sentinel module ID is empty.");
            }

            if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            {
                return SentinelResult.Failure(SentinelError.InvalidArgument,
                $"Sentinel module '{descriptor.ModuleID}' has no display name.");
            }

            if (Modules.ContainsKey(descriptor.Key))
            {
                return SentinelResult.Failure(SentinelError.Conflict,
                $"More than one Sentinel runtime module registered exact key '{descriptor.Key}'.");
            }

            Modules.Add(descriptor.Key, descriptor);
            return SentinelResult.Success();
        }

        internal static SentinelResult<SentinelModuleDescriptor> Resolve(SentinelPlatformMarker platform,
        SentinelDistribution distribution)
        {
            var key = new SentinelModuleKey(platform, distribution);

            return Modules.TryGetValue(key, out var module)
            ? SentinelResult<SentinelModuleDescriptor>.Success(module)
            : SentinelResult<SentinelModuleDescriptor>.Failure(SentinelError.Unsupported,
            $"No installed Sentinel runtime module implements '{key}'.");
        }
    }
}
