namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Utilities.Collections;

    public partial class Netrunner
    {
        public const ushort MAX_PLAYERS = 4;

        public bool IsHost
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => listenSocket.IsValid;
        }

        public TransportConnectionHandle HostConnection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsHost || !0.IsWithinBoundsOf(connections) ? TransportConnectionHandle.Invalid : connections[0];
        }

        public int LocalPlayerIndex { get; internal set; } = -1;

        private TransportConnectionHandle listenSocket = TransportConnectionHandle.Invalid;
        private UnsafeHashMap<TransportConnectionHandle, int> playerIndicesMap = default;
        private NativeArray<TransportConnectionHandle> connections = default;
        private UnsafeQueue<int> availablePlayerIndices = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposeConnectivity()
        {
            if (connections.IsCreated)
            {
                int length = connections.Length;

                for (int i = 0; i < length; i++)
                {
                    var conn = connections[i];
                    if (conn.IsValid)
                        transport?.CloseConnection(conn, 0, "Application Quit", false);
                }

                connections.Dispose();
            }

            playerIndicesMap.DisposeSafely();
            availablePlayerIndices.DisposeSafely();

            if (listenSocket.IsValid)
            {
                transport?.CloseListenSocket(listenSocket);
                listenSocket = TransportConnectionHandle.Invalid;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BootConnectivity()
        {
            for (int i = 1; i < MAX_PLAYERS; i++) // 0 is reserved for the host
                availablePlayerIndices.Enqueue(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryRegisterConnection(TransportConnectionHandle connection, out int playerIndex)
        {
            if (connections.IsCreated
            && !availablePlayerIndices.IsEmpty()
            && playerIndicesMap.IsCreated
            && availablePlayerIndices.TryDequeue(out playerIndex)
            && playerIndex.IsWithinBoundsOf(connections))
            {
                playerIndicesMap[connection] = playerIndex;
                connections[playerIndex] = connection;
                return true;
            }

            playerIndex = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryUnregisterConnection(TransportConnectionHandle connection, out int playerIndex)
        {
            if (connections.IsCreated
            && TryGetPlayerIndexOf(connection, out playerIndex)
            && playerIndicesMap.Remove(connection)
            && playerIndex.IsWithinBoundsOf(connections))
            {
                connections[playerIndex] = TransportConnectionHandle.Invalid;
                availablePlayerIndices.Enqueue(playerIndex);
                return true;
            }

            playerIndex = -1;
            return false;
        }

        private void OnTransportConnectionStatusChanged(
            TransportConnectionHandle connection,
            TransportConnectionState state,
            TransportConnectionHandle listenSocketHandle,
            int endReason,
            string endDebug)
        {
            switch (state)
            {
                case TransportConnectionState.Connecting:
                    if (listenSocketHandle.IsValid)
                        transport.AcceptConnection(connection);
                    break;

                case TransportConnectionState.Connected:
                    if (IsHost && TryRegisterConnection(connection, out int playerIndex))
                    {
                        var payload = new ConnectionAcceptedPayload(CurrentTick, playerIndex);
                        var identity = NetworkPayloadIdentity.ForRPC(GamePayloadHeader.ConnectionHandshake);
                        SendTo(playerIndex, in identity, in payload, NetMsgReliability.Reliable);
                        PushFlowEvent(FlowEvent.Tag.PlayerJoined, playerIndex);
                    }
                    break;

                case TransportConnectionState.ClosedByPeer:
                case TransportConnectionState.ProblemDetected:
                    if (TryUnregisterConnection(connection, out int leavingIndex))
                    {
                        var tag = state is TransportConnectionState.ClosedByPeer ?
                        FlowEvent.Tag.PlayerLeft : FlowEvent.Tag.Disconnected;

                        sessionFlowEventsBuffer.Push(new FlowEvent(tag, leavingIndex));
                        transport.CloseConnection(connection, endReason, endDebug, false);
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlayerIndexOf(TransportConnectionHandle connection, out int result)
        {
            if (!playerIndicesMap.IsEmpty && playerIndicesMap.TryGetValue(connection, out result))
                return true;

            result = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlayerConnectionAt(int playerIndex, out TransportConnectionHandle result)
        {
            if (connections.IsCreated && playerIndex.IsWithinBoundsOf(connections))
            {
                result = connections[playerIndex];
                return true;
            }

            result = TransportConnectionHandle.Invalid;
            return false;
        }
    }
}
