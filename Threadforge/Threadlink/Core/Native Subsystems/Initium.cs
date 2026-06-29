namespace Threadlink.Core.NativeSubsystems.Initium
{
    using Cysharp.Threading.Tasks;
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
            const FindObjectsSortMode NONE = FindObjectsSortMode.None;

            return Object.FindObjectsByType<LinkableBehaviour>(EXCLUDE, NONE).OfType<IDiscoverable>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async UniTask BootAndInitUnityObjectsAsync()
        {
            await PreloadBootAndInitAsync(DiscoverLinkableBehaviours());
        }

        internal static async UniTask BootAndInitUnityObjectsAsync(Scene scene)
        {
            var discoverables = DiscoverLinkableBehaviours();

            if (scene.IsValid() && scene != default)
                discoverables = discoverables.Where(x => (x as LinkableBehaviour).gameObject.scene == scene);

            await PreloadBootAndInitAsync(discoverables);
        }

        internal static async UniTask PreloadBootAndInitAsync<T>(IEnumerable<T> objects)
        {
            if (objects == null) return;

            var preloaders = objects.OfType<IAddressablesPreloader>();
            var tasks = ListPool<UniTask>.Get();

            try
            {
                foreach (var preloader in preloaders)
                    tasks.Add(preloader.TryPreloadAssetsAsync());

                await tasks.AwaitAllThenClear();

                var bootables = objects.OfType<IBootable>();

                foreach (var bootable in bootables)
                    tasks.Add(BootAsync(bootable));

                await tasks.AwaitAllThenClear();

                var initializables = objects.OfType<IInitializable>();

                foreach (var initializable in initializables)
                    tasks.Add(InitializeAsync(initializable));

                await tasks.AwaitAllThenClear(true);
            }
            finally
            {
                ListPool<UniTask>.Release(tasks);
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