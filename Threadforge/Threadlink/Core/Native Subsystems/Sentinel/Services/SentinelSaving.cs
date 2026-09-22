namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Shared;

    /// <summary>
    /// A logical save transaction. Implementations must keep the currently published save
    /// untouched while preparing writes/deletes. Commit publishes the complete new state
    /// atomically or leaves the previous published state unchanged.
    /// </summary>
    public interface ISaveTransaction : IDiscardable
    {
        int SaveID { get; }
        bool IsCommitted { get; }

        UniTask<SentinelResult<byte[]>> ReadAsync(int fileID);
        UniTask<SentinelResult> WriteAsync(int fileID, byte[] data);
        UniTask<SentinelResult> DeleteAsync(int fileID);
        UniTask<SentinelResult> CommitAsync();
    }

    public interface ISaveService : ISentinelService
    {
        UniTask<SentinelResult<byte[]>> ReadAsync(int saveID, int fileID);
        UniTask<SentinelResult<ISaveTransaction>> BeginTransactionAsync(int saveID);

        /// <summary>
        /// Atomically publishes logical deletion. Existing published bytes must remain
        /// untouched until the deletion state itself has been successfully published.
        /// </summary>
        UniTask<SentinelResult> DeleteSaveAsync(int saveID);
    }
}
