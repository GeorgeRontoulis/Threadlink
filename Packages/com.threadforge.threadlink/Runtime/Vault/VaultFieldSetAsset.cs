namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>The kind of value a <see cref="VaultFieldSetAsset"/> entry holds.</summary>
    public enum VaultValueKind
    {
        /// <summary>A 32-bit integer.</summary>
        Integer,

        /// <summary>A single-precision number.</summary>
        Float,

        /// <summary>True or false.</summary>
        Boolean,

        /// <summary>A string.</summary>
        Text,

        /// <summary>A 64-bit integer.</summary>
        Long,

        /// <summary>A double-precision number.</summary>
        Double,

        /// <summary>A <see cref="UnityEngine.Vector2"/>.</summary>
        Vector2,

        /// <summary>A <see cref="UnityEngine.Vector3"/>.</summary>
        Vector3,

        /// <summary>A <see cref="UnityEngine.Vector4"/>.</summary>
        Vector4,

        /// <summary>A <see cref="UnityEngine.Vector2Int"/>.</summary>
        Vector2Int,

        /// <summary>A <see cref="UnityEngine.Vector3Int"/>.</summary>
        Vector3Int,

        /// <summary>A <see cref="UnityEngine.Color"/>.</summary>
        Color,

        /// <summary>A <see cref="Quaternion"/>, authored as Euler angles in degrees.</summary>
        Rotation,

        /// <summary>A reference to a Unity object, such as a sprite, clip or prefab.</summary>
        Object,
    }

    /// <summary>
    /// A Vault definition that designers build without code: a list of fields, each a generated <see cref="VaultField"/>
    /// key with a typed value. Code reads <c>Vault.Get(asset).Get&lt;int&gt;(VaultFields.Damage)</c>. A set can inherit
    /// every field of a parent set and add or replace fields.
    /// </summary>
    [CreateAssetMenu(menuName = "Threadlink/Vault/Field Set", order = 10)]
    public sealed class VaultFieldSetAsset : VaultAsset<VaultFieldSet>
    {
        /// <summary>One field of a field set.</summary>
        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("The field: an entry of an ID domain that extends Vault fields.")]
            private string field = string.Empty;

            [SerializeField, Tooltip("The kind of value.")]
            private VaultValueKind kind;

            [SerializeField]
            private long whole;

            [SerializeField]
            private double number;

            [SerializeField]
            private bool flag;

            [SerializeField]
            private string text = string.Empty;

            [SerializeField]
            private Vector4 vector;

            [SerializeField]
            private Vector3Int wholes;

            [SerializeField]
            private Object reference;

            /// <summary>The field this entry sets.</summary>
            public VaultField Field => new VaultField(field ?? string.Empty);

            /// <summary>The kind of value.</summary>
            public VaultValueKind Kind => kind;

            /// <summary>The value, typed by <see cref="Kind"/>.</summary>
            public object Value =>
                kind switch
                {
                    VaultValueKind.Integer => (int)whole,
                    VaultValueKind.Long => whole,
                    VaultValueKind.Float => (float)number,
                    VaultValueKind.Double => number,
                    VaultValueKind.Boolean => flag,
                    VaultValueKind.Text => text ?? string.Empty,
                    VaultValueKind.Vector2 => (Vector2)vector,
                    VaultValueKind.Vector3 => (Vector3)vector,
                    VaultValueKind.Vector4 => vector,
                    VaultValueKind.Vector2Int => new Vector2Int(wholes.x, wholes.y),
                    VaultValueKind.Vector3Int => wholes,
                    VaultValueKind.Color => (Color)vector,
                    VaultValueKind.Rotation => Quaternion.Euler(vector.x, vector.y, vector.z),
                    VaultValueKind.Object => reference,
                    _ => throw new InvalidOperationException("Unknown field kind " + kind + "."),
                };

            /// <summary>An entry without a field, as the Inspector adds one.</summary>
            public Entry() { }

            /// <summary>Creates an entry; <see cref="VaultFieldSetAsset.Set"/> adds one to a set.</summary>
            public Entry(VaultField field, object value)
            {
                this.field = field.Name ?? throw new ArgumentException("The field has no name.", nameof(field));
                Assign(value);
            }

            internal void Assign(object value)
            {
                switch (value)
                {
                    case int number32:
                        (kind, whole) = (VaultValueKind.Integer, number32);
                        break;
                    case long number64:
                        (kind, whole) = (VaultValueKind.Long, number64);
                        break;
                    case float single:
                        (kind, number) = (VaultValueKind.Float, single);
                        break;
                    case double precise:
                        (kind, number) = (VaultValueKind.Double, precise);
                        break;
                    case bool truth:
                        (kind, flag) = (VaultValueKind.Boolean, truth);
                        break;
                    case string words:
                        (kind, text) = (VaultValueKind.Text, words);
                        break;
                    case Vector2 two:
                        (kind, vector) = (VaultValueKind.Vector2, two);
                        break;
                    case Vector3 three:
                        (kind, vector) = (VaultValueKind.Vector3, three);
                        break;
                    case Vector4 four:
                        (kind, vector) = (VaultValueKind.Vector4, four);
                        break;
                    case Vector2Int twoWhole:
                        (kind, wholes) = (VaultValueKind.Vector2Int, new Vector3Int(twoWhole.x, twoWhole.y, 0));
                        break;
                    case Vector3Int threeWhole:
                        (kind, wholes) = (VaultValueKind.Vector3Int, threeWhole);
                        break;
                    case Color color:
                        (kind, vector) = (VaultValueKind.Color, color);
                        break;
                    case Quaternion rotation:
                        (kind, vector) = (VaultValueKind.Rotation, rotation.eulerAngles);
                        break;
                    case Object unityObject:
                        (kind, reference) = (VaultValueKind.Object, unityObject);
                        break;
                    case null:
                        (kind, reference) = (VaultValueKind.Object, null);
                        break;
                    default:
                        throw new ArgumentException("A field set cannot hold a " + value.GetType().Name + ".", nameof(value));
                }
            }
        }

        [SerializeField]
        [Tooltip("Optional set this one inherits every field from. Fields listed here are added or replace inherited ones.")]
        private VaultFieldSetAsset parent;

        [SerializeField]
        [Tooltip("The fields. Each field may appear once.")]
        private List<Entry> fields = new List<Entry>();

        /// <summary>The set this one inherits from, or null.</summary>
        public VaultFieldSetAsset Parent
        {
            get => parent;
            set => parent = value;
        }

        /// <summary>The fields authored on this set, without inherited ones.</summary>
        public IReadOnlyList<Entry> Fields => fields;

        /// <inheritdoc />
        public override VaultAuthoring ParentSource => parent;

        /// <summary>Adds the field, or replaces its value when this set already has it. For tools and tests.</summary>
        public void Set(VaultField field, object value)
        {
            foreach (var entry in fields)
                if (entry.Field.Equals(field))
                {
                    entry.Assign(value);
                    return;
                }
            fields.Add(new Entry(field, value));
        }

        /// <summary>Removes the field from this set. Returns whether it was there.</summary>
        public bool Remove(VaultField field) => fields.RemoveAll(entry => entry.Field.Equals(field)) > 0;

        /// <inheritdoc />
        protected override void ValidateAuthoring()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in fields)
            {
                string name = entry?.Field.Name;
                if (string.IsNullOrEmpty(name))
                    throw new InvalidOperationException("A field has no ID. Choose one of the Vault fields.");
                if (!seen.Add(name))
                    throw new InvalidOperationException("The field " + name + " appears more than once.");
            }
        }

        /// <inheritdoc />
        protected override VaultFieldSet BuildRuntime(VaultContext context)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            if (parent != null)
                foreach (var pair in context.Prepare(parent).Value.Values)
                    values[pair.Key] = pair.Value;
            foreach (var entry in fields)
                values[entry.Field.Name] = entry.Value;
            return new VaultFieldSet(values);
        }
    }
}
