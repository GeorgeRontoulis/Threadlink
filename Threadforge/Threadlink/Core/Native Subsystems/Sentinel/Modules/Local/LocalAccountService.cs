namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;

    internal sealed class LocalAccountService : IAccountService
    {
        public IReadOnlyList<ISentinelAccount> Accounts => AccountsBuffer;
        public ISentinelAccount PrimaryAccount => Account;

        public event Action<ISentinelAccount> AccountAdded;
        public event Action<ISentinelAccount> AccountRemoved;
        public event Action<ISentinelAccount> PrimaryAccountChanged;

        private LocalAccount Account { get; }
        private ISentinelAccount[] AccountsBuffer { get; }

        internal LocalAccountService(string accountID, string displayName, string saveRoot)
        {
            Account = new LocalAccount(accountID, displayName, saveRoot);
            AccountsBuffer = new ISentinelAccount[] { Account };
        }

        public UniTask<SentinelResult> RefreshAsync() =>
            UniTask.FromResult(SentinelResult.Success());

        public UniTask<SentinelResult<ISentinelAccount>> GetPrimaryAccountAsync(bool allowUI = true) =>
            UniTask.FromResult(SentinelResult<ISentinelAccount>.Success(Account));

        public UniTask<SentinelResult<ISentinelAccount>> PickAccountAsync() =>
            UniTask.FromResult(SentinelResult<ISentinelAccount>.Failure(
                SentinelError.Unsupported,
                "The Local Sentinel module exposes one implicit account."));

        public void Discard()
        {
            Account?.Discard();
            AccountAdded = null;
            AccountRemoved = null;
            PrimaryAccountChanged = null;
        }
    }

    internal sealed class LocalAccount : SentinelAccount
    {
        internal LocalAccount(string id, string displayName, string saveRoot)
            : base(id, displayName, SentinelAccountState.SignedIn, true)
        {
            RegisterService<ISaveService>(new LocalSaveService(saveRoot));
            RegisterService<IAchievementService>(new LocalAchievementService());
        }
    }
}
