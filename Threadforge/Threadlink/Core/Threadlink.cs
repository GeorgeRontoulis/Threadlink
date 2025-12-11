namespace Threadlink.Core
{
    using Addressables;
    using Cysharp.Threading.Tasks;
    using NativeSubsystems.Iris;
    using NativeSubsystems.Scribe;
    using Shared;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using Utilities.Collections;

    /// <summary>
    /// The Core. Controls all aspects of the runtime, managing subsystems at the lowest level.
    /// </summary>
    public sealed partial class Threadlink : Weaver<Threadlink, IThreadlinkSubsystem>
    {
        internal ThreadlinkNativeConfig NativeConfig { get; set; }
        public ThreadlinkUserConfig UserConfig { get; internal set; }

        #region Main Lifecycle API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ShutDown() => Instance.Discard();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
        #endregion

        #region Unity Update Messages:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Update() => Iris.Publish(Iris.Events.OnUpdate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixedUpdate() => Iris.Publish(Iris.Events.OnFixedUpdate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LateUpdate() => Iris.Publish(Iris.Events.OnLateUpdate);
        #endregion

        #region Public API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask WaitForFramesAsync(int frameCount)
        {
            for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Weave<T>() where T : IThreadlinkSubsystem => Instance.TryWeave(out T wovenSubsystem) ? wovenSubsystem : default;
        #endregion

        #region Asset Reference/Loading/Unloading API:
        public static async UniTask<T> LoadAssetAsync<T>(AssetGroups group, int indexInDB) where T : Object
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var reference))
            {
                return await LoadAssetAsync<T>(reference);
            }

            return null;
        }

        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : Object
        {
            _ = reference.LoadAssetAsync<T>();

            var handle = reference.OperationHandle;
            await handle.ToUniTask();

            if (handle.Status is AsyncOperationStatus.Succeeded)
                return handle.Convert<T>().Result;
            else if (handle.IsValid())
                handle.Release();

            return null;
        }

        public static async UniTask<T> LoadPrefabAsync<T>(AssetGroups group, int indexInDB) where T : Component
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var reference))
            {
                return await LoadPrefabAsync<T>(reference);
            }

            return null;
        }

        public static async UniTask<T> LoadPrefabAsync<T>(AssetReferenceGameObject reference) where T : Component
        {
            _ = reference.LoadAssetAsync();

            var handle = reference.OperationHandle;
            await handle.ToUniTask();

            if (handle.Status is AsyncOperationStatus.Succeeded
            && handle.Convert<GameObject>().Result.TryGetComponent(out T result))
            {
                return result;
            }
            else
            {
                if (handle.IsValid())
                    handle.Release();

                Instance.Send("Could not find the requested component of type ",
                typeof(T).Name, " on the loaded prefab!").ToUnityConsole(DebugType.Error);
                return null;
            }
        }

        public static void ReleaseAsset(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var reference) && reference.IsValid())
                reference.ReleaseAsset();
        }

        public static void ReleaseAsset(AssetReference reference)
        {
            if (reference.IsValid())
                reference.ReleaseAsset();
        }

        public static void ReleasePrefab(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var reference) && reference.IsValid())
                reference.ReleaseAsset();
        }

        public static void ReleasePrefab(AssetReferenceGameObject reference)
        {
            if (reference.IsValid())
                reference.ReleaseAsset();
        }

        public static async UniTask<SceneInstance> LoadSceneAsync(int sceneReferenceIndex, LoadSceneMode mode)
        {
            if (ValidateDatabaseRequest(sceneReferenceIndex, out var reference))
            {
                _ = reference.LoadSceneAsync(mode);

                var handle = reference.OperationHandle;

                await handle.ToUniTask();

                if (handle.Status is AsyncOperationStatus.Succeeded)
                    return handle.Convert<SceneInstance>().Result;
                else if (handle.IsValid())
                    handle.Release();

                return default;
            }

            return default;
        }

        public static async UniTask<SceneInstance> UnloadSceneAsync(int sceneReferenceIndex)
        {
            if (ValidateDatabaseRequest(sceneReferenceIndex, out var reference))
            {
                _ = reference.UnLoadScene();

                return await reference.OperationHandle.Convert<SceneInstance>().ToUniTask();
            }

            return default;
        }

        public static bool TryGetAssetReference(AssetGroups group, int indexInDB, out AssetReference result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out result);
        }

        public static bool TryGetPrefabReference(AssetGroups group, int indexInDB, out AssetReferenceGameObject result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out result);
        }

        public static bool TryGetSceneReference(int indexInDB, out SceneAssetReference result)
        {
            return ValidateDatabaseRequest(indexInDB, out result);
        }
        #endregion

        #region Addressable Database Request Validation:
        private static bool ValidateDatabaseRequest(int indexInDB, out SceneAssetReference reference)
        {
            return ValidateAssetReferenceRequest(Instance.UserConfig.Scenes, indexInDB, out reference);
        }

        private static bool ValidateDatabaseRequest<T>(Dictionary<AssetGroups, T[]> database,
        AssetGroups group, int indexInDB, out T reference) where T : AssetReference
        {
            if (database.TryGetValue(group, out var assetRefCollection))
            {
                return ValidateAssetReferenceRequest(assetRefCollection, indexInDB, out reference);
            }
            else Instance.Send("The requested asset group does not exist in the database!").ToUnityConsole(DebugType.Error);

            reference = null;
            return false;
        }

        private static bool ValidateAssetReferenceRequest<T>(T[] assetRefCollection, int indexInDB, out T reference) where T : AssetReference
        {
            reference = null;

            if (!indexInDB.IsWithinBoundsOf(assetRefCollection))
            {
                Instance.Send("The Asset Reference Index ", indexInDB, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            var assetReference = assetRefCollection[indexInDB];

            if (assetReference == null)
            {
                Instance.Send(assetReference, " at index ", indexInDB, " is NULL!").ToUnityConsole(DebugType.Error);
                return false;
            }
            else if (!assetReference.RuntimeKeyIsValid())
            {
                Instance.Send("RuntimeKey of ", assetReference, ", ", assetReference.RuntimeKey, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            reference = assetReference;
            return true;
        }
        #endregion
    }
}