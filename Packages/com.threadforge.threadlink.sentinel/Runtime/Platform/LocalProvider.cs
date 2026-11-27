namespace Threadforge.Threadlink
{
    using System;

    /// <summary>SDK-free local profile selection and explicit startup-intent handoff; no storefront, cloud or achievement claims.</summary>
    public sealed class LocalProvider : ISentinelProvider, IAccounts
    {
        private readonly AccountSession accounts;
        private bool disposed;
        public string Identity => "local";
        public Capability Supported => Capability.Accounts | Capability.JoinIntents;
        public JoinIntentBuffer JoinIntents { get; }
        public AccountIdentity? Current =>
            !disposed && accounts.Current.HasValue && accounts.Current.Value.Provider == Identity
                ? accounts.Current
                : null;

        /// <summary>The caller owns the account session and chooses a local profile explicitly.</summary>
        public LocalProvider(AccountSession accounts, JoinIntentBuffer joinIntents = null)
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            JoinIntents = joinIntents ?? new JoinIntentBuffer();
        }

        public void SelectProfile(string profile)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(LocalProvider));
            accounts.Select(new AccountIdentity(Identity, profile));
        }

        public CapabilityStatus Status(Capability capability)
        {
            if ((Supported & capability) != capability || capability == Capability.None)
                return new CapabilityStatus(false, Availability.Unsupported);
            if (disposed)
                return new CapabilityStatus(true, Availability.Failed, "Provider is disposed.");
            return new CapabilityStatus(
                true,
                capability == Capability.Accounts && !Current.HasValue
                    ? Availability.AccountRequired
                    : Availability.Ready
            );
        }

        public bool TryGet<T>(out T capability)
            where T : class
        {
            capability =
                disposed ? null
                : typeof(T) == typeof(IAccounts) ? this as T
                : typeof(T) == typeof(IJoinIntents) ? JoinIntents as T
                : null;
            return capability != null;
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}
