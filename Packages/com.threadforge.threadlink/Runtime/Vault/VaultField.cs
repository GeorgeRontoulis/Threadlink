namespace Threadforge.Threadlink
{
    using System;

    /// <summary>
    /// A typed key of a <see cref="VaultFieldSet"/>. Projects generate them as <c>VaultFields.Name</c> from ID Domain
    /// assets that extend Vault fields.
    /// </summary>
    public readonly struct VaultField : IEquatable<VaultField>
    {
        /// <summary>The field's key in a field set.</summary>
        public readonly string Name;

        /// <summary>Creates a field key. Generated code calls this.</summary>
        public VaultField(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        /// <inheritdoc />
        public bool Equals(VaultField other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is VaultField field && Equals(field);

        /// <inheritdoc />
        public override int GetHashCode() => Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }
}
