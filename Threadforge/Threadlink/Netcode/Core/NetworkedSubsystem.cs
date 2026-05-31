namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;

    /// <summary>
    /// Base class for all networked subsystems in Threadlink's Netcode.
    /// </summary>
    /// <typeparam name="Singleton">The singleton instance of the subsystem.</typeparam>
    /// <typeparam name="Payload">The ECS Component acting as the network payload.</typeparam>
    public abstract class NetworkedSubsystem<Singleton, Payload> : ThreadlinkSubsystem<Singleton>
    where Singleton : NetworkedSubsystem<Singleton, Payload>
    where Payload : unmanaged, INetworkedComponent
    {
        protected virtual NetMsgReliability OutgoingReliability
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetMsgReliability.Unreliable;
        }

        protected abstract GamePayloadHeader RequiredPayloadHeader { get; }

        public event Action<Entity, Payload> OnStateApplied;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            base.Boot();

            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(RequiredPayloadHeader, ProcessPayload);
        }

        #region Egress:
        /// <summary>
        /// Allows derived subsystems to mutate the payload before broadcasting (e.g., stamping network ticks).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void PreprocessOutgoingPayload(Netrunner netrunner, Entity entity, ref Payload payload)
        {
            payload.NetworkTick = netrunner.CurrentTick;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void BroadcastState(Entity targetEntity, Payload payloadState)
        {
            if (ECSWorld.TryGetSingleton(out var world)
            && Netrunner.TryGetSingleton(out var netrunner)
            && world.TryGetPointer(targetEntity, out NetworkEntity* netEntityPtr)
            && netEntityPtr->BelongsToHost
            && world.TryGetPointer(targetEntity, out Payload* payloadPtr))
            {
                PreprocessOutgoingPayload(netrunner, targetEntity, ref payloadState);

                // Update authoritative ECS state
                *payloadPtr = payloadState;

                var identity = NetworkPayloadIdentity.ForGamePayload(RequiredPayloadHeader, netEntityPtr->NetworkID);
                netrunner.Send(identity, payloadState, OutgoingReliability);
            }
        }
        #endregion

        #region Ingress:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void ProcessPayload(Entity targetEntity, ReadOnlyMemory<byte> payload)
        {
            if (NetworkSerializer.TryGetSingleton(out var serializer) && serializer.TryDeserialize(payload, out Payload deserializedPayload))
                ApplyState(targetEntity, deserializedPayload);
        }

        /// <summary>
        /// Implemented by derived subsystems to write data into ECS memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void ApplyState(Entity entity, Payload packetData)
        {
            OnStateApplied?.Invoke(entity, packetData);
        }
        #endregion
    }
}
