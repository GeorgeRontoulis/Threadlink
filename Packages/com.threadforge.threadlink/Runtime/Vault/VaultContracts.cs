namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;

    /// <summary>A typed read-only runtime definition. T must be an engineering-defined immutable representation.</summary>
    public sealed class VaultRecord<T>
    {
        public Guid Identity { get; }
        public Guid Schema { get; }
        public int SchemaVersion { get; }
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
            SchemaVersion = version;
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

    /// <summary>Default schemas: a name-based GUID (RFC 4122 version 5) of the runtime type's stable name.</summary>
    internal static class VaultSchema
    {
        private static readonly Dictionary<Type, Guid> cache = new Dictionary<Type, Guid>();
        private static readonly byte[] space = new Guid("5d0c7a4e-6f0b-4a8e-9a3e-1f6c2b7d4e90").ToByteArray();

        public static Guid Of(Type type)
        {
            lock (cache)
            {
                if (cache.TryGetValue(type, out var known))
                    return known;
                var schema = Named(StableName(type));
                cache.Add(type, schema);
                return schema;
            }
        }

        /// <summary>The type's namespace-qualified name with generic arguments, without assembly versions.</summary>
        internal static string StableName(Type type)
        {
            if (type.IsArray)
                return StableName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            string simple = type.Name;
            int arity = simple.IndexOf('`');
            if (arity >= 0)
                simple = simple.Substring(0, arity);
            string name = type.IsNested ? StableName(type.DeclaringType) + "+" + simple : (type.Namespace == null ? simple : type.Namespace + "." + simple);
            if (!type.IsGenericType)
                return name;
            var arguments = type.GetGenericArguments();
            var text = new System.Text.StringBuilder(name).Append('<');
            for (int i = 0; i < arguments.Length; i++)
                text.Append(i == 0 ? string.Empty : ",").Append(StableName(arguments[i]));
            return text.Append('>').ToString();
        }

        internal static Guid Named(string name)
        {
            // RFC 4122 orders the namespace bytes big-endian; Guid.ToByteArray is little-endian in its first three fields.
            var bytes = (byte[])space.Clone();
            Swap(bytes);
            var data = System.Text.Encoding.UTF8.GetBytes(name);
            var input = new byte[bytes.Length + data.Length];
            Buffer.BlockCopy(bytes, 0, input, 0, bytes.Length);
            Buffer.BlockCopy(data, 0, input, bytes.Length, data.Length);
            byte[] hash;
            using (var sha = System.Security.Cryptography.SHA1.Create())
                hash = sha.ComputeHash(input);
            var result = new byte[16];
            Array.Copy(hash, result, 16);
            result[6] = (byte)((result[6] & 0x0F) | 0x50);
            result[8] = (byte)((result[8] & 0x3F) | 0x80);
            Swap(result);
            return new Guid(result);
        }

        private static void Swap(byte[] guid)
        {
            (guid[0], guid[3]) = (guid[3], guid[0]);
            (guid[1], guid[2]) = (guid[2], guid[1]);
            (guid[4], guid[5]) = (guid[5], guid[4]);
            (guid[6], guid[7]) = (guid[7], guid[6]);
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

    /// <summary>
    /// A bag of named, immutable values read by generated <see cref="VaultField"/> keys. Designers author one as a
    /// <see cref="VaultFieldSetAsset"/>. Values are scalars, strings, Unity vectors, colors and rotations, and references
    /// to Unity objects such as sprites or prefabs.
    /// </summary>
    public sealed class VaultFieldSet
    {
        private readonly Dictionary<string, object> fields;

        public VaultFieldSet(IEnumerable<KeyValuePair<string, object>> values)
        {
            fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                var value = pair.Value;
                if (!IsSupported(value))
                    throw new ArgumentException(
                        "Field set values must be immutable scalars, strings, Unity vectors, colors or rotations, Unity object references or nested field sets."
                    );
                fields.Add(pair.Key, value);
            }
        }

        /// <summary>Number of fields.</summary>
        public int Count => fields.Count;

        internal IReadOnlyDictionary<string, object> Values => fields;

        internal static bool IsSupported(object value) =>
            value == null
            || value is string
            || value is bool
            || value is int
            || value is long
            || value is float
            || value is double
            || value is Guid
            || value is UnityEngine.Vector2
            || value is UnityEngine.Vector3
            || value is UnityEngine.Vector4
            || value is UnityEngine.Vector2Int
            || value is UnityEngine.Vector3Int
            || value is UnityEngine.Color
            || value is UnityEngine.Quaternion
            || value is UnityEngine.Object
            || value is VaultFieldSet;

        /// <summary>Whether the set has the field, whatever its value type.</summary>
        public bool Contains(VaultField field) => field.Name != null && fields.ContainsKey(field.Name);

        /// <summary>A field's value by its generated ID.</summary>
        public T Get<T>(VaultField field) =>
            field.Name != null && fields.TryGetValue(field.Name, out var value) && value is T typed
                ? typed
                : throw new InvalidOperationException("Missing or incompatible definition field: " + field.Name);

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
    }
}
