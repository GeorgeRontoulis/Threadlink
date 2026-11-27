namespace Threadforge.Threadlink
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    /// <summary>
    /// The Content mapping: which Addressables entries game code loads by ID, in three read-only dictionaries, one per
    /// load strategy. Threadlink writes them when you apply the mapping (Threadlink ▸ Workspace ▸ Content); Addressables
    /// stays the source of truth for groups, addresses, labels and delivery.
    /// </summary>
    public sealed class ContentSettings : ThreadlinkSettings<ContentSettings>
    {
        [Tooltip("Mapped assets and the reference each loads through. Generated; edit the mapping on the Workspace's Content page.")]
        [DictionaryDisplay(keyLabel = "Asset", valueLabel = "Addressable reference", keyColumnFraction = 0.4f)]
        [SerializeField, GeneratedSetting("Threadlink/Content Mapping")]
        private Dictionary<ContentAssetId, AssetReference> assets = new Dictionary<ContentAssetId, AssetReference>();

        [Tooltip("Mapped prefabs and the reference each loads through. Generated; edit the mapping on the Workspace's Content page.")]
        [DictionaryDisplay(keyLabel = "Prefab", valueLabel = "Addressable reference", keyColumnFraction = 0.4f)]
        [SerializeField, GeneratedSetting("Threadlink/Content Mapping")]
        private Dictionary<ContentPrefabId, AssetReferenceGameObject> prefabs = new Dictionary<ContentPrefabId, AssetReferenceGameObject>();

        [Tooltip("Mapped scenes and the reference each loads through. Generated; edit the mapping on the Workspace's Content page.")]
        [DictionaryDisplay(keyLabel = "Scene", valueLabel = "Addressable reference", keyColumnFraction = 0.4f)]
        [SerializeField, GeneratedSetting("Threadlink/Content Mapping")]
        private Dictionary<ContentSceneId, AssetReference> scenes = new Dictionary<ContentSceneId, AssetReference>();

        /// <summary>Mapped assets.</summary>
        public IReadOnlyDictionary<ContentAssetId, AssetReference> Assets => assets;

        /// <summary>Mapped prefabs.</summary>
        public IReadOnlyDictionary<ContentPrefabId, AssetReferenceGameObject> Prefabs => prefabs;

        /// <summary>Mapped scenes.</summary>
        public IReadOnlyDictionary<ContentSceneId, AssetReference> Scenes => scenes;

        /// <inheritdoc />
        public override string PageName => "Content";

#if UNITY_EDITOR
        /// <summary>Replaces the mapping. Called by the Content mapping when it is applied.</summary>
        internal void ReplaceMapping(
            Dictionary<ContentAssetId, AssetReference> nextAssets,
            Dictionary<ContentPrefabId, AssetReferenceGameObject> nextPrefabs,
            Dictionary<ContentSceneId, AssetReference> nextScenes
        )
        {
            assets = nextAssets;
            prefabs = nextPrefabs;
            scenes = nextScenes;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>Marks a settings field that Threadlink's code generation writes; Project Settings shows it read-only.</summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class GeneratedSettingAttribute : System.Attribute
    {
        /// <summary>Creates the marker; <paramref name="editedWith"/> is the menu item of the tool that writes the field.</summary>
        public GeneratedSettingAttribute(string editedWith = null) => EditedWith = editedWith;

        /// <summary>The menu item of the tool that writes the field, or null.</summary>
        public string EditedWith { get; }
    }
}
