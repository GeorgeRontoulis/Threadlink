namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public sealed partial class LocalSaveService : ISaveService, ISaveMaintenanceService
    {
        private LocalSaveStore Store { get; }

        /// <summary>Uses an exclusively owned application directory on an atomic-replacement filesystem.</summary>
        public LocalSaveService(string root, int maximumFileBytes = 16 * 1024 * 1024) =>
            Store = new LocalSaveStore(root, maximumFileBytes);

        public async UniTask<PersistenceResult<byte[]>> ReadAsync(int saveID, int fileID)
        {
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotInitialized);
            using var operation = activity;
            if (saveID == 0 || fileID == 0)
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.InvalidArgument,
                    "Save and file IDs must be non-zero."
                );

            var pointer = await Store.ReadPointerAsync(saveID);

            if (!pointer.Succeeded)
                return PersistenceResult<byte[]>.Failure(
                    pointer.Error,
                    pointer.Message,
                    pointer.NativeCode
                );

            string path = LocalSaveStore.GetFilePath(
                Store.GetGenerationDirectory(saveID, pointer.Value),
                fileID
            );

            if (!File.Exists(path))
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotFound);

            try
            {
                return PersistenceResult<byte[]>.Success(await Store.ReadBytesAsync(path));
            }
            catch (InvalidDataException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.CorruptData,
                    exception.Message
                );
            }
            catch (UnauthorizedAccessException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.PermissionDenied,
                    exception.Message
                );
            }
            catch (IOException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.NativeFailure,
                    exception.Message
                );
            }
        }

        public async UniTask<PersistenceResult<ISaveTransaction>> BeginTransactionAsync(int saveID)
        {
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult<ISaveTransaction>.Failure(PersistenceError.NotInitialized);
            try
            {
                if (saveID == 0)
                    return PersistenceResult<ISaveTransaction>.Failure(
                        PersistenceError.InvalidArgument,
                        "Save ID 0 is reserved."
                    );

                var pointer = await Store.ReadRawPointerAsync(saveID);

                if (!pointer.Succeeded)
                    return PersistenceResult<ISaveTransaction>.Failure(
                        pointer.Error,
                        pointer.Message,
                        pointer.NativeCode
                    );

                var transaction = new LocalSaveTransaction(Store, saveID, pointer.Value, activity);
                activity = null; // The transaction owns the snapshot until commit/discard.

                return PersistenceResult<ISaveTransaction>.Success(transaction);
            }
            finally
            {
                activity?.Dispose();
            }
        }

        public async UniTask<PersistenceResult> DeleteSaveAsync(int saveID)
        {
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult.Failure(PersistenceError.NotInitialized);
            using var operation = activity;
            if (saveID == 0)
                return PersistenceResult.Failure(
                    PersistenceError.InvalidArgument,
                    "Save ID 0 is reserved."
                );

            var gate = Store.GetPublishLock(saveID);

            await gate.WaitAsync();

            try
            {
                var current = await Store.ReadRawPointerAsync(saveID);

                if (!current.Succeeded)
                    return current.Untyped();

                if (current.Value == null || current.Value == LocalSaveStore.DELETED)
                    return PersistenceResult.Success();

                // Logical delete is published atomically. Existing generation bytes are not
                // destroyed or renamed before/after publication.
                return await Store.PublishPointerAsync(saveID, LocalSaveStore.DELETED);
            }
            finally
            {
                gate.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Store?.Dispose();
        }

        public PersistenceResult<SaveStorageReport> InspectStorage(int saveID) =>
            Store.InspectStorage(saveID);

        public PersistenceResult<int> PruneHistory(int saveID) => Store.PruneHistory(saveID);
    }

    internal sealed class LocalSaveTransaction : ISaveTransaction
    {
        public int SaveID { get; }
        public bool IsCommitted { get; private set; } = false;
        public bool HasPublishedState =>
            !string.IsNullOrEmpty(BasePointer) && BasePointer != LocalSaveStore.DELETED;

        private LocalSaveStore Store { get; }
        private bool discarded,
            committing;
        private IDisposable snapshotLease;
        private string BasePointer { get; } = null;
        private System.Collections.Generic.Dictionary<int, byte[]> Writes { get; set; } = new();
        private System.Collections.Generic.HashSet<int> Deletes { get; set; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LocalSaveTransaction(
            LocalSaveStore store,
            int saveID,
            string basePointer,
            IDisposable snapshotLease
        )
        {
            Store = store;
            SaveID = saveID;
            BasePointer = basePointer;
            this.snapshotLease = snapshotLease;
        }

        public async UniTask<PersistenceResult<byte[]>> ReadAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
                return PersistenceResult<byte[]>.Failure(failure.Error, failure.Message);
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotInitialized);
            using var operation = activity;

            if (Deletes.Contains(fileID))
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotFound);

            if (Writes.TryGetValue(fileID, out var staged))
                return PersistenceResult<byte[]>.Success((byte[])staged.Clone());

            if (string.IsNullOrEmpty(BasePointer) || BasePointer == LocalSaveStore.DELETED)
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotFound);

            string generationDirectory = Store.GetGenerationDirectory(SaveID, BasePointer);
            if (!Directory.Exists(generationDirectory))
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.CorruptData,
                    $"Base save generation '{BasePointer}' is missing."
                );
            string path = LocalSaveStore.GetFilePath(generationDirectory, fileID);

            if (!File.Exists(path))
                return PersistenceResult<byte[]>.Failure(PersistenceError.NotFound);

            try
            {
                return PersistenceResult<byte[]>.Success(await Store.ReadBytesAsync(path));
            }
            catch (InvalidDataException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.CorruptData,
                    exception.Message
                );
            }
            catch (UnauthorizedAccessException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.PermissionDenied,
                    exception.Message
                );
            }
            catch (IOException exception)
            {
                return PersistenceResult<byte[]>.Failure(
                    PersistenceError.NativeFailure,
                    exception.Message
                );
            }
        }

        public UniTask<PersistenceResult> WriteAsync(int fileID, byte[] data)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            if (data == null || data.Length > Store.MaximumFileBytes)
            {
                return UniTask.FromResult(
                    PersistenceResult.Failure(
                        PersistenceError.InvalidArgument,
                        "Cannot write null save data."
                    )
                );
            }

            Deletes.Remove(fileID);
            Writes[fileID] = (byte[])data.Clone();

            return UniTask.FromResult(PersistenceResult.Success());
        }

        public UniTask<PersistenceResult> DeleteAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            Writes.Remove(fileID);
            Deletes.Add(fileID);

            return UniTask.FromResult(PersistenceResult.Success());
        }

        public async UniTask<PersistenceResult> CommitAsync()
        {
            if (discarded || Store.IsDisposed)
                return PersistenceResult.Failure(PersistenceError.NotInitialized);
            if (committing)
                return PersistenceResult.Failure(PersistenceError.Busy);
            if (IsCommitted)
                return PersistenceResult.Failure(
                    PersistenceError.Conflict,
                    "This save transaction has already been committed."
                );

            var generation = Guid.NewGuid().ToString("N");
            var staging = Store.GetStagingDirectory(SaveID, generation);
            var publishedGeneration = Store.GetGenerationDirectory(SaveID, generation);
            committing = true;

            try
            {
                LocalSaveStore.TryDeleteDirectory(staging);

                if (!string.IsNullOrEmpty(BasePointer) && BasePointer != LocalSaveStore.DELETED)
                {
                    var baseGeneration = Store.GetGenerationDirectory(SaveID, BasePointer);

                    if (!Directory.Exists(baseGeneration))
                        return PersistenceResult.Failure(
                            PersistenceError.CorruptData,
                            $"Base save generation '{BasePointer}' is missing."
                        );

                    LocalSaveStore.CopyDirectory(baseGeneration, staging);
                }
                else
                    Directory.CreateDirectory(staging);

                foreach (var pair in Writes)
                {
                    string path = LocalSaveStore.GetFilePath(staging, pair.Key);

                    if (File.Exists(path))
                        File.Delete(path);

                    await LocalSaveStore.WriteDurableBytesAsync(path, pair.Value);
                }

                foreach (int fileID in Deletes)
                    File.Delete(LocalSaveStore.GetFilePath(staging, fileID));

                // All data preparation completed. Only inactive data has been touched so far.
                Directory.CreateDirectory(Store.GetGenerationRoot(SaveID));
                Directory.Move(staging, publishedGeneration);

                var gate = Store.GetPublishLock(SaveID);

                await gate.WaitAsync();

                try
                {
                    // Optimistic concurrency check. A transaction never overwrites a newer save.
                    var current = await Store.ReadRawPointerAsync(SaveID);

                    if (!current.Succeeded)
                        return current.Untyped();

                    if (!string.Equals(current.Value, BasePointer, StringComparison.Ordinal))
                        return PersistenceResult.Failure(
                            PersistenceError.Conflict,
                            "The save changed after this transaction began."
                        );

                    // The only mutation of published state:
                    // atomically point readers at the fully prepared immutable generation.
                    if (discarded || Store.IsDisposed)
                        return PersistenceResult.Failure(PersistenceError.NotInitialized);
                    var publish = await Store.PublishPointerAsync(SaveID, generation);

                    if (!publish.Succeeded)
                        return publish;

                    IsCommitted = true;
                    Writes.Clear();
                    Deletes.Clear();

                    return PersistenceResult.Success();
                }
                finally
                {
                    gate.Release();

                    if (!IsCommitted)
                        LocalSaveStore.TryDeleteDirectory(publishedGeneration);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                return PersistenceResult.Failure(
                    PersistenceError.PermissionDenied,
                    exception.Message
                );
            }
            catch (IOException exception)
            {
                return PersistenceResult.Failure(PersistenceError.NativeFailure, exception.Message);
            }
            finally
            {
                LocalSaveStore.TryDeleteDirectory(staging);
                committing = false;
                if (discarded || IsCommitted)
                    ReleaseSnapshot();
                if (discarded)
                    ClearStagedChanges();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            discarded = true;
            if (!committing)
            {
                ClearStagedChanges();
                ReleaseSnapshot();
            }
        }

        private void ReleaseSnapshot()
        {
            snapshotLease?.Dispose();
            snapshotLease = null;
        }

        private void ClearStagedChanges()
        {
            Writes?.Clear();
            Deletes?.Clear();

            Writes = null;
            Deletes = null;
        }

        private bool CanOperate(int fileID, out PersistenceResult failure)
        {
            if (discarded || Store.IsDisposed || committing)
            {
                failure = PersistenceResult.Failure(
                    committing ? PersistenceError.Busy : PersistenceError.NotInitialized
                );
                return false;
            }
            if (IsCommitted)
            {
                failure = PersistenceResult.Failure(
                    PersistenceError.Conflict,
                    "This save transaction has already been committed."
                );
                return false;
            }

            if (fileID == 0)
            {
                failure = PersistenceResult.Failure(
                    PersistenceError.InvalidArgument,
                    "File ID 0 is reserved."
                );
                return false;
            }

            failure = PersistenceResult.Success();
            return true;
        }
    }
}
