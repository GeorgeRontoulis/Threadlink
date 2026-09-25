namespace Threadlink.SentinelModules.Steam
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using Steamworks;
    using System;
    using System.Threading;
    using UnityEngine;

    internal sealed class SteamAchievementService : IAchievementService
    {
        private Callback<UserStatsStored_t> StatsStoredCallback { get; set; }
        private SemaphoreSlim StoreGate { get; set; } = new(1, 1);

        private bool WaitingForStore { get; set; }
        private SentinelResult LastStoreResult { get; set; }

        internal SteamAchievementService()
        {
            StatsStoredCallback = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
        }

        public UniTask<SentinelResult<SentinelAchievementState>> GetStateAsync(int achievementID)
        {
            if (!TryResolveBinding(achievementID, out var binding, out var failure))
            {
                return UniTask.FromResult(
                    SentinelResult<SentinelAchievementState>.Failure(
                        failure.Error,
                        failure.Message,
                        failure.NativeCode
                    )
                );
            }

            if (!SteamUserStats.GetAchievement(binding.AchievementAPIName, out var unlocked))
            {
                return UniTask.FromResult(
                    SentinelResult<SentinelAchievementState>.Failure(
                        SentinelError.NativeFailure,
                        $"Steam could not query achievement '{binding.AchievementAPIName}'."
                    )
                );
            }

            if (unlocked)
            {
                return UniTask.FromResult(
                    SentinelResult<SentinelAchievementState>.Success(
                        new SentinelAchievementState(achievementID, 1d)
                    )
                );
            }

            var progress = 0d;

            if (binding.HasProgressStat)
            {
                if (!SteamUserStats.GetStat(binding.ProgressStatAPIName, out int current))
                {
                    return UniTask.FromResult(
                        SentinelResult<SentinelAchievementState>.Failure(
                            SentinelError.NativeFailure,
                            $"Steam could not query progress stat '{binding.ProgressStatAPIName}'."
                        )
                    );
                }

                progress = Clamp01((double)current / binding.ProgressMaximum);
            }

            return UniTask.FromResult(SentinelResult<SentinelAchievementState>.Success(new(achievementID, progress)));
        }

        public async UniTask<SentinelResult> SetProgressAsync(int achievementID, double progress)
        {
            if (!TryResolveBinding(achievementID, out var binding, out var failure))
                return failure;

            progress = Clamp01(progress);

            if (progress >= 1d)
                return await UnlockAsync(achievementID);

            if (!binding.HasProgressStat)
            {
                return SentinelResult.Failure(
                    SentinelError.Unsupported,
                    $"Achievement '{binding.AchievementAPIName}' has no Steam progress-stat override. "
                        + "Register one through SteamSentinelSettings.RegisterAchievement to persist partial progress."
                );
            }

            if (!SteamUserStats.GetAchievement(binding.AchievementAPIName, out var unlocked))
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam could not query achievement '{binding.AchievementAPIName}'."
                );
            }

            if (unlocked)
                return SentinelResult.Success();

            if (!SteamUserStats.GetStat(binding.ProgressStatAPIName, out int existing))
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam could not query progress stat '{binding.ProgressStatAPIName}'."
                );
            }

            var requested = (int)
                Math.Round(progress * binding.ProgressMaximum, MidpointRounding.AwayFromZero);

            if (requested <= existing)
                return SentinelResult.Success();

            if (!SteamUserStats.SetStat(binding.ProgressStatAPIName, requested))
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam rejected progress stat '{binding.ProgressStatAPIName}'."
                );
            }

            SteamUserStats.IndicateAchievementProgress(
                binding.AchievementAPIName,
                (uint)requested,
                (uint)binding.ProgressMaximum
            );

            return await StoreStatsAsync();
        }

        public async UniTask<SentinelResult> UnlockAsync(int achievementID)
        {
            if (!TryResolveBinding(achievementID, out var binding, out var failure))
                return failure;

            if (!SteamUserStats.GetAchievement(binding.AchievementAPIName, out var alreadyUnlocked))
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam could not query achievement '{binding.AchievementAPIName}'."
                );
            }

            if (alreadyUnlocked)
                return SentinelResult.Success();

            if (!SteamUserStats.SetAchievement(binding.AchievementAPIName))
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam rejected achievement '{binding.AchievementAPIName}'."
                );
            }

            if (
                binding.HasProgressStat
                && !SteamUserStats.SetStat(binding.ProgressStatAPIName, binding.ProgressMaximum)
            )
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    $"Steam rejected progress stat '{binding.ProgressStatAPIName}'."
                );
            }

            return await StoreStatsAsync();
        }

        public void Discard()
        {
            StatsStoredCallback?.Dispose();
            StatsStoredCallback = null;

            StoreGate?.Dispose();
            StoreGate = null;

            WaitingForStore = false;
        }

        private async UniTask<SentinelResult> StoreStatsAsync()
        {
            await StoreGate.WaitAsync();

            try
            {
                WaitingForStore = true;
                LastStoreResult = SentinelResult.Failure(
                    SentinelError.Busy,
                    "Waiting for Steam UserStatsStored callback."
                );

                if (!SteamUserStats.StoreStats())
                {
                    WaitingForStore = false;

                    return SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        "SteamUserStats.StoreStats returned false."
                    );
                }

                var deadline = Time.realtimeSinceStartupAsDouble + 10d;

                while (WaitingForStore && Time.realtimeSinceStartupAsDouble < deadline)
                    await UniTask.Yield();

                if (WaitingForStore)
                {
                    WaitingForStore = false;

                    return SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        "Timed out waiting for Steam to confirm stored stats."
                    );
                }

                return LastStoreResult;
            }
            finally
            {
                StoreGate.Release();
            }
        }

        private void OnUserStatsStored(UserStatsStored_t callback)
        {
            if (!WaitingForStore)
                return;

            LastStoreResult = SteamResultMapper.From(callback.m_eResult, "Steam UserStatsStored");
            WaitingForStore = false;
        }

        private static bool TryResolveBinding(
            int achievementID,
            out SteamAchievementBinding binding,
            out SentinelResult failure
        )
        {
            if (achievementID == 0)
            {
                binding = default;
                failure = SentinelResult.Failure(
                    SentinelError.InvalidArgument,
                    "Achievement ID 0 is reserved."
                );

                return false;
            }

            binding = SteamSentinelSettings.ResolveAchievement(achievementID);

            if (string.IsNullOrWhiteSpace(binding.AchievementAPIName))
            {
                failure = SentinelResult.Failure(
                    SentinelError.InvalidArgument,
                    $"Steam achievement mapping {achievementID} has no API Name."
                );

                return false;
            }

            failure = SentinelResult.Success();
            return true;
        }

        private static double Clamp01(double value) =>
            value < 0d ? 0d
            : value > 1d ? 1d
            : value;
    }
}
