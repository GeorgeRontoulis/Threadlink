namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal sealed class LocalAchievementService : IAchievementService
    {
        private Dictionary<int, double> Progress { get; set; } = new();

        public UniTask<SentinelResult<SentinelAchievementState>> GetStateAsync(int achievementID)
        {
            if (achievementID == 0)
            {
                return UniTask.FromResult(SentinelResult<SentinelAchievementState>.Failure(SentinelError.InvalidArgument,
                "Achievement ID 0 is reserved."));
            }

            Progress.TryGetValue(achievementID, out var progress);

            return UniTask.FromResult(SentinelResult<SentinelAchievementState>.Success(
            new SentinelAchievementState(achievementID, progress)));
        }

        public UniTask<SentinelResult> SetProgressAsync(int achievementID, double progress)
        {
            if (achievementID == 0)
                return UniTask.FromResult(SentinelResult.Failure(SentinelError.InvalidArgument, "Achievement ID 0 is reserved."));

            progress = progress < 0d ? 0d : progress > 1d ? 1d : progress;

            if (Progress.TryGetValue(achievementID, out double previous))
                progress = System.Math.Max(previous, progress);

            Progress[achievementID] = progress;
            return UniTask.FromResult(SentinelResult.Success());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask<SentinelResult> UnlockAsync(int achievementID) => SetProgressAsync(achievementID, 1d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Discard()
        {
            Progress?.Clear();
            Progress = null;
        }
    }
}
