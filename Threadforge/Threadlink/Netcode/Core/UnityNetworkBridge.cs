namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Utilities.ECS;
    using Threadlink.Utilities.Netcode;

    public abstract class UnityNetworkBridge<NS, NC> : LinkableBehaviour
    where NS : NetworkBridgeSubsystem<NS, NC>
    where NC : unmanaged, INetworkedComponent
    {
        public bool HasLocalAuthority
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => linkedNetworkedEntity.IsValid() && linkedNetworkedEntity.HasLocalAuthority();
        }

        protected Entity linkedNetworkedEntity = default;
        protected uint lastValidNetworkTick = 0;

        public override void Discard()
        {
            if (linkedNetworkedEntity.IsValid() && NetworkBridgeSubsystem<NS, NC>.TryGetSingleton(out var subsystem))
                subsystem.UnregisterBridge(linkedNetworkedEntity);

            linkedNetworkedEntity = default;
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Bind(in Entity entity)
        {
            linkedNetworkedEntity = entity;

            // Register directly with the authoritative subsystem
            if (NetworkBridgeSubsystem<NS, NC>.TryGetSingleton(out var subsystem))
                subsystem.RegisterBridge(entity, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLinkedEntity(out Entity result)
        {
            result = linkedNetworkedEntity;
            return result.IsValid();
        }

        protected internal abstract bool TryGetOutgoingState(out NC state);
        protected internal abstract void ApplyNetworkStateToUnity(in Entity entity, in NC receivedState);
    }
}