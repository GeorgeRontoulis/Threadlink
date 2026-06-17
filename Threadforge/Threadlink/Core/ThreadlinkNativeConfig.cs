namespace Threadlink.Core
{
    using Collections;
    using Cysharp.Threading.Tasks;
    using Shared;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using NativeResources = Shared.ThreadlinkIDs.Addressables.NativeResources;

    [CreateAssetMenu(fileName = "ThreadlinkConfig.Native.asset", menuName = "Threadlink/Native Config")]
    public sealed class ThreadlinkNativeConfig : ScriptableObject
    {
#if UNITY_EDITOR
        /// <summary>
        /// Provides the GUIDs of all native Threadlink resources.
        /// Does NOT clear the buffer before or after the operation!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EditorOnly_GetNativeGUIDs(List<string> buffer)
        {
            foreach (var assetRef in nativeResources.Values)
                buffer.Add(assetRef.AssetGUID);
        }
#endif
        [SerializeField]
        private FieldHashMap<NativeResources, AssetReference> nativeResources = new();

        #region Public API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<T> LoadNativeResourceAsync<T>(NativeResources resourceID)
        where T : Object
        {
            if (nativeResources.TryGetValue(resourceID, out var reference))
                return await Threadlink.LoadAssetAsync<T>(reference);
            else
                return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask LoadNativeResourcesAsync(AddressablesRequest<NativeResources> request,
        Dictionary<NativeResources, Object> loadedAssets)
        {
            await Threadlink.LoadResourcesAsync(request, nativeResources, loadedAssets);
        }
        #endregion
    }
}