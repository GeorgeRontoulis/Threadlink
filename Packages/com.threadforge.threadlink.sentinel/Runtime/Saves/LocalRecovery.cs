namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;

    public sealed partial class LocalSaveService : IRecoverableSaveService, ISaveCatalog
    {
        public PersistenceResult<int[]> ListSlots()
        {
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult<int[]>.Failure(PersistenceError.NotInitialized);
            using var lease = activity;
            try
            {
                var slots = new List<int>();
                if (Directory.Exists(Store.Root))
                    foreach (var directory in Directory.GetDirectories(Store.Root))
                        if (
                            int.TryParse(
                                Path.GetFileName(directory),
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out int slot
                            )
                            && slot > 0
                            && File.Exists(Store.GetPointerPath(slot))
                        )
                            slots.Add(slot);
                slots.Sort();
                return PersistenceResult<int[]>.Success(slots.ToArray());
            }
            catch (IOException error)
            {
                return PersistenceResult<int[]>.Failure(
                    PersistenceError.NativeFailure,
                    error.Message
                );
            }
            catch (UnauthorizedAccessException error)
            {
                return PersistenceResult<int[]>.Failure(
                    PersistenceError.PermissionDenied,
                    error.Message
                );
            }
        }

        public PersistenceResult<SaveVersion[]> ListVersions(int saveID)
        {
            if (saveID < 1)
                return PersistenceResult<SaveVersion[]>.Failure(PersistenceError.InvalidArgument);
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult<SaveVersion[]>.Failure(PersistenceError.NotInitialized);
            using var lease = activity;
            try
            {
                string pointer = Store.GetPointerPath(saveID);
                string current = File.Exists(pointer) ? File.ReadAllText(pointer).Trim() : null;
                string parent = Store.GetGenerationRoot(saveID);
                var versions = new List<SaveVersion>();
                if (Directory.Exists(parent))
                    foreach (var directory in Directory.GetDirectories(parent))
                    {
                        string name = Path.GetFileName(directory);
                        if (
                            Guid.TryParseExact(name, "N", out _)
                            && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0
                            && Directory.GetDirectories(directory).Length == 0
                        )
                            versions.Add(
                                new SaveVersion(
                                    name,
                                    name == current,
                                    Directory.GetCreationTimeUtc(directory)
                                )
                            );
                    }
                return PersistenceResult<SaveVersion[]>.Success(versions.ToArray());
            }
            catch (IOException error)
            {
                return PersistenceResult<SaveVersion[]>.Failure(
                    PersistenceError.NativeFailure,
                    error.Message
                );
            }
            catch (UnauthorizedAccessException error)
            {
                return PersistenceResult<SaveVersion[]>.Failure(
                    PersistenceError.PermissionDenied,
                    error.Message
                );
            }
        }

        private sealed class VersionReader : ISaveReader
        {
            private readonly LocalSaveStore store;
            private readonly string directory;

            internal VersionReader(LocalSaveStore store, string directory)
            {
                this.store = store;
                this.directory = directory;
            }

            public async UniTask<PersistenceResult<byte[]>> ReadAsync(int fileID)
            {
                if (fileID == 0)
                    return PersistenceResult<byte[]>.Failure(PersistenceError.InvalidArgument);
                string path = LocalSaveStore.GetFilePath(directory, fileID);
                if (!File.Exists(path))
                    return PersistenceResult<byte[]>.Failure(PersistenceError.NotFound);
                try
                {
                    return PersistenceResult<byte[]>.Success(await store.ReadBytesAsync(path));
                }
                catch (InvalidDataException error)
                {
                    return PersistenceResult<byte[]>.Failure(
                        PersistenceError.CorruptData,
                        error.Message
                    );
                }
                catch (IOException error)
                {
                    return PersistenceResult<byte[]>.Failure(
                        PersistenceError.NativeFailure,
                        error.Message
                    );
                }
                catch (UnauthorizedAccessException error)
                {
                    return PersistenceResult<byte[]>.Failure(
                        PersistenceError.PermissionDenied,
                        error.Message
                    );
                }
            }
        }

        /// <summary>Validates a selected immutable version while holding the local publication gate. No implicit newest-version recovery.</summary>
        public async UniTask<PersistenceResult> RestoreVersionAsync(
            int saveID,
            string generation,
            Func<ISaveReader, CancellationToken, UniTask<PersistenceResult>> validate,
            CancellationToken token = default
        )
        {
            if (saveID < 1 || !Guid.TryParseExact(generation, "N", out _) || validate == null)
                return PersistenceResult.Failure(PersistenceError.InvalidArgument);
            token.ThrowIfCancellationRequested();
            if (!Store.TryAcquire(out var activity))
                return PersistenceResult.Failure(PersistenceError.NotInitialized);
            using var lease = activity;
            var gate = Store.GetPublishLock(saveID);
            await gate.WaitAsync(token);
            try
            {
                var list = ListVersions(saveID);
                if (!list.Succeeded)
                    return list.Untyped();
                if (!Array.Exists(list.Value, item => item.Generation == generation))
                    return PersistenceResult.Failure(PersistenceError.NotFound);
                var valid = await validate(
                    new VersionReader(Store, Store.GetGenerationDirectory(saveID, generation)),
                    token
                );
                if (!valid.Succeeded)
                    return valid;
                token.ThrowIfCancellationRequested();
                return await Store.PublishPointerAsync(saveID, generation);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error)
            {
                return PersistenceResult.Failure(PersistenceError.CorruptData, error.Message);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
