namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TransportConnectionHandle : IEquatable<TransportConnectionHandle>
    {
        public static readonly TransportConnectionHandle Invalid = default;

        public readonly uint Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransportConnectionHandle(uint value) => Value = value;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TransportConnectionHandle other) => Value == other.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is TransportConnectionHandle other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (int)Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TransportConnectionHandle a, TransportConnectionHandle b) => a.Value == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TransportConnectionHandle a, TransportConnectionHandle b) => a.Value != b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"TransportHandle({Value})";
    }

    public enum TransportConnectionState : byte
    {
        Connecting,
        Connected,
        ClosedByPeer,
        ProblemDetected,
    }

    public delegate void TransportConnectionChangedDelegate(TransportConnectionHandle connection,
    TransportConnectionState state, TransportConnectionHandle listenSocket, int endReason, string endDebug);

    public interface ITransportLayer : IDisposable
    {
        event TransportConnectionChangedDelegate OnConnectionStatusChanged;

        /// <summary>Initialize the underlying SDK. Returns false if initialization fails.</summary>
        bool TryInit();

        /// <summary>Pump the transport's internal callbacks. Called every network tick.</summary>
        void RunCallbacks();

        /// <summary>Initialize relay / TURN network access if supported. No-op on non-relay transports.</summary>
        void InitRelayNetworkAccess();

        /// <summary>Gracefully shut down the SDK.</summary>
        void Shutdown();

        // ── Hosting ───────────────────────────────────────────────────────────────

        TransportConnectionHandle HostP2P(int virtualPort);
        TransportConnectionHandle HostIP(ushort port);

        // ── Connecting ────────────────────────────────────────────────────────────

        /// <param name="remoteId">Transport-specific remote peer identifier (e.g. SteamID64).</param>
        TransportConnectionHandle ConnectP2P(ulong remoteId, int virtualPort);
        TransportConnectionHandle ConnectIP(uint ipv4, ushort port);

        // ── Connection management ─────────────────────────────────────────────────

        void AcceptConnection(TransportConnectionHandle handle);
        void CloseConnection(TransportConnectionHandle handle, int reason, string debug, bool linger);
        void CloseListenSocket(TransportConnectionHandle socket);

        // ── Data transfer ─────────────────────────────────────────────────────────

        unsafe bool SendMessage(TransportConnectionHandle connection, IntPtr data, uint size, NetMsgReliability reliability);
        unsafe int ReceiveMessages(TransportConnectionHandle connection, IntPtr[] messageBuffer, int maxMessages);

        /// <summary>Returns the payload data pointer from a native message handle.</summary>
        unsafe IntPtr GetMessageData(IntPtr nativeMessage);

        /// <summary>Returns the payload byte size from a native message handle.</summary>
        unsafe int GetMessageSize(IntPtr nativeMessage);

        /// <summary>Releases ownership of a native message handle back to the transport.</summary>
        void ReleaseMessage(IntPtr nativeMessage);
    }
}
