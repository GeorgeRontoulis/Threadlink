namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Utilities.Collections;

    public sealed class EntityOwnershipRegistry : ThreadlinkSubsystem<EntityOwnershipRegistry>, IDisposable
    {
        private UnsafeHashMap<Entity, int> entityToOwnerMap;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => entityToOwnerMap.DisposeSafely();

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
            // Allocate for expected concurrent networked entities.
            entityToOwnerMap = new(4096, Allocator.Persistent);
            this.PreventEditorMemoryLeaks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNeutral(in Entity entity) => !entityToOwnerMap.IsCreated || !entityToOwnerMap.ContainsKey(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Bind(in Entity entity, int playerIndex)
        {
            // Overwrites if the entity is transferred (e.g., player enters a neutral vehicle)
            if (entityToOwnerMap.IsCreated)
                entityToOwnerMap[entity] = playerIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Relinquish(in Entity entity) => entityToOwnerMap.IsCreated && entityToOwnerMap.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasOwnership(int playerIndex, in Entity entity)
        {
            return entityToOwnerMap.IsCreated && entityToOwnerMap.TryGetValue(entity, out int ownerIndex) && ownerIndex == playerIndex;
        }

        /// <summary>
        /// Extracts all entities owned by a specific user (critical for disconnects)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllEntitiesOwnedBy(int playerIndex, ref UnsafeList<Entity> buffer)
        {
            if (!entityToOwnerMap.IsCreated)
                return;

            using var keysAndValues = entityToOwnerMap.GetKeyValueArrays(Allocator.Temp);
            int length = keysAndValues.Length;

            for (int i = 0; i < length; i++)
            {
                if (keysAndValues.Values[i] == playerIndex)
                    buffer.Add(keysAndValues.Keys[i]);
            }
        }
    }
}