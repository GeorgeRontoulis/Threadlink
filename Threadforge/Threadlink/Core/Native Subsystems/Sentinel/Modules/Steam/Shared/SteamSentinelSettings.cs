namespace Threadlink.SentinelModules.Steam
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Optional code-only policy for the Steam Sentinel module.
    /// Configure overrides before Sentinel deploys.
    /// </summary>
    public static class SteamSentinelSettings
    {
        public static bool RestartAppIfNecessary { get; set; } = true;

        public static int RetainedSaveGenerations
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => retainedSaveGenerations;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => retainedSaveGenerations = value < 1 ? 1 : value;
        }

        private static int retainedSaveGenerations = 3;
        private static readonly Dictionary<int, SteamAchievementBinding> AchievementBindings = new();

        /// <summary>
        /// Registers an explicit Steam achievement mapping.
        /// Unregistered IDs use TL_ACH_{uint-id-as-8-hex-digits}.
        /// </summary>
        public static bool RegisterAchievement(int threadlinkAchievementID, string achievementAPIName,
        string progressStatAPIName = null, int progressMaximum = 100)
        {
            if (threadlinkAchievementID == 0
            || string.IsNullOrWhiteSpace(achievementAPIName)
            || progressMaximum < 1)
            {
                return false;
            }

            lock (AchievementBindings)
            {
                AchievementBindings[threadlinkAchievementID] = new SteamAchievementBinding
                (
                    threadlinkAchievementID,
                    achievementAPIName,
                    progressStatAPIName,
                    progressMaximum
                );
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveAchievement(int threadlinkAchievementID)
        {
            lock (AchievementBindings)
                return AchievementBindings.Remove(threadlinkAchievementID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearAchievementOverrides()
        {
            lock (AchievementBindings)
                AchievementBindings.Clear();
        }

        public static SteamAchievementBinding ResolveAchievement(int threadlinkAchievementID)
        {
            lock (AchievementBindings)
            {
                if (AchievementBindings.TryGetValue(threadlinkAchievementID, out var binding))
                    return binding;
            }

            return new(threadlinkAchievementID, BuildDefaultAchievementAPIName(threadlinkAchievementID), null, 100);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string BuildDefaultAchievementAPIName(int threadlinkAchievementID)
        {
            return $"TL_ACH_{unchecked((uint)threadlinkAchievementID):X8}";
        }
    }

    public readonly struct SteamAchievementBinding
    {
        public int ThreadlinkAchievementID { get; }
        public string AchievementAPIName { get; }
        public string ProgressStatAPIName { get; }
        public int ProgressMaximum { get; }

        public bool HasProgressStat
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !string.IsNullOrWhiteSpace(ProgressStatAPIName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SteamAchievementBinding(int threadlinkAchievementID, string achievementAPIName,
        string progressStatAPIName, int progressMaximum)
        {
            ThreadlinkAchievementID = threadlinkAchievementID;
            AchievementAPIName = achievementAPIName;
            ProgressStatAPIName = progressStatAPIName;
            ProgressMaximum = progressMaximum < 1 ? 1 : progressMaximum;
        }
    }
}
