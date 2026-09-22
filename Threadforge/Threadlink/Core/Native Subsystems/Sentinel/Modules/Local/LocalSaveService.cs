namespace Threadlink.SentinelModules.Local
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;

    internal sealed class LocalSaveService : ISaveService
    {
        private LocalSaveStore Store { get; set; }

        internal LocalSaveService(string root) => Store = new LocalSaveStore(root);

        public async UniTask<SentinelResult<byte[]>> ReadAsync(int saveID, int fileID)
        {
            if (saveID == 0 || fileID == 0)
                return SentinelResult<byte[]>.Failure(SentinelError.InvalidArgument, "Save and file IDs must be non-zero.");

            var pointer = await Store.ReadPointerAsync(saveID);

            if (!pointer.Succeeded)
                return SentinelResult<byte[]>.Failure(pointer.Error, pointer.Message, pointer.NativeCode);

            string path = LocalSaveStore.GetFilePath(Store.GetGenerationDirectory(saveID, pointer.Value), fileID);

            if (!File.Exists(path))
                return SentinelResult<byte[]>.Failure(SentinelError.NotFound);

            try
            {
                return SentinelResult<byte[]>.Success(await File.ReadAllBytesAsync(path).AsUniTask());
            }
            catch (UnauthorizedAccessException exception)
            {
                return SentinelResult<byte[]>.Failure(SentinelError.PermissionDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult<byte[]>.Failure(SentinelError.NativeFailure, exception.Message);
            }
        }

        public async UniTask<SentinelResult<ISaveTransaction>> BeginTransactionAsync(int saveID)
        {
            if (saveID == 0)
                return SentinelResult<ISaveTransaction>.Failure(SentinelError.InvalidArgument, "Save ID 0 is reserved.");

            var pointer = await Store.ReadRawPointerAsync(saveID);

            if (!pointer.Succeeded)
                return SentinelResult<ISaveTransaction>.Failure(pointer.Error, pointer.Message, pointer.NativeCode);

            var transaction = new LocalSaveTransaction(Store, saveID, pointer.Value);

            return SentinelResult<ISaveTransaction>.Success(transaction);
        }

        public async UniTask<SentinelResult> DeleteSaveAsync(int saveID)
        {
            if (saveID == 0)
                return SentinelResult.Failure(SentinelError.InvalidArgument, "Save ID 0 is reserved.");

            var gate = Store.GetPublishLock(saveID);

            await gate.WaitAsync();

            try
            {
                var current = await Store.ReadRawPointerAsync(saveID);

                if (!current.Succeeded)
                    return current.Untyped();

                if (current.Value == null || current.Value == LocalSaveStore.DELETED)
                    return SentinelResult.Success();

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
        public void Discard()
        {
            Store?.Dispose();
            Store = null;
        }
    }

    internal sealed class LocalSaveTransaction : ISaveTransaction
    {
        public int SaveID { get; }
        public bool IsCommitted { get; private set; } = false;

        private LocalSaveStore Store { get; set; } = null;
        private string BasePointer { get; } = null;
        private System.Collections.Generic.Dictionary<int, byte[]> Writes { get; set; } = new();
        private System.Collections.Generic.HashSet<int> Deletes { get; set; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LocalSaveTransaction(LocalSaveStore store, int saveID, string basePointer)
        {
            Store = store;
            SaveID = saveID;
            BasePointer = basePointer;
        }

        public async UniTask<SentinelResult<byte[]>> ReadAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
                return SentinelResult<byte[]>.Failure(failure.Error, failure.Message);

            if (Deletes.Contains(fileID))
                return SentinelResult<byte[]>.Failure(SentinelError.NotFound);

            if (Writes.TryGetValue(fileID, out var staged))
                return SentinelResult<byte[]>.Success((byte[])staged.Clone());

            if (string.IsNullOrEmpty(BasePointer) || BasePointer == LocalSaveStore.DELETED)
                return SentinelResult<byte[]>.Failure(SentinelError.NotFound);

            string path = LocalSaveStore.GetFilePath(Store.GetGenerationDirectory(SaveID, BasePointer), fileID);

            if (!File.Exists(path))
                return SentinelResult<byte[]>.Failure(SentinelError.NotFound);

            try
            {
                return SentinelResult<byte[]>.Success(await File.ReadAllBytesAsync(path).AsUniTask());
            }
            catch (UnauthorizedAccessException exception)
            {
                return SentinelResult<byte[]>.Failure(SentinelError.PermissionDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult<byte[]>.Failure(SentinelError.NativeFailure, exception.Message);
            }
        }

        public UniTask<SentinelResult> WriteAsync(int fileID, byte[] data)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            if (data == null)
            {
                return UniTask.FromResult(SentinelResult.Failure(SentinelError.InvalidArgument, "Cannot write null save data."));
            }

            Deletes.Remove(fileID);
            Writes[fileID] = (byte[])data.Clone();

            return UniTask.FromResult(SentinelResult.Success());
        }

        public UniTask<SentinelResult> DeleteAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            Writes.Remove(fileID);
            Deletes.Add(fileID);

            return UniTask.FromResult(SentinelResult.Success());
        }

        public async UniTask<SentinelResult> CommitAsync()
        {
            if (IsCommitted)
                return SentinelResult.Failure(SentinelError.Conflict, "This save transaction has already been committed.");

            var generation = Guid.NewGuid().ToString("N");
            var staging = Store.GetStagingDirectory(SaveID, generation);
            var publishedGeneration = Store.GetGenerationDirectory(SaveID, generation);

            try
            {
                LocalSaveStore.TryDeleteDirectory(staging);

                if (!string.IsNullOrEmpty(BasePointer) && BasePointer != LocalSaveStore.DELETED)
                {
                    var baseGeneration = Store.GetGenerationDirectory(SaveID, BasePointer);

                    if (!Directory.Exists(baseGeneration))
                        return SentinelResult.Failure(SentinelError.CorruptData, $"Base save generation '{BasePointer}' is missing.");

                    LocalSaveStore.CopyDirectory(baseGeneration, staging);
                }
                else Directory.CreateDirectory(staging);

                foreach (var pair in Writes)
                {
                    string path = LocalSaveStore.GetFilePath(staging, pair.Key);

                    if (File.Exists(path))
                        File.Delete(path);

                    await LocalSaveStore.WriteDurableBytesAsync(path, pair.Value);
                }

                foreach (int fileID in Deletes)
                    LocalSaveStore.TryDeleteFile(LocalSaveStore.GetFilePath(staging, fileID));

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
                        return SentinelResult.Failure(SentinelError.Conflict, "The save changed after this transaction began.");

                    // The only mutation of published state:
                    // atomically point readers at the fully prepared immutable generation.
                    var publish = await Store.PublishPointerAsync(SaveID, generation);

                    if (!publish.Succeeded)
                        return publish;

                    IsCommitted = true;
                    Writes.Clear();
                    Deletes.Clear();

                    return SentinelResult.Success();
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
                return SentinelResult.Failure(SentinelError.PermissionDenied, exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult.Failure(SentinelError.NativeFailure, exception.Message);
            }
            finally
            {
                LocalSaveStore.TryDeleteDirectory(staging);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Discard()
        {
            Writes?.Clear();
            Deletes?.Clear();

            Writes = null;
            Deletes = null;
            Store = null;
        }

        private bool CanOperate(int fileID, out SentinelResult failure)
        {
            if (IsCommitted)
            {
                failure = SentinelResult.Failure(SentinelError.Conflict, "This save transaction has already been committed.");
                return false;
            }

            if (fileID == 0)
            {
                failure = SentinelResult.Failure(SentinelError.InvalidArgument, "File ID 0 is reserved.");
                return false;
            }

            failure = SentinelResult.Success();
            return true;
        }
    }
}
