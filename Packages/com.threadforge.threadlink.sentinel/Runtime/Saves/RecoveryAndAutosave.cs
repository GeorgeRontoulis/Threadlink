namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;

    /// <summary>One immutable retained generation. It is not automatically selected as a recovery source.</summary>
    public readonly struct SaveVersion
    {
        public string Generation { get; }
        public bool IsCurrent { get; }
        public DateTime CreatedUtc { get; }

        public SaveVersion(string generation, bool current, DateTime createdUtc)
        {
            Generation = generation;
            IsCurrent = current;
            CreatedUtc = createdUtc;
        }
    }

    public interface ISaveReader
    {
        UniTask<PersistenceResult<byte[]>> ReadAsync(int fileID);
    }

    /// <summary>Lists logical slot identifiers, including slots whose published data needs recovery.</summary>
    public interface ISaveCatalog
    {
        PersistenceResult<int[]> ListSlots();
    }

    /// <summary>Explicit recovery; selected bytes must pass the supplied validator before pointer publication.</summary>
    public interface IRecoverableSaveService
    {
        PersistenceResult<SaveVersion[]> ListVersions(int saveID);
        UniTask<PersistenceResult> RestoreVersionAsync(
            int saveID,
            string generation,
            Func<ISaveReader, CancellationToken, UniTask<PersistenceResult>> validate,
            CancellationToken token = default
        );
    }

    /// <summary>Explicitly advanced autosave policy with no installed loop, timer, implicit retry or consistency boundary.</summary>
    public sealed class AutosaveSchedule
    {
        private readonly TimeSpan interval;
        private TimeSpan next,
            last;
        private bool running;
        public bool Paused { get; set; }
        public bool IsSaving => running;

        public AutosaveSchedule(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval));
            this.interval = interval;
            next = interval;
        }

        /// <summary>Runs at most one due save. The game callback owns snapshot capture and the returned commit outcome.</summary>
        public async UniTask<bool> AdvanceAsync(
            TimeSpan elapsed,
            bool consistencyBoundaryReady,
            Func<CancellationToken, UniTask> save,
            CancellationToken token = default
        )
        {
            if (elapsed < last)
                throw new ArgumentException("Autosave time must be monotonic.");
            last = elapsed;
            if (Paused || running || !consistencyBoundaryReady || elapsed < next)
                return false;
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            token.ThrowIfCancellationRequested();
            running = true;
            next = elapsed + interval;
            try
            {
                await save(token);
                return true;
            }
            finally
            {
                running = false;
            }
        }
    }
}
