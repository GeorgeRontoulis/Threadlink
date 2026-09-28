namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using Cysharp.Threading.Tasks;
    using Shared;

    public interface ISentinelPlatform : ISentinelServiceProvider, IDiscardable
    {
        SentinelCapability Capabilities { get; }

        /// <summary>
        /// Raised when the platform goes away after initializing, for example when the Steam client shuts down.
        /// Sentinel moves to <c>Failed</c> and discards the platform.
        /// </summary>
        event Action<SentinelResult> Lost;

        UniTask<SentinelResult> InitializeAsync();
    }

    public abstract class SentinelPlatform : SentinelServiceProvider, ISentinelPlatform
    {
        public abstract SentinelCapability Capabilities { get; }
        public event Action<SentinelResult> Lost;

        public abstract UniTask<SentinelResult> InitializeAsync();

        /// <summary>
        /// Report that the platform has gone away. Safe to call from a platform SDK callback: Sentinel discards the
        /// platform only after the callback has returned.
        /// </summary>
        protected void ReportLost(SentinelResult reason) => Lost?.Invoke(reason);
    }
}
