namespace Threadlink.Netcode
{
    using Steamworks;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Utilities.Netcode;

    public sealed class NetworkRouter : ThreadlinkSubsystem<NetworkRouter>
    {
        public const int RPC_NETWORKD_ID = 0;
        public const int SYSTEMS_NETWORK_ID = int.MinValue;

        private readonly Action<Entity, ReadOnlyMemory<byte>>[] GameDispatchTable = new Action<Entity, ReadOnlyMemory<byte>>[256];
        private readonly Action<HSteamNetConnection, ReadOnlyMemory<byte>>[] SystemsDispatchTable = new Action<HSteamNetConnection, ReadOnlyMemory<byte>>[256];

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
        public void Register(GamePayloadHeader header, Action<Entity, ReadOnlyMemory<byte>> handler)
        {
            GameDispatchTable[(byte)header] = handler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(SystemsPayloadHeader header, Action<HSteamNetConnection, ReadOnlyMemory<byte>> handler)
        {
            SystemsDispatchTable[(byte)header] = handler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RoutePayload(HSteamNetConnection sender, ReadOnlyMemory<byte> payload)
        {
            if (!payload.IsValidNetworkPayload())
                return;

            var routingData = MemoryMarshal.Read<NetworkPayloadIdentity>(payload.Span);

            //Route this as a Systems command.
            if (routingData.NetworkID == int.MinValue)
            {
                SystemsDispatchTable[routingData.HeaderID]?.Invoke(sender, payload);
                return;
            }

            //Route this as an RPC.
            if (routingData.NetworkID == 0)
            {
                GameDispatchTable[routingData.HeaderID]?.Invoke(default, payload);
                return;
            }

            //Standard routing for gameplay logic.
            if (Networld.TryGetSingleton(out var networld) && networld.TryGetLocalEntity(routingData.NetworkID, out Entity targetEntity))
                GameDispatchTable[routingData.HeaderID]?.Invoke(targetEntity, payload);
        }
    }
}
