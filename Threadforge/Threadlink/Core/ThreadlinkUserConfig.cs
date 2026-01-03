namespace Threadlink.Core
{
    using Addressables;
    using Collections;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.User.asset", menuName = "Threadlink/User Config")]
    public sealed class ThreadlinkUserConfig : ScriptableObject
    {
        internal Threadlink.UpdateLoop UpdateLoopBehaviour => updateLoop;
        public FieldTable<AssetGroups, AssetReference[]> Assets => addressableAssets;
        public FieldTable<AssetGroups, AssetReferenceGameObject[]> Prefabs => addressablePrefabs;

#if UNITY_EDITOR
        public UnityEditor.DefaultAsset BinariesFolder => binariesFolder;

        [Header("Editor Options:")]
        [Space(10)]

        [SerializeField] private UnityEditor.DefaultAsset binariesFolder = null;
#endif

        [Header("Runtime Options:")]
        [Space(10)]

        [Tooltip("Whether to deploy Threadlink with its native update loop, or let you hook up your own." +
        "Use Iris to get a callback when the core is deployed and set up your update loop there.")]
        [SerializeField] private Threadlink.UpdateLoop updateLoop = Threadlink.UpdateLoop.Native;

        [Header("Runtime Resources:")]
        [Space(10)]

        [SerializeField] private SceneAssetReference[] addressableScenes = new SceneAssetReference[0];

        [Space(10)]

        [SerializeField] private FieldTable<AssetGroups, AssetReference[]> addressableAssets = new();

        [Space(10)]

        [SerializeField] private FieldTable<AssetGroups, AssetReferenceGameObject[]> addressablePrefabs = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetScenes(out ReadOnlySpan<SceneAssetReference> result) => !(result = addressableScenes).IsEmpty;
    }
}