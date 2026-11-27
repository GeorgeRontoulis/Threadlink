namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using UnityEngine;

    /// <summary>Declares a type as save data: its stable name and current version. Rename the type freely; never the name.</summary>
    /// <remarks>The type is serialized with MessagePack: mark it [MessagePackObject] and its members [Key(n)].</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class SaveAttribute : Attribute
    {
        /// <summary>Declares save data.</summary>
        public SaveAttribute(string name, int version = 1)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A save needs a stable name.", nameof(name));
            Name = name;
            Version = version;
        }

        /// <summary>Stable name; it identifies the saved data across versions.</summary>
        public string Name { get; }

        /// <summary>Current schema version. Register a migration for every older version still in players' saves.</summary>
        public int Version { get; }
    }

    /// <summary>
    /// Sentinel: accounts, platform services and saves for the session. A local profile is selected from the start; a
    /// platform package (such as Steam) adds its provider, and capabilities route to it. Saves are per account,
    /// transactional, versioned and recoverable.
    /// </summary>
    public sealed class Sentinel : ThreadlinkSubsystem<Sentinel>
    {
        private static readonly Dictionary<Capability, Type> contracts = new Dictionary<Capability, Type>
        {
            { Capability.Accounts, typeof(IAccounts) },
            { Capability.LocalStorage, typeof(ILocalPersistence) },
            { Capability.CloudStorage, typeof(ICloudStorage) },
            { Capability.Achievements, typeof(IAchievements) },
            { Capability.Statistics, typeof(IStatistics) },
            { Capability.Leaderboards, typeof(ILeaderboards) },
            { Capability.Presence, typeof(IPresence) },
            { Capability.Friends, typeof(IFriends) },
            { Capability.Invites, typeof(IInvites) },
            { Capability.Entitlements, typeof(IEntitlements) },
            { Capability.Catalog, typeof(ICommerce) },
            { Capability.Purchases, typeof(ICommerce) },
            { Capability.PurchaseRestore, typeof(ICommerce) },
            { Capability.JoinIntents, typeof(IJoinIntents) },
        };

        private readonly List<ISentinelProvider> providers = new List<ISentinelProvider>();
        private readonly Dictionary<Capability, ISentinelProvider> routes = new Dictionary<Capability, ISentinelProvider>();
        private readonly Dictionary<Type, object> saves = new Dictionary<Type, object>();
        private AccountSession accounts;
        private LocalProvider local;
        private AccountLocalStores stores;
        private AccountIdentity? announced;
        private bool announcing;

        /// <summary>The signed-in account, or null.</summary>
        public static AccountIdentity? Account => Instance.accounts.Current;

        /// <summary>The account session, for leases that end when the player switches account.</summary>
        public static AccountSession Accounts => Instance.accounts;

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            var settings = SentinelSettings.Current;
            accounts = new AccountSession();
            accounts.Changed += () => saves.Clear();
            local = new LocalProvider(accounts);
            stores = AccountLocalStores.FileSystem(accounts, Path.Combine(Application.persistentDataPath, "Threadlink", settings.SaveFolder));
            providers.Add(local);
            providers.Add(stores);
            Route(local, Capability.Accounts | Capability.JoinIntents);
            Route(stores, Capability.LocalStorage);
            if (!string.IsNullOrWhiteSpace(settings.DefaultProfile))
                local.SelectProfile(settings.DefaultProfile);
            // Later account changes, such as a platform signing in during its initialization, are announced as they happen.
            accounts.Changed += AnnounceAccount;
        }

        // Announces the startup account now that every subsystem has subscribed.
        /// <inheritdoc />
        protected override void OnSubsystemInit()
        {
            announcing = true;
            AnnounceAccount();
        }

        private void AnnounceAccount()
        {
            var account = accounts.Current;
            if (!announcing || Nullable.Equals(account, announced))
                return;
            var previous = announced;
            announced = account;
            Iris.TryNotify(new AccountChanged(previous, account));
        }

        /// <inheritdoc />
        protected override void OnSubsystemShutdown()
        {
            saves.Clear();
            routes.Clear();
            for (int i = providers.Count - 1; i >= 0; i--)
            {
                try
                {
                    providers[i].Dispose();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
            providers.Clear();
            accounts?.Dispose();
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            var account = accounts.Current;
            report.Summary(account.HasValue ? account.Value.Provider + " account" : "No account", !account.HasValue);
            if (!report.Detailed)
                return;
            report.Section("Account");
            report.Field("Provider", account.HasValue ? account.Value.Provider : "—");
            report.Section("Capabilities");
            foreach (var capability in contracts.Keys)
            {
                var status = Status(capability);
                if (status.Supported)
                    report.Field(capability.ToString(), status.Availability.ToString(), status.Availability == Availability.Ready ? MonitorTone.Good : MonitorTone.Caution);
            }
            report.Section("Saves");
            report.Field("Open save types", saves.Count);
        }

        /// <summary>Selects a local profile; saves switch to that profile's folder.</summary>
        public static void SelectProfile(string profile) => Instance.local.SelectProfile(profile);

        /// <summary>Adds a platform provider and routes <paramref name="capabilities"/> to it, replacing earlier routes.</summary>
        /// <remarks>Platform packages call this from their own subsystem. Sentinel disposes the provider at shutdown.</remarks>
        public static void AddProvider(ISentinelProvider provider, Capability capabilities)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            var sentinel = Instance;
            if (!sentinel.providers.Contains(provider))
                sentinel.providers.Add(provider);
            sentinel.Route(provider, capabilities);
            Iris.TryNotify(new PlatformReady(provider.Identity, capabilities));
        }

        /// <summary>Whether a capability is supported and ready, and by which provider's status.</summary>
        public static CapabilityStatus Status(Capability capability) =>
            Instance.routes.TryGetValue(capability, out var provider) ? provider.Status(capability) : new CapabilityStatus(false, Availability.Unsupported);

        /// <summary>A capability contract that is supported and ready, or a result saying why not. Never a silent no-op.</summary>
        public static PlatformResult<T> Resolve<T>()
            where T : class
        {
            var sentinel = Instance;
            foreach (var pair in contracts)
            {
                if (pair.Value != typeof(T) || !sentinel.routes.TryGetValue(pair.Key, out var provider))
                    continue;
                var status = provider.Status(pair.Key);
                if (!status.Supported)
                    return PlatformResult<T>.Failure(PlatformError.Unsupported, status.Detail);
                if (status.Availability != Availability.Ready)
                    return PlatformResult<T>.Failure(PlatformError.Unavailable, status.Detail ?? status.Availability.ToString());
                return provider.TryGet<T>(out var value) ? PlatformResult<T>.Success(value) : PlatformResult<T>.Failure(PlatformError.Unsupported);
            }
            return PlatformResult<T>.Failure(PlatformError.Unsupported, "No provider offers " + typeof(T).Name + ".");
        }

        /// <summary>Whether a capability is supported and ready, returning it.</summary>
        public static bool TryGet<T>(out T capability)
            where T : class
        {
            var result = Resolve<T>();
            capability = result.Succeeded ? result.Value : null;
            return result.Succeeded;
        }

        /// <summary>Unlocks an achievement. The result says whether it was unsupported, unavailable or failed.</summary>
        public static UniTask<PlatformResult<Unit>> UnlockAchievementAsync(AchievementId achievement, CancellationToken token = default) =>
            SetAchievementProgressAsync(achievement, 1, token);

        /// <summary>Reports achievement progress from 0 to 1.</summary>
        public static async UniTask<PlatformResult<Unit>> SetAchievementProgressAsync(AchievementId achievement, double progress, CancellationToken token = default)
        {
            var achievements = Resolve<IAchievements>();
            if (!achievements.Succeeded)
                return PlatformResult<Unit>.Failure(achievements.Error, achievements.Detail);
            var lease = Instance.accounts.Capture();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease.Token);
            var result = await achievements.Value.SetProgressAsync(achievement, progress, linked.Token);
            if (result.Succeeded && progress >= 1)
                Iris.TryNotify(new AchievementUnlocked(achievement));
            return result;
        }

        private void Route(ISentinelProvider provider, Capability capabilities)
        {
            foreach (var capability in contracts.Keys)
                if ((capabilities & capability) != 0)
                    routes[capability] = provider;
        }

        private SaveSlots<T> Slots<T>()
        {
            if (saves.TryGetValue(typeof(T), out var existing))
                return (SaveSlots<T>)existing;
            var declaration = (SaveAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(SaveAttribute));
            if (declaration == null)
                throw new InvalidOperationException(typeof(T).Name + " is not save data: add [Save(\"name\")] and [MessagePackObject].");
            var slots = new SaveSlots<T>(declaration, stores);
            saves.Add(typeof(T), slots);
            return slots;
        }

        /// <summary>Saves of the current account. Each save type has its own slots; slots are any non-negative number.</summary>
        public static class Saves
        {
            /// <summary>Saves <paramref name="data"/> to a slot atomically. Fails with Conflict if the slot changed since this session last read or wrote it.</summary>
            /// <remarks>Announces <see cref="SaveWritten"/> or <see cref="SaveFailed"/>.</remarks>
            public static UniTask<PersistenceResult<SlotSummary>> SaveAsync<T>(int slot, T data, string title = null, CancellationToken token = default) =>
                Written(typeof(T), slot, Instance.Slots<T>().SaveAsync(Stored(slot), data, title, token));

            /// <summary>Loads a slot. Fails with NotFound for an empty slot, CorruptData for damaged data; older versions are migrated.</summary>
            /// <remarks>Announces <see cref="SaveFailed"/> when the slot exists but cannot be read.</remarks>
            public static UniTask<PersistenceResult<T>> LoadAsync<T>(int slot, CancellationToken token = default) =>
                Loaded(typeof(T), slot, Instance.Slots<T>().LoadAsync(Stored(slot), token));

            /// <summary>The slots that hold a save of this type.</summary>
            public static int[] List<T>()
            {
                var slots = Instance.Slots<T>().Store.Store.ListSlots();
                if (!slots.Succeeded)
                    return Array.Empty<int>();
                var result = new int[slots.Value.Length];
                for (int i = 0; i < result.Length; i++)
                    result[i] = slots.Value[i] - 1;
                Array.Sort(result);
                return result;
            }

            /// <summary>Deletes a slot. Earlier versions stay recoverable.</summary>
            /// <remarks>Announces <see cref="SaveFailed"/> when the slot exists but cannot be deleted.</remarks>
            public static UniTask<PersistenceResult> DeleteAsync<T>(int slot, CancellationToken token = default) =>
                Finished(typeof(T), slot, SaveOperation.Delete, Instance.Slots<T>().Store.Store.DeleteAsync(Stored(slot), token));

            /// <summary>Restores the newest earlier version of a slot, for example after CorruptData.</summary>
            /// <remarks>Announces <see cref="SaveRecovered"/> or <see cref="SaveFailed"/>.</remarks>
            public static UniTask<PersistenceResult> RestorePreviousAsync<T>(int slot, CancellationToken token = default) =>
                Finished(typeof(T), slot, SaveOperation.Restore, Instance.Slots<T>().RestorePreviousAsync(Stored(slot), token));

            // The public methods validate synchronously, as before; these await the store and announce the outcome.
            private static async UniTask<PersistenceResult<SlotSummary>> Written(Type save, int slot, UniTask<PersistenceResult<SlotSummary>> saving)
            {
                var result = await saving;
                if (result.Succeeded)
                    Iris.TryNotify(new SaveWritten(save, slot, result.Value));
                else
                    Failed(save, slot, SaveOperation.Save, result.Error, result.Message);
                return result;
            }

            private static async UniTask<PersistenceResult<T>> Loaded<T>(Type save, int slot, UniTask<PersistenceResult<T>> loading)
            {
                var result = await loading;
                if (!result.Succeeded)
                    Failed(save, slot, SaveOperation.Load, result.Error, result.Message);
                return result;
            }

            private static async UniTask<PersistenceResult> Finished(Type save, int slot, SaveOperation operation, UniTask<PersistenceResult> working)
            {
                var result = await working;
                if (result.Succeeded)
                {
                    if (operation == SaveOperation.Restore)
                        Iris.TryNotify(new SaveRecovered(save, slot));
                }
                else
                {
                    Failed(save, slot, operation, result.Error, result.Message);
                }
                return result;
            }

            private static void Failed(Type save, int slot, SaveOperation operation, PersistenceError error, string message)
            {
                if (error != PersistenceError.Cancelled && error != PersistenceError.NotFound)
                    Iris.TryNotify(new SaveFailed(save, slot, operation, error, message));
            }

            // Slots are numbered from 0 here; the store numbers them from 1.
            private static int Stored(int slot) =>
                slot >= 0 && slot < int.MaxValue ? slot + 1 : throw new ArgumentOutOfRangeException(nameof(slot), "Slots are numbered from 0.");

            /// <summary>Reads saves written by an older version. Call it from your save subsystem's OnSubsystemInit.</summary>
            public static void AddMigration<T>(int fromVersion, Func<ReadOnlyMemory<byte>, T> read) =>
                Instance.Slots<T>().Schema.RegisterMigration(fromVersion, read);
        }

        private sealed class SaveSlots<T>
        {
            private readonly Dictionary<int, Guid> revisions = new Dictionary<int, Guid>();
            private readonly AccountLocalStores stores;
            private AccountSnapshot<T> store;

            internal SaveSlots(SaveAttribute declaration, AccountLocalStores stores)
            {
                this.stores = stores;
                var codec = new MessagePackSnapshotCodec<T>(MessagePack.Resolvers.StandardResolver.Instance, SentinelSettings.Current.MaximumSaveBytes);
                Schema = new SnapshotSchema<T>(Identify(declaration.Name), declaration.Version, codec, _ => { });
            }

            internal SnapshotSchema<T> Schema { get; }

            internal AccountSnapshot<T> Store
            {
                get
                {
                    if (store == null || !store.Account.IsCurrent)
                    {
                        store = stores.Open(Schema);
                        revisions.Clear();
                    }
                    return store;
                }
            }

            internal async UniTask<PersistenceResult<SlotSummary>> SaveAsync(int slot, T data, string title, CancellationToken token)
            {
                var current = Store;
                revisions.TryGetValue(slot, out var expected);
                if (expected == Guid.Empty)
                {
                    var existing = await current.Store.ReadAsync(slot, token);
                    if (existing.Succeeded)
                        expected = existing.Value.Summary.Revision;
                }
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, current.Account.Token);
                var saved = await current.Store.SaveAsync(slot, data, title ?? typeof(T).Name, expected, linked.Token);
                if (saved.Succeeded)
                    revisions[slot] = saved.Value.Revision;
                return saved;
            }

            internal async UniTask<PersistenceResult<T>> LoadAsync(int slot, CancellationToken token)
            {
                var current = Store;
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, current.Account.Token);
                var read = await current.Store.ReadAsync(slot, linked.Token);
                if (!read.Succeeded)
                    return PersistenceResult<T>.Failure(read.Error, read.Message);
                revisions[slot] = read.Value.Summary.Revision;
                return PersistenceResult<T>.Success(read.Value.Value);
            }

            internal async UniTask<PersistenceResult> RestorePreviousAsync(int slot, CancellationToken token)
            {
                var current = Store;
                var versions = current.Store.ListRecoveryVersions(slot);
                if (!versions.Succeeded)
                    return PersistenceResult.Failure(versions.Error, versions.Message);
                SaveVersion? newest = null;
                foreach (var version in versions.Value)
                    if (!version.IsCurrent && (newest == null || version.CreatedUtc > newest.Value.CreatedUtc))
                        newest = version;
                if (newest == null)
                    return PersistenceResult.Failure(PersistenceError.NotFound, "No earlier version exists.");
                revisions.Remove(slot);
                return await current.Store.RecoverAsync(slot, newest.Value.Generation, token);
            }

            private static Guid Identify(string name)
            {
                using var hash = SHA256.Create();
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes("threadlink.save:" + name));
                Array.Resize(ref bytes, 16);
                return new Guid(bytes);
            }
        }
    }
}
