namespace Threadlink.ECS
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe sealed class ECSWorld : ThreadlinkSubsystem<ECSWorld>
    {
        public const int MAX_COMPONENT_COUNT = 256;

        private UnsafeList<int> generations = default;
        private UnsafeList<int> availableIDs = default;
        private UnsafeList<ComponentMask> masks = default;
        private Dictionary<int, IComponentPool> componentPools = null;

        public override void Discard()
        {
            generations.DisposeSafely();
            availableIDs.DisposeSafely();
            masks.DisposeSafely();

            if (componentPools != null)
            {
                foreach (var pool in componentPools.Values)
                    pool.Dispose();

                componentPools.Clear();
                componentPools.TrimExcess();
                componentPools = null;
            }

            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            const Allocator ALLOC_PERSIST = Allocator.Persistent;

            generations = new(2, ALLOC_PERSIST);
            availableIDs = new(2, ALLOC_PERSIST);
            masks = new(2, ALLOC_PERSIST, NativeArrayOptions.ClearMemory);
            componentPools = new(MAX_COMPONENT_COUNT);

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
            if (!IsValid(entity))
                return;

            var index = entity.ID;
            generations[index]++;
            availableIDs.Add(index);

            ref var mask = ref masks.ElementAt(index);

            foreach (var componentBit in mask)
                componentPools[componentBit].Remove(entity);

            mask = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* Add<T>(in Entity entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                return null;

            var componentBit = GetBitOf<T>();

            if (!componentPools.ContainsKey(componentBit))
                componentPools[componentBit] = new ComponentPool<T>(masks.Length);

            masks.ElementAt(entity.ID).Set(componentBit);

            return ((ComponentPool<T>)componentPools[componentBit]).Add(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove<T>(in Entity entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                return false;

            int bit = GetBitOf<T>();
            var index = entity.ID;

            if (!masks.ElementAt(index).Has(bit))
                return false;

            masks.ElementAt(index).Clear(bit);
            return TryGetPool(out ComponentPool<T> pool) && pool.Remove(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>(in Entity entity) where T : unmanaged, IComponent
        {
            return IsValid(entity) && masks.ElementAt(entity.ID).Has(GetBitOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer<T>(in Entity entity, out T* result) where T : unmanaged, IComponent
        {
            if (IsValid(entity)
            && TryGetPool(out ComponentPool<T> pool)
            && pool.TryGetPointer(entity, out var ptr))
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
            if (componentPools.TryGetValue(GetBitOf<T>(), out var pool) && pool is ComponentPool<T> typedPool)
            {
                result = typedPool;
                return true;
            }

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBitOf<T>() where T : unmanaged, IComponent => ComponentType.Of<T>.Bit;
    }
}