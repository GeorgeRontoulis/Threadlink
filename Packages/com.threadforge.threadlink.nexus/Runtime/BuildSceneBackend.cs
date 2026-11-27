namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>Loads Build Profile scenes additively and presents them by activating their roots.</summary>
    internal sealed partial class BuildSceneBackend : ISceneBackend
    {
        private readonly Dictionary<Guid, string> scenes = new Dictionary<Guid, string>();

        private static readonly Dictionary<Guid, string> known = new Dictionary<Guid, string>();

        /// <summary>Records the current path of Build Profile scenes. Generated <c>GameScenes</c> calls this.</summary>
        internal static void AddKnown(IEnumerable<SceneId> scenes)
        {
            foreach (var scene in scenes)
                if (scene.Value != Guid.Empty && !string.IsNullOrEmpty(scene.Path))
                    known[scene.Value] = scene.Path;
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        private static void OnCodeUnloading() => known.Clear();

        internal bool Owns(Guid scene) => scenes.ContainsKey(scene);

        internal Guid Register(SceneId scene)
        {
            if (scene.Value == Guid.Empty)
                throw new ArgumentException("The scene ID is empty.", nameof(scene));
            string path = known.TryGetValue(scene.Value, out var current) ? current : null;
#if UNITY_EDITOR
            if (path == null)
            {
                string asset = UnityEditor.AssetDatabase.GUIDToAssetPath(scene.Value.ToString("N"));
                path = string.IsNullOrEmpty(asset) ? null : asset;
            }
#endif
            path ??= scene.Path;
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Scene " + scene.Value.ToString("D") + " is not in the generated scene table. Add it to the Build Profile.", nameof(scene));
            scenes[scene.Value] = path;
            return scene.Value;
        }

        public async UniTask<ISceneResidency> LoadAsync(Guid scene, CancellationToken token)
        {
            if (!scenes.TryGetValue(scene, out var name))
                throw new KeyNotFoundException("Scene is not registered: " + scene);
            if (SceneUtility.GetBuildIndexByScenePath(name) < 0 && !Application.CanStreamedLevelBeLoaded(name))
                throw new InvalidOperationException($"Scene '{name}' is not in the active Build Profile.");
            token.ThrowIfCancellationRequested();
            int before = SceneManager.sceneCount;
            var operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive) ?? throw new InvalidOperationException($"Scene '{name}' could not be loaded.");
            await Await(operation);
            var loaded = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            if (SceneManager.sceneCount <= before || !loaded.isLoaded)
                throw new InvalidOperationException($"Scene '{name}' did not finish loading.");
            var residency = new Residency(loaded);
            residency.SetPresented(false);
            if (token.IsCancellationRequested)
            {
                // Native loading cannot be cancelled; release the completed scene before reporting cancellation.
                await residency.ReleaseAsync();
                token.ThrowIfCancellationRequested();
            }
            return residency;
        }

        internal static UniTask Await(AsyncOperation operation)
        {
            if (operation.isDone)
                return UniTask.CompletedTask;
            var completion = new UniTaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }

        private sealed class Residency : ISceneResidency
        {
            private readonly Scene scene;
            private readonly GameObject[] roots;
            private readonly bool[] activeOnPresentation;
            private UniTask? release;

            internal Residency(Scene scene)
            {
                this.scene = scene;
                roots = scene.GetRootGameObjects();
                activeOnPresentation = new bool[roots.Length];
                for (int i = 0; i < roots.Length; i++)
                    activeOnPresentation[i] = roots[i].activeSelf;
            }

            public Scene Scene => scene;

            public bool IsLoaded => release == null && scene.IsValid() && scene.isLoaded;

            public void SetPresented(bool presented)
            {
                if (!IsLoaded)
                    return;
                for (int i = 0; i < roots.Length; i++)
                    if (roots[i] != null)
                        roots[i].SetActive(presented && activeOnPresentation[i]);
                if (presented)
                    SceneManager.SetActiveScene(scene);
            }

            public UniTask ReleaseAsync()
            {
                if (release.HasValue)
                    return release.Value;
                var operation = scene.IsValid() && scene.isLoaded ? SceneManager.UnloadSceneAsync(scene) : null;
                release = operation == null ? UniTask.CompletedTask : Await(operation);
                return release.Value;
            }
        }
    }

    /// <summary>Routes Build Profile scenes and Addressables scenes to their backends.</summary>
    internal sealed class NexusBackend : ISceneBackend
    {
        internal readonly BuildSceneBackend Build = new BuildSceneBackend();
        internal ISceneBackend Content;

        public UniTask<ISceneResidency> LoadAsync(Guid scene, CancellationToken token)
        {
            if (Build.Owns(scene))
                return Build.LoadAsync(scene, token);
            if (Content == null)
                throw new InvalidOperationException("Addressables scenes need the Threadlink content catalog.");
            return Content.LoadAsync(scene, token);
        }
    }
}
