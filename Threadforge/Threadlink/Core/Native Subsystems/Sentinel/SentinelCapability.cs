namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using System.Runtime.CompilerServices;

    [Flags]
    public enum SentinelCapability : ulong
    {
        None = 0,

        Accounts = 1UL << 0,
        AccountPicker = 1UL << 1,
        MultipleLocalAccounts = 1UL << 2,

        LocalSaves = 1UL << 8,
        CloudSaves = 1UL << 9,
        SaveTransactions = 1UL << 10,

        Achievements = 1UL << 16,
        AchievementProgress = 1UL << 17,

        InputOwnership = 1UL << 24,

        Stats = 1UL << 32,
        Leaderboards = 1UL << 33,
        Presence = 1UL << 34,
        Friends = 1UL << 35,
        Entitlements = 1UL << 36,
        Commerce = 1UL << 37,
        Invites = 1UL << 38,
    }

    public static class SentinelCapabilityExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this SentinelCapability capabilities, SentinelCapability capability)
        {
            return (capabilities & capability) == capability;
        }
    }
}
