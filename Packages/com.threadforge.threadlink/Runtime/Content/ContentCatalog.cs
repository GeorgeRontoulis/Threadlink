namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>A stable typed identity independent of accessor names and asset paths.</summary>
    internal readonly struct ResourceId<T> : IEquatable<ResourceId<T>>
    {
        /// <summary>The persisted identity. Empty is invalid.</summary>
        public Guid Value { get; }

        /// <summary>Constructs an ID from an existing persisted identity.</summary>
        public ResourceId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Content identity must be nonempty.");
            Value = value;
        }

        /// <inheritdoc/>
        public bool Equals(ResourceId<T> other) => Value.Equals(other.Value);

        /// <inheritdoc/>
        public override bool Equals(object other) => other is ResourceId<T> id && Equals(id);

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>Canonical persisted identity text.</summary>
        public override string ToString() => Value.ToString("D");
    }

    /// <summary>One authoritative identity/type/backend-location mapping.</summary>
    internal sealed class ContentEntry
    {
        /// <summary>Stable persisted identity.</summary>
        public Guid Identity { get; }

        /// <summary>Declared result type.</summary>
        public Type AssetType { get; }

        /// <summary>Backend-specific location, such as an Addressables asset GUID.</summary>
        public string Location { get; }

        private ContentEntry(Guid identity, Type type, string location)
        {
            Identity = identity;
            AssetType = type;
            Location = location;
        }

        /// <summary>Defines an explicit typed mapping without runtime type discovery.</summary>
        public static ContentEntry Create<T>(ResourceId<T> id, string location) => Create(id.Value, typeof(T), location);

        /// <summary>
        /// Defines a mapping of a load strategy: <see cref="UnityEngine.Object"/> accepts any asset type,
        /// <see cref="UnityEngine.GameObject"/> also accepts root components.
        /// </summary>
        public static ContentEntry Create(Guid identity, Type type, string location)
        {
            if (identity == Guid.Empty || type == null || string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Invalid content mapping.");
            return new ContentEntry(identity, type, location);
        }

        /// <summary>Whether a request for <paramref name="requested"/> may load this entry.</summary>
        internal bool Accepts(Type requested) =>
            requested == AssetType
            || AssetType == typeof(UnityEngine.Object) && typeof(UnityEngine.Object).IsAssignableFrom(requested) && !typeof(UnityEngine.Component).IsAssignableFrom(requested)
            || AssetType == typeof(UnityEngine.GameObject) && typeof(UnityEngine.Component).IsAssignableFrom(requested);
    }

    /// <summary>Immutable validated runtime catalog. Removed identities are never silently remapped.</summary>
    internal sealed class ContentCatalog
    {
        private readonly Dictionary<Guid, ContentEntry> entries =
            new Dictionary<Guid, ContentEntry>();

        /// <summary>Copies and validates the complete catalog before use.</summary>
        public ContentCatalog(IEnumerable<ContentEntry> mappings)
        {
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));
            foreach (var entry in mappings)
            {
                if (entry == null || entries.ContainsKey(entry.Identity))
                    throw new ArgumentException("Null or duplicate content mapping.");
                entries.Add(entry.Identity, entry);
            }
        }

        /// <summary>Number of mapped identities.</summary>
        public int Count => entries.Count;

        internal void Merge(ContentEntry entry)
        {
            // A mapping made from code replaces the mapping of the same ID.
            entries[entry.Identity] = entry;
        }

        /// <summary>Resolves an ID and validates its declared type before acquisition.</summary>
        public ContentEntry Resolve<T>(ResourceId<T> id)
        {
            if (!entries.TryGetValue(id.Value, out var entry))
                throw new KeyNotFoundException("Content identity is not mapped: " + id);
            if (!entry.Accepts(typeof(T)))
                throw new InvalidOperationException("Content identity type mismatch: " + id);
            return entry;
        }
    }
}
