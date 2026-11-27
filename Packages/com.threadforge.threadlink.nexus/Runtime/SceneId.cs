namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    /// <summary>
    /// A type-safe reference to a Build Profile scene, by its asset GUID. Generated as <c>GameScenes.Name</c>; moving or
    /// renaming the scene keeps its ID, and the generated table supplies the current path.
    /// </summary>
    public readonly struct SceneId : IEquatable<SceneId>
    {
        /// <summary>The scene asset's GUID.</summary>
        public readonly Guid Value;

        /// <summary>The scene path when the ID was created, used when the generated table does not know the scene.</summary>
        public readonly string Path;

        /// <summary>Creates an ID. Generated code calls this.</summary>
        public SceneId(Guid value, string path)
        {
            Value = value;
            Path = path;
        }

        /// <summary>The scene's name, from its path.</summary>
        public string Name => string.IsNullOrEmpty(Path) ? Value.ToString("D") : System.IO.Path.GetFileNameWithoutExtension(Path);

        /// <inheritdoc />
        public bool Equals(SceneId other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is SceneId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>A scene field for designer data: pick a scene asset in the Inspector; code reads it as a <see cref="SceneId"/>.</summary>
    [Serializable]
    public struct SceneReference
    {
        [SerializeField]
        [Tooltip("Scene asset GUID. Kept when the scene moves or is renamed.")]
        private string guid;

        [SerializeField]
        [Tooltip("Scene path when last assigned, for loading and messages.")]
        private string path;

        /// <summary>Whether a scene is assigned.</summary>
        public bool IsAssigned => Guid.TryParse(guid, out var value) && value != Guid.Empty;

        /// <summary>The referenced scene.</summary>
        public SceneId Id =>
            IsAssigned ? new SceneId(new Guid(guid), path) : throw new InvalidOperationException("No scene is assigned.");

        /// <summary>The referenced scene.</summary>
        public static implicit operator SceneId(SceneReference reference) => reference.Id;

        /// <inheritdoc />
        public override string ToString() => IsAssigned ? Id.Name : "None";
    }
}
