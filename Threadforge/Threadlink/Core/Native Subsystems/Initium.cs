namespace Threadlink.Core.NativeSubsystems.Initium
{
    using Cysharp.Threading.Tasks;
    using Shared;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using Utilities.UniTask;

    /// <summary>
    /// Threadlink's Initialization Pipeline.
    /// </summary>
    public static class Initium
    {
        private static readonly List<UniTask> taskCache = new(1);

        internal static async UniTask BootAndInitUnityObjectsAsync()
        {
            const FindObjectsInactive EXCLUDE = FindObjectsInactive.Exclude;
            const FindObjectsSortMode NONE = FindObjectsSortMode.None;

            var discoverables = Object.FindObjectsByType<LinkableBehaviour>(EXCLUDE, NONE).OfType<IDiscoverable>();

            await PreloadBootAndInitAsync(discoverables);
        }

        internal static async UniTask PreloadBootAndInitAsync<T>(IEnumerable<T> objects)
        {
            if (objects == null) return;

            var preloaders = objects.OfType<IAddressablesPreloader>();

            foreach (var preloader in preloaders)
                taskCache.Add(preloader.TryPreloadAssetsAsync());

            await taskCache.AwaitAllThenClear();

            var bootables = objects.OfType<IBootable>();

            foreach (var bootable in bootables)
                taskCache.Add(BootAsync(bootable));

            await taskCache.AwaitAllThenClear();

            var initializables = objects.OfType<IInitializable>();

            foreach (var initializable in initializables)
                taskCache.Add(InitializeAsync(initializable));

            await taskCache.AwaitAllThenClear(true);
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