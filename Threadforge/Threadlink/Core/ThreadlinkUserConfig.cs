namespace Threadlink.Core
{
#if UNITY_EDITOR
    using Authoring;
    using AssetAuthoringTable = Authoring.ThreadlinkSerializableAuthoringTable<Addressables.AssetGroups, UnityEngine.AddressableAssets.AssetReference[]>;
    using PrefabAuthoringTable = Authoring.ThreadlinkSerializableAuthoringTable<Addressables.AssetGroups, UnityEngine.AddressableAssets.AssetReferenceGameObject[]>;
#endif
    using ThreadlinkDatabase = System.Collections.Generic.Dictionary<Addressables.AssetGroups, string[]>;

    using Addressables;
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using Utilities.Strings;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.User.asset", menuName = "Threadlink/User Config")]
    public sealed class ThreadlinkUserConfig : ScriptableObject, IAsyncBinaryConsumer, IBinaryAuthor
    {
        #region Runtime:
        public ThreadlinkDatabase Assets { get; private set; }
        public ThreadlinkDatabase Prefabs { get; private set; }

        [Header("Runtime Properties:")]
        [Space(10)]

        [SerializeField] private SceneAssetReference[] sceneDatabase = new SceneAssetReference[0];

        [Space(10)]

        [Tooltip("Reference to the serialized file which will be deserialized at runtime to populate Threadlink's internal asset registry.")]
        [SerializeField] private AssetReferenceT<TextAsset> assetDatabaseBinary = null;

        [Tooltip("Reference to the serialized file which will be deserialized at runtime to populate Threadlink's internal prefab registry.")]
        [SerializeField] private AssetReferenceT<TextAsset> prefabDatabaseBinary = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetScenes(out ReadOnlySpan<SceneAssetReference> result) => !(result = sceneDatabase).IsEmpty;

        public async UniTask ConsumeBinariesAsync()
        {
            var assetTask = assetDatabaseBinary.DeserializeIntoDictionaryAsync<AssetGroups, string[]>().Preserve();
            var prefabTask = prefabDatabaseBinary.DeserializeIntoDictionaryAsync<AssetGroups, string[]>().Preserve();

            await UniTask.WhenAll(assetTask, prefabTask);

            Assets = assetTask.AsValueTask().Result;
            Prefabs = prefabTask.AsValueTask().Result;
        }
        #endregion

#if UNITY_EDITOR
        #region Edit-time:
        public AssetAuthoringTable AssetAuthoringTable => assetAuthoringTable;
        public PrefabAuthoringTable PrefabAuthoringTable => prefabAuthoringTable;

        [Header("Edit-time Properties:")]
        [Space(10)]

        [SerializeField] private AssetAuthoringTable assetAuthoringTable = new();
        [SerializeField] private PrefabAuthoringTable prefabAuthoringTable = new();

        [ContextMenu("Serialize Authoring Data Into Binary")]
        public void SerializeAuthoringDataIntoBinary()
        {
            ThreadlinkDatabase ConvertToSerializableRuntimeData<T>(ThreadlinkAuthoringTable<AssetGroups, T[]> authoringData)
            where T : AssetReference
            {
                var result = new ThreadlinkDatabase(authoringData.Count);

                foreach (var entry in authoringData)
                {
                    var key = entry.Key;
                    var refs = entry.Value;
                    int length = refs.Length;
                    var guids = new string[length];

                    for (int i = 0; i < length; i++)
                        guids[i] = refs[i].RuntimeKey.ToString();

                    if (!result.TryAdd(key, guids))
                        this.Send($"Duplicate key detected: {key}").ToUnityConsole(DebugType.Warning);
                }

                return result;
            }

            assetAuthoringTable.SerializeIntoBinary(ConvertToSerializableRuntimeData(assetAuthoringTable), "ThreadlinkAssetDatabase");
            prefabAuthoringTable.SerializeIntoBinary(ConvertToSerializableRuntimeData(prefabAuthoringTable), "ThreadlinkPrefabDatabase");
        }
        #endregion
#endif
    }
}