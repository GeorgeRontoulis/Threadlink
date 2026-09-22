namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.Collections.Generic;
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using Steamworks;

    internal sealed class SteamAccountService : IAccountService
    {
        public IReadOnlyList<ISentinelAccount> Accounts => AccountsBuffer;
        public ISentinelAccount PrimaryAccount => Account;

        public event Action<ISentinelAccount> AccountAdded;
        public event Action<ISentinelAccount> AccountRemoved;
        public event Action<ISentinelAccount> PrimaryAccountChanged;

        private SteamAccount Account { get; }
        private ISentinelAccount[] AccountsBuffer { get; }

        internal SteamAccountService(CSteamID steamID, string displayName, uint appID)
        {
            Account = new SteamAccount(steamID, displayName, appID);
            AccountsBuffer = new ISentinelAccount[] { Account };
        }

        public UniTask<SentinelResult> RefreshAsync()
        {
            Account.RefreshIdentity();

            return UniTask.FromResult(
                Account.State is SentinelAccountState.SignedOut
                    ? SentinelResult.Failure(
                        SentinelError.AccountUnavailable,
                        "Steam primary account is unavailable."
                    )
                    : SentinelResult.Success()
            );
        }

        public UniTask<SentinelResult<ISentinelAccount>> GetPrimaryAccountAsync(bool allowUI = true)
        {
            Account.RefreshIdentity();

            return UniTask.FromResult(
                Account.State is SentinelAccountState.SignedOut
                    ? SentinelResult<ISentinelAccount>.Failure(
                        SentinelError.AccountUnavailable,
                        "Steam primary account is unavailable."
                    )
                    : SentinelResult<ISentinelAccount>.Success(Account)
            );
        }

        public UniTask<SentinelResult<ISentinelAccount>> PickAccountAsync() =>
            UniTask.FromResult(
                SentinelResult<ISentinelAccount>.Failure(
                    SentinelError.Unsupported,
                    "Steam uses the account currently signed into the Steam client and does not provide an in-game account picker."
                )
            );

        public void Discard()
        {
            Account?.Discard();
            AccountAdded = null;
            AccountRemoved = null;
            PrimaryAccountChanged = null;
        }
    }

    internal sealed class SteamAccount : SentinelAccount
    {
        private CSteamID SteamID { get; }

        internal SteamAccount(CSteamID steamID, string displayName, uint appID)
            : base(
                steamID.ToString(),
                displayName,
                SteamUser.BLoggedOn()
                    ? SentinelAccountState.SignedIn
                    : SentinelAccountState.Suspended,
                true
            )
        {
            SteamID = steamID;

            RegisterService<ISaveService>(
                new SteamSaveService(SteamSentinelSettings.RetainedSaveGenerations)
            );

            RegisterService<IAchievementService>(new SteamAchievementService());

            RegisterService<IMultiplayerService>(new SteamMultiplayerService(appID));
        }

        internal void RefreshIdentity()
        {
            var current = SteamUser.GetSteamID();

            if (!current.IsValid() || current != SteamID)
            {
                State = SentinelAccountState.SignedOut;
                return;
            }

            DisplayName = SteamFriends.GetPersonaName();
            State = SteamUser.BLoggedOn()
                ? SentinelAccountState.SignedIn
                : SentinelAccountState.Suspended;
        }
    }
}
