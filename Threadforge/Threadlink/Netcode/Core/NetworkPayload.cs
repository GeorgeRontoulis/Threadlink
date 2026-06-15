namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntitySpawnPayload : INetworkedComponent
    {
        public enum ActionType : byte
        {
            Despawn,
            Spawn
        }

        public readonly int NetworkID;
        public readonly int OwnerIndex;
        public readonly ActionType Action;

        public uint NetworkTick { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntitySpawnPayload(int networkID, int playerIndex, ActionType action, uint tick)
        {
            NetworkID = networkID;
            OwnerIndex = playerIndex;
            Action = action;
            NetworkTick = tick;
        }

        public readonly void Dispose() { }
    }

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
        public static NetworkPayloadIdentity ForState(GamePayloadHeader header, int networkID)
        {
            return new(header, networkID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkPayloadIdentity ForRPC(GamePayloadHeader header)
        {
            return new(header, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NetworkPayloadIdentity(GamePayloadHeader header, int networkID)
        {
            HeaderID = unchecked((byte)header);
            NetworkID = networkID;
        }
    }
}
