namespace Threadlink.ECS
{
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Utilities.Collections;

    public unsafe struct EntityCommandBuffer : IDisposable
    {
        private enum CommandType : byte { Add, Remove, Destroy }

        private struct Command
        {
            public CommandType Type;
            public Entity Entity;
            public int ComponentBit;
            public void* ComponentData;
        }

        private UnsafeList<Command> commands;
        private readonly Allocator CurrentAllocator;

        #region Main Lifecycle:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityCommandBuffer* Create(int initialCapacity, Allocator allocator)
        {
            var cmdPtr = (EntityCommandBuffer*)UnsafeUtility.Malloc(sizeof(EntityCommandBuffer), UnsafeUtility.AlignOf<EntityCommandBuffer>(), allocator);
            *cmdPtr = new(initialCapacity, allocator);
            return cmdPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Playback(ref EntityCommandBuffer* ptr)
        {
            if (ptr == null)
                return;

            ptr->Playback();
            UnsafeUtility.Free(ptr, ptr->CurrentAllocator);
            ptr = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EntityCommandBuffer(int initialCapacity, Allocator allocator)
        {
            CurrentAllocator = allocator;
            commands = new UnsafeList<Command>(initialCapacity, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            int length = commands.Length;

            for (int i = 0; i < length; i++)
            {
                ref var cmd = ref commands.ElementAt(i);

                if (cmd.ComponentData != null)
                    UnsafeUtility.Free(cmd.ComponentData, CurrentAllocator);
            }

            commands.DisposeSafely();
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(in Entity entity, in T componentData) where T : unmanaged, IComponent
        {
            var ptr = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), CurrentAllocator);
            var copy = componentData; // Pin local reference to copy
            UnsafeUtility.CopyStructureToPtr(ref copy, ptr);

            commands.Add(new Command
            {
                Type = CommandType.Add,
                Entity = entity,
                ComponentBit = ComponentType.Of<T>.BitIndex,
                ComponentData = ptr
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(in Entity entity) where T : unmanaged, IComponent
        {
            commands.Add(new Command
            {
                Type = CommandType.Remove,
                Entity = entity,
                ComponentBit = ComponentType.Of<T>.BitIndex,
                ComponentData = null
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(in Entity entity)
        {
            commands.Add(new Command
            {
                Type = CommandType.Destroy,
                Entity = entity,
                ComponentBit = -1,
                ComponentData = null
            });
        }

        private void Playback()
        {
            if (!ECSWorld.TryGetSingleton(out var world))
                return;

            int length = commands.Length;

            for (int i = 0; i < length; i++)
            {
                ref var cmd = ref commands.ElementAt(i);

                switch (cmd.Type)
                {
                    case CommandType.Add:
                        var addPool = world.GetPoolByBitUnsafe(cmd.ComponentBit);

                        if (addPool != null)
                        {
                            addPool.ApplyCommand(cmd.Entity, cmd.ComponentData);
                            world.SetMaskBitUnsafe(cmd.Entity.ID, cmd.ComponentBit);
                        }

                        break;

                    case CommandType.Remove:
                        var removePool = world.GetPoolByBitUnsafe(cmd.ComponentBit);

                        if (removePool != null && removePool.Remove(cmd.Entity))
                            world.ClearMaskBitUnsafe(cmd.Entity.ID, cmd.ComponentBit);

                        break;

                    case CommandType.Destroy:
                        world.Destroy(cmd.Entity);
                        break;
                }
            }

            Dispose();
        }
    }
}