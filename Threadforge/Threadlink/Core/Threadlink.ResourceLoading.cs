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
        public static T LoadAsset<T>(AssetIDs assetID) where T : Object
        {
            return TryGetAssetReference(assetID, out var reference) ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(AssetReference reference) where T : Object
        {
            return reference.RuntimeKeyIsValid() ? reference.LoadSynchronously<T>() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(AssetIDs assetID) where T : Object
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
        public static T LoadPrefab<T>(PrefabIDs prefabID) where T : Component
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
        public static async UniTask<T> LoadPrefabAsync<T>(PrefabIDs prefabID) where T : Component
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
        public static async UniTask<SceneInstance> LoadSceneAsync(SceneIDs sceneID, LoadSceneMode mode)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.LoadAsync(mode) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> UnloadSceneAsync(SceneIDs sceneID)
        {
            return TryGetSceneReference(sceneID, out var reference) ? await reference.UnloadAsync() : default;
        }
        #endregion

        #region Release:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseAsset(AssetIDs assetID)
        {
            if (TryGetAssetReference(assetID, out var reference))
                reference.ReleaseAsset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleasePrefab(PrefabIDs prefabID)
        {
            if (TryGetPrefabReference(prefabID, out var reference))
                reference.ReleaseAsset();
        }
        #endregion
    }
}
