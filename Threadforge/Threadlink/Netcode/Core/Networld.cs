namespace Threadlink.Netcode
{
    using Core;
    using ECS;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Utilities.ECS;
    using Utilities.Netcode;
    using Steamworks;

    public sealed class Networld : ThreadlinkSubsystem<Networld>, IDisposable
    {
        private UnsafeHashMap<int, Entity> networkedEntities = default;

        private int globalNetworkIDCounter = 0;
        private int globalNetworkIDWrapped = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            networkedEntities.DisposeSafely();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            Dispose();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            networkedEntities = new(4096, Allocator.Persistent);

            this.GuardAgainstEditorMemoryLeaks();
            base.Boot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(int networkID, Entity ecsEntity) => networkedEntities[networkID] = ecsEntity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterEntity(int networkID) => networkedEntities.Remove(networkID);

        /// <summary>
        /// Safely generates a globally unique ID using hardware-level atomics.
        /// </summary>
        private int GenerateID(ref UnsafeHashMap<int, Entity> entities, ref int targetCounter, ref int wrapFlag)
        {
            int current, nextID;

            //1. Atomic CAS Loop: Safely increments and wraps directly to 1, skipping 0 entirely (0 = RPCs).
            do
            {
                current = targetCounter;
                nextID = current == int.MaxValue ? 1 : current + 1;
            }
            while (Interlocked.CompareExchange(ref targetCounter, nextID, current) != current);

            //2. Trip the wrap flag the first time we loop back to 1.
            if (nextID == 1)
                Interlocked.Exchange(ref wrapFlag, 1);

            //3. Collision resolution for wrapped IDs.
            if (Volatile.Read(ref wrapFlag) == 1)
            {
                while (entities.ContainsKey(nextID))
                {
                    //Must use the same CAS loop to safely skip taken IDs
                    do
                    {
                        current = targetCounter;
                        nextID = current == int.MaxValue ? 1 : current + 1;
                    }
                    while (Interlocked.CompareExchange(ref targetCounter, nextID, current) != current);
                }
            }

            return nextID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GenerateNetworkID() => GenerateID(ref networkedEntities, ref globalNetworkIDCounter, ref globalNetworkIDWrapped);

        /// <summary>
        /// HOST ONLY: Generates a new authoritative network entity.
        /// Outputs the entity's network ID.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Entity CreateAuthoritativeEntity(out int newNetworkID)
        {
            if (!ECSWorld.TryGetSingleton(out var world))
            {
                newNetworkID = -1;
                return default;
            }

            var entity = world.CreateNewEntity();
            var netEntityPtr = world.Add<NetworkEntity>(entity);

            newNetworkID = GenerateNetworkID();
            netEntityPtr->NetworkID = newNetworkID;
            netEntityPtr->BelongsToHost = true;

            RegisterEntity(newNetworkID, entity);
            return entity;
        }

        /// <summary>
        /// CLIENT ONLY: Accepts an ID from a network packet and generates the corresponding network entity for the client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Entity CreateReplicatedEntity(int authoritativeNetworkID)
        {
            if (!ECSWorld.TryGetSingleton(out var world))
                return default;

            var entity = world.CreateNewEntity();
            var netEntityPtr = world.Add<NetworkEntity>(entity);

            netEntityPtr->NetworkID = authoritativeNetworkID;
            netEntityPtr->BelongsToHost = false;

            RegisterEntity(authoritativeNetworkID, entity);
            return entity;
        }

        /// <summary>
        /// Safely tears down the network routing and destroys the ECS entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyNetworkEntity(int networkId)
        {
            if (ECSWorld.TryGetSingleton(out var world) && networkedEntities.TryGetValue(networkId, out Entity entity))
            {
                UnregisterEntity(networkId);
                world.Destroy(entity);
            }
        }

        /// <summary>
        /// Safely retrieves the local ECS entity associated with a global Network ID.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLocalEntity(int networkID, out Entity entity)
        {
            if (networkedEntities.IsCreated)
                return networkedEntities.TryGetValue(networkID, out entity) && entity.IsValid();
            else
            {
                entity = default;
                return false;
            }
        }
    }
}
