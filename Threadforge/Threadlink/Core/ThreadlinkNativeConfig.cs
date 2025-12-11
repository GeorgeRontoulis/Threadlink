namespace Threadlink.Core
{
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Sentinel;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.EventSystems;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.Native.asset", menuName = "Threadlink/Native Config")]
    internal sealed class ThreadlinkNativeConfig : ScriptableObject
    {
        [Header("Runtime Resources:")]
        [Space(10)]

        [SerializeField] private AssetReferenceT<ThreadlinkUserConfig> userConfig = null;
        [SerializeField] private AssetReferenceT<SentinelConfig> sentinelConfig = null;
        [SerializeField] private AssetReferenceGameObject dextraDependenciesPrefab = null;

        #region Internal API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<ThreadlinkUserConfig> LoadUserConfigAsync() => await Threadlink.LoadAssetAsync<ThreadlinkUserConfig>(userConfig);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<SentinelConfig> LoadSentinelConfigAsync() => await Threadlink.LoadAssetAsync<SentinelConfig>(sentinelConfig);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async UniTask<EventSystem> LoadDextraDependenciesAsync() => await Threadlink.LoadPrefabAsync<EventSystem>(dextraDependenciesPrefab);
        #endregion
    }
}