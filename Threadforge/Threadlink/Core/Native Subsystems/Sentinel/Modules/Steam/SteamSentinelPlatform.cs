namespace Threadlink.SentinelModules.Steam
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using Steamworks;

    internal sealed class SteamSentinelPlatform : SentinelPlatform
    {
        public override SentinelCapability Capabilities
        {
            get
            {
                var capabilities =
                    SentinelCapability.Accounts
                    | SentinelCapability.LocalSaves
                    | SentinelCapability.SaveTransactions
                    | SentinelCapability.Achievements
                    | SentinelCapability.AchievementProgress
                    | SentinelCapability.Presence
                    | SentinelCapability.Friends
                    | SentinelCapability.Invites;

                if (CloudEnabled)
                    capabilities |= SentinelCapability.CloudSaves;

                return capabilities;
            }
        }

        private SteamApiSession ApiSession { get; set; }
        private bool CloudEnabled { get; set; }

        public override UniTask<SentinelResult> InitializeAsync()
        {
            var acquisition = SteamApiSession.Acquire();

            if (!acquisition.Succeeded)
                return UniTask.FromResult(acquisition.Untyped());

            ApiSession = acquisition.Value;

            try
            {
                CloudEnabled =
                    SteamRemoteStorage.IsCloudEnabledForApp()
                    && SteamRemoteStorage.IsCloudEnabledForAccount();

                var steamID = SteamUser.GetSteamID();

                if (!steamID.IsValid())
                {
                    SafeDisposeApi();

                    return UniTask.FromResult(
                        SentinelResult.Failure(
                            SentinelError.AccountUnavailable,
                            "Steam returned an invalid primary Steam ID."
                        )
                    );
                }

                var accounts = new SteamAccountService(
                    steamID,
                    SteamFriends.GetPersonaName(),
                    ApiSession.AppID
                );

                if (!RegisterService<IAccountService>(accounts))
                {
                    accounts.Discard();
                    SafeDisposeApi();

                    return UniTask.FromResult(
                        SentinelResult.Failure(
                            SentinelError.NativeFailure,
                            "Failed to register the Steam account service."
                        )
                    );
                }

                return UniTask.FromResult(SentinelResult.Success());
            }
            catch (System.Exception exception)
            {
                SafeDisposeApi();

                return UniTask.FromResult(
                    SentinelResult.Failure(
                        SentinelError.NativeFailure,
                        $"Steam platform initialization failed: {exception.Message}"
                    )
                );
            }
        }

        public override void Discard()
        {
            // Account-scoped services may still use Steam during disposal.
            base.Discard();
            SafeDisposeApi();
        }

        private void SafeDisposeApi()
        {
            try
            {
                ApiSession?.Dispose();
            }
            finally
            {
                ApiSession = null;
            }
        }
    }
}
