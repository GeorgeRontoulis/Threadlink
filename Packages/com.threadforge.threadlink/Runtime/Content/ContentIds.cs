namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;

    // Content IDs, one per load strategy. Each is a mapped Addressables entry, generated as ContentAssets.Group.Name,
    // ContentPrefabs.Group.Name and ContentScenes.Group.Name from the Content mapping (Threadlink ▸ Workspace ▸ Content).
    // The value derives from the entry's asset GUID and never changes, so serialized fields and saves stay valid when an
    // asset is renamed, moved or regrouped. Fields of these types show a dropdown of the mapped entries.

    /// <summary>A mapped asset of any <see cref="UnityEngine.Object"/> type: a sprite, clip, cue, material or your own asset.</summary>
    [Serializable]
    public struct ContentAssetId : IEquatable<ContentAssetId>
    {
        [SerializeField]
        private int value;

        /// <summary>An ID by value. Generated code calls this.</summary>
        public ContentAssetId(int value) => this.value = value;

        /// <summary>The stable value; zero is none.</summary>
        public int Value => value;

        /// <summary>Whether the ID names an asset.</summary>
        public bool IsAssigned => value != 0;

        internal Guid Identity => ContentIdentity.Of(ContentKind.Asset, value);

        /// <inheritdoc />
        public bool Equals(ContentAssetId other) => value == other.value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is ContentAssetId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => value;

        /// <inheritdoc />
        public override string ToString() => "Asset " + value;
    }

    /// <summary>A mapped prefab. It loads or instantiates as a GameObject, or as one of its root components.</summary>
    [Serializable]
    public struct ContentPrefabId : IEquatable<ContentPrefabId>
    {
        [SerializeField]
        private int value;

        /// <summary>An ID by value. Generated code calls this.</summary>
        public ContentPrefabId(int value) => this.value = value;

        /// <summary>The stable value; zero is none.</summary>
        public int Value => value;

        /// <summary>Whether the ID names a prefab.</summary>
        public bool IsAssigned => value != 0;

        internal Guid Identity => ContentIdentity.Of(ContentKind.Prefab, value);

        /// <inheritdoc />
        public bool Equals(ContentPrefabId other) => value == other.value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is ContentPrefabId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => value;

        /// <inheritdoc />
        public override string ToString() => "Prefab " + value;
    }

    /// <summary>A mapped Addressables scene. It loads additively as a SceneInstance; Nexus transitions to it.</summary>
    [Serializable]
    public struct ContentSceneId : IEquatable<ContentSceneId>
    {
        [SerializeField]
        private int value;

        /// <summary>An ID by value. Generated code calls this.</summary>
        public ContentSceneId(int value) => this.value = value;

        /// <summary>The stable value; zero is none.</summary>
        public int Value => value;

        /// <summary>Whether the ID names a scene.</summary>
        public bool IsAssigned => value != 0;

        internal Guid Identity => ContentIdentity.Of(ContentKind.Scene, value);

        /// <inheritdoc />
        public bool Equals(ContentSceneId other) => value == other.value;

        /// <inheritdoc />
        public override bool Equals(object other) => other is ContentSceneId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => value;

        /// <inheritdoc />
        public override string ToString() => "Scene " + value;
    }

    /// <summary>The load strategy of a mapped Addressables entry.</summary>
    public enum ContentKind : byte
    {
        /// <summary>Any asset, loaded as its type or a base type.</summary>
        Asset = 1,

        /// <summary>A prefab, loaded or instantiated as a GameObject or one of its root components.</summary>
        Prefab = 2,

        /// <summary>A scene, loaded additively as a SceneInstance.</summary>
        Scene = 3,
    }

    internal static class ContentIdentity
    {
        // Request identities for diagnostics and scene residency: unique per kind and value, computed without hashing.
        internal static Guid Of(ContentKind kind, int value) => new Guid(value, (short)kind, 0x7468, 0x54, 0x4C, 0x43, 0x4F, 0x4E, 0x54, 0x45, 0x4E);
    }
}
