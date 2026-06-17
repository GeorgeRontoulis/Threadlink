namespace Threadlink.Core
{
    using Collections;
    using Cysharp.Threading.Tasks;
    using Shared;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using Utilities.Objects;
    using UnityObject = UnityEngine.Object;

    public sealed partial class Threadlink
    {
        #region Asset:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T LoadAsset<T>(ThreadlinkIDs.Addressables.Assets assetID) where T : UnityObject
        {
            return TryGetAssetReference(assetID, out var reference) ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(AssetReference reference) where T : UnityObject
        {
            return reference.RuntimeKeyIsValid() ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<T> LoadAssetAsync<T>(ThreadlinkIDs.Addressables.Assets assetID) where T : UnityObject
        {
            return TryGetAssetReference(assetID, out var reference) ? await reference.LoadAsync<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : UnityObject
        {
            return reference.RuntimeKeyIsValid() ? await reference.LoadAsync<T>() : null;
        }
        #endregion

        #region Prefab:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs prefabID) where T : Component
        {
            if (!TryGetPrefabReference(prefabID, out var reference))
                return null;

            var prefab = reference.LoadSynchronously<GameObject>();

            return prefab != null && prefab.As<T>(out var component) ? component : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadPrefab<T>(AssetReferenceGameObject reference) where T : Component
        {
            if (!reference.RuntimeKeyIsValid())
                return null;

            var prefab = reference.LoadSynchronously<GameObject>();

            return prefab != null && prefab.As<T>(out var component) ? component : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<T> LoadPrefabAsync<T>(ThreadlinkIDs.Addressables.Prefabs prefabID) where T : Component
        {
            if (!TryGetPrefabReference(prefabID, out var reference))
                return null;

            var prefab = await reference.LoadAsync<GameObject>();

            return prefab != null && prefab.As<T>(out var component) ? component : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadPrefabAsync<T>(AssetReferenceGameObject reference) where T : Component
        {
            if (!reference.RuntimeKeyIsValid())
                return null;

            var prefab = await reference.LoadAsync<GameObject>();

            return prefab != null && prefab.As<T>(out var component) ? component : null;
        }
        #endregion

        #region Scene:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<SceneInstance> LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes sceneID, LoadSceneMode mode)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.LoadAsync(mode) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<SceneInstance> UnloadSceneAsync(ThreadlinkIDs.Addressables.Scenes sceneID)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.UnloadAsync() : default;
        }
        #endregion

        #region Bulk Loading:
        internal static async UniTask LoadResourcesAsync<T>
        (
            AddressablesRequest<T> request,
            ThreadlinkHashMap<T, AssetReference> references,
            Dictionary<T, UnityObject> loadedResources
        )
        where T : unmanaged, Enum
        {
            int length = request.Length;
            var taskBuffer = ArrayPool<UniTask>.Shared.Rent(length);

            try
            {
                for (int i = 0; i < length; i++)
                {
                    if (references.TryGetValue(request[i], out var requestedRef))
                    {
                        _ = requestedRef.LoadAssetAsync<UnityObject>();
                        taskBuffer[i] = requestedRef.OperationHandle.ToUniTask();
                    }
                }

                await UniTask.WhenAll(taskBuffer);

                for (int i = 0; i < length; i++)
                {
                    var resourceID = request[i];

                    if (references.TryGetValue(resourceID, out var requestedRef) && requestedRef.OperationHandle.IsValid())
                        loadedResources[resourceID] = requestedRef.OperationHandle.Result as UnityObject;
                }
            }
            finally { ArrayPool<UniTask>.Shared.Return(taskBuffer, clearArray: true); }
        }
        #endregion

        #region Release:
        internal static void ReleaseResources<T>(ref AddressablesRequest<T> request, ThreadlinkHashMap<T, AssetReference> references)
        where T : unmanaged, Enum
        {
            int length = request.Length;

            for (int i = 0; i < length; i++)
            {
                if (references.TryGetValue(request[i], out var reference) && reference.OperationHandle.IsValid())
                    reference.ReleaseAsset();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseAsset(ThreadlinkIDs.Addressables.Assets assetID)
        {
            if (TryGetAssetReference(assetID, out var reference))
                reference.ReleaseAsset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleasePrefab(ThreadlinkIDs.Addressables.Prefabs prefabID)
        {
            if (TryGetPrefabReference(prefabID, out var reference))
                reference.ReleaseAsset();
        }
        #endregion
    }
}
