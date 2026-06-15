namespace Threadlink.Netcode
{
    using System.Runtime.InteropServices;
    using Threadlink.ECS;
    using UnityEngine.Scripting;

    [RuntimeComponent, Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ConnectionAcceptedPayload : INetworkedComponent
    {
        public uint NetworkTick
        {
            get => InitialTick;
            set => _ = 0;
        }

        public readonly uint InitialTick;
        public readonly int AssignedPlayerIndex;

        public ConnectionAcceptedPayload(uint tick, int playerIndex)
        {
            InitialTick = tick;
            AssignedPlayerIndex = playerIndex;
        }

        public void Dispose() { }
    }
}
