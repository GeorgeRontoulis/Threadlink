namespace Threadlink.Core.NativeSubsystems.Sentinel
{
#if MICROSOFT_GDK_SUPPORT
    using Cysharp.Threading.Tasks;
    using Scribe;
    using System;
    using Unity.XGamingRuntime;

    [Serializable]
    internal sealed class XBOX : Sentinel.Environment
    {
        private GDKRuntime Runtime { get; set; }
        private GDKUser User { get; set; }
        private GDKSaveProvider SaveProvider { get; set; }

        public override void Discard()
        {
            User?.Discard();
            SaveProvider?.Discard();
            Runtime?.Discard();

            User = null;
            SaveProvider = null;
            Runtime = null;
        }

        internal override async UniTask<bool> TryDeployAsync()
        {
            Runtime = new();
            Runtime.Boot();

            await Threadlink.WaitForFramesAsync(1);

            User = new();

            await User.AddDefaultUserAsync();

            SaveProvider = new();

            await SaveProvider.BootAsync(User.Handle, Runtime.GameConfigScid);

            return true;
        }

        internal override async UniTask<byte[]> ReadFromStorageAsync(string folderID, string fileID)
        {
            var providerHandle = SaveProvider.Handle;

            if (providerHandle == null || providerHandle.IsInvalid)
            {
                this.Send("The Save Provider has not been initialized!").ToUnityConsole(DebugType.Error);
                return null;
            }

            int hresult = SDK.XGameSaveCreateContainer(providerHandle, folderID, out var containerHandler);

            if (HR.FAILED(hresult))
            {
                this.Send("Error while retrieving container ", folderID, "!").ToUnityConsole(DebugType.Error);
                return null;
            }

            hresult = SDK.XGameSaveEnumerateBlobInfo(containerHandler, out var blobInfos);

            if (HR.FAILED(hresult))
            {
                this.Send("Failed to enumerate blob info from container ", folderID, "!").ToUnityConsole(DebugType.Error);
                return null;
            }

            hresult = SDK.XGameSaveReadBlobData(containerHandler, blobInfos, out var blobs);

            if (HR.FAILED(hresult))
            {
                this.Send("Failed to read blob data from container ", folderID, "!").ToUnityConsole(DebugType.Error);
                return null;
            }

            if (blobs == null || blobs.Length <= 0)
            {
                this.Send("Container ", folderID, " is empty. Nothing to read!").ToUnityConsole(DebugType.Warning);
                return null;
            }

            int length = blobs.Length;
            var byteData = default(byte[]);

            for (int i = 0; i < length; i++)
            {
                var candidateBlob = blobs[i];
                var blobID = candidateBlob.Info.Name;

                if (!string.IsNullOrEmpty(blobID) && blobID.Equals(fileID))
                {
                    this.Send("Stored data found for container ", folderID, " in blob ", fileID, "! Retrieving!").ToUnityConsole();
                    byteData = candidateBlob.Data;
                    await UniTask.Yield();
                    break;
                }
            }

            SDK.XGameSaveCloseContainer(containerHandler);
            return byteData;
        }

        internal override async UniTask<bool> TryWriteToStorageAsync(string folderID, string fileID, byte[] serializedData)
        {
            var providerHandle = SaveProvider.Handle;

            if (providerHandle == null || providerHandle.IsInvalid)
            {
                this.Send("The Save Provider has not been initialized!").ToUnityConsole(DebugType.Error);
                return false;
            }

            int hresult = SDK.XGameSaveCreateContainer(providerHandle, folderID, out var createdContainer);

            if (HR.FAILED(hresult))
            {
                this.Send("Error while retrieving container ", folderID, "!").ToUnityConsole(DebugType.Error);
                return false;
            }

            hresult = SDK.XGameSaveCreateUpdate(createdContainer, folderID, out var handle);

            if (HR.FAILED(hresult))
            {
                this.Send("Error when creating update handle! The handle is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            SDK.XGameSaveSubmitBlobWrite(handle, fileID, serializedData);
            SDK.XGameSaveSubmitUpdate(handle);
            SDK.XGameSaveCloseUpdate(handle);

            await UniTask.Yield();

            this.Send("Error when creating update handle! The handle is invalid!").ToUnityConsole(DebugType.Error);
            return true;
        }

        internal override void DeleteStoredData(string folderID, string fileID)
        {
            int hresult = SDK.XGameSaveCreateContainer(SaveProvider.Handle, folderID, out var createdContainer);

            if (HR.FAILED(hresult))
            {
                this.Send("Error while retrieving container ", folderID, "!").ToUnityConsole(DebugType.Error);
                return;
            }

            hresult = SDK.XGameSaveCreateUpdate(createdContainer, folderID, out var handle);

            if (HR.FAILED(hresult))
            {
                this.Send("Error when creating update handle! The handle is invalid!").ToUnityConsole(DebugType.Error);
                return;
            }

            SDK.XGameSaveSubmitBlobDelete(handle, fileID);
            SDK.XGameSaveSubmitUpdate(handle);
            SDK.XGameSaveCloseUpdate(handle);
        }
    }
#endif
}
