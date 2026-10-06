namespace Threadlink.Core
{
    using Collections;
    using Generated;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using Utilities.Attributes;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.User.asset", menuName = "Threadlink/User Config")]
    public sealed class ThreadlinkUserConfig : ScriptableObject
    {
        internal Threadlink.UpdateLoop UpdateLoopBehaviour => updateLoop;

#if UNITY_EDITOR
        [Header("Editor Options:")]
        [Space(10)]

        [SerializeField] private UnityEditor.DefaultAsset binariesFolder = null;
#endif

        [Header("Runtime Options and Resources:")]
        [Space(10)]

        [Tooltip("Whether to deploy Threadlink with its native update loop, or let you hook up your own." +
        "Use Iris to get a callback when the core is deployed and set up your update loop there.")]
        [SerializeField] private Threadlink.UpdateLoop updateLoop = Threadlink.UpdateLoop.Native;

        [Space(10)]

        [ReadOnly]
#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [Tooltip("Populated by the Threadlink Addressables Mapping Window. Not editable here.")]
        [SerializeField] private FieldHashMap<ThreadlinkIDs.Addressables.Scenes, SceneAssetReference> sceneReferences = new();

        [Space(10)]

        [ReadOnly]
#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [Tooltip("Populated by the Threadlink Addressables Mapping Window. Not editable here.")]
        [SerializeField] private FieldHashMap<ThreadlinkIDs.Addressables.Assets, AssetReference> assetReferences = new();

        [Space(10)]

        [ReadOnly]
#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [Tooltip("Each mapped asset's main type (\"Namespace.Type, Assembly\"), recorded by the Threadlink Addressables Mapping Window, so assets can be found by type without loading them. Not editable here.")]
        [SerializeField] private FieldHashMap<ThreadlinkIDs.Addressables.Assets, string> assetTypes = new();

        [Space(10)]

        [ReadOnly]
#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [Tooltip("Old ID of a renamed or moved asset -> its current ID. Written by the Mapping Window.")]
        [SerializeField] private FieldHashMap<ThreadlinkIDs.Addressables.Assets, ThreadlinkIDs.Addressables.Assets> assetAliases = new();

        [Space(10)]

        [ReadOnly]
#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [Tooltip("Populated by the Threadlink Addressables Mapping Window. Not editable here.")]
        [SerializeField] private FieldHashMap<ThreadlinkIDs.Addressables.Prefabs, AssetReferenceGameObject> prefabReferences = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneReference(ThreadlinkIDs.Addressables.Scenes sceneID, out SceneAssetReference result)
        {
            return sceneReferences.TryGetValue(sceneID, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetReference(ThreadlinkIDs.Addressables.Assets assetID, out AssetReference result)
        {
            return assetReferences.TryGetValue(assetID, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPrefabReference(ThreadlinkIDs.Addressables.Prefabs prefabID, out AssetReferenceGameObject result)
        {
            return prefabReferences.TryGetValue(prefabID, out result);
        }

        public int SceneReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sceneReferences.Count;
        }

        public int AssetReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => assetReferences.Count;
        }

        public int PrefabReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => prefabReferences.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneReferences(out ReadOnlySpan<SceneAssetReference> result)
        {
            if (sceneReferences.Count <= 0)
            {
                result = default;
                return false;
            }

            result = sceneReferences.Values;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetReferences(out ReadOnlySpan<AssetReference> result)
        {
            if (assetReferences.Count <= 0)
            {
                result = default;
                return false;
            }

            result = assetReferences.Values;
            return true;
        }

        /// <summary>Every mapped asset's ID, in the same order as <see cref="TryGetAssetReferences"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetIDs(out ReadOnlySpan<ThreadlinkIDs.Addressables.Assets> result)
        {
            if (assetReferences.Count <= 0)
            {
                result = default;
                return false;
            }

            result = assetReferences.Keys;
            return true;
        }

        /// <summary>The main type the Mapping Window recorded for <paramref name="assetID"/>: "Namespace.Type, Assembly".</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetType(ThreadlinkIDs.Addressables.Assets assetID, out string result) => assetTypes.TryGetValue(assetID, out result);

        /// <summary>The current ID of an asset that was renamed or moved since <paramref name="oldID"/> was its ID.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetAlias(ThreadlinkIDs.Addressables.Assets oldID, out ThreadlinkIDs.Addressables.Assets result) => assetAliases.TryGetValue(oldID, out result);

        /// <summary>Every old ID that is an alias of a current one.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetAliasIDs(out ReadOnlySpan<ThreadlinkIDs.Addressables.Assets> result)
        {
            if (assetAliases.Count <= 0)
            {
                result = default;
                return false;
            }

            result = assetAliases.Keys;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPrefabReferences(out ReadOnlySpan<AssetReferenceGameObject> result)
        {
            if (prefabReferences.Count <= 0)
            {
                result = default;
                return false;
            }

            result = prefabReferences.Values;
            return true;
        }

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_ClearSceneReferences() => sceneReferences.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_ClearAssetReferences()
        {
            assetReferences.Clear();
            assetTypes.Clear();
            assetAliases.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_ClearPrefabReferences() => prefabReferences.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EditorOnly_TryAddSceneReference(ThreadlinkIDs.Addressables.Scenes sceneID, SceneAssetReference reference)
        {
            return sceneReferences.EditorOnly_TryAdd(sceneID, reference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EditorOnly_TryAddAssetReference(ThreadlinkIDs.Addressables.Assets assetID, AssetReference reference)
        {
            return assetReferences.EditorOnly_TryAdd(assetID, reference);
        }

        /// <summary>Record the main type of a mapped asset, as "Namespace.Type, Assembly".</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EditorOnly_TryAddAssetType(ThreadlinkIDs.Addressables.Assets assetID, Type type)
        {
            return type != null && assetTypes.EditorOnly_TryAdd(assetID, type.FullName + ", " + type.Assembly.GetName().Name);
        }

        /// <summary>Record that <paramref name="oldID"/>, a renamed or moved asset's tombstoned ID, now means <paramref name="currentID"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EditorOnly_TryAddAssetAlias(ThreadlinkIDs.Addressables.Assets oldID, ThreadlinkIDs.Addressables.Assets currentID)
        {
            return assetAliases.EditorOnly_TryAdd(oldID, currentID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EditorOnly_TryAddPrefabReference(ThreadlinkIDs.Addressables.Prefabs prefabID, AssetReferenceGameObject reference)
        {
            return prefabReferences.EditorOnly_TryAdd(prefabID, reference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetBinariesFolder(out UnityEditor.DefaultAsset result) => (result = binariesFolder) != null;
#endif
    }
}