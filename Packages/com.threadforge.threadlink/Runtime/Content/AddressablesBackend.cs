namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using UnityEngine;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using NativeAddressables = UnityEngine.AddressableAssets.Addressables;

    /// <summary>One native Addressables acquisition per admitted request; no extra reference accounting.</summary>
    public sealed class AddressablesBackend : IContentBackend
    {
        private sealed class Operation<T> : IResourceOperation<T>, IAsyncResourceCleanup
        {
            private readonly AsyncOperationHandle<T> handle;
            private readonly Func<AsyncOperationHandle<T>, UniTask> release;
            public UniTask Cleanup { get; private set; } = UniTask.CompletedTask;
            private readonly UniTaskCompletionSource<T> completion = new UniTaskCompletionSource<T>();
            private bool released;

            public Operation(
                AsyncOperationHandle<T> handle,
                Func<AsyncOperationHandle<T>, UniTask> release
            )
            {
                this.handle = handle;
                this.release = release;
                handle.Completed += result =>
                {
                    if (result.Status == AsyncOperationStatus.Succeeded)
                        completion.TrySetResult(result.Result);
                    else
                        completion.TrySetException(
                            result.OperationException
                                ?? new InvalidOperationException("Addressables acquisition failed.")
                        );
                };
            }

            public UniTask<T> Completion => completion.Task;
            public object Handle => handle;

            public void Release()
            {
                if (released)
                    return;
                released = true;
                Cleanup = release(handle);
            }
        }

        /// <summary>Acquires an asset, prefab instance (GameObject) or additive scene (SceneInstance).
        /// Scene activation is enabled; transition policy belongs to Nexus.</summary>
        public IResourceOperation<T> Acquire<T>(string location, AcquisitionKind kind)
        {
            if (kind == AcquisitionKind.Asset)
                return new Operation<T>(
                    NativeAddressables.LoadAssetAsync<T>(location),
                    handle =>
                    {
                        NativeAddressables.Release(handle);
                        return UniTask.CompletedTask;
                    }
                );
            if (kind == AcquisitionKind.Instance && typeof(T) == typeof(GameObject))
                return (IResourceOperation<T>)
                    (object)
                        new Operation<GameObject>(
                            NativeAddressables.InstantiateAsync(location),
                            handle =>
                            {
                                // Releasing a tracked instance handle invokes Addressables' instance-provider cleanup.
                                NativeAddressables.Release(handle);
                                return UniTask.CompletedTask;
                            }
                        );
            if (kind == AcquisitionKind.Scene && typeof(T) == typeof(SceneInstance))
                return (IResourceOperation<T>)
                    (object)
                        new Operation<SceneInstance>(
                            NativeAddressables.LoadSceneAsync(
                                location,
                                LoadSceneMode.Additive,
                                true
                            ),
                            handle =>
                            {
                                var cleanup = new UniTaskCompletionSource<bool>();
                                if (!handle.IsValid())
                                    return UniTask.CompletedTask;
                                // Unload a successfully loaded scene even when the consumer cancels before native completion.
                                void Unload(AsyncOperationHandle<SceneInstance> completed)
                                {
                                    try
                                    {
                                        if (
                                            completed.Status == AsyncOperationStatus.Succeeded
                                            && completed.Result.Scene.isLoaded
                                        )
                                        {
                                            var unloading = NativeAddressables.UnloadSceneAsync(
                                                completed,
                                                false
                                            );
                                            unloading.Completed += result =>
                                            {
                                                try
                                                {
                                                    var error =
                                                        result.Status
                                                        == AsyncOperationStatus.Succeeded
                                                            ? null
                                                            : result.OperationException
                                                                ?? new InvalidOperationException(
                                                                    "Scene unload failed."
                                                                );
                                                    NativeAddressables.Release(result);
                                                    if (error == null)
                                                        cleanup.TrySetResult(true);
                                                    else
                                                        cleanup.TrySetException(error);
                                                }
                                                catch (Exception error)
                                                {
                                                    cleanup.TrySetException(error);
                                                }
                                            };
                                        }
                                        else
                                        {
                                            NativeAddressables.Release(completed);
                                            cleanup.TrySetResult(true);
                                        }
                                    }
                                    catch (Exception error)
                                    {
                                        cleanup.TrySetException(error);
                                    }
                                }
                                if (handle.IsDone)
                                    Unload(handle);
                                else
                                    handle.Completed += Unload;
                                return cleanup.Task;
                            }
                        );
            throw new NotSupportedException(
                "Addressables acquisition kind/result type is unsupported."
            );
        }

        /// <summary>Loads every asset with an Addressables label as one handle; releasing it releases them all.</summary>
        public IResourceOperation<System.Collections.Generic.IList<T>> AcquireLabel<T>(string label) =>
            new Operation<System.Collections.Generic.IList<T>>(
                NativeAddressables.LoadAssetsAsync<T>(label, null),
                handle =>
                {
                    NativeAddressables.Release(handle);
                    return UniTask.CompletedTask;
                }
            );
    }
}
