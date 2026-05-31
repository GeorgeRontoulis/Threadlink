namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe sealed class ECSWorld : ThreadlinkSubsystem<ECSWorld>, IDisposable
    {
        private UnsafeList<int> generations = default;
        private UnsafeList<int> availableIDs = default;
        private UnsafeList<ComponentMask> masks = default;
        private IComponentPool[] componentPools = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            generations.DisposeSafely();
            availableIDs.DisposeSafely();
            masks.DisposeSafely();

            if (componentPools != null)
            {
                int length = componentPools.Length;

                for (int i = 0; i < length; i++)
                    componentPools[i]?.Dispose();

                componentPools = null;
            }
        }

        public override void Discard()
        {
            Dispose();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            const Allocator ALLOC_PERSIST = Allocator.Persistent;

            ComponentRegistry.Hydrate();
            generations = new(1024, ALLOC_PERSIST);
            availableIDs = new(1024, ALLOC_PERSIST);
            masks = new(1024, ALLOC_PERSIST, NativeArrayOptions.ClearMemory);
            componentPools = new IComponentPool[ComponentRegistry.ComponentCount];

            this.GuardAgainstEditorMemoryLeaks();
            base.Boot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateNewEntity()
        {
            int id;
            int generation;
            var length = availableIDs.Length;

            if (length > 0)
            {
                id = availableIDs[^1];
                availableIDs.RemoveAtSwapBack(length - 1);
                generation = generations[id];
            }
            else
            {
                id = generations.Length;
                generations.Add(0);
                generation = 0;

                if (id >= masks.Length)
                    masks.EnsureSize(id, NativeArrayOptions.ClearMemory);
            }

            return new Entity(id, generation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(in Entity entity)
        {
            var index = entity.ID;

            if ((uint)index >= (uint)generations.Length)
                return false;

            return generations[index] == entity.Generation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(in Entity entity)
        {
            if (!IsValid(entity)) return;

            var index = entity.ID;
            generations[index]++;
            availableIDs.Add(index);

            ref var mask = ref masks.ElementAt(index);
            foreach (var componentBit in mask)
                componentPools[componentBit]?.Remove(entity);

            mask = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* Add<T>(in Entity entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity)) return null;

            int bit = ComponentType.Of<T>.BitIndex;

            if (componentPools[bit] == null)
                componentPools[bit] = new ComponentPool<T>(masks.Length);

            masks.ElementAt(entity.ID).Set(bit);

            return ((ComponentPool<T>)componentPools[bit]).Add(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer<T>(in Entity entity, out T* result) where T : unmanaged, IComponent
        {
            if (IsValid(entity) && TryGetPool(out ComponentPool<T> pool) && pool.TryGetPointer(entity, out var ptr))
            {
                result = ptr;
                return true;
            }

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetPool<T>(out ComponentPool<T> result) where T : unmanaged, IComponent
        {
            int bit = ComponentType.Of<T>.BitIndex;

            if (bit >= 0 && bit < componentPools.Length && componentPools[bit] != null)
            {
                result = (ComponentPool<T>)componentPools[bit];
                return true;
            }

            result = null;
            return false;
        }

        #region Command Buffer Helpers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetMaskBitUnsafe(int entityId, int bit)
        {
            masks.ElementAt(entityId).Set(bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearMaskBitUnsafe(int entityId, int bit)
        {
            masks.ElementAt(entityId).Clear(bit);
        }
        #endregion

        #region Queries
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ForEach<T1>(delegate*<Entity, T1*, void> action)
        where T1 : unmanaged, IComponent
        {
            if (!TryGetPool<T1>(out var pool1)) return;

            int count = pool1.Count;
            int* entities = pool1.GetEntitiesPointer();

            for (int i = count - 1; i >= 0; i--)
            {
                int id = entities[i];
                var entity = new Entity(id, generations[id]);
                action(entity, pool1.GetPointerUnsafe(id));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ForEach<T1, T2>(delegate*<Entity, T1*, T2*, void> action)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        {
            if (!TryGetPool<T1>(out var pool1) || !TryGetPool<T2>(out var pool2))
                return;

            int count = pool1.Count < pool2.Count ? pool1.Count : pool2.Count;
            int* entities = pool1.Count < pool2.Count ? pool1.GetEntitiesPointer() : pool2.GetEntitiesPointer();

            int bit1 = ComponentType.Of<T1>.BitIndex;
            int bit2 = ComponentType.Of<T2>.BitIndex;

            for (int i = count - 1; i >= 0; i--)
            {
                int id = entities[i];
                ref var mask = ref masks.ElementAt(id);

                if (mask.Has(bit1) && mask.Has(bit2))
                {
                    var entity = new Entity(id, generations[id]);
                    action(entity, pool1.GetPointerUnsafe(id), pool2.GetPointerUnsafe(id));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ForEach<T1, T2, T3>(delegate*<Entity, T1*, T2*, T3*, void> action)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        {
            if (!TryGetPool<T1>(out var pool1) || !TryGetPool<T2>(out var pool2) || !TryGetPool<T3>(out var pool3)) return;

            int minCount = Math.Min(pool1.Count, Math.Min(pool2.Count, pool3.Count));
            int* entities = minCount == pool1.Count ? pool1.GetEntitiesPointer() : (minCount == pool2.Count ? pool2.GetEntitiesPointer() : pool3.GetEntitiesPointer());

            int bit1 = ComponentType.Of<T1>.BitIndex;
            int bit2 = ComponentType.Of<T2>.BitIndex;
            int bit3 = ComponentType.Of<T3>.BitIndex;

            for (int i = minCount - 1; i >= 0; i--)
            {
                int id = entities[i];
                ref var mask = ref masks.ElementAt(id);

                if (mask.Has(bit1) && mask.Has(bit2) && mask.Has(bit3))
                {
                    var entity = new Entity(id, generations[id]);
                    action(entity, pool1.GetPointerUnsafe(id), pool2.GetPointerUnsafe(id), pool3.GetPointerUnsafe(id));
                }
            }
        }
        #endregion

        internal IComponentPool GetPoolByBitUnsafe(int bit) => componentPools[bit];
    }
}