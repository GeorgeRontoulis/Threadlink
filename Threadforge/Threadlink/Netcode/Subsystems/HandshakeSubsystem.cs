namespace Threadlink.Netcode
{
    using System;
    using Threadlink.Core;
    using Threadlink.ECS;

    public sealed class HandshakeSubsystem : ThreadlinkSubsystem<HandshakeSubsystem>
    {
        public override void Boot()
        {
            base.Boot();

            if (NetworkRouter.TryGetSingleton(out var router))
                router.Register(GamePayloadHeader.ConnectionHandshake, RPC_ProcessHandshake);
        }

        private unsafe void RPC_ProcessHandshake(in Entity _, IntPtr dataPtr, int size)
        {
            if (!Netrunner.TryGetSingleton(out var netrunner) || netrunner.IsHost)
                return;

            // Offset the pointer by the Identity Header size to read the exact struct memory block
            var payloadPtr = (ConnectionAcceptedPayload*)(dataPtr + NetworkPayloadIdentity.Size).ToPointer();

            // Apply authoritative synchronization
            netrunner.LocalPlayerIndex = payloadPtr->AssignedPlayerIndex;
            netrunner.CurrentTick = payloadPtr->InitialTick;

            // The client is officially synchronized and enters the simulation flow
            netrunner.PushFlowEvent(FlowEvent.Tag.LobbyJoined, netrunner.LocalPlayerIndex);
        }
    }
}