namespace Threadlink.Netcode
{
    using Steamworks;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;
    using Threadlink.Utilities.ECS;

    public partial class Netrunner
    {
        #region Remote (Steam P2P)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HostRemotely(int virtualPort = 0)
        {
            LocalPlayerIndex = 0;

            if (connections.IsCreated && 0.IsWithinBoundsOf(connections))
                connections[0] = TransportConnectionHandle.Invalid; // Host uses the listen socket

            listenSocket = transport.HostP2P(virtualPort);
            Scribe.Send<Netrunner>("Hosting Game! Listening for P2P connections...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToRemoteHost(CSteamID hostSteamID, int virtualPort = 0)
        {
            ConnectToRemoteHost(hostSteamID.m_SteamID, virtualPort);
            Scribe.Send<Netrunner>($"Connected to Host: {hostSteamID}. Awaiting Handshake...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToRemoteHost(ulong hostId, int virtualPort = 0)
        {
            var outbound = transport.ConnectP2P(hostId, virtualPort);

            if (connections.IsCreated && 0.IsWithinBoundsOf(connections))
                connections[0] = outbound;
        }

        #endregion

        #region Local Machine Testing

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HostLocally(ushort port = 27015)
        {
            LocalPlayerIndex = 0;

            if (connections.IsCreated && 0.IsWithinBoundsOf(connections))
                connections[0] = TransportConnectionHandle.Invalid;

            listenSocket = transport.HostIP(port);
            Scribe.Send<Netrunner>($"Listening for Local IP connections on port {port}...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToLocalHost(ushort port = 27015)
        {
            var outbound = transport.ConnectIP(0x7F000001, port); // 127.0.0.1

            if (connections.IsCreated && 0.IsWithinBoundsOf(connections))
                connections[0] = outbound;

            Scribe.Send<Netrunner>($"Initiated local loopback transport to Host. Waiting for Authoritative Handshake...").ToUnityConsole();
        }

        #endregion
    }
}
