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

            if (!reference.OperationHandle.IsValid()) _ = reference.LoadAssetAsync<T>();
            reference.OperationHandle.WaitForCompletion();

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

            // Asset is null while a load is pending. Reuse that operation instead of issuing a second load on the
            // same reference when another consumer (or a replacement session) asks for it before completion.
            var operation = reference.OperationHandle;
            if (!operation.IsValid())
            {
                _ = reference.LoadAssetAsync<T>();
                operation = reference.OperationHandle;
            }
            try { await operation.ToUniTask(); }
            catch { /* The operation's status below reports failure without abandoning other consumers. */ }

            if (!operation.IsValid() || operation.Status is not AsyncOperationStatus.Succeeded)
            {
                if (reference.OperationHandle.Equals(operation)) reference.ReleaseAsset();

                Scribe.Send<T>("Failed to load resource from address: ", reference.RuntimeKey).ToUnityConsole(DebugType.Error);
                return default;
            }

            return operation.Result as T;
        }

        /// <summary>
        /// Asynchronously load or get the already loaded scene at the specified <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <param name="mode">The load mode of the scene.</param>
        /// <returns>The loaded scene instance.</returns>
        public static UniTask<SceneInstance> LoadAsync(this SceneAssetReference reference, LoadSceneMode mode)
        {
            return reference.LoadAsync(new LoadSceneParameters(mode));
        }

        /// <summary>
        /// Asynchronously load or get the already loaded scene at the specified <paramref name="reference"/>.
        /// A scene that is already loaded is returned as is, regardless of <paramref name="parameters"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <param name="parameters">The load mode and local physics mode of the scene.</param>
        /// <returns>The loaded scene instance.</returns>
        public static async UniTask<SceneInstance> LoadAsync(this SceneAssetReference reference, LoadSceneParameters parameters)
        {
            var operation = reference.SceneOperation;

            if (!operation.IsValid())
                operation = reference.LoadSceneAsync(parameters);

            if (await TryCompleteAsync(operation))
                return operation.Result;

            if (reference.SceneOperation.Equals(operation))
                reference.ReleaseAsset();

            Scribe.Send<SceneAssetReference>("Failed to load scene from reference: ", reference.RuntimeKey).ToUnityConsole(DebugType.Error);
            return default;
        }

        /// <summary>
        /// Asynchronously unload the scene loaded through the specified <paramref name="reference"/>.
        /// Completes once the scene has been unloaded.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <returns>The unloaded scene instance.</returns>
        public static async UniTask<SceneInstance> UnloadAsync(this SceneAssetReference reference)
        {
            if (!reference.SceneOperation.IsValid())
                return default;

            var operation = reference.UnloadSceneAsync(false);
            bool unloaded = await TryCompleteAsync(operation);
            var result = unloaded ? operation.Result : default;

            if (operation.IsValid()) Addressables.Release(operation);

            if (!unloaded)
                Scribe.Send<SceneAssetReference>("Failed to unload scene from reference: ", reference.RuntimeKey).ToUnityConsole(DebugType.Error);

            return result;
        }

        private static async UniTask<bool> TryCompleteAsync<T>(AsyncOperationHandle<T> operation)
        {
            if (!operation.IsValid())
                return false;

            try
            {
                await operation.ToUniTask();
            }
            catch
            {
                // Addressables reports the failure itself; the operation status below is authoritative.
            }

            return operation.IsValid() && operation.Status is AsyncOperationStatus.Succeeded;
        }
    }
}
