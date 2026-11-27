namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    /// <summary>The kinds of platform data game code refers to by platform-agnostic ID.</summary>
    public enum PlatformIdKind
    {
        /// <summary>Generates <c>Achievements</c> (<see cref="AchievementId"/>).</summary>
        Achievement,

        /// <summary>Generates <c>Statistics</c> (<see cref="StatisticId"/>).</summary>
        Statistic,

        /// <summary>Generates <c>Leaderboards</c> (<see cref="LeaderboardId"/>).</summary>
        Leaderboard,

        /// <summary>Generates <c>Products</c> (<see cref="ProductId"/>).</summary>
        Product,
    }

    /// <summary>One ID a platform authors: the shared name game code uses, and the platform's own name for it.</summary>
    public readonly struct AuthoredPlatformId
    {
        /// <summary>What the ID identifies.</summary>
        public readonly PlatformIdKind Kind;

        /// <summary>The platform-agnostic name; the same achievement on two platforms has the same name.</summary>
        public readonly string Name;

        /// <summary>The platform's own name, such as a Steam API name, for documentation.</summary>
        public readonly string Native;

        /// <summary>Creates an authored ID.</summary>
        public AuthoredPlatformId(PlatformIdKind kind, string name, string native)
        {
            Kind = kind;
            Name = name;
            Native = native;
        }
    }

    /// <summary>
    /// A platform's authoring of achievements, statistics, leaderboards and products. Each platform package implements
    /// this in an Editor assembly; Sentinel generates its platform-agnostic IDs from every installed platform and never
    /// reads platform data itself.
    /// </summary>
    public interface IPlatformAuthoring
    {
        /// <summary>The platform's name, such as Steam.</summary>
        string Platform { get; }

        /// <summary>Everything the platform authors.</summary>
        IEnumerable<AuthoredPlatformId> Authored { get; }

        /// <summary>Opens the place the platform's data is authored.</summary>
        void Open();
    }

    /// <summary>
    /// Generates <c>Achievements</c>, <c>Statistics</c>, <c>Leaderboards</c> and <c>Products</c> from what installed
    /// platforms author. An ID authored on any platform is generated; each member lists the platforms that author it.
    /// </summary>
    public sealed class PlatformIdGenerator : IIdGenerator
    {
        private static readonly (PlatformIdKind kind, string className, string type)[] Kinds =
        {
            (PlatformIdKind.Achievement, "Achievements", "AchievementId"),
            (PlatformIdKind.Statistic, "Statistics", "StatisticId"),
            (PlatformIdKind.Leaderboard, "Leaderboards", "LeaderboardId"),
            (PlatformIdKind.Product, "Products", "ProductId"),
        };

        /// <summary>Every installed platform's authoring.</summary>
        public static List<IPlatformAuthoring> Platforms() =>
            TypeCache
                .GetTypesDerivedFrom<IPlatformAuthoring>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (IPlatformAuthoring)Activator.CreateInstance(type))
                .OrderBy(platform => platform.Platform, StringComparer.Ordinal)
                .ToList();

        /// <inheritdoc />
        public string Title => "Platform services";

        /// <inheritdoc />
        public string Source
        {
            get
            {
                var platforms = Platforms();
                return platforms.Count == 0
                    ? "No platform package is installed"
                    : "Authored per platform: " + string.Join(", ", platforms.Select(platform => platform.Platform));
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> Outputs => Kinds.Select(output => output.className);

        /// <inheritdoc />
        public void OpenSource()
        {
            var first = Platforms().FirstOrDefault();
            if (first != null)
                first.Open();
        }

        /// <inheritdoc />
        public bool Generate()
        {
            var authored = Platforms().SelectMany(platform => platform.Authored.Select(id => (platform: platform.Platform, id))).ToList();
            bool changed = false;
            foreach (var (kind, className, type) in Kinds)
            {
                var members = authored
                    .Where(entry => entry.id.Kind == kind && !string.IsNullOrWhiteSpace(entry.id.Name))
                    .GroupBy(entry => entry.id.Name.Trim(), StringComparer.Ordinal)
                    .Select(group => new GeneratedIds.Member
                    {
                        Name = group.Key,
                        Key = group.Key,
                        Type = "global::Threadforge.Threadlink." + type,
                        Value = "new global::Threadforge.Threadlink." + type + "(" + GeneratedIds.Quote(group.Key) + ")",
                        Summary = string.Join("; ", group.Select(entry => entry.platform + ": " + (string.IsNullOrWhiteSpace(entry.id.Native) ? group.Key : entry.id.Native))) + ".",
                    })
                    .ToList();
                // Only write a class once something is authored for it, or to keep tombstones of a class that exists.
                if (members.Count > 0 || System.IO.File.Exists(GeneratedIds.PathOf(className)))
                    changed |= GeneratedIds.Write(className, $"{className} authored on the installed platforms. Sentinel passes them to each platform unchanged.", members);
            }
            return changed;
        }
    }

    /// <summary>Warns when an ID is authored on some installed platforms but not others. Read-only.</summary>
    public sealed class PlatformAuthoringValidator : IWorkspaceValidator
    {
        /// <inheritdoc />
        public string Name => "Platform services";

        /// <inheritdoc />
        public void Validate(List<ValidationIssue> issues)
        {
            var platforms = PlatformIdGenerator.Platforms();
            if (platforms.Count < 2)
                return;
            var authored = platforms.ToDictionary(platform => platform.Platform, platform => new HashSet<(PlatformIdKind, string)>(platform.Authored.Select(id => (id.Kind, id.Name?.Trim()))));
            foreach (var id in authored.Values.SelectMany(set => set).Distinct())
            {
                var missing = authored.Where(pair => !pair.Value.Contains(id)).Select(pair => pair.Key).ToList();
                if (missing.Count > 0)
                    issues.Add(new ValidationIssue(Name, IssueSeverity.Warning, $"{id.Item1} {id.Item2} is not authored for {string.Join(", ", missing)}; there it reports NotFound."));
            }
        }
    }
}
