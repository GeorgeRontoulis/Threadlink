namespace Threadforge.Threadlink
{
    using System;

    // Platform-agnostic IDs. Sentinel never knows where platform data comes from: each platform authors its own
    // achievements, statistics, leaderboards and products (for Steam, in Project Settings ▸ Threadlink ▸ Steam), and
    // game code refers to them through these IDs, generated as Achievements.Name, Statistics.Name, Leaderboards.Name and
    // Products.Name from every installed platform's authoring. Sentinel passes an ID to the provider unchanged; the
    // provider resolves it against its own authoring, much as a boxed value is unboxed to its concrete type. An ID a
    // platform does not author fails there with PlatformError.NotFound.

    /// <summary>A platform-agnostic achievement; see the remarks on platform IDs above.</summary>
    public readonly struct AchievementId : IEquatable<AchievementId>
    {
        /// <summary>The name every platform that authors this achievement uses for it.</summary>
        public readonly string Name;

        /// <summary>An achievement by name. Generated code calls this.</summary>
        public AchievementId(string name) => Name = PlatformIdName.Require(name, "achievement");

        /// <inheritdoc />
        public bool Equals(AchievementId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is AchievementId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => PlatformIdName.Hash(Name);

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }

    /// <summary>A platform-agnostic statistic, such as a lifetime count; see the remarks on platform IDs above.</summary>
    public readonly struct StatisticId : IEquatable<StatisticId>
    {
        /// <summary>The name every platform that authors this statistic uses for it.</summary>
        public readonly string Name;

        /// <summary>A statistic by name. Generated code calls this.</summary>
        public StatisticId(string name) => Name = PlatformIdName.Require(name, "statistic");

        /// <inheritdoc />
        public bool Equals(StatisticId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is StatisticId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => PlatformIdName.Hash(Name);

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }

    /// <summary>A platform-agnostic leaderboard; see the remarks on platform IDs above.</summary>
    public readonly struct LeaderboardId : IEquatable<LeaderboardId>
    {
        /// <summary>The name every platform that authors this leaderboard uses for it.</summary>
        public readonly string Name;

        /// <summary>A leaderboard by name. Generated code calls this.</summary>
        public LeaderboardId(string name) => Name = PlatformIdName.Require(name, "leaderboard");

        /// <inheritdoc />
        public bool Equals(LeaderboardId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is LeaderboardId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => PlatformIdName.Hash(Name);

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }

    /// <summary>A platform-agnostic product: DLC, an entitlement or a purchasable item; see the remarks on platform IDs above.</summary>
    public readonly struct ProductId : IEquatable<ProductId>
    {
        /// <summary>The name every platform that authors this product uses for it.</summary>
        public readonly string Name;

        /// <summary>A product by name. Generated code calls this.</summary>
        public ProductId(string name) => Name = PlatformIdName.Require(name, "product");

        /// <inheritdoc />
        public bool Equals(ProductId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is ProductId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => PlatformIdName.Hash(Name);

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }

    internal static class PlatformIdName
    {
        internal static string Require(string name, string kind) =>
            !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("A " + kind + " needs a name.", nameof(name));

        internal static int Hash(string name) => name != null ? StringComparer.Ordinal.GetHashCode(name) : 0;
    }
}
