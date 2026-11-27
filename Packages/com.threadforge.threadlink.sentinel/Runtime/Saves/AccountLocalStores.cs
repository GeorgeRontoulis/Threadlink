namespace Threadforge.Threadlink
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    public interface ILocalPersistence
    {
        AccountSnapshot<T> Open<T>(SnapshotSchema<T> schema);
    }

    /// <summary>Borrowed account-bound snapshot store. Owner disposal or account selection invalidates native storage admission.</summary>
    public sealed class AccountSnapshot<T>
    {
        public AccountSession.Lease Account { get; }
        public SnapshotStore<T> Store { get; }

        internal AccountSnapshot(AccountSession.Lease account, SnapshotStore<T> store)
        {
            Account = account;
            Store = store;
        }
    }

    /// <summary>Optional local persistence capability with explicit provider-qualified account directories.</summary>
    public sealed class AccountLocalStores : ISentinelProvider, ILocalPersistence
    {
        private readonly AccountSession accounts;
        private readonly Func<AccountIdentity, ISaveService> factory;
        private ISaveService active;
        private bool disposed;
        public string Identity => "local-storage";
        public Capability Supported => Capability.LocalStorage;

        public AccountLocalStores(
            AccountSession accounts,
            Func<AccountIdentity, ISaveService> factory
        )
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            accounts.Changed += Invalidate;
        }

        /// <summary>Creates a filesystem adapter. No account change copies or moves saved data.</summary>
        public static AccountLocalStores FileSystem(AccountSession accounts, string root) =>
            new AccountLocalStores(
                accounts,
                account => new LocalSaveService(AccountDirectory(root, account))
            );

        public static string AccountDirectory(string root, AccountIdentity account)
        {
            account.Validate();
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("A private save root is required.");
            string key =
                account.Provider.Length
                + ":"
                + account.Provider
                + account.Value.Length
                + ":"
                + account.Value;
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = new StringBuilder(64);
            foreach (byte value in hash)
                name.Append(value.ToString("x2"));
            return Path.Combine(Path.GetFullPath(root), name.ToString());
        }

        public AccountSnapshot<T> Open<T>(SnapshotSchema<T> schema)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AccountLocalStores));
            var lease = accounts.Capture();
            active ??=
                factory(lease.Account)
                ?? throw new InvalidOperationException("Local storage factory returned no store.");
            return new AccountSnapshot<T>(lease, new SnapshotStore<T>(active, schema));
        }

        public CapabilityStatus Status(Capability capability) =>
            capability != Capability.LocalStorage
                ? new CapabilityStatus(false, Availability.Unsupported)
                : new CapabilityStatus(
                    true,
                    disposed ? Availability.Failed
                        : accounts.Current.HasValue ? Availability.Ready
                        : Availability.AccountRequired
                );

        public bool TryGet<T>(out T capability)
            where T : class
        {
            capability = !disposed && typeof(T) == typeof(ILocalPersistence) ? this as T : null;
            return capability != null;
        }

        private void Invalidate()
        {
            var previous = active;
            active = null;
            previous?.Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            accounts.Changed -= Invalidate;
            Invalidate();
        }
    }
}
