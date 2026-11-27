namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Draws a <see cref="Vector2"/> or <see cref="Vector2Int"/> field as a range slider between <see cref="Min"/> and
    /// <see cref="Max"/>: x is the low end, y the high end.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinMaxRangeAttribute : PropertyAttribute
    {
        /// <summary>The lowest value.</summary>
        public float Min { get; }

        /// <summary>The highest value.</summary>
        public float Max { get; }

        /// <summary>A range between <paramref name="min"/> and <paramref name="max"/>.</summary>
        public MinMaxRangeAttribute(float min, float max)
        {
            if (max < min)
                throw new ArgumentException("The maximum is below the minimum.", nameof(max));
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// An Animator parameter or state, authored by name and used by its hash, so code never hashes or types the name:
    /// <c>animator.SetFloat(speed, value)</c>.
    /// </summary>
    [Serializable]
    public struct AnimatorHash : ISerializationCallbackReceiver, IEquatable<AnimatorHash>
    {
        [SerializeField]
        [Tooltip("The parameter or state name, as written in the Animator Controller.")]
        private string name;

        [NonSerialized]
        private int value;

        [NonSerialized]
        private bool hashed;

        /// <summary>An Animator name and its hash.</summary>
        public AnimatorHash(string name)
        {
            this.name = name ?? string.Empty;
            value = Animator.StringToHash(this.name);
            hashed = true;
        }

        /// <summary>The name, as written in the Animator Controller.</summary>
        public string Name => name ?? string.Empty;

        /// <summary>The hash Animator methods take (<see cref="Animator.StringToHash"/>).</summary>
        public int Value
        {
            get
            {
                if (!hashed)
                {
                    value = Animator.StringToHash(Name);
                    hashed = true;
                }
                return value;
            }
        }

        /// <summary>Whether a name is set.</summary>
        public bool IsAssigned => !string.IsNullOrEmpty(name);

        /// <summary>The hash, for Animator methods.</summary>
        public static implicit operator int(AnimatorHash hash) => hash.Value;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => hashed = false;

        /// <inheritdoc />
        public bool Equals(AnimatorHash other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is AnimatorHash hash && Equals(hash);

        /// <inheritdoc />
        public override int GetHashCode() => Value;

        /// <inheritdoc />
        public override string ToString() => Name;
    }
}
