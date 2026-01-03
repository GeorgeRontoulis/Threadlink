namespace Threadlink.Core
{
    using Addressables;
    using Collections;
    using Collections.Extensions;
    using Cysharp.Threading.Tasks;
    using MessagePack;
    using NativeSubsystems.Iris;
    using NativeSubsystems.Scribe;
    using Shared;
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
        /// <see cref="Native"/> = Instantiate the native <see cref="ThreadlinkLoop"/> 
        /// <see cref="GameObject"/> to start receiving update callbacks through <see cref="Iris"/>.
        /// <para></para>
        /// <see cref="Custom"/> = Use your own logic to instantiate a custom update loop 
        /// <see cref="MonoBehaviour"/> that will publish <see cref="Iris"/>' update events.
        /// Subscribe to <see cref="Iris.Events.OnCoreDeployed"/> to get a callback when
        /// the core is deployed, then set up your update loop there.
        /// <para></para>
        /// This is useful when using Threadlink alongside another framework.
        /// For example, in the context of <see href="https://doc.photonengine.com/quantum/current/quantum-intro">Photon Quantum</see>,
        /// Threadlink would only manage the View, while Quantum would manage the deterministic multiplayer Simulation.
        /// </summary>
        internal enum UpdateLoop : byte { Native, Custom }

        /// <summary>
        /// Uses <see cref="MessagePackSerializerOptions.Standard"/> by default.
        /// You may customize this as you see fit. Use <see cref="Threadlink"/>'s
        /// serialization API to make sure serialization uses these options.
        /// <para></para>
        /// When using <see cref="MessagePack"/> manually, make sure you pass these
        /// or any options from your custom source into all serialization methods.
        /// </summary>
        public static readonly MessagePackSerializerOptions serializerOptions = MessagePackSerializerOptions.Standard;

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

            if (UserConfig != null && UserConfig.UpdateLoopBehaviour is UpdateLoop.Native)
            {
                ///Start the Threadlink Update Loop.
                Object.DontDestroyOnLoad(new GameObject(nameof(ThreadlinkLoop), typeof(ThreadlinkLoop))
                {
                    hideFlags = HideFlags.HideAndDontSave
                });
            }
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
        /// Attempt to serialize data into bytes using <see cref="MessagePack"/> and <see cref="serializerOptions"/>.
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
                Scribe.Send<Threadlink>("NULL input data detected! Will not serialize!").ToUnityConsole(DebugType.Error);
                result = null;
                return false;
            }

            result = MessagePackSerializer.Serialize(input, serializerOptions);
            return true;
        }

        /// <summary>
        /// Attempt to deserialize byte data into the desired data type using <see cref="MessagePack"/> and <see cref="serializerOptions"/>.
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
                Scribe.Send<Threadlink>("NULL or empty byte data detected! Will not deserialize!").ToUnityConsole(DebugType.Error);
                result = default;
                return false;
            }

            result = MessagePackSerializer.Deserialize<T>(input, serializerOptions);
            return true;
        }
        #endregion

        #region Asset Reference/Loading/Unloading API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(AssetGroups group, int indexInDB) where T : Object
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var runtimeKey))
            {
                return ThreadlinkResourceProvider<T>.LoadOrGetCachedAt(runtimeKey);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadAsset<T>(AssetReference reference) where T : Object
        {
            return reference.RuntimeKeyIsValid() ? ThreadlinkResourceProvider<T>.LoadOrGetCachedAt(reference) : null;
        }

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
        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : Object
        {
            return reference.RuntimeKeyIsValid() ? await ThreadlinkResourceProvider<T>.LoadOrGetCachedAtRefAsync(reference) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T LoadPrefab<T>(AssetGroups group, int indexInDB) where T : Component
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var reference))
            {
                return ThreadlinkResourceProvider<T>.LoadOrGetCachedAt(reference);
            }

            return null;
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
        public static async UniTask<T> LoadPrefabAsync<T>(AssetReferenceGameObject reference) where T : Component
        {
            if (!reference.RuntimeKeyIsValid())
                return null;

            var prefab = await ThreadlinkResourceProvider<GameObject>.LoadOrGetCachedAtRefAsync(reference);

            if (prefab != null && prefab.As<T>(out var component))
                return component;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseAsset(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out var reference))
                reference.ReleaseAsset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleasePrefab(AssetGroups group, int indexInDB)
        {
            if (ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out var reference))
                reference.ReleaseAsset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> LoadSceneAsync(int sceneReferenceIndex, LoadSceneMode mode)
        {
            if (TryGetSceneReference(sceneReferenceIndex, out var reference))
                return await ThreadlinkResourceProvider<Object>.LoadSceneAsync(reference, mode);

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask<SceneInstance> UnloadSceneAsync(int sceneReferenceIndex)
        {
            if (TryGetSceneReference(sceneReferenceIndex, out var reference))
                return await ThreadlinkResourceProvider<Object>.UnloadSceneAsync(reference);

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAssetReference(AssetGroups group, int indexInDB, out AssetReference result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDB, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPrefabReference(AssetGroups group, int indexInDB, out AssetReferenceGameObject result)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDB, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSceneReference(int indexInDB, out SceneAssetReference result)
        {
            var config = Instance.UserConfig;

            if (config.TryGetScenes(out var scenes) && indexInDB.IsWithinBoundsOf(scenes))
            {
                result = scenes[indexInDB];
                return result != null && result.RuntimeKeyIsValid();
            }

            result = null;
            return false;
        }
        #endregion

        #region Addressable Database Request Validation:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckAssetPointerValidity(AssetGroups group, int indexInDatabase)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Assets, group, indexInDatabase, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckPrefabPointerValidity(AssetGroups group, int indexInDatabase)
        {
            return ValidateDatabaseRequest(Instance.UserConfig.Prefabs, group, indexInDatabase, out _);
        }

        private static bool ValidateDatabaseRequest<T>(FieldTable<AssetGroups, T[]> database, AssetGroups group, int indexInDB, out T reference)
        where T : AssetReference
        {
            if (database.TryGetValue(group, out var assetRefCollection))
            {
                return ValidateAssetReferenceRequest(assetRefCollection, indexInDB, out reference);
            }
            else Instance.Send("The requested asset group does not exist in the database!").ToUnityConsole(DebugType.Error);

            reference = null;
            return false;
        }

        private static bool ValidateAssetReferenceRequest<T>(T[] assetRefCollection, int indexInDB, out T reference)
        where T : AssetReference
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