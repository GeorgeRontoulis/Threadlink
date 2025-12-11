namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using Cysharp.Threading.Tasks;
    using Scribe;
    using System;
    using System.IO;
    using UnityEngine;

    [Serializable]
    internal sealed class Steam : Sentinel.Environment
    {
        [SerializeField] private string saveFileExtension = ".SENT";

        private string GetPersistentDirectory(string folderID)
        {
            return Path.Combine(Application.persistentDataPath, folderID);
        }

        private string ConstructPath(string folderID, string fileID)
        {
            return Path.Combine(Application.persistentDataPath, folderID, fileID, saveFileExtension);
        }

        public override void Discard()
        {
            saveFileExtension = null;
        }

        internal override async UniTask<bool> TryDeployAsync()
        {
            await Threadlink.WaitForFramesAsync(1);
            return true;
        }

        internal override async UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID)
        {
            if (string.IsNullOrEmpty(folderID) || string.IsNullOrEmpty(fileID))
            {
                this.Send("Attempted to read from an invalid directory!").ToUnityConsole(DebugType.Error);
                return null;
            }

            var serializedData = await File.ReadAllBytesAsync(ConstructPath(folderID, fileID)).AsUniTask();

            if (serializedData == null)
            {
                this.Send("Invalid data retrieved from read!").ToUnityConsole(DebugType.Warning);
                return null;
            }

            this.Send("Data successfully read from storage!").ToUnityConsole();
            return serializedData;
        }

        internal override async UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData)
        {
            if (string.IsNullOrEmpty(folderID) || string.IsNullOrEmpty(fileID))
            {
                this.Send("Attempted to write to an invalid directory!").ToUnityConsole(DebugType.Error);
                return false;
            }

            if (serializedData == null)
            {
                this.Send("Attempted to write invalid data!").ToUnityConsole(DebugType.Error);
                return false;
            }

            await File.WriteAllBytesAsync(ConstructPath(folderID, fileID), serializedData).AsUniTask();

            this.Send("Data successfully written to storage!").ToUnityConsole();
            return true;
        }

        internal override void DeleteStoredData(string folderID, string fileID)
        {
            Directory.Delete(GetPersistentDirectory(folderID));
        }
    }
}
