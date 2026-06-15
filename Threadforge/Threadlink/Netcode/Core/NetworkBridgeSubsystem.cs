namespace Threadlink.Netcode
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Utilities.Netcode;

    public abstract class NetworkBridgeSubsystem<Singleton, Payload> : ThreadlinkSubsystem<Singleton>
    where Singleton : NetworkBridgeSubsystem<Singleton, Payload>
    where Payload : unmanaged, INetworkedComponent
    {
        protected virtual NetMsgReliability OutgoingReliability
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetMsgReliability.Unreliable;
        }

        protected abstract GamePayloadHeader RequiredPayloadHeader { get; }

        public delegate void StateAppliedDelegate(in Entity entity, in Payload payload);
        public event StateAppliedDelegate OnStateApplied = null;

        protected readonly Dictionary<Entity, UnityNetworkBridge<Singleton, Payload>> bridgeRegistry = new(64);

        #region Threadlink Lifecycle API
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(RequiredPayloadHeader, null);

            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnNetworkTick -= OnNetworkTick;

            bridgeRegistry.Clear();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            base.Boot();

            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(RequiredPayloadHeader, ProcessPayload);

            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnNetworkTick += OnNetworkTick;
        }
        #endregion

        #region Bridge Registry
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterBridge(Entity entity, UnityNetworkBridge<Singleton, Payload> bridge)
        {
            bridgeRegistry[entity] = bridge;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterBridge(Entity entity)
        {
            bridgeRegistry.Remove(entity);
        }
        #endregion

        #region Egress (Deterministic Polling)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void PreprocessOutgoingPayload(Netrunner netrunner, Entity entity, ref Payload payload)
        {
            payload.NetworkTick = netrunner.CurrentTick;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual unsafe void OnNetworkTick()
        {
            if (bridgeRegistry.Count > 0 && ECSWorld.TryGetSingleton(out var world))
                world.ForEach<Payload>(&EvaluateEgress);
        }

        private static unsafe void EvaluateEgress(in Entity entity, Payload* payloadPtr)
        {
            if (!entity.HasLocalAuthority() || !TryGetSingleton(out var subsystem) || !Netrunner.TryGetSingleton(out var netrunner))
                return;

            if (subsystem.bridgeRegistry.TryGetValue(entity, out var bridge) && bridge.TryGetOutgoingState(out Payload state))
            {
                subsystem.PreprocessOutgoingPayload(netrunner, entity, ref state);

                *payloadPtr = state;

                if (ECSWorld.TryGetSingleton(out var world) && world.TryGetPointer(entity, out NetworkEntity* netEntityPtr))
                {
                    var identity = NetworkPayloadIdentity.ForState(subsystem.RequiredPayloadHeader, netEntityPtr->NetworkID);
                    netrunner.Send(in identity, in state, subsystem.OutgoingReliability);
                }
            }
        }

        /// <summary>
        /// For manual RPC-style state broadcasts outside the polling loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void BroadcastState(in Entity targetEntity, Payload statePayload)
        {
            if (Netrunner.TryGetSingleton(out var netrunner)
            && netrunner.IsHost
            && targetEntity.HasLocalAuthority()
            && ECSWorld.TryGetSingleton(out var world)
            && world.TryGetPointer(targetEntity, out NetworkEntity* netEntityPtr)
            && world.TryGetPointer(targetEntity, out Payload* payloadPtr))
            {
                PreprocessOutgoingPayload(netrunner, targetEntity, ref statePayload);
                *payloadPtr = statePayload;

                var identity = NetworkPayloadIdentity.ForState(RequiredPayloadHeader, netEntityPtr->NetworkID);
                netrunner.Send(in identity, in statePayload, OutgoingReliability);
            }
        }
        #endregion

        #region Ingress
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual unsafe void ProcessPayload(in Entity targetEntity, IntPtr dataPtr, int size)
        {
            if (!ThreadlinkNetcode.IsHost
            && targetEntity.HasLocalAuthority()
            && NetworkSerializer.TryGetSingleton(out var serializer)
            && serializer.TryDeserialize(dataPtr, size, out Payload deserializedPayload))
            {
                WriteToECS(in targetEntity, in deserializedPayload);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual unsafe void WriteToECS(in Entity entity, in Payload payload)
        {
            if (ECSWorld.TryGetSingleton(out var ecsWorld) && ecsWorld.TryGetPointer(in entity, out Payload* payloadPtr))
            {
                *payloadPtr = payload;

                if (bridgeRegistry.TryGetValue(entity, out var bridge))
                    bridge.ApplyNetworkStateToUnity(in entity, in payload);

                OnStateApplied?.Invoke(in entity, in payload);
            }
        }
        #endregion
    }
}