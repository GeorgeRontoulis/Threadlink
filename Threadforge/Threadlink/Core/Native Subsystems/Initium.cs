namespace Threadlink.Core.NativeSubsystems.Initium
{
    using Cysharp.Threading.Tasks;
    using Shared;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;

    public static class Initium
    {
        public static async UniTask BootAndInitUnityObjectsAsync()
        {
            const FindObjectsInactive EXCLUDE = FindObjectsInactive.Exclude;
            const FindObjectsSortMode NONE = FindObjectsSortMode.None;

            var discoverables = Object.FindObjectsByType<LinkableBehaviour>(EXCLUDE, NONE).OfType<IDiscoverable>();
            var bootables = discoverables.OfType<IBootable>();
            var tasks = new HashSet<UniTask>(1);

            foreach (var bootable in bootables)
                tasks.Add(BootAsync(bootable));

            await UniTask.WhenAll(tasks);

            tasks.Clear();

            var initializables = discoverables.OfType<IInitializable>();

            foreach (var initializable in initializables)
                tasks.Add(InitializeAsync(initializable));

            await UniTask.WhenAll(tasks);

            tasks.Clear();
            tasks.TrimExcess();
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