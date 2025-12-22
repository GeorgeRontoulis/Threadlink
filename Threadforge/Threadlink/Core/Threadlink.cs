namespace Threadlink.Core
{
    using Addressables;
    using Cysharp.Threading.Tasks;
    using MessagePack;
    using NativeSubsystems.Scribe;
    using Shared;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.InputSystem;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;
    using Utilities.Collections;
    using Utilities.Objects;

    /// <summary>
    /// The Core. Controls all aspects of the runtime, managing subsystems at the lowest level.
    /// </summary>
    public sealed partial class Threadlink : Weaver<Threadlink, IThreadlinkSubsystem>
    {
        /// <summary>
        /// Uses <see cref="MessagePackSerializerOptions.Standard"/> by default.
        /// You may customize this as you see fit. Use <see cref="Threadlink"/>'s
        /// serialization API to make sure serialization uses these options.
        /// <para></para>
        /// When using <see cref="MessagePack"/> manually, make sure you pass these
        /// or any options from your custom source into all serialization methods.
        /// </summary>
        public static MessagePackSerializerOptions SerializerOptions { get; set; } = MessagePackSerializerOptions.Standard;

        internal ThreadlinkNativeConfig NativeConfig { get; set; }
        public ThreadlinkUserConfig UserConfig { get; internal set; }

        #region Main Lifecycle API:
        /// <summary>
        /// Terminate the runtime.
        /// Same as <see cref="Discard"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Terminate() => Instance.Discard();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        public override void Boot()
        {
            base.Boot();

            ///Start the Threadlink Update Loop.
            Object.DontDestroyOnLoad(new GameObject(nameof(ThreadlinkLoop), typeof(ThreadlinkLoop))
            {
                hideFlags = HideFlags.HideAndDontSave
            });
        }
        #endregion

        #region Public API:
        /// <summary>
        /// Wait for <paramref name="frameCount"/> frames in an async context.
        /// </summary>
        /// <param name="frameCount">The number of frames to wait for.</param>
        /// <returns>The awaitable task.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask WaitForFramesAsync(int frameCount)
        {
            for (int i = 0; i < frameCount; i++) await UniTask.NextFrame();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Weave<T>() where T : IThreadlinkSubsystem => Instance.TryWeave(out T wovenSubsystem) ? wovenSubsystem : default;

        /// <summary>
        /// Attempt to serialize data into bytes using <see cref="MessagePack"/> and <see cref="SerializerOptions"/>.
        /// Any and all requirements and limitations of the serializer apply to the data you want to serialize.
        /// </summary>
        /// <typeparam name="T">The type of data.</typeparam>
        /// <param name="input">The input data.</param>
        /// <param name="result">The resulting byte data.</param>
        /// <returns>A byte array containing the serialized data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySerialize<T>(T input, out byte[] result)
        {
            if (input == null)
            {
                result = null;
                return false;
            }

            result = MessagePackSerializer.Serialize(input, SerializerOptions);
            return true;
        }

        /// <summary>
        /// Attempt to deserialize byte data into the desired data type using <see cref="MessagePack"/> and <see cref="SerializerOptions"/>.
        /// Any and all requirements and limitations of the serializer apply to the data you want to deserialize.
        /// </summary>
        /// <typeparam name="T">The type of data.</typeparam>
        /// <param name="input">The input byte data.</param>
        /// <param name="result">The resulting data.</param>
        /// <returns>The deserialized data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserialize<T>(byte[] input, out T result)
        {
            if (input == null || input.Length <= 0)
            {
                result = default;
                return false;
            }

            result = MessagePackSerializer.Deserialize<T>(input, SerializerOptions);
            return true;
        }
        #endregion

        #region Asset Reference/Loading/Unloading API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(AssetGroups group, int indexInDB) where T : Object
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var runtimeKey))
            {
                return await LoadAssetAsync<T>(runtimeKey);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(object runtimeKey) where T : Object
        {
            return await ThreadlinkResourceProvider<T>.LoadOrGetCachedAtKeyAsync(runtimeKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : Object
        {
            return await ThreadlinkResourceProvider<T>.LoadOrGetCachedAtKeyAsync(reference.RuntimeKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadPrefabAsync<T>(AssetGroups group, int indexInDB) where T : Component
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var runtimeKey))
            {
                return await LoadPrefabAsync<T>(runtimeKey);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadPrefabAsync<T>(object runtimeKey) where T : Component
        {
            var prefab = await ThreadlinkResourceProvider<GameObject>.LoadOrGetCachedAtKeyAsync(runtimeKey);

            if (prefab != null && prefab.As<T>(out var component))
                return component;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<T> LoadPrefabAsync<T>(AssetReferenceGameObject reference) where T : Component
        {
            var prefab = await ThreadlinkResourceProvider<GameObject>.LoadOrGetCachedAtKeyAsync(reference.RuntimeKey);

            if (prefab != null && prefab.As<T>(out var component))
                return component;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseAsset(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var runtimeKey))
                ThreadlinkResourceProvider<Object>.ReleaseAt(runtimeKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseAsset(AssetReference reference)
        {
            if (reference != null)
                ThreadlinkResourceProvider<Object>.ReleaseAt(reference.RuntimeKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleasePrefab(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var runtimeKey))
                ThreadlinkResourceProvider<GameObject>.ReleaseAt(runtimeKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleasePrefab(AssetReferenceGameObject reference)
        {
            if (reference != null)
                ThreadlinkResourceProvider<GameObject>.ReleaseAt(reference.RuntimeKey);
        }

        public static async UniTask<SceneInstance> LoadSceneAsync(int sceneReferenceIndex, LoadSceneMode mode)
        {
            if (TryGetSceneReference(sceneReferenceIndex, out var reference))
            {
                var runtimeKey = reference.RuntimeKey;
                var handle = Addressables.LoadSceneAsync(runtimeKey, mode, true, 100, SceneReleaseMode.ReleaseSceneWhenSceneUnloaded);

                await handle.ToUniTask();

                if (handle.Status is AsyncOperationStatus.Succeeded)
                {
                    ThreadlinkResourceProvider<SceneInstance>.Track(runtimeKey, handle);
                    return handle.Result;
                }
                else if (handle.IsValid())
                    handle.Release();

                return default;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> UnloadSceneAsync(int sceneReferenceIndex)
        {
            if (TryGetSceneReference(sceneReferenceIndex, out var reference)
            && ThreadlinkResourceProvider<SceneInstance>.TryGetHandleAt(reference.RuntimeKey, out var handle))
            {
                var result = await Addressables.UnloadSceneAsync(handle, true).ToUniTask();

                ThreadlinkResourceProvider<SceneInstance>.Remove(reference.RuntimeKey);
                return result;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAssetKey(AssetGroups group, int indexInDB, out string result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPrefabKey(AssetGroups group, int indexInDB, out string result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSceneReference(int indexInDB, out SceneAssetReference result)
        {
            var config = Instance.UserConfig;
            var scenes = config.Scenes;

            if (indexInDB.IsWithinBoundsOf(scenes))
            {
                result = scenes[indexInDB];
                return result != null && result.RuntimeKeyIsValid();
            }

            result = null;
            return false;
        }
        #endregion

        #region Addressable Database Request Validation:
        private static bool ValidateDatabaseRequest(Dictionary<AssetGroups, string[]> database,
        AssetGroups group, int indexInDB, out string runtimeKey)
        {
            if (database.TryGetValue(group, out var runtimeKeyCollection))
                return ValidateAssetReferenceRequest(runtimeKeyCollection, indexInDB, out runtimeKey);
            else
                Instance.Send("The requested asset group does not exist in the database!").ToUnityConsole(DebugType.Error);

            runtimeKey = null;
            return false;
        }

        private static bool ValidateAssetReferenceRequest(string[] runtimeKeyCollection, int indexInDB, out string runtimeKey)
        {
            runtimeKey = null;

            if (!indexInDB.IsWithinBoundsOf(runtimeKeyCollection))
            {
                Instance.Send("The Runtime Key Index ", indexInDB, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            var matchedKey = runtimeKeyCollection[indexInDB];

            if (string.IsNullOrEmpty(matchedKey))
            {
                Instance.Send("RuntimeKey ", matchedKey, " is invalid!").ToUnityConsole(DebugType.Error);
                return false;
            }

            runtimeKey = matchedKey;
            return true;
        }
        #endregion
    }
}