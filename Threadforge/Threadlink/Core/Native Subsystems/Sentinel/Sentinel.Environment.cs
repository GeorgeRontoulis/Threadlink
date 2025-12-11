namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Shared;
    using System;

    public partial class Sentinel
    {
        [Serializable]
        public abstract class Environment : IDiscardable
        {
            public abstract void Discard();
            internal abstract UniTask<bool> TryDeployAsync();
            internal abstract UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData);
            internal abstract UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID);
            internal abstract void DeleteStoredData(string folderID, string fileID);
        }
    }

    /// <summary>
    /// Remove after implementing async methods:
    /// </summary>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    [Serializable]
    internal sealed class PlayStation : Sentinel.Environment
    {
        public override void Discard()
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<bool> TryDeployAsync()
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID)
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData)
        {
            throw new NotImplementedException();
        }

        internal override void DeleteStoredData(string folderID, string fileID)
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    internal sealed class NintendoSwitch : Sentinel.Environment
    {
        public override void Discard()
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<bool> TryDeployAsync()
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID)
        {
            throw new NotImplementedException();
        }

        internal override async UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData)
        {
            throw new NotImplementedException();
        }

        internal override void DeleteStoredData(string folderID, string fileID)
        {
            throw new NotImplementedException();
        }
    }
}