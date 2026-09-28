namespace Threadlink.Shared
{
    using Core.NativeSubsystems.Scribe;
    using System;
    using System.Collections.Generic;
    using Unity.Scripting.LifecycleManagement;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// A special <see cref="AssetReference"/> restricted to selecting <see cref="UnityEditor.SceneAsset"/>s.
    /// <para/>
    /// Scene loads are tracked by <see cref="SceneOperation"/> rather than <see cref="AssetReference.OperationHandle"/>,
    /// allowing scenes to be loaded with full <see cref="LoadSceneParameters"/>, including a <see cref="LocalPhysicsMode"/>.
    /// </summary>
    [Serializable]
    public sealed partial class SceneAssetReference : AssetReference
    {
        /// <summary>
        /// The references holding a <see cref="SceneOperation"/>. A reference lives on a config asset, which the Editor
        /// keeps from one Play Mode session to the next when Enter Play Mode runs without a domain reload. Play Mode
        /// ending unloads every scene, so their operations are forgotten then (<see cref="ForgetSceneOperations"/>);
        /// otherwise the next session would find the scene loaded, and get a scene that no longer exists.
        /// </summary>
        [NoAutoStaticsCleanup]
        private static readonly List<SceneAssetReference> Holding = new(4);

        /// <summary>
        /// The operation of the scene loaded through this reference. Invalid while no scene is loaded.
        /// </summary>
        public AsyncOperationHandle<SceneInstance> SceneOperation { get; private set; }

        /// <summary>
        /// Runs after the application quit, when Threadlink's shutdown has unloaded what it could, and Unity unloads the rest.
        /// Addressables forgets its own references' operations the same way.
        /// </summary>
        [OnExitingPlayMode]
        private static void ForgetSceneOperations()
        {
            foreach (var reference in Holding)
                reference.SceneOperation = default;

            Holding.Clear();
        }

        private void SetHeld(bool held)
        {
            for (int i = Holding.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Holding[i], this))
                    Holding.RemoveAt(i);
            }

            if (held)
                Holding.Add(this);
        }

        public SceneAssetReference(string guid) : base(guid) { }

        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            return !string.IsNullOrEmpty(path) && path.EndsWith(".unity");
#else
			return true;
#endif
        }

        /// <summary>
        /// Load the referenced scene. Only one load may exist per reference; unload the scene before loading it again.
        /// </summary>
        /// <param name="parameters">The load mode and local physics mode of the scene.</param>
        /// <param name="activateOnLoad">If <see langword="false"/>, the scene loads without activating.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The load operation, or an invalid operation if the scene is already loaded.</returns>
        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(LoadSceneParameters parameters, bool activateOnLoad = true, int priority = 100)
        {
            if (SceneOperation.IsValid())
            {
                Scribe.Send<SceneAssetReference>("Scene ", RuntimeKey, " is already loaded through this reference. Unload it before loading it again.")
                .ToUnityConsole(DebugType.Error);

                return default;
            }

            SceneOperation = Addressables.LoadSceneAsync(RuntimeKey, parameters, activateOnLoad, priority);
            SetHeld(SceneOperation.IsValid());

            return SceneOperation;
        }

        public override AsyncOperationHandle<SceneInstance> LoadSceneAsync(LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return LoadSceneAsync(new LoadSceneParameters(loadMode), activateOnLoad, priority);
        }

        /// <summary>
        /// Unload the scene loaded through this reference.
        /// </summary>
        /// <param name="autoReleaseHandle">
        /// If <see langword="true"/>, the returned operation releases itself on completion and its result must not be read afterwards.
        /// Otherwise, the caller releases it through <see cref="Addressables.Release(AsyncOperationHandle)"/>.
        /// </param>
        /// <returns>The unload operation, or an invalid operation if no scene is loaded.</returns>
        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(bool autoReleaseHandle)
        {
            if (!SceneOperation.IsValid())
                return default;

            var unloadOperation = Addressables.UnloadSceneAsync(SceneOperation, autoReleaseHandle);

            SceneOperation = default;
            SetHeld(false);

            return unloadOperation;
        }

        public override AsyncOperationHandle<SceneInstance> UnLoadScene() => UnloadSceneAsync(true);

        /// <summary>
        /// Attempt to get the loaded <see cref="Scene"/> of this reference.
        /// </summary>
        /// <param name="result">The loaded scene.</param>
        /// <returns><see langword="true"/> if the scene has finished loading. <see langword="false"/> otherwise.</returns>
        public bool TryGetLoadedScene(out Scene result)
        {
            var operation = SceneOperation;

            if (operation.IsValid() && operation.IsDone && operation.Status is AsyncOperationStatus.Succeeded)
            {
                result = operation.Result.Scene;
                return result.IsValid() && result.isLoaded;
            }

            result = default;
            return false;
        }

        public override void ReleaseAsset()
        {
            if (SceneOperation.IsValid())
            {
                Addressables.Release(SceneOperation);
                SceneOperation = default;
                SetHeld(false);

                return;
            }

            base.ReleaseAsset();
        }
    }
}
