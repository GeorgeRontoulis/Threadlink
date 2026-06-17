namespace Threadlink.Utilities.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public static class ECSUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, NativeArray<T> collection) where T : unmanaged
        {
            return collection.IsCreated && index >= 0 && index < collection.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWithinBoundsOf<T>(this int index, INativeList<T> collection) where T : unmanaged
        {
            return !collection.IsEmpty && index >= 0 && index < collection.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(in this Entity target)
        {
            return ECSWorld.TryGetSingleton(out var world) && world.IsValid(target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(in this Entity target, out ECSWorld ecsWorld)
        {
            return ECSWorld.TryGetSingleton(out ecsWorld) && ecsWorld.IsValid(target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeList<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeHashSet<T> target) where T : unmanaged, IEquatable<T>
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this UnsafeQueue<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<K, V>(ref this UnsafeHashMap<K, V> target)
        where K : unmanaged, IEquatable<K>
        where V : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafely<T>(ref this NativeArray<T> target) where T : unmanaged
        {
            if (target.IsCreated)
                target.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSize<T>(ref this UnsafeList<T> target, int count,
        NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        where T : unmanaged
        {
            if (!target.IsCreated)
                return;

            var length = target.Length;
            target.Resize(Math.Max(count + 1, length + length), options);
        }
    }
}
