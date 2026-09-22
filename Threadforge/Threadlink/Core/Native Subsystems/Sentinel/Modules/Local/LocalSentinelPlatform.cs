namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using System.IO;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    internal sealed class LocalSentinelPlatform : SentinelPlatform
    {
        public override SentinelCapability Capabilities
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return SentinelCapability.Accounts
                | SentinelCapability.LocalSaves
                | SentinelCapability.SaveTransactions
                | SentinelCapability.Achievements
                | SentinelCapability.AchievementProgress;
            }
        }


        public override UniTask<SentinelResult> InitializeAsync()
        {
            string root = Path.Combine(Application.persistentDataPath, "Sentinel");
            var accounts = new LocalAccountService("local-user", "Local User", root);

            if (!RegisterService<IAccountService>(accounts))
            {
                accounts.Discard();

                return UniTask.FromResult(SentinelResult.Failure(SentinelError.NativeFailure,
                "Failed to register the local account service."));
            }

            return UniTask.FromResult(SentinelResult.Success());
        }
    }
}
