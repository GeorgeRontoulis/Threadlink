namespace Threadlink.Addressables
{
    using Core;
    using Core.NativeSubsystems.Scribe;
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;

    /// <summary>
    /// Threadlink's Resource Provider based on the <see cref="UnityEngine.AddressableAssets"/> Pipeline.
    /// Uses an internal cache to determine whether a resource has already been loaded, effectively circumventing
    /// the reference counter implemented in the Pipeline by default. 
    /// <para></para>
    /// Assets only need to be loaded once, then accessed/used as much as desired. This approach maintains a centralized
    /// registry of the loaded resources, instead of having the hidden reference count altered from multiple places in your code. 
    /// This makes it easier to track the memory overhead of your assets at runtime.
    /// Naturally, you are responsible for correctly loading/releasing your assets at the correct times.
    /// <para></para>
    /// To encourage the use of <see cref="Threadlink"/>'s async methods for loading, the asynchronous API of the
    /// provider has been kept <see langword="internal"/> to the framework. If, however, you need to load assets synchronously, you may freely
    /// use <see cref="LoadOrGetCachedAt(object)"/>, which calls <see cref="AsyncOperationHandle.WaitForCompletion"/> internally.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class ThreadlinkResourceProvider<T>
    {
        private static readonly Dictionary<object, AsyncOperationHandle<T>> cache = new();

        /// <summary>
        /// Synchronously load or get the cached resource at the specified <paramref name="runtimeKey"/>.
        /// </summary>
        /// <param name="runtimeKey">The runtime key.</param>
        /// <returns>The loaded resouce.</returns>
        public static T LoadOrGetCachedAt(object runtimeKey)
        {
            if (cache.TryGetValue(runtimeKey, out var handle) && handle.IsValid())
                return handle.Result;

            handle = Addressables.LoadAssetAsync<T>(runtimeKey);
            handle.WaitForCompletion();

            if (handle.Status is not AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                    handle.Release();

                Scribe.Send<T>("Failed to load resource from address: ", runtimeKey.ToString()).ToUnityConsole(DebugType.Error);
                return default;
            }

            cache[runtimeKey] = handle;
            return handle.Result;
        }

        /// <summary>
        /// Asynchronously load or get the cached resource at the specified <paramref name="runtimeKey"/>.
        /// </summary>
        /// <param name="runtimeKey">The runtime key.</param>
        /// <returns>The loaded resouce.</returns>
        internal static async UniTask<T> LoadOrGetCachedAtKeyAsync(object runtimeKey)
        {
            if (cache.TryGetValue(runtimeKey, out var handle) && handle.IsValid())
                return handle.Result;

            handle = Addressables.LoadAssetAsync<T>(runtimeKey);

            await handle.ToUniTask();

            if (handle.Status is not AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                    handle.Release();

                Scribe.Send<T>("Failed to load resource from address: ", runtimeKey.ToString()).ToUnityConsole(DebugType.Error);
                return default;
            }

            cache[runtimeKey] = handle;
            return handle.Result;
        }

        /// <summary>
        /// Unload the resource at the specified <paramref name="runtimeKey"/>.
        /// </summary>
        /// <param name="runtimeKey">The runtime key.</param>
        internal static void ReleaseAt(object runtimeKey)
        {
            if (!cache.TryGetValue(runtimeKey, out var handle))
                return;

            if (handle.IsValid())
                handle.Release();

            cache.Remove(runtimeKey);
        }

        /// <summary>
        /// Check whether a resource has been loaded using the specified <paramref name="runtimeKey"/>.
        /// </summary>
        /// <param name="runtimeKey">The runtime key.</param>
        /// <returns><see langword="true"/> if the resource is loaded. <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has(object runtimeKey) => cache.TryGetValue(runtimeKey, out var handle) && handle.IsValid();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetHandleAt(object runtimeKey, out AsyncOperationHandle<T> result)
        {
            if (cache.TryGetValue(runtimeKey, out result))
                return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Remove(object runtimeKey) => cache.Remove(runtimeKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Track(object runtimeKey, AsyncOperationHandle<T> handle) => cache.TryAdd(runtimeKey, handle);
    }
}