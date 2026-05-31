namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Tightly packed struct representing the first 5 bytes of every network payload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct NetworkPayloadIdentity
    {
        public static unsafe int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sizeof(NetworkPayloadIdentity);
        }

        public readonly byte HeaderID;
        public readonly int NetworkID;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkPayloadIdentity ForGamePayload(GamePayloadHeader header, int networkID)
        {
            return new(header, networkID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkPayloadIdentity ForSystemsPayload(SystemsPayloadHeader header)
        {
            return new(header, NetworkRouter.SYSTEMS_NETWORK_ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkPayloadIdentity ForRPCPayload(GamePayloadHeader header)
        {
            return new(header, NetworkRouter.RPC_NETWORKD_ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NetworkPayloadIdentity(GamePayloadHeader header, int networkID)
        {
            HeaderID = unchecked((byte)header);
            NetworkID = networkID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NetworkPayloadIdentity(SystemsPayloadHeader header, int networkID)
        {
            HeaderID = unchecked((byte)header);
            NetworkID = networkID;
        }
    }
}
