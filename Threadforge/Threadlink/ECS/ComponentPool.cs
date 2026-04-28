namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public interface IComponentPool : IDisposable
    {
        public bool Remove(in Entity entity);
    }

    public unsafe sealed class ComponentPool<T> : IComponentPool where T : unmanaged, IComponent
    {
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        private UnsafeList<int> sparse;
        private UnsafeList<int> dense;
        private UnsafeList<T> data;

        private int count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (data.IsCreated)
            {
                for (int i = 0; i < data.Length; i++)
                    data.ElementAt(i).Dispose();

                data.Dispose();
            }

            dense.DisposeSafely();
            sparse.DisposeSafely();
        }

        public ComponentPool(int initialCapacity)
        {
            const Allocator ALLOC = Allocator.Persistent;

            sparse = new UnsafeList<int>(initialCapacity, ALLOC);
            dense = new UnsafeList<int>(initialCapacity, ALLOC);
            data = new UnsafeList<T>(initialCapacity, ALLOC);

            count = 0;
            sparse.AddReplicate(-1, initialCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOccupiedBy(in Entity entity)
        {
            int id = entity.ID;
            if (id >= sparse.Length) return false;

            int index = sparse[id];
            return index >= 0 && index < count && dense[index] == id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* Add(in Entity entity)
        {
            int id = entity.ID;
            EnsureCapacity(entity);

            int index = count++;

            while (dense.Length <= index)
                dense.AddNoResize(default);

            while (data.Length <= index)
                data.AddNoResize(default);

            dense[index] = id;
            sparse[id] = index;

            return data.Ptr + index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer(in Entity entity, out T* ptr)
        {
            int id = entity.ID;
            if (id >= sparse.Length)
            {
                ptr = null;
                return false;
            }

            int index = sparse[id];
            if (index >= 0 && index < count && dense[index] == id)
            {
                ptr = data.Ptr + index;
                return true;
            }

            ptr = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(in Entity entity)
        {
            int id = entity.ID;

            if (id >= sparse.Length)
                return false;

            int index = sparse[id];

            if (index < 0 || index >= count)
                return false;

            int last = --count;
            int lastEntity = dense[last];

            // swap last element into removed slot
            dense[index] = lastEntity;
            data.Ptr[index] = data.Ptr[last];

            sparse[lastEntity] = index;
            sparse[id] = -1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetDataPointer() => data.Ptr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int* GetEntitiesPointer() => dense.Ptr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(in Entity entity)
        {
            int id = entity.ID;

            while (id >= sparse.Length)
                sparse.AddNoResize(-1);

            if (count >= dense.Length)
            {
                dense.Resize(count + 1, NativeArrayOptions.UninitializedMemory);
                data.Resize(count + 1, NativeArrayOptions.UninitializedMemory);
            }
        }
    }
}