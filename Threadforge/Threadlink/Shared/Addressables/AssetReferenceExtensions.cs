namespace Threadlink.Shared
{
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    public static class AssetReferenceExtensions
    {
        /// <summary>
        /// Synchronously load or get the cached resource at the specified <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <returns>The loaded resouce.</returns>
        public static T LoadSynchronously<T>(this AssetReference reference) where T : Object
        {
            if (reference.Asset is T loadedAsset)
                return loadedAsset;

            reference.LoadAssetAsync<T>().WaitForCompletion();

            if (reference.OperationHandle.Status is not AsyncOperationStatus.Succeeded)
            {
                reference.ReleaseAsset();

                Scribe.Send<T>("Failed to load resource from reference: ", reference.RuntimeKey).ToUnityConsole(DebugType.Error);
                return default;
            }

            return (T)reference.Asset;
        }

        /// <summary>
        /// Asynchronously load or get the cached resource at the specified <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <returns>The loaded resouce.</returns>
        public static async UniTask<T> LoadAsync<T>(this AssetReference reference) where T : Object
        {
            if (reference.Asset is T loadedAsset)
                return loadedAsset;

            _ = reference.LoadAssetAsync<T>();

            await reference.OperationHandle.ToUniTask();

            if (reference.OperationHandle.Status is not AsyncOperationStatus.Succeeded)
            {
                reference.ReleaseAsset();

                Scribe.Send<T>("Failed to load resource from address: ", reference.RuntimeKey).ToUnityConsole(DebugType.Error);
                return default;
            }

            return (T)reference.Asset;
        }

        public static async UniTask<SceneInstance> LoadAsync(this SceneAssetReference reference, LoadSceneMode mode)
        {
            _ = reference.LoadSceneAsync(mode);

            await reference.OperationHandle.ToUniTask();

            if (reference.OperationHandle.Status is AsyncOperationStatus.Succeeded)
                return reference.OperationHandle.Convert<SceneInstance>().Result;
            else
                reference.ReleaseAsset();

            return default;
        }

        public static async UniTask<SceneInstance> UnloadAsync(this SceneAssetReference reference)
        {
            _ = reference.UnLoadScene();

            await reference.OperationHandle.ToUniTask();

            if (reference.OperationHandle.Status is AsyncOperationStatus.Succeeded)
                return reference.OperationHandle.Convert<SceneInstance>().Result;
            else
                reference.ReleaseAsset();

            return default;
        }
    }
}
