namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>
    /// A type-safe audio category, generated as <c>AudioCategories.Name</c> from Project Settings ▸ Threadlink ▸ Aura.
    /// Construct one by name only for data-driven cases, such as volumes stored in a save.
    /// </summary>
    public readonly struct AudioCategory : IEquatable<AudioCategory>
    {
        /// <summary>The category name, as cues store it.</summary>
        public readonly string Name;

        /// <summary>A category by name.</summary>
        public AudioCategory(string name) =>
            Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("A category needs a name.", nameof(name));

        /// <inheritdoc />
        public bool Equals(AudioCategory other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object other) => other is AudioCategory category && Equals(category);

        /// <inheritdoc />
        public override int GetHashCode() => Name != null ? Name.GetHashCode() : 0;

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>Marks a string field that names an audio category; the Inspector shows a dropdown of the project's categories.</summary>
    public sealed class AudioCategoryNameAttribute : PropertyAttribute { }
}
