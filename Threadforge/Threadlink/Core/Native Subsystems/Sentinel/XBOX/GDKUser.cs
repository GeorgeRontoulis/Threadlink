namespace Threadlink.Core.NativeSubsystems.Sentinel
{
#if THREADLINK_SENTINEL_XBOX
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using System;
    using Unity.XGamingRuntime;

    public sealed class GDKUser : IDiscardable
    {
        public string GamerTag { get; private set; }
        public XUserHandle Handle { get; private set; }
        public XblContextHandle XBLContextHandle { get; private set; }

        private XUserChangeRegistrationToken RegistrationToken { get; set; }

        public void Discard()
        {
            if (XBLContextHandle != null)
            {
                if (!XBLContextHandle.IsClosed) SDK.XBL.XblContextCloseHandle(XBLContextHandle);
                if (!XBLContextHandle.IsInvalid) XBLContextHandle.Dispose();

                XBLContextHandle = null;
            }

            if (Handle != null)
            {
                if (!Handle.IsClosed) SDK.XUserCloseHandle(Handle);
                if (!Handle.IsInvalid) Handle.Dispose();

                Handle = null;
            }
        }

        public async UniTask AddDefaultUserAsync()
        {
            SDK.XUserAddAsync(XUserAddOptions.AddDefaultUserAllowingUI, OnUserAddCompleted);

            while (Handle == null || Handle.IsInvalid) await UniTask.Yield();
        }

        public void UpdateUserRegistrationState(bool state)
        {
            if (state)
            {
                if (RegistrationToken == null || !RegistrationToken.IsValid)
                {
                    SDK.XUserRegisterForChangeEvent(OnUserStateChanged, out var token);
                    RegistrationToken = token;
                }
            }
            else
            {
                SDK.XUserUnregisterForChangeEvent(RegistrationToken);
                Discard();
            }
        }

        private void OnUserAddCompleted(int _, XUserHandle userHandle)
        {
            if (HR.SUCCEEDED(SDK.XUserGetGamertag(userHandle, XUserGamertagComponent.UniqueModern, out string gamertag)))
            {
                GamerTag = gamertag;
            }

            SDK.XBL.XblContextCreateHandle(userHandle, out var xblCtxHandle);
            XBLContextHandle = xblCtxHandle;

            this.Send("Add Complete! User Gamertag: ", gamertag).ToUnityConsole();
            UpdateUserRegistrationState(true);
            Handle = userHandle;
        }

        private void OnUserStateChanged(IntPtr _, XUserLocalId __, XUserChangeEvent eventType)
        {
            if (eventType == XUserChangeEvent.SignedOut)
            {
                UpdateUserRegistrationState(false);
                SDK.XUserAddAsync(XUserAddOptions.AddDefaultUserAllowingUI, OnUserAddCompleted);
            }
        }
    }
#endif
}
