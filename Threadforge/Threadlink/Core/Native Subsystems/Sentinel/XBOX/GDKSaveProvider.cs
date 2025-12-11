namespace Threadlink.Core.NativeSubsystems.Sentinel
{
#if MICROSOFT_GDK_SUPPORT
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using Unity.XGamingRuntime;

    public sealed class GDKSaveProvider : IDiscardable
    {
        public XGameSaveProviderHandle Handle { get; private set; }

        public void Discard()
        {
            if (Handle != null && !Handle.IsInvalid)
            {
                SDK.XGameSaveCloseProvider(Handle);
                Handle.Dispose();
                Handle = null;
            }
        }

        public async UniTask BootAsync(XUserHandle userHandle, string scid)
        {
            SDK.XGameSaveInitializeProviderAsync(userHandle, scid, false, OnSaveProviderInitialized);

            while (Handle == null || Handle.IsInvalid) await Threadlink.WaitForFramesAsync(1);
        }

        private void OnSaveProviderInitialized(int hresult, XGameSaveProviderHandle gameSaveProviderHandle)
        {
            if (HR.FAILED(hresult))
            {
                Handle = null;
                this.Send("Failed to create handle!").ToUnityConsole(DebugType.Error);
            }
            else
            {
                Handle = gameSaveProviderHandle;
                this.Send("Handle created!").ToUnityConsole();
            }
        }
    }
#endif
}
