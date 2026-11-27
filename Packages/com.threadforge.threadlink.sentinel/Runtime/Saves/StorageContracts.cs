namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    /// <summary>Optional, explicitly invoked local save-history maintenance. Never runs automatically.</summary>
    public interface ISaveMaintenanceService : System.IDisposable
    {
        /// <summary>Reports retained generation count and bytes without changing saved data.</summary>
        PersistenceResult<SaveStorageReport> InspectStorage(int saveId);

        /// <summary>
        /// Deletes old generations, retaining the current generation and one previous generation.
        /// Refuses while any reader or transaction in this process uses the same save root.
        /// The application must exclusively own that save root; do not run against externally edited/shared saves.
        /// </summary>
        PersistenceResult<int> PruneHistory(int saveId);
    }

    /// <summary>Retained local save history, including the current generation.</summary>
    public readonly struct SaveStorageReport
    {
        public int Generations { get; }
        public long Bytes { get; }

        public SaveStorageReport(int generations, long bytes)
        {
            Generations = generations;
            Bytes = bytes;
        }
    }

    /// <summary>
    /// A logical save transaction. Implementations must keep the currently published save
    /// untouched while preparing writes/deletes. Commit publishes the complete new state
    /// atomically or leaves the previous published state unchanged.
    /// </summary>
    public interface ISaveTransaction : System.IDisposable, ISaveReader
    {
        int SaveId { get; }
        bool IsCommitted { get; }

        /// <summary>Whether a generation existed when this transaction began; missing expected files then indicate corruption.</summary>
        bool HasPublishedState { get; }

        UniTask<PersistenceResult> WriteAsync(int fileId, byte[] data);
        UniTask<PersistenceResult> DeleteAsync(int fileId);
        UniTask<PersistenceResult> CommitAsync();
    }

    public interface ISaveService : System.IDisposable
    {
        UniTask<PersistenceResult<byte[]>> ReadAsync(int saveId, int fileId);
        UniTask<PersistenceResult<ISaveTransaction>> BeginTransactionAsync(int saveId);

        /// <summary>
        /// Atomically publishes logical deletion. Existing published bytes must remain
        /// untouched until the deletion state itself has been successfully published.
        /// </summary>
        UniTask<PersistenceResult> DeleteSaveAsync(int saveId);
    }
}
