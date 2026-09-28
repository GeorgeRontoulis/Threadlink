namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using Shared;

    public enum SentinelAccountState : byte
    {
        Unknown = 0,
        SignedOut,
        SigningIn,
        SignedIn,
        Suspended,
    }

    public interface ISentinelAccount : ISentinelServiceProvider, IDiscardable
    {
        string ID { get; }
        string DisplayName { get; }
        SentinelAccountState State { get; }
        bool IsPrimary { get; }
    }

    public abstract class SentinelAccount : SentinelServiceProvider, ISentinelAccount
    {
        public string ID { get; protected set; }
        public string DisplayName { get; protected set; }
        public SentinelAccountState State { get; protected set; }
        public bool IsPrimary { get; protected set; }

        protected SentinelAccount(
            string id,
            string displayName,
            SentinelAccountState state = SentinelAccountState.SignedIn,
            bool isPrimary = false
        )
        {
            ID = id;
            DisplayName = displayName;
            State = state;
            IsPrimary = isPrimary;
        }
    }

    public interface IAccountService : ISentinelService
    {
        IReadOnlyList<ISentinelAccount> Accounts { get; }
        ISentinelAccount PrimaryAccount { get; }

        event Action<ISentinelAccount> AccountAdded;
        event Action<ISentinelAccount> AccountRemoved;
        event Action<ISentinelAccount> PrimaryAccountChanged;

        /// <summary>
        /// An account's <see cref="ISentinelAccount.State"/> changed, for example from <c>SignedIn</c> to <c>Suspended</c>
        /// when the platform loses its connection to its servers.
        /// </summary>
        event Action<ISentinelAccount> AccountStateChanged;

        UniTask<SentinelResult> RefreshAsync();
        UniTask<SentinelResult<ISentinelAccount>> GetPrimaryAccountAsync(bool allowUI = true);
        UniTask<SentinelResult<ISentinelAccount>> PickAccountAsync();
    }
}
