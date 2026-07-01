namespace Threadlink.Netcode
{
    using Core;
    using ECS;
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Utilities.Netcode;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Utilities.Collections;
    using Utilities.ECS;

    public sealed class Networld : ThreadlinkSubsystem<Networld>, IDisposable
    {
        private UnsafeHashMap<int, Entity> networkedEntities;

        private int globalNetworkIDCounter = 0;
        private bool globalNetworkIDWrapped = false;

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
            base.Boot();
            networkedEntities = new UnsafeHashMap<int, Entity>(4096, Allocator.Persistent);
            globalNetworkIDCounter = 0;
            globalNetworkIDWrapped = false;

            this.PreventEditorMemoryLeaks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(int networkID, Entity ecsEntity)
        {
            if (networkedEntities.IsCreated)
                networkedEntities[networkID] = ecsEntity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterEntity(int networkID)
        {
            if (networkedEntities.IsCreated)
                networkedEntities.Remove(networkID);
        }

        /// <summary>
        /// Generates a globally unique ID deterministically.
        /// MUST be called from the main thread or a single, synchronized deterministic context.
        /// </summary>
        private int GenerateNetworkID()
        {
            int nextID;

            do
            {
                nextID = globalNetworkIDCounter == int.MaxValue ? 1 : ++globalNetworkIDCounter;

                if (nextID == 1)
                    globalNetworkIDWrapped = true;

            }
            while (globalNetworkIDWrapped && networkedEntities.ContainsKey(nextID));

            return nextID;
        }

        /// <summary>
        /// HOST ONLY: Generates a new authoritative network entity.
        /// Outputs the entity's network ID.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Entity CreateAuthoritativeEntity(out int newNetworkID)
        {
            if (!ThreadlinkNetcode.IsHost || !ECSWorld.TryGetSingleton(out var world))
            {
                newNetworkID = -1;
                return default;
            }

            var entity = world.CreateNewEntity();
            var netEntityPtr = world.Add<NetworkEntity>(entity);

            newNetworkID = GenerateNetworkID();
            netEntityPtr->NetworkID = newNetworkID;

            RegisterEntity(newNetworkID, entity);
            return entity;
        }

        /// <summary>
        /// CLIENT ONLY: Accepts an ID from a network packet and generates the corresponding network entity for the client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Entity CreateReplicatedEntity(int authoritativeNetworkID)
        {
            if (ThreadlinkNetcode.IsHost || !ECSWorld.TryGetSingleton(out var world))
                return default;

            var entity = world.CreateNewEntity();
            var netEntityPtr = world.Add<NetworkEntity>(entity);

            netEntityPtr->NetworkID = authoritativeNetworkID;
            RegisterEntity(authoritativeNetworkID, entity);
            return entity;
        }

        /// <summary>
        /// Safely tears down the network routing and destroys the ECS entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyNetworkEntity(int networkId)
        {
            if (networkedEntities.IsCreated && networkedEntities.TryGetValue(networkId, out Entity entity))
            {
                UnregisterEntity(networkId);

                if (ECSWorld.TryGetSingleton(out var world))
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

            entity = default;
            return false;
        }
    }
}