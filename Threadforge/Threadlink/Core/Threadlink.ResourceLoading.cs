namespace Threadlink.Core
{
    using Cysharp.Threading.Tasks;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using Utilities.Objects;

    public sealed partial class Threadlink
    {
        #region Asset:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(ThreadlinkIDs.Addressables.Assets assetID) where T : Object
        {
            return TryGetAssetReference(assetID, out var reference) ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(AssetReference reference) where T : Object
        {
            return reference.RuntimeKeyIsValid() ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(ThreadlinkIDs.Addressables.Assets assetID) where T : Object
        {
            return TryGetAssetReference(assetID, out var reference) ? await reference.LoadAsync<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : Object
        {
            return reference.RuntimeKeyIsValid() ? await reference.LoadAsync<T>() : null;
        }
        #endregion

        #region Prefab:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs prefabID) where T : Component
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
        public static async UniTask<T> LoadPrefabAsync<T>(ThreadlinkIDs.Addressables.Prefabs prefabID) where T : Component
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
        public static async UniTask<SceneInstance> LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes sceneID, LoadSceneMode mode)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.LoadAsync(mode) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> UnloadSceneAsync(ThreadlinkIDs.Addressables.Scenes sceneID)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.UnloadAsync() : default;
        }
        #endregion

        #region Release:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseAsset(ThreadlinkIDs.Addressables.Assets assetID)
        {
            if (TryGetAssetReference(assetID, out var reference))
                reference.ReleaseAsset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleasePrefab(ThreadlinkIDs.Addressables.Prefabs prefabID)
        {
            if (TryGetPrefabReference(prefabID, out var reference))
                reference.ReleaseAsset();
        }
        #endregion
    }
}
