namespace Threadlink.Core.NativeSubsystems.Sentinel
{
#if THREADLINK_SENTINEL_XBOX
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using System;
    using System.Threading;
    using Unity.Microsoft.GDK.Tools;
    using Unity.XGamingRuntime;
    using UnityEngine;

    [Serializable]
    public sealed class GDKRuntime : IBootable, IDiscardable
    {
        public string GameConfigSandbox { get; private set; } = "XDKS.1";
        public string GameConfigScid { get; private set; } = "00000000-0000-0000-0000-0000FFFFFFFF";
        public string GameConfigTitleId { get; private set; } = "FFFFFFFF";

        private static CancellationTokenSource CancellationTokenSource { get; set; } = null;

        [SerializeField] private GameConfigAsset gameConfigAsset = null;

        public void Discard()
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();

            SDK.CloseDefaultXTaskQueue();
            SDK.XBL.XblCleanup(null);
            SDK.XGameRuntimeUninitialize();
        }

        private static async UniTaskVoid DispatchTaskQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                SDK.XTaskQueueDispatch(0);
                await UniTask.Delay(32, cancellationToken: token);
            }

            SDK.CloseDefaultXTaskQueue();
        }

        public void Boot()
        {
            //STEP 1: First, we initialize the XGame Runtime Library.
            SDK.XGameRuntimeInitialize();
            SDK.CreateDefaultTaskQueue();

            //STEP 2: Then, we initliaze the XBOX LIVE API.
            GameConfigScid = gameConfigAsset.SCID;
            SDK.XBL.XblInitialize(GameConfigScid);

            //STEP 3: Configure Title data and Sandbox.
            SDK.XGameGetXboxTitleId(out var titleId);
            GameConfigTitleId = titleId.ToString("X");

            SDK.XSystemGetXboxLiveSandboxId(out var sandboxId);
            GameConfigSandbox = sandboxId;

            CancellationTokenSource = new();
            DispatchTaskQueue(CancellationTokenSource.Token).Forget();

            var newLine = Environment.NewLine;

            //We have successfully completed the initialization process.
            this.Send(
            "GDK Runtime Initialized!",
            newLine,
            "GDK Xbox Live API SCID: ", GameConfigScid,
            newLine,
            "GDK TitleId:", GameConfigTitleId,
            newLine,
            "GDK Sandbox:", GameConfigSandbox).ToUnityConsole();
        }
    }
#endif
}
