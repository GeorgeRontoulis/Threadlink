namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>A typed read-only runtime definition. T must be an engineering-defined immutable representation.</summary>
    public sealed class VaultRecord<T>
    {
        public Guid Identity { get; }
        public Guid Schema { get; }
        public int Version { get; }
        public T Value { get; }

        public VaultRecord(Guid identity, Guid schema, int version, T value)
        {
            if (
                identity == Guid.Empty
                || schema == Guid.Empty
                || version < 1
                || ReferenceEquals(value, null)
            )
                throw new ArgumentException("Invalid runtime definition.");
            Identity = identity;
            Schema = schema;
            Version = version;
            Value = value;
        }
    }

    /// <summary>Explicit authoring override state. Reset restores inheritance rather than copying a parent value.</summary>
    [Serializable]
    public struct VaultOverride<T>
    {
        [UnityEngine.Tooltip("Replace the inherited value. Off inherits the parent's.")]
        public bool Enabled;
        [UnityEngine.Tooltip("Value used while the override is enabled.")]
        public T Value;

        public T Resolve(T inherited) => Enabled ? Value : inherited;

        public void Reset()
        {
            Enabled = false;
            Value = default;
        }
    }

    /// <summary>Explicit source contract for schema validation and immutable mapping.</summary>
    public interface IVaultSource<T>
    {
        Guid Identity { get; }
        Guid Schema { get; }
        int SchemaVersion { get; }
        T Build(VaultContext context);
    }

    /// <summary>One explicit preparation pass, detecting reference/variant cycles and incompatible duplicate identities.</summary>
    public sealed class VaultContext
    {
        private readonly HashSet<Guid> visiting = new HashSet<Guid>();
        private readonly Dictionary<Guid, object> sources = new Dictionary<Guid, object>();
        private readonly Dictionary<Guid, object> prepared = new Dictionary<Guid, object>();

        /// <summary>Number of definitions prepared by this context.</summary>
        public int PreparedCount => prepared.Count;

        public VaultRecord<T> Prepare<T>(IVaultSource<T> source)
        {
            if (
                source == null
                || source.Identity == Guid.Empty
                || source.Schema == Guid.Empty
                || source.SchemaVersion < 1
            )
                throw new ArgumentException("Invalid definition schema or identity.");
            if (
                sources.TryGetValue(source.Identity, out var previous)
                && !ReferenceEquals(previous, source)
            )
                throw new InvalidOperationException("Two definition sources share one identity.");
            sources[source.Identity] = source;
            if (prepared.TryGetValue(source.Identity, out var cached))
                return cached is VaultRecord<T> typed
                    ? typed
                    : throw new InvalidOperationException("Definition runtime type mismatch.");
            if (!visiting.Add(source.Identity))
                throw new InvalidOperationException("Definition reference/variant cycle.");
            try
            {
                var definition = new VaultRecord<T>(
                    source.Identity,
                    source.Schema,
                    source.SchemaVersion,
                    source.Build(this)
                );
                prepared.Add(source.Identity, definition);
                return definition;
            }
            finally
            {
                visiting.Remove(source.Identity);
            }
        }
    }

    /// <summary>Opt-in dynamic primitive field bag. Values are restricted to immutable scalar types.</summary>
    public sealed class VaultFieldSet
    {
        private readonly Dictionary<string, object> fields;

        public VaultFieldSet(IEnumerable<KeyValuePair<string, object>> values)
        {
            fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                var value = pair.Value;
                if (
                    !(
                        value == null
                        || value is string
                        || value is bool
                        || value is int
                        || value is long
                        || value is float
                        || value is double
                        || value is Guid
                        || value is VaultFieldSet
                    )
                )
                    throw new ArgumentException(
                        "Dynamic definition values must be immutable primitives or nested field sets."
                    );
                fields.Add(pair.Key, value);
            }
        }

        /// <summary>A field's value by its generated ID.</summary>
        public T Get<T>(VaultField field) => Get<T>(field.Name);

        /// <summary>Whether the field exists with a value of type <typeparamref name="T"/>.</summary>
        public bool TryGet<T>(VaultField field, out T value)
        {
            if (field.Name != null && fields.TryGetValue(field.Name, out var found) && found is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>A field's value by name.</summary>
        public T Get<T>(string field) =>
            fields.TryGetValue(field, out var value) && value is T typed
                ? typed
                : throw new InvalidOperationException(
                    "Missing or incompatible definition field: " + field
                );
    }
}
