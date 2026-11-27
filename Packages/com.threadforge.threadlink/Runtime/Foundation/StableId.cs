namespace Threadforge.Threadlink
{
    using System;

    /// <summary>A stable, nonempty GUID identity independent of display names, paths, and generated accessors.</summary>
    /// <remarks>The default value is invalid. Persist the canonical text or GUID, never a hash code.
    /// This type does not reinterpret integer identifiers.</remarks>
    public readonly struct StableId : IEquatable<StableId>
    {
        private readonly Guid value;

        /// <summary>Creates an identity from an existing nonempty GUID.</summary>
        public StableId(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Identity must be nonempty.", nameof(value));
            this.value = value;
        }

        /// <summary>Whether this value contains a valid identity.</summary>
        public bool IsValid => value != Guid.Empty;

        /// <summary>Returns the GUID, or throws for an uninitialized value.</summary>
        public Guid Value =>
            IsValid ? value : throw new InvalidOperationException("Identity is uninitialized.");

        /// <summary>Parses canonical GUID text without creating a new identity.</summary>
        public static bool TryParse(string text, out StableId identity)
        {
            if (Guid.TryParseExact(text, "D", out var parsed) && parsed != Guid.Empty)
            {
                identity = new StableId(parsed);
                return true;
            }
            identity = default;
            return false;
        }

        /// <summary>Compares stored identity values.</summary>
        public bool Equals(StableId other) => value.Equals(other.value);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is StableId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => value.GetHashCode();

        /// <summary>Returns canonical persisted GUID text; rejects an uninitialized identity.</summary>
        public override string ToString() => Value.ToString("D");

        /// <summary>Compares stored identity values.</summary>
        public static bool operator ==(StableId left, StableId right) => left.Equals(right);

        /// <summary>Compares stored identity values.</summary>
        public static bool operator !=(StableId left, StableId right) => !left.Equals(right);
    }
}
