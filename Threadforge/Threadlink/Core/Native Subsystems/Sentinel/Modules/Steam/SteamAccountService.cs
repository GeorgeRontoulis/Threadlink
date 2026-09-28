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

        // One account, the one signed into the Steam client, so these never fire.
        public event Action<ISentinelAccount> AccountAdded { add { } remove { } }
        public event Action<ISentinelAccount> AccountRemoved { add { } remove { } }
        public event Action<ISentinelAccount> PrimaryAccountChanged { add { } remove { } }

        /// <summary>
        /// Raised when the Steam client connects to or disconnects from Steam's servers: offline mode, or a lost
        /// connection, suspends the account; reconnecting signs it back in.
        /// </summary>
        public event Action<ISentinelAccount> AccountStateChanged;

        private SteamAccount Account { get; }
        private ISentinelAccount[] AccountsBuffer { get; }

        // Dispatched by SteamAPI.RunCallbacks, which Sentinel pumps every frame.
        private Callback<SteamServersConnected_t> ConnectedCallback { get; set; }
        private Callback<SteamServersDisconnected_t> DisconnectedCallback { get; set; }
        private Callback<SteamServerConnectFailure_t> ConnectFailureCallback { get; set; }

        internal SteamAccountService(CSteamID steamID, string displayName, uint appID)
        {
            Account = new SteamAccount(steamID, displayName, appID);
            AccountsBuffer = new ISentinelAccount[] { Account };

            ConnectedCallback = Callback<SteamServersConnected_t>.Create(_ => OnConnectionChanged());
            DisconnectedCallback = Callback<SteamServersDisconnected_t>.Create(_ => OnConnectionChanged());
            ConnectFailureCallback = Callback<SteamServerConnectFailure_t>.Create(_ => OnConnectionChanged());
        }

        public UniTask<SentinelResult> RefreshAsync()
        {
            Refresh();

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
            Refresh();

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
            ConnectedCallback?.Dispose();
            DisconnectedCallback?.Dispose();
            ConnectFailureCallback?.Dispose();

            ConnectedCallback = null;
            DisconnectedCallback = null;
            ConnectFailureCallback = null;
            AccountStateChanged = null;

            Account?.Discard();
        }

        private void OnConnectionChanged() => Refresh();

        /// <summary>
        /// Read the account's state from the Steam client, and report a change.
        /// </summary>
        private void Refresh()
        {
            var previous = Account.State;

            Account.RefreshIdentity();

            if (Account.State != previous)
                AccountStateChanged?.Invoke(Account);
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
