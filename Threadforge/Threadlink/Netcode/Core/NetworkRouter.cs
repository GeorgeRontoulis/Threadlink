namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.ECS;

    public sealed class NetworkRouter : ThreadlinkSubsystem<NetworkRouter>
    {
        public delegate void NetworkRouterDelegate(in Entity entity, IntPtr dataPtr, int size);

        private readonly NetworkRouterDelegate[] GameDispatchTable = new NetworkRouterDelegate[256];

        public override void Discard()
        {
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnNetworkPayloadReceived -= RoutePayload;

            base.Discard();
        }

        public override void Boot()
        {
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.OnNetworkPayloadReceived += RoutePayload;

            base.Boot();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(GamePayloadHeader header, NetworkRouterDelegate handler)
        {
            GameDispatchTable[(byte)header] = handler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void RoutePayload(TransportConnectionHandle sender, IntPtr dataPtr, int size)
        {
            if (size < sizeof(NetworkPayloadIdentity))
            {
                this.Send("Payload size is too small to contain a valid identity.").ToUnityConsole(DebugType.Warning);
                return;
            }

            var routingData = *(NetworkPayloadIdentity*)dataPtr.ToPointer();

            try
            {
                var header = (byte)routingData.HeaderID;
                var nid = routingData.NetworkID;

                switch (nid)
                {
                    case < 0:
                        this.Send($"Payload with invalid NetworkID detected: {nid}").ToUnityConsole(DebugType.Warning);
                        return;
                    case 0:
                        GameDispatchTable[header]?.Invoke(default, dataPtr, size);
                        return;
                    default:
                        if (Networld.TryGetSingleton(out var networld) && networld.TryGetLocalEntity(nid, out var entity))
                            GameDispatchTable[header]?.Invoke(entity, dataPtr, size);
                        return;
                }
            }
            catch (Exception ex)
            {
                this.Send($"Exception during payload routing (Header: {routingData.HeaderID}): {ex.Message}").ToUnityConsole(DebugType.Error);
            }
        }
    }
}