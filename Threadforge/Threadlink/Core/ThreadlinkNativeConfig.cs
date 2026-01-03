namespace Threadlink.Core
{
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Aura;
    using NativeSubsystems.Dextra;
    using NativeSubsystems.Sentinel;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.Audio;
    using UnityEngine.EventSystems;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.Native.asset", menuName = "Threadlink/Native Config")]
    public sealed class ThreadlinkNativeConfig : ScriptableObject
    {
#if UNITY_EDITOR
        public string[] NativeAssetGUIDs
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new string[7]
                {
                    userConfig.AssetGUID,
                    sentinelConfig.AssetGUID,
                    dextraConfig.AssetGUID,
                    auraConfig.AssetGUID,
                    dextraComponentsPrefab.AssetGUID,
                    auraComponentsPrefab.AssetGUID,
                    auraMixer.AssetGUID
                };
            }
        }
#endif

        [Header("Runtime Resources:")]
        [Space(10)]

        [SerializeField] private AssetReferenceT<ThreadlinkUserConfig> userConfig = null;
        [SerializeField] private AssetReferenceT<SentinelConfig> sentinelConfig = null;
        [SerializeField] private AssetReferenceT<DextraConfig> dextraConfig = null;
        [SerializeField] private AssetReferenceT<AuraConfig> auraConfig = null;

        [Space(10)]

        [SerializeField] private AssetReferenceGameObject dextraComponentsPrefab = null;
        [SerializeField] private AssetReferenceGameObject auraComponentsPrefab = null;

        [Space(10)]

        [SerializeField] private AssetReferenceT<AudioMixer> auraMixer = null;

        #region Internal API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<ThreadlinkUserConfig> LoadUserConfigAsync() => await Threadlink.LoadAssetAsync<ThreadlinkUserConfig>(userConfig);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<SentinelConfig> LoadSentinelConfigAsync() => await Threadlink.LoadAssetAsync<SentinelConfig>(sentinelConfig);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<(EventSystem, DextraConfig)> LoadDextraResourcesAsync()
        {
            var prefabTask = Threadlink.LoadPrefabAsync<EventSystem>(dextraComponentsPrefab).Preserve();
            var configTask = Threadlink.LoadAssetAsync<DextraConfig>(dextraConfig).Preserve();

            await UniTask.WhenAll(prefabTask, configTask);

            return new(prefabTask.AsValueTask().Result, configTask.AsValueTask().Result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<(Transform, AuraConfig, AudioMixer)> LoadAuraResourcesAsync()
        {
            var mixerTask = Threadlink.LoadAssetAsync<AudioMixer>(auraMixer).Preserve();
            var configTask = Threadlink.LoadAssetAsync<AuraConfig>(auraConfig).Preserve();

            await UniTask.WhenAll(mixerTask, configTask);

            var prefab = await Threadlink.LoadPrefabAsync<Transform>(auraComponentsPrefab);

            return new(prefab, configTask.AsValueTask().Result, mixerTask.AsValueTask().Result);
        }
        #endregion
    }
}