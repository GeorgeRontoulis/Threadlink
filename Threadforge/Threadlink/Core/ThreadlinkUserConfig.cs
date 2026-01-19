namespace Threadlink.Core
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.User.asset", menuName = "Threadlink/User Config")]
    public sealed class ThreadlinkUserConfig : ScriptableObject
    {
        internal Threadlink.UpdateLoop UpdateLoopBehaviour => updateLoop;

#if UNITY_EDITOR
        public UnityEditor.DefaultAsset BinariesFolder => binariesFolder;

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

#if UNITY_EDITOR && ODIN_INSPECTOR
        [Sirenix.OdinInspector.DrawWithUnity]
#endif
        [SerializeField] private SceneAssetReference[] sceneReferences = new SceneAssetReference[0];

        [Space(10)]

        [SerializeField] private AssetReference[] assetReferneces = new AssetReference[0];

        [Space(10)]

        [SerializeField] private AssetReferenceGameObject[] prefabReferences = new AssetReferenceGameObject[0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneRefs(out ReadOnlySpan<SceneAssetReference> result)
        {
            result = sceneReferences;
            return sceneReferences != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAssetRefs(out ReadOnlySpan<AssetReference> result)
        {
            result = assetReferneces;
            return assetReferneces != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPrefabRefs(out ReadOnlySpan<AssetReferenceGameObject> result)
        {
            result = prefabReferences;
            return prefabReferences != null;
        }
    }
}