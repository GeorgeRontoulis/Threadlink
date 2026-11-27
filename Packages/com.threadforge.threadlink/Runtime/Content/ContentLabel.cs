namespace Threadforge.Threadlink
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// An Addressables label, generated as <c>ContentLabels.Name</c> from the labels in the Addressables settings.
    /// <c>Threadlink.LoadAllAsync&lt;T&gt;(label)</c> loads every asset with it.
    /// </summary>
    public readonly struct ContentLabel : IEquatable<ContentLabel>
    {
        /// <summary>The label as Addressables spells it.</summary>
        public readonly string Name;

        /// <summary>A label by name. Generated code calls this.</summary>
        public ContentLabel(string name) =>
            Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("A label needs a name.", nameof(name));

        /// <summary>Identity of requests made by this label, for diagnostics.</summary>
        internal Guid Identity
        {
            get
            {
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("label:" + Name)));
            }
        }

        /// <inheritdoc />
        public bool Equals(ContentLabel other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is ContentLabel label && Equals(label);

        /// <inheritdoc />
        public override int GetHashCode() => Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;

        /// <inheritdoc />
        public override string ToString() => Name ?? string.Empty;
    }
}
