namespace Threadlink.Addressables
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Threading.Tasks;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Threadlink's Resource Provider based on the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
    /// Works exclusively with <see cref="AssetReference"/> to enforce a simple, 
    /// centralized way of authoring and managing content. 
    /// <para></para>
    /// Please set up your
    /// references in your project's <see cref="ThreadlinkUserConfig"/> asset,
    /// then use <see cref="Threadlink"/>'s <see langword="static"/> Resource Loading API to fetch your content.
    /// <para></para>
    /// Please remember that you are responsible for loading/releasing your assets at the correct times.
    /// </summary>
    /// <typeparam name="T">The resource type, in the context of a call site.</typeparam>
    internal static class ThreadlinkResourceProvider<T> where T : Object
    {
        /// <summary>
        /// Synchronously load or get the cached resource at the specified <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference.</param>
        /// <returns>The loaded resouce.</returns>
        internal static T LoadOrGetCachedAt(AssetReference reference)
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
        internal static async UniTask<T> LoadOrGetCachedAtRefAsync(AssetReference reference)
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

        public static async UniTask<SceneInstance> LoadSceneAsync(SceneAssetReference reference, LoadSceneMode mode)
        {
            _ = reference.LoadSceneAsync(mode);

            await reference.OperationHandle.ToUniTask();

            if (reference.OperationHandle.Status is AsyncOperationStatus.Succeeded)
                return reference.OperationHandle.Convert<SceneInstance>().Result;
            else
                reference.ReleaseAsset();

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> UnloadSceneAsync(SceneAssetReference reference)
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