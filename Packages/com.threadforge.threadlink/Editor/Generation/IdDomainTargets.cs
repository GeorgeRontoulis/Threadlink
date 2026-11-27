namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    /// <summary>
    /// A native ID domain that projects extend with <see cref="IdDomain"/> assets, such as StatelessRNG random domains.
    /// Every domain extending a target adds its entries to the target's one generated class. Packages implement this in
    /// an Editor assembly; Threadlink finds implementations with TypeCache.
    /// </summary>
    public interface IIdDomainTarget
    {
        /// <summary>Stable identifier stored in domain assets, for example <c>StatelessRNG.Domains</c>.</summary>
        string Id { get; }

        /// <summary>Name shown in the domain's Extends dropdown.</summary>
        string DisplayName { get; }

        /// <summary>The generated class, for example <c>RandomDomains</c>.</summary>
        string ClassName { get; }

        /// <summary>Documentation of the generated class.</summary>
        string Summary { get; }

        /// <summary>An assembly the generated code needs, added to the generated folder's assembly; null for none.</summary>
        string RequiredAssembly => null;

        /// <summary>The first value of a new entry. Defaults to a hash of its name.</summary>
        int InitialValue(string name) => IdDomain.Hash(name);

        /// <summary>The generated member for one entry.</summary>
        GeneratedIds.Member Member(IdDomain domain, IdDomain.Entry entry);
    }

    /// <summary>The installed native ID domains.</summary>
    public static class IdDomainTargets
    {
        private static List<IIdDomainTarget> cache;

        /// <summary>Every installed target, by display name.</summary>
        public static IReadOnlyList<IIdDomainTarget> All =>
            cache ??= TypeCache
                .GetTypesDerivedFrom<IIdDomainTarget>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (IIdDomainTarget)Activator.CreateInstance(type))
                .OrderBy(target => target.DisplayName, StringComparer.Ordinal)
                .ToList();

        /// <summary>The target with this identifier, or null when it is empty or not installed.</summary>
        public static IIdDomainTarget Find(string id) =>
            string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(target => target.Id == id);
    }

    /// <summary>Vault field IDs: typed keys for <see cref="VaultFieldSet"/>, generated as <c>VaultFields.Name</c>.</summary>
    public sealed class VaultFieldsTarget : IIdDomainTarget
    {
        /// <inheritdoc />
        public string Id => "Vault.Fields";

        /// <inheritdoc />
        public string DisplayName => "Vault fields";

        /// <inheritdoc />
        public string ClassName => "VaultFields";

        /// <inheritdoc />
        public string Summary => "Vault field IDs: the keys of Vault field sets.";

        /// <inheritdoc />
        public GeneratedIds.Member Member(IdDomain domain, IdDomain.Entry entry) =>
            new GeneratedIds.Member
            {
                Name = entry.Name,
                Key = entry.Value.ToString(),
                Type = "global::Threadforge.Threadlink.VaultField",
                Value = "new global::Threadforge.Threadlink.VaultField(" + GeneratedIds.Quote(GeneratedIds.Identifier(entry.Name)) + ")",
                Summary = string.IsNullOrEmpty(entry.Summary) ? "Vault field " + entry.Name + " (" + domain.name + ")." : entry.Summary,
            };
    }
}
