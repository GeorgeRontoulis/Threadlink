namespace Threadlink.SentinelModules.Steam
{
    using System.Collections.Generic;
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;

    internal sealed class SteamSaveService : ISaveService
    {
        private SteamSaveStore Store { get; set; }

        internal SteamSaveService(int retainedGenerations)
        {
            Store = new SteamSaveStore(retainedGenerations);
        }

        public UniTask<SentinelResult<byte[]>> ReadAsync(int saveID, int fileID)
        {
            if (fileID == 0)
            {
                return UniTask.FromResult(
                    SentinelResult<byte[]>.Failure(
                        SentinelError.InvalidArgument,
                        "File ID 0 is reserved."
                    )
                );
            }

            var latest = Store.LoadLatest(saveID);

            if (!latest.Succeeded)
            {
                return UniTask.FromResult(
                    SentinelResult<byte[]>.Failure(latest.Error, latest.Message, latest.NativeCode)
                );
            }

            var snapshot = latest.Value;

            if (snapshot.IsDeleted || !snapshot.Files.TryGetValue(fileID, out var data))
                return UniTask.FromResult(SentinelResult<byte[]>.Failure(SentinelError.NotFound));

            return UniTask.FromResult(SentinelResult<byte[]>.Success((byte[])data.Clone()));
        }

        public UniTask<SentinelResult<ISaveTransaction>> BeginTransactionAsync(int saveID)
        {
            var latest = Store.LoadLatest(saveID);

            if (!latest.Succeeded)
            {
                return UniTask.FromResult(
                    SentinelResult<ISaveTransaction>.Failure(
                        latest.Error,
                        latest.Message,
                        latest.NativeCode
                    )
                );
            }

            return UniTask.FromResult(
                SentinelResult<ISaveTransaction>.Success(
                    new SteamSaveTransaction(Store, latest.Value)
                )
            );
        }

        public async UniTask<SentinelResult> DeleteSaveAsync(int saveID)
        {
            var gate = Store.GetGate(saveID);
            await gate.WaitAsync();

            try
            {
                var latest = Store.LoadLatest(saveID);

                if (!latest.Succeeded)
                    return latest.Untyped();

                if (latest.Value.IsDeleted)
                    return SentinelResult.Success();

                return Store.Commit(latest.Value, new Dictionary<int, byte[]>(), true);
            }
            finally
            {
                gate.Release();
            }
        }

        public void Discard()
        {
            Store?.Dispose();
            Store = null;
        }
    }

    internal sealed class SteamSaveTransaction : ISaveTransaction
    {
        public int SaveID => Base.SaveID;
        public bool IsCommitted { get; private set; }

        private SteamSaveStore Store { get; set; }
        private SteamSaveSnapshot Base { get; }
        private Dictionary<int, byte[]> Files { get; set; }

        internal SteamSaveTransaction(SteamSaveStore store, SteamSaveSnapshot baseSnapshot)
        {
            Store = store;
            Base = baseSnapshot;
            Files = CloneFiles(baseSnapshot.IsDeleted ? null : baseSnapshot.Files);
        }

        public UniTask<SentinelResult<byte[]>> ReadAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
            {
                return UniTask.FromResult(
                    SentinelResult<byte[]>.Failure(
                        failure.Error,
                        failure.Message,
                        failure.NativeCode
                    )
                );
            }

            if (!Files.TryGetValue(fileID, out var data))
                return UniTask.FromResult(SentinelResult<byte[]>.Failure(SentinelError.NotFound));

            return UniTask.FromResult(SentinelResult<byte[]>.Success((byte[])data.Clone()));
        }

        public UniTask<SentinelResult> WriteAsync(int fileID, byte[] data)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            if (data == null)
            {
                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.InvalidArgument,
                        "Cannot write null save data."
                    )
                );
            }

            Files[fileID] = (byte[])data.Clone();
            return UniTask.FromResult(SentinelResult.Success());
        }

        public UniTask<SentinelResult> DeleteAsync(int fileID)
        {
            if (!CanOperate(fileID, out var failure))
                return UniTask.FromResult(failure);

            Files.Remove(fileID);
            return UniTask.FromResult(SentinelResult.Success());
        }

        public async UniTask<SentinelResult> CommitAsync()
        {
            if (IsCommitted)
            {
                return SentinelResult.Failure(
                    SentinelError.Conflict,
                    "This Steam save transaction has already been committed."
                );
            }

            var gate = Store.GetGate(SaveID);
            await gate.WaitAsync();

            try
            {
                var result = Store.Commit(Base, Files, false);

                if (result.Succeeded)
                    IsCommitted = true;

                return result;
            }
            finally
            {
                gate.Release();
            }
        }

        public void Discard()
        {
            Files?.Clear();
            Files = null;
            Store = null;
        }

        private bool CanOperate(int fileID, out SentinelResult failure)
        {
            if (IsCommitted)
            {
                failure = SentinelResult.Failure(
                    SentinelError.Conflict,
                    "This Steam save transaction has already been committed."
                );

                return false;
            }

            if (fileID == 0)
            {
                failure = SentinelResult.Failure(
                    SentinelError.InvalidArgument,
                    "File ID 0 is reserved."
                );

                return false;
            }

            failure = SentinelResult.Success();
            return true;
        }

        private static Dictionary<int, byte[]> CloneFiles(Dictionary<int, byte[]> source)
        {
            var result = new Dictionary<int, byte[]>(source?.Count ?? 0);

            if (source != null)
            {
                foreach (var pair in source)
                    result.Add(pair.Key, (byte[])pair.Value.Clone());
            }

            return result;
        }
    }
}
