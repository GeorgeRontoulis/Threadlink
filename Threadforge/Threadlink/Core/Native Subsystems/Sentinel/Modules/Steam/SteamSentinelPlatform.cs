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
        private Callback<SteamShutdown_t> ShutdownCallback { get; set; }
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

                // The Steam client is exiting: Steam expects games to close with it, and the API stops working.
                ShutdownCallback = Callback<SteamShutdown_t>.Create(_ =>
                    ReportLost(
                        SentinelResult.Failure(
                            SentinelError.PlatformLost,
                            "The Steam client is shutting down."
                        )
                    )
                );

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
            ShutdownCallback?.Dispose();
            ShutdownCallback = null;

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
