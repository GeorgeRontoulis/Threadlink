namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    [Flags]
    public enum Capability
    {
        None = 0,
        Accounts = 1,
        LocalStorage = 2,
        CloudStorage = 4,
        Achievements = 8,
        Statistics = 16,
        Leaderboards = 32,
        Presence = 64,
        Friends = 128,
        Invites = 256,
        Entitlements = 512,
        Catalog = 1024,
        Purchases = 2048,
        PurchaseRestore = 4096,
        JoinIntents = 8192,
    }

    public enum Availability
    {
        Unsupported,
        Initializing,
        Ready,
        Offline,
        AccountRequired,
        TemporarilyUnavailable,
        Failed,
    }

    public enum PlatformError
    {
        None,
        Unsupported,
        Unavailable,
        AccountChanged,
        Cancelled,
        Conflict,
        NotFound,
        PermissionDenied,
        QuotaExceeded,
        InvalidData,
        NativeFailure,
    }

    /// <summary>Supported implementation and current availability are independent facts.</summary>
    public readonly struct CapabilityStatus
    {
        public bool Supported { get; }
        public Availability Availability { get; }
        public string Detail { get; }

        public CapabilityStatus(bool supported, Availability availability, string detail = null)
        {
            Supported = supported;
            Availability = availability;
            Detail = detail;
        }
    }

    public readonly struct Unit { }

    /// <summary>Explicit platform outcome; ambiguous externally committed work must be reconciled before retry.</summary>
    public readonly struct PlatformResult<T>
    {
        public bool Succeeded { get; }
        public T Value { get; }
        public PlatformError Error { get; }
        public string Detail { get; }
        public bool MayHaveCommitted { get; }

        private PlatformResult(
            bool success,
            T value,
            PlatformError error,
            string detail,
            bool mayHaveCommitted
        )
        {
            Succeeded = success;
            Value = value;
            Error = error;
            Detail = detail;
            MayHaveCommitted = mayHaveCommitted;
        }

        public static PlatformResult<T> Success(T value) =>
            new PlatformResult<T>(true, value, PlatformError.None, null, false);

        public static PlatformResult<T> Failure(
            PlatformError error,
            string detail = null,
            bool mayHaveCommitted = false
        ) =>
            new PlatformResult<T>(
                false,
                default,
                error == PlatformError.None ? PlatformError.NativeFailure : error,
                detail,
                mayHaveCommitted
            );
    }

    /// <summary>Provider-owned capabilities. Installation alone performs no SDK initialization.</summary>
    public interface ISentinelProvider : IDisposable
    {
        string Identity { get; }
        Capability Supported { get; }
        CapabilityStatus Status(Capability capability);
        bool TryGet<T>(out T capability)
            where T : class;
    }

    public interface IAccounts
    {
        AccountIdentity? Current { get; }
    }

    /// <summary>
    /// A platform's achievements. It receives the platform-agnostic <see cref="AchievementId"/> and resolves it against
    /// the achievements authored for the platform; an ID the platform does not author fails with <see cref="PlatformError.NotFound"/>.
    /// </summary>
    public interface IAchievements
    {
        /// <summary>Reports progress from 0 to 1; 1 unlocks the achievement.</summary>
        UniTask<PlatformResult<Unit>> SetProgressAsync(
            AchievementId achievement,
            double progress,
            CancellationToken token
        );
    }

    /// <summary>A platform's statistics, addressed by platform-agnostic <see cref="StatisticId"/>s it resolves itself.</summary>
    public interface IStatistics
    {
        /// <summary>Reads the signed-in player's value.</summary>
        UniTask<PlatformResult<double>> ReadAsync(StatisticId statistic, CancellationToken token);

        /// <summary>Writes the signed-in player's value.</summary>
        UniTask<PlatformResult<Unit>> WriteAsync(
            StatisticId statistic,
            double value,
            CancellationToken token
        );
    }

    public readonly struct LeaderboardEntry
    {
        public AccountIdentity Account { get; }
        public long Rank { get; }
        public double Score { get; }

        public LeaderboardEntry(AccountIdentity account, long rank, double score)
        {
            Account = account;
            Rank = rank;
            Score = score;
        }
    }

    /// <summary>A platform's leaderboards, addressed by platform-agnostic <see cref="LeaderboardId"/>s it resolves itself.</summary>
    public interface ILeaderboards
    {
        /// <summary>Reads <paramref name="count"/> entries from rank <paramref name="start"/>.</summary>
        UniTask<PlatformResult<LeaderboardEntry[]>> ReadAsync(
            LeaderboardId leaderboard,
            long start,
            int count,
            CancellationToken token
        );
    }

    public interface IPresence
    {
        UniTask<PlatformResult<Unit>> SetAsync(string state, CancellationToken token);
    }

    public interface IFriends
    {
        UniTask<PlatformResult<AccountIdentity[]>> ListAsync(CancellationToken token);
    }

    public interface IInvites
    {
        UniTask<PlatformResult<Unit>> SendAsync(
            AccountIdentity recipient,
            string joinData,
            CancellationToken token
        );
    }

    /// <summary>A platform's ownership checks, addressed by platform-agnostic <see cref="ProductId"/>s it resolves itself.</summary>
    public interface IEntitlements
    {
        /// <summary>Whether the signed-in player owns the product.</summary>
        UniTask<PlatformResult<bool>> HasAsync(ProductId product, CancellationToken token);
    }

    public enum Acknowledgment
    {
        NotRequired,
        Pending,
        Acknowledged,
    }

    /// <summary>Stable native transaction identity. Restoration and duplicate notifications preserve the same key.</summary>
    public sealed class PurchaseReceipt
    {
        public AccountIdentity Account { get; }
        public string Transaction { get; }

        /// <summary>The purchased product, translated by the provider from the platform's own product.</summary>
        public ProductId Product { get; }
        public Acknowledgment Acknowledgment { get; }
        public bool IsRestored { get; }
        public string StableKey =>
            Account.Provider.Length
            + ":"
            + Account.Provider
            + Account.Value.Length
            + ":"
            + Account.Value
            + Transaction.Length
            + ":"
            + Transaction;

        public PurchaseReceipt(
            AccountIdentity account,
            string transaction,
            ProductId product,
            Acknowledgment acknowledgment,
            bool restored = false
        )
        {
            account.Validate();
            if (string.IsNullOrWhiteSpace(transaction) || string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Purchase identities are required.");
            Account = account;
            Transaction = transaction;
            Product = product;
            Acknowledgment = acknowledgment;
            IsRestored = restored;
        }
    }

    /// <summary>A purchasable product with its localized price, as the platform reports it.</summary>
    public readonly struct CatalogItem
    {
        /// <summary>The product, translated by the provider from the platform's own product.</summary>
        public ProductId Product { get; }

        /// <summary>The price as the platform formats it for the player.</summary>
        public string DisplayPrice { get; }

        public CatalogItem(ProductId product, string displayPrice)
        {
            Product = product;
            DisplayPrice = displayPrice;
        }
    }

    /// <summary>Native purchase and acknowledgment. Games commit fulfillment plus their ledger atomically before acknowledging.</summary>
    public interface ICommerce
    {
        UniTask<PlatformResult<CatalogItem[]>> CatalogAsync(CancellationToken token);
        UniTask<PlatformResult<PurchaseReceipt>> PurchaseAsync(
            ProductId product,
            CancellationToken token
        );
        UniTask<PlatformResult<PurchaseReceipt[]>> RestoreAsync(CancellationToken token);
        UniTask<PlatformResult<Unit>> AcknowledgeAsync(
            PurchaseReceipt receipt,
            CancellationToken token
        );
    }

    public readonly struct CloudAddress
    {
        public AccountIdentity Account { get; }
        public int Slot { get; }

        public CloudAddress(AccountIdentity account, int slot)
        {
            account.Validate();
            if (slot < 1)
                throw new ArgumentOutOfRangeException(nameof(slot));
            Account = account;
            Slot = slot;
        }
    }

    /// <summary>Opaque remote version and immutable bytes. Divergent versions remain distinct; timestamps never resolve conflicts.</summary>
    public sealed class CloudSaveVersion
    {
        private readonly byte[] bytes;
        public string Version { get; }
        public Guid LocalRevision { get; }
        public string Title { get; }
        public int ByteCount => bytes.Length;

        public byte[] Export() => (byte[])bytes.Clone();

        public CloudSaveVersion(string version, byte[] bytes, Guid localRevision, string title)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            this.bytes = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone();
            LocalRevision = localRevision;
            Title = title ?? string.Empty;
        }
    }

    public interface ICloudStorage
    {
        bool ExposesDivergentVersions { get; }
        UniTask<PlatformResult<CloudSaveVersion[]>> ReadVersionsAsync(
            CloudAddress address,
            CancellationToken token
        );

        /// <summary>Compare-and-publish. Null expectedVersion requires an unused remote slot. Providers must return Conflict on stale versions.</summary>
        UniTask<PlatformResult<CloudSaveVersion>> PublishAsync(
            CloudAddress address,
            CloudSaveVersion value,
            string expectedVersion,
            CancellationToken token
        );
    }

    /// <summary>Explicit capability routing, without dependency injection or implicit provider fallback/data migration.</summary>
    public sealed class SentinelComposition : IDisposable
    {
        private sealed class Route
        {
            internal ISentinelProvider Provider;
            internal Capability Capability;
        }

        private readonly List<ISentinelProvider> providers = new List<ISentinelProvider>();
        private readonly Dictionary<Type, Route> routes = new Dictionary<Type, Route>();
        private bool disposed;

        public SentinelComposition(params ISentinelProvider[] providers)
        {
            if (providers == null)
                throw new ArgumentNullException(nameof(providers));
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var provider in providers)
            {
                if (
                    provider == null
                    || string.IsNullOrWhiteSpace(provider.Identity)
                    || !identities.Add(provider.Identity)
                )
                    throw new ArgumentException("Provider identities must be unique.");
                this.providers.Add(provider);
            }
        }

        public void Select<T>(string provider, Capability capability)
            where T : class
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SentinelComposition));
            var selected =
                providers.Find(item => item.Identity == provider)
                ?? throw new ArgumentException("Provider was not selected for this composition.");
            if (
                capability == Capability.None
                || ((int)capability & ((int)capability - 1)) != 0
                || !selected.TryGet<T>(out _)
            )
                throw new ArgumentException(
                    "Provider does not implement this capability contract."
                );
            routes[typeof(T)] = new Route { Provider = selected, Capability = capability };
        }

        public PlatformResult<T> Resolve<T>()
            where T : class
        {
            if (disposed)
                return PlatformResult<T>.Failure(PlatformError.Unavailable);
            if (!routes.TryGetValue(typeof(T), out var route))
                return PlatformResult<T>.Failure(PlatformError.Unsupported);
            var status = route.Provider.Status(route.Capability);
            if (!status.Supported)
                return PlatformResult<T>.Failure(PlatformError.Unsupported, status.Detail);
            if (status.Availability != Availability.Ready)
                return PlatformResult<T>.Failure(
                    PlatformError.Unavailable,
                    status.Detail ?? status.Availability.ToString()
                );
            return route.Provider.TryGet<T>(out var value)
                ? PlatformResult<T>.Success(value)
                : PlatformResult<T>.Failure(PlatformError.Unsupported);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            routes.Clear();
            var failures = new List<Exception>();
            for (int i = providers.Count - 1; i >= 0; i--)
                try
                {
                    providers[i].Dispose();
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            providers.Clear();
            if (failures.Count > 0)
                throw new AggregateException(failures);
        }
    }
}
