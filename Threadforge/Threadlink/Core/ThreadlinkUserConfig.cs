namespace Threadlink.Core
{
    using Addressables;
    using AYellowpaper.SerializedCollections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.User.asset", menuName = "Threadlink/User Config")]
    public sealed class ThreadlinkUserConfig : ScriptableObject
    {
        public enum CoreDeploymentMethod : byte { Automatic, Manual }

        public CoreDeploymentMethod CoreDeployment => coreDeployment;
        public SceneAssetReference[] Scenes => sceneDatabase;
        public Dictionary<AssetGroups, AssetReference[]> Assets => assetDatabase;
        public Dictionary<AssetGroups, AssetReferenceGameObject[]> Prefabs => prefabDatabase;

        [Tooltip("Automatic: Automatically loads and deploys Threadlink when entering playmode, or in the first scene in a Built Player." +
        " Manual: You will be responsible for deploying the core using Threadlink's Lifecycle API in your custom logic.")]
        [SerializeField] private CoreDeploymentMethod coreDeployment;

        [Space(10)]

        [SerializeField] private SceneAssetReference[] sceneDatabase = new SceneAssetReference[0];

        [Space(10)]

        [SerializeField] private SerializedDictionary<AssetGroups, AssetReference[]> assetDatabase = null;

        [Space(10)]

        [SerializeField] private SerializedDictionary<AssetGroups, AssetReferenceGameObject[]> prefabDatabase = null;
    }
}