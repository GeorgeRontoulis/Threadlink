namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FlowEvent
    {
        public enum Tag : byte
        {
            LobbyCreated,
            LobbyJoined,
            PlayerJoined,
            PlayerLeft,
            Disconnected
        }

        public readonly Tag EventTag;
        public readonly int PlayerIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FlowEvent(Tag tag, int playerIndex)
        {
            EventTag = tag;
            PlayerIndex = playerIndex;
        }
    }
}
