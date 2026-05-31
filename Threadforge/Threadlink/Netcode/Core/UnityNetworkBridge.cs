namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Utilities.ECS;

    /// <summary>
    /// <see cref="LinkableBehaviour"/> Bridge used for Unity-Netcode communication.
    /// </summary>
    /// <typeparam name="NS">The networked subsystem communicating with this bridge.</typeparam>
    /// <typeparam name="NC">The networked ECS component used as the network payload.</typeparam>
    public abstract class UnityNetworkBridge<NS, NC> : LinkableBehaviour
    where NS : NetworkedSubsystem<NS, NC>
    where NC : unmanaged, INetworkedComponent
    {
        public bool BelongsToHost { get; protected set; }

        protected Entity linkedNetworkedEntity = default;
        protected uint lastValidNetworkTick = 0;

        public override void Discard()
        {
            UnsubscribeFromNetworkLoop();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Bind(in Entity entity, bool belongsToHost)
        {
            linkedNetworkedEntity = entity;
            BelongsToHost = belongsToHost;
            SubscribeToNetworkLoop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLinkedEntity(out Entity result)
        {
            result = linkedNetworkedEntity;
            return result.IsValid();
        }

        protected abstract bool TryGetOutgoingState(out NC state);
        protected abstract void ApplyNetworkStateToUnity(Entity entity, NC receivedState);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendUnityStateToNetwork()
        {
            if (!linkedNetworkedEntity.IsValid())
                return;

            if (NetworkedSubsystem<NS, NC>.TryGetSingleton(out var targetSubsystem) && TryGetOutgoingState(out NC outgoingState))
                targetSubsystem.BroadcastState(linkedNetworkedEntity, outgoingState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SubscribeToNetworkLoop()
        {
            if (BelongsToHost)
            {
                if (Netrunner.TryGetSingleton(out var netrunner))
                    netrunner.OnNetworkTick += SendUnityStateToNetwork;
            }
            else
            {
                if (NetworkedSubsystem<NS, NC>.TryGetSingleton(out var targetSubsystem))
                    targetSubsystem.OnStateApplied += ApplyNetworkStateToUnity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UnsubscribeFromNetworkLoop()
        {
            if (BelongsToHost)
            {
                if (Netrunner.TryGetSingleton(out var netrunner))
                    netrunner.OnNetworkTick -= SendUnityStateToNetwork;
            }
            else
            {
                if (NetworkedSubsystem<NS, NC>.TryGetSingleton(out var targetSubsystem))
                    targetSubsystem.OnStateApplied -= ApplyNetworkStateToUnity;
            }
        }
    }
}
