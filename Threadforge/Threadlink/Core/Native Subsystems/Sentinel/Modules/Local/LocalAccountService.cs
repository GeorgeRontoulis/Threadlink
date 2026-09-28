namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal sealed class LocalAccountService : IAccountService
    {
        public IReadOnlyList<ISentinelAccount> Accounts
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AccountsBuffer;
        }

        public ISentinelAccount PrimaryAccount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Account;
        }

        // One implicit account that never changes, so these never fire.
        public event Action<ISentinelAccount> AccountAdded { add { } remove { } }
        public event Action<ISentinelAccount> AccountRemoved { add { } remove { } }
        public event Action<ISentinelAccount> PrimaryAccountChanged { add { } remove { } }
        public event Action<ISentinelAccount> AccountStateChanged { add { } remove { } }

        private LocalAccount Account { get; } = null;
        private ISentinelAccount[] AccountsBuffer { get; } = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LocalAccountService(string accountID, string displayName, string saveRoot)
        {
            Account = new(accountID, displayName, saveRoot);
            AccountsBuffer = new ISentinelAccount[] { Account };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask<SentinelResult> RefreshAsync() => UniTask.FromResult(SentinelResult.Success());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask<SentinelResult<ISentinelAccount>> GetPrimaryAccountAsync(bool allowUI = true)
        {
            return UniTask.FromResult(SentinelResult<ISentinelAccount>.Success(Account));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask<SentinelResult<ISentinelAccount>> PickAccountAsync()
        {
            return UniTask.FromResult(SentinelResult<ISentinelAccount>.Failure(SentinelError.Unsupported,
            "The Local Sentinel module exposes one implicit account."));
        }

        public void Discard()
        {
            Account?.Discard();
        }
    }

    internal sealed class LocalAccount : SentinelAccount
    {
        internal LocalAccount(string id, string displayName, string saveRoot) :
        base(id, displayName, SentinelAccountState.SignedIn, true)
        {
            RegisterService<ISaveService>(new LocalSaveService(saveRoot));
            RegisterService<IAchievementService>(new LocalAchievementService());
        }
    }
}
