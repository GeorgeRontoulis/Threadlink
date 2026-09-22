namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;

    public readonly struct SentinelAchievementState
    {
        public int AchievementID { get; }
        public double Progress { get; }
        public bool Unlocked => Progress >= 1d;

        public SentinelAchievementState(int achievementID, double progress)
        {
            AchievementID = achievementID;
            Progress =
                progress < 0d ? 0d
                : progress > 1d ? 1d
                : progress;
        }
    }

    public interface IAchievementService : ISentinelService
    {
        UniTask<SentinelResult<SentinelAchievementState>> GetStateAsync(int achievementID);
        UniTask<SentinelResult> SetProgressAsync(int achievementID, double progress);
        UniTask<SentinelResult> UnlockAsync(int achievementID);
    }
}
