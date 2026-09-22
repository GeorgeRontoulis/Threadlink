namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Shared;

    public interface ISentinelPlatform : ISentinelServiceProvider, IDiscardable
    {
        SentinelCapability Capabilities { get; }
        UniTask<SentinelResult> InitializeAsync();
    }

    public abstract class SentinelPlatform : SentinelServiceProvider, ISentinelPlatform
    {
        public abstract SentinelCapability Capabilities { get; }
        public abstract UniTask<SentinelResult> InitializeAsync();
    }
}
