namespace Threadlink.Netcode
{
    using Core;
    using Core.NativeSubsystems.Iris;
    using Core.NativeSubsystems.Scribe;
    using Shared;
    using Steamworks;
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;
    using Utilities.Netcode;

    public sealed class Netrunner : ThreadlinkSubsystem<Netrunner>, IDisposable
    {
        private const ThreadlinkIDs.Iris.Events NETWORK_TICK_EVENT = ThreadlinkIDs.Iris.Events.OnUpdate;
        private const int MAX_MESSAGES = 32;
        public const int TICK_RATE = 30; //30 TPS

        public bool IsHost
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => listenSocket != HSteamListenSocket.Invalid;
        }

        /// <summary>
        /// Retrieves the singular Host connection.
        /// Returns Invalid if this instance IS the Host, or if disconnected.
        /// </summary>
        public HSteamNetConnection HostConnection
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsHost || activeConnections.IsEmpty)
                    return HSteamNetConnection.Invalid;

                // For a client, the only active connection is the Host
                foreach (var conn in activeConnections)
                    return conn;

                return HSteamNetConnection.Invalid;
            }
        }

        private Callback<SteamNetConnectionStatusChangedCallback_t> ConnectionStatusChangedCallback { get; set; } = null;

        private readonly IntPtr[] MessagesBuffer = new IntPtr[MAX_MESSAGES];
        private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        private UnsafeHashSet<HSteamNetConnection> activeConnections = default;

        private double tickAccumulator = 0.0;
        private double lastUpdateTime = 0.0;

        // Expose a global tick counter so the Host can stamp its outgoing packets
        public uint CurrentTick { get; private set; }

        public event Action<HSteamNetConnection> OnClientConnected = null;
        public event Action OnConnectedToHost;
        public event Action<HSteamNetConnection> OnClientDisconnected = null;
        public event Action<HSteamNetConnection, ReadOnlyMemory<byte>> OnNetworkPayloadReceived = null;
        public event Action OnNetworkTick = null;

        #region Threadlink Lifecycle API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (activeConnections.IsCreated)
            {
                foreach (var connection in activeConnections)
                {
                    if (connection != HSteamNetConnection.Invalid)
                        SteamNetworkingSockets.CloseConnection(connection, 0, "Application Quit", false);
                }

                activeConnections.Dispose();
            }

            if (listenSocket != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket);
                listenSocket = HSteamListenSocket.Invalid;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            Iris.Unsubscribe<Action>(NETWORK_TICK_EVENT, TickNetwork);
            Dispose();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            base.Boot();
            activeConnections = new(4, Allocator.Persistent);
            lastUpdateTime = Time.realtimeSinceStartupAsDouble;
            InitializeSteamworks();
            this.GuardAgainstEditorMemoryLeaks();
        }
        #endregion

        #region Steamworks Connectivity API:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSteamworks()
        {
            if (!Packsize.Test())
                this.Send("[Steamworks.NET] Packsize Test: The wrong version of Steamworks.NET is being run in this platform.").ToUnityConsole(DebugType.Error);

            if (!DllCheck.Test())
                this.Send("[Steamworks.NET] DllCheck Test: One or more of the Steamworks binaries seems to be the wrong version.").ToUnityConsole(DebugType.Error);

            try
            {
                if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
                {
                    Application.Quit();
                    return;
                }
            }
            catch (DllNotFoundException e)
            {
                // We catch this exception here, as it will be the first occurrence of it.
                this.Send($"[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib: {e}").ToUnityConsole(DebugType.Error);

                Application.Quit();
                return;
            }

            if (!SteamAPI.Init())
            {
                this.Send("[Steamworks.NET] SteamAPI_Init() failed.").ToUnityConsole(DebugType.Error);
                return;
            }
            else
            {
                //We're assigning this callback to a member to prevent GC from collecting it.
                ConnectionStatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
                Iris.Subscribe<Action>(NETWORK_TICK_EVENT, TickNetwork);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartHosting(int virtualPort = 0)
        {
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            Scribe.Send<Netrunner>("Hosting Game! Listening for P2P connections...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToHost(CSteamID hostSteamID, int virtualPort = 0)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(hostSteamID);

            if (activeConnections.Add(SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null)))
                Scribe.Send<Netrunner>($"Connected to Host: {hostSteamID}...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToHost(ulong hostSteamID, int virtualPort = 0)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(hostSteamID));

            if (activeConnections.Add(SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null)))
                Scribe.Send<Netrunner>($"Connected to Host: {hostSteamID}...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartHostingLocal(ushort port = 27015)
        {
            // Instead of listening for SteamIDs, we listen on a standard local port
            var localAddress = new SteamNetworkingIPAddr();
            localAddress.Clear(); // Clears to 'any IP', meaning it listens locally
            localAddress.SetIPv4(0, port);

            listenSocket = SteamNetworkingSockets.CreateListenSocketIP(ref localAddress, 0, null);
            Scribe.Send<Netrunner>($"Listening for Local IP connections on port {port}...").ToUnityConsole();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectToHostLocal(string ipAddress = "127.0.0.1", ushort port = 27015)
        {
            var serverAddress = new SteamNetworkingIPAddr();
            serverAddress.Clear();
            serverAddress.ParseString(ipAddress);
            serverAddress.SetIPv4(serverAddress.GetIPv4(), port);

            if (activeConnections.Add(SteamNetworkingSockets.ConnectByIPAddress(ref serverAddress, 0, null)))
                Scribe.Send<Netrunner>($"Connected to Local Host: {ipAddress}:{port}...").ToUnityConsole();
        }
        #endregion

        #region Core Networking:
        private void TickNetwork()
        {
            // Using double to maintain precision even during long sessions.
            double currentTime = Time.realtimeSinceStartupAsDouble;
            double deltaTime = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;

            tickAccumulator += deltaTime;
            double tickInterval = 1.0 / TICK_RATE;

            // Run the actual network logic per tick interval.
            while (tickAccumulator >= tickInterval)
            {
                tickAccumulator -= tickInterval;
                ++CurrentTick;

                SteamAPI.RunCallbacks();
                ReceiveData();
                OnNetworkTick?.Invoke();
            }
        }

        /// <summary>
        /// Bypasses the <see cref="NetworkSerializer"/> to broadcast a manually-packed memory span to all connections.
        /// </summary>
        public unsafe void SendRaw(ReadOnlyMemory<byte> memoryPayload, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        {
            if (memoryPayload.IsEmpty) return;

            int payloadSize = memoryPayload.Length;
            int reliabilityFlag = reliability is NetMsgReliability.Reliable ?
            Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

            fixed (byte* dataPtr = memoryPayload.Span)
            {
                foreach (var connection in activeConnections)
                {
                    if (connection != HSteamNetConnection.Invalid)
                        SteamNetworkingSockets.SendMessageToConnection(connection, (IntPtr)dataPtr, (uint)payloadSize, reliabilityFlag, out _);
                }
            }
        }

        /// <summary>
        /// Bypasses the <see cref="NetworkSerializer"/> to send a manually-packed memory span to a specific connection.
        /// </summary>
        public unsafe void SendRawTo(HSteamNetConnection targetClient, ReadOnlyMemory<byte> memoryPayload, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        {
            if (targetClient == HSteamNetConnection.Invalid || memoryPayload.IsEmpty)
                return;

            int payloadSize = memoryPayload.Length;
            int reliabilityFlag = reliability is NetMsgReliability.Reliable ?
            Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

            fixed (byte* dataPtr = memoryPayload.Span)
                SteamNetworkingSockets.SendMessageToConnection(targetClient, (IntPtr)dataPtr, (uint)payloadSize, reliabilityFlag, out _);
        }

        /// <summary>
        /// Packs and sends the payload to all connected clients.
        /// </summary>
        public unsafe void Send<T>(in NetworkPayloadIdentity payloadIdentity, T payloadData, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        where T : unmanaged
        {
            if (!NetworkSerializer.TryGetSingleton(out var serializer))
                return;

            var memoryPayload = serializer.Serialize(payloadIdentity, payloadData);

            if (memoryPayload.IsEmpty)
                return;

            try
            {
                int payloadSize = memoryPayload.Length;

                foreach (var connection in activeConnections)
                {
                    if (connection == HSteamNetConnection.Invalid || payloadSize <= 0)
                        continue;

                    int reliabilityFlag = reliability is NetMsgReliability.Reliable ?
                    Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

                    fixed (byte* dataPtr = memoryPayload.Span)
                        SteamNetworkingSockets.SendMessageToConnection(connection, (IntPtr)dataPtr, (uint)payloadSize, reliabilityFlag, out _);
                }
            }
            finally
            {
                memoryPayload.Deallocate();
            }
        }

        /// <summary>
        /// Packs a payload and routes it to a specific client. Typically used for RPCs and Late Joiner syncs.
        /// </summary>
        public unsafe void SendTo<T>(HSteamNetConnection targetClient, in NetworkPayloadIdentity payloadIdentity,
        T payloadData, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        where T : unmanaged
        {
            if (targetClient == HSteamNetConnection.Invalid || !NetworkSerializer.TryGetSingleton(out var serializer))
                return;

            var memoryPayload = serializer.Serialize(payloadIdentity, payloadData);

            if (memoryPayload.IsEmpty)
                return;

            try
            {
                int payloadSize = memoryPayload.Length;
                int reliabilityFlag = reliability is NetMsgReliability.Reliable ?
                Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;

                fixed (byte* dataPtr = memoryPayload.Span)
                    SteamNetworkingSockets.SendMessageToConnection(targetClient, (IntPtr)dataPtr, (uint)payloadSize, reliabilityFlag, out _);
            }
            finally
            {
                memoryPayload.Deallocate();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ReceiveData()
        {
            foreach (var connection in activeConnections)
            {
                if (connection == HSteamNetConnection.Invalid)
                    continue;

                Array.Fill(MessagesBuffer, IntPtr.Zero);
                int messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, MessagesBuffer, MAX_MESSAGES);

                for (int i = 0; i < messageCount; i++)
                {
                    ref var msgPtr = ref MessagesBuffer[i];

                    // Direct pointer cast avoids all GC allocations
                    var netMessage = (SteamNetworkingMessage_t*)msgPtr;
                    int msgSize = netMessage->m_cbSize;

                    var rentedBuffer = ArrayPool<byte>.Shared.Rent(msgSize);
                    Marshal.Copy(netMessage->m_pData, rentedBuffer, 0, msgSize);

                    var validMemory = new ReadOnlyMemory<byte>(rentedBuffer, 0, msgSize);
                    OnNetworkPayloadReceived?.Invoke(connection, validMemory);

                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                    SteamNetworkingMessage_t.Release(msgPtr);
                }
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
        {
            var info = callback.m_info;
            var newState = info.m_eState;
            var connection = callback.m_hConn;

            if (newState is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                // Only accept incoming connections targeting the host's listen socket
                if (info.m_hListenSocket != HSteamListenSocket.Invalid)
                    SteamNetworkingSockets.AcceptConnection(connection);
            }
            else if (newState is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                bool isNewConnection = activeConnections.Add(connection);

                if (IsHost)
                {
                    if (isNewConnection)
                        OnClientConnected?.Invoke(connection);
                }
                else OnConnectedToHost?.Invoke();
            }
            else if (newState is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
            || newState is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                ///Client dropped out. Remove them, fire the cleanup event, and sever the socket.
                if (activeConnections.IsCreated && activeConnections.Remove(connection))
                    OnClientDisconnected?.Invoke(connection);

                SteamNetworkingSockets.CloseConnection(connection, info.m_eEndReason, info.m_szEndDebug, false);
            }
        }
        #endregion
    }
}