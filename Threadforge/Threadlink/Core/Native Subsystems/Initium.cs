namespace Threadlink.Core.NativeSubsystems.Initium
{
    using Cysharp.Threading.Tasks;
    using Scribe;
    using Shared;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.Pool;
    using UnityEngine.SceneManagement;
    using Utilities.UniTask;

    /// <summary>
    /// Threadlink's Initialization Pipeline.
    /// </summary>
    public static class Initium
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IEnumerable<IDiscoverable> DiscoverLinkableBehaviours()
        {
            const FindObjectsInactive EXCLUDE = FindObjectsInactive.Exclude;

            return Object.FindObjectsByType<LinkableBehaviour>(EXCLUDE).OfType<IDiscoverable>();
        }

        /// <summary>
        /// Collect the <see cref="LinkableBehaviour"/>s placed in <paramref name="scene"/> that are of type <typeparamref name="T"/>.
        /// </summary>
        private static void CollectSceneObjects<T>(Scene scene, bool includeInactive, List<T> results)
        {
            using var _ = ListPool<GameObject>.Get(out var roots);
            using var __ = ListPool<LinkableBehaviour>.Get(out var behaviours);

            scene.GetRootGameObjects(roots);

            int rootCount = roots.Count;

            for (int i = 0; i < rootCount; i++)
            {
                behaviours.Clear();
                roots[i].GetComponentsInChildren(includeInactive, behaviours);

                int behaviourCount = behaviours.Count;

                for (int j = 0; j < behaviourCount; j++)
                {
                    if (behaviours[j] is T result)
                        results.Add(result);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLoaded(Scene scene) => scene.IsValid() && scene.isLoaded;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async UniTask BootAndInitUnityObjectsAsync()
        {
            await PreloadBootAndInitAsync(DiscoverLinkableBehaviours());
        }

        /// <summary>
        /// Preload, boot and initialize the active <see cref="IDiscoverable"/> objects placed in <paramref name="scene"/>.
        /// <para/>
        /// Scenes Nexus holds (<see cref="Nexus.Nexus.HoldAsync"/>) are booted automatically.
        /// Use this for scenes loaded through any other means.
        /// </summary>
        /// <param name="scene">A loaded scene.</param>
        public static async UniTask BootAndInitSceneObjectsAsync(Scene scene)
        {
            if (!IsLoaded(scene))
            {
                Scribe.Send<Threadlink>("Cannot boot the objects of a scene that is not loaded.").ToUnityConsole(DebugType.Error);
                return;
            }

            using var _ = ListPool<IDiscoverable>.Get(out var discoverables);

            CollectSceneObjects(scene, false, discoverables);

            await PreloadBootAndInitAsync(discoverables);
        }

        /// <summary>
        /// Discard every <see cref="IDiscoverable"/> placed in <paramref name="scene"/>, active or not: what Initium boots,
        /// it discards. Other objects are their developer's to discard, as they are to boot.
        /// This is the unload counterpart of <see cref="BootAndInitSceneObjectsAsync(Scene)"/>
        /// and must run before the scene is unloaded, as Unity destroys scene objects without discarding them.
        /// <para/>
        /// Nexus discards every scene it unloads automatically (Docs/Netcode/M1-Design.md D42).
        /// </summary>
        /// <param name="scene">A loaded scene.</param>
        public static void DiscardSceneObjects(Scene scene)
        {
            if (!IsLoaded(scene))
            {
                Scribe.Send<Threadlink>("Cannot discard the objects of a scene that is not loaded.").ToUnityConsole(DebugType.Error);
                return;
            }

            using var _ = ListPool<IDiscoverable>.Get(out var discoverables);

            CollectSceneObjects(scene, true, discoverables);

            // Children before parents, mirroring Iris's reverse dispatch order.
            for (int i = discoverables.Count - 1; i >= 0; i--)
            {
                if (discoverables[i] is LinkableBehaviour behaviour && behaviour != null)
                    behaviour.Discard();
            }
        }

        internal static async UniTask PreloadBootAndInitAsync<T>(IEnumerable<T> objects)
        {
            if (objects == null) return;

            // Materialize once: every phase must operate on the same objects.
            // Re-evaluating a discovery query per phase would initialize objects created during Boot without booting them.
            using var _ = ListPool<T>.Get(out var buffer);

            buffer.AddRange(objects);

            int count = buffer.Count;

            {
                using var __ = ListPool<IAddressablesPreloader>.Get(out var preloaders);
                using var ___ = ListPool<UniTask<bool>>.Get(out var preloadingTasks);

                for (int i = 0; i < count; i++)
                {
                    if (buffer[i] is IAddressablesPreloader preloader)
                    {
                        preloaders.Add(preloader);
                        preloadingTasks.Add(preloader.TryPreloadAssetsAsync());
                    }
                }

                var preloadingResults = await UniTask.WhenAll(preloadingTasks);

                if (preloadingResults != null)
                {
                    int length = preloadingResults.Length;

                    for (int i = 0; i < length; i++)
                    {
                        if (!preloadingResults[i])
                        {
                            var msg = $"Preloader {preloaders[i].GetType().Name} failed to load its dependencies!";
                            throw new System.InvalidOperationException(msg);
                        }
                    }
                }
            }

            {
                using var __ = ListPool<UniTask>.Get(out var initTasks);

                for (int i = 0; i < count; i++)
                {
                    if (buffer[i] is IBootable bootable)
                        initTasks.Add(BootAsync(bootable));
                }

                await initTasks.AwaitAllThenClear();

                for (int i = 0; i < count; i++)
                {
                    if (buffer[i] is IInitializable initializable)
                        initTasks.Add(InitializeAsync(initializable));
                }

                await initTasks.AwaitAllThenClear(true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask BootAndInitAsync<T>(T entity) where T : IBootable, IInitializable
        {
            await BootAsync(entity);
            await InitializeAsync(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask BootAsync(IBootable entity)
        {
            entity.Boot();
            await Threadlink.WaitForFramesAsync(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask InitializeAsync(IInitializable entity)
        {
            entity.Initialize();
            await Threadlink.WaitForFramesAsync(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BootAndInit<T>(T entity) where T : IBootable, IInitializable
        {
            Boot(entity);
            Initialize(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask BootAndInit<T>(IReadOnlyList<T> entities) where T : IBootable, IInitializable
        {
            await Boot(entities);
            await Initialize(entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask Boot<T>(IReadOnlyList<T> entities) where T : IBootable
        {
            if (entities == null) return;

            int length = entities.Count;

            for (int i = 0; i < length; i++)
            {
                entities[i].Boot();
                await Threadlink.WaitForFramesAsync(1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask Initialize<T>(IReadOnlyList<T> entities) where T : IInitializable
        {
            if (entities == null) return;

            int length = entities.Count;

            for (int i = 0; i < length; i++)
            {
                entities[i].Initialize();
                await Threadlink.WaitForFramesAsync(1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Boot(IBootable entity) => entity?.Boot();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(IInitializable entity) => entity?.Initialize();
    }
}