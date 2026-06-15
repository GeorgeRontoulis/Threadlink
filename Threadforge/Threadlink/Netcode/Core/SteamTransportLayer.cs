namespace Threadlink.Netcode
{
    using Steamworks;
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core.NativeSubsystems.Scribe;

    /// <summary>
    /// Steamworks.NET implementation of <see cref="ITransportLayer"/>.
    /// This is the default transport used by <see cref="Netrunner"/>.
    /// </summary>
    public sealed class SteamTransportLayer : ITransportLayer
    {
        public event TransportConnectionChangedDelegate OnConnectionStatusChanged;

        private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusCallback;

        // Local/LAN testing options (no auth required)
        private readonly SteamNetworkingConfigValue_t[] localOptions = new SteamNetworkingConfigValue_t[]
        {
            new()
            {
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_IP_AllowWithoutAuth,
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 1 }
            }
        };

        // Remote / relay options
        private readonly SteamNetworkingConfigValue_t[] remoteOptions = new SteamNetworkingConfigValue_t[]
        {
            new()
            {
                m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 10000 }
            }
        };

        #region Lifecycle

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            connectionStatusCallback?.Dispose();
            connectionStatusCallback = null;
        }

        public bool TryInit()
        {
            if (!Packsize.Test())
                Scribe.Send<Netrunner>("[Steamworks.NET] Packsize Test: wrong version running on this platform.").ToUnityConsole(DebugType.Error);

            if (!DllCheck.Test())
                Scribe.Send<Netrunner>("[Steamworks.NET] DllCheck Test: one or more Steamworks binaries appear to be the wrong version.").ToUnityConsole(DebugType.Error);

            try
            {
                if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
                {
                    UnityEngine.Application.Quit();
                    return false;
                }
            }
            catch (DllNotFoundException e)
            {
                Scribe.Send<Netrunner>($"[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib: {e}").ToUnityConsole(DebugType.Error);
                UnityEngine.Application.Quit();
                return false;
            }

            bool initialized = SteamAPI.Init();

            if (!initialized)
            {
                Scribe.Send<Netrunner>("[Steamworks.NET] SteamAPI_Init() failed.").ToUnityConsole(DebugType.Error);
                return false;
            }

            connectionStatusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(HandleConnectionStatusChanged);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RunCallbacks() => SteamAPI.RunCallbacks();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitRelayNetworkAccess() => SteamNetworkingUtils.InitRelayNetworkAccess();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Shutdown()
        {
            Dispose();
            SteamAPI.Shutdown();
        }

        #endregion

        #region Hosting

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransportConnectionHandle HostP2P(int virtualPort)
        {
            var socket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            return new(socket.m_HSteamListenSocket);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransportConnectionHandle HostIP(ushort port)
        {
            var address = new SteamNetworkingIPAddr();
            address.Clear();
            address.SetIPv4(0, port);
            var socket = SteamNetworkingSockets.CreateListenSocketIP(ref address, localOptions.Length, localOptions);
            return new(socket.m_HSteamListenSocket);
        }

        #endregion

        #region Connecting

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransportConnectionHandle ConnectP2P(ulong remoteId, int virtualPort)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(remoteId));
            var conn = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, remoteOptions.Length, remoteOptions);
            return new(conn.m_HSteamNetConnection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TransportConnectionHandle ConnectIP(uint ipv4, ushort port)
        {
            var address = new SteamNetworkingIPAddr();
            address.Clear();
            address.SetIPv4(ipv4, port);
            var conn = SteamNetworkingSockets.ConnectByIPAddress(ref address, localOptions.Length, localOptions);
            return new(conn.m_HSteamNetConnection);
        }

        #endregion

        #region Connection Management

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AcceptConnection(TransportConnectionHandle handle)
        {
            SteamNetworkingSockets.AcceptConnection(ToSteamConnection(handle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseConnection(TransportConnectionHandle handle, int reason, string debug, bool linger)
        {
            SteamNetworkingSockets.CloseConnection(ToSteamConnection(handle), reason, debug, linger);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseListenSocket(TransportConnectionHandle socket)
        {
            SteamNetworkingSockets.CloseListenSocket(new HSteamListenSocket { m_HSteamListenSocket = socket.Value });
        }

        #endregion

        #region Data Transfer

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool SendMessage(TransportConnectionHandle connection, IntPtr data, uint size, NetMsgReliability reliability)
        {
            int flag = reliability == NetMsgReliability.Reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            return SteamNetworkingSockets.SendMessageToConnection(ToSteamConnection(connection), data, size, flag, out _)
                == EResult.k_EResultOK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int ReceiveMessages(TransportConnectionHandle connection, IntPtr[] messageBuffer, int maxMessages)
        {
            return SteamNetworkingSockets.ReceiveMessagesOnConnection(ToSteamConnection(connection), messageBuffer, maxMessages);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe IntPtr GetMessageData(IntPtr nativeMessage)
            => ((SteamNetworkingMessage_t*)nativeMessage)->m_pData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int GetMessageSize(IntPtr nativeMessage)
            => ((SteamNetworkingMessage_t*)nativeMessage)->m_cbSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseMessage(IntPtr nativeMessage) => SteamNetworkingMessage_t.Release(nativeMessage);

        #endregion

        #region Steam-specific helpers (Steam-only callers may use these)

        /// <summary>Converts a <see cref="TransportConnectionHandle"/> to a Steam connection handle.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HSteamNetConnection ToSteamConnection(TransportConnectionHandle h)
            => new() { m_HSteamNetConnection = h.Value };

        #endregion

        #region Callback

        private void HandleConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
        {
            const ESteamNetworkingConnectionState CONNECTING = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting;
            const ESteamNetworkingConnectionState CONNECTED = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
            const ESteamNetworkingConnectionState CLOSED_PEER = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer;
            const ESteamNetworkingConnectionState LOCAL_PROBLEM = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;

            var state = callback.m_info.m_eState;

            TransportConnectionState mapped = state switch
            {
                CONNECTING => TransportConnectionState.Connecting,
                CONNECTED => TransportConnectionState.Connected,
                CLOSED_PEER => TransportConnectionState.ClosedByPeer,
                LOCAL_PROBLEM => TransportConnectionState.ProblemDetected,
                _ => (TransportConnectionState)255
            };

            if ((byte)mapped == 255) return;

            var connHandle = new TransportConnectionHandle(callback.m_hConn.m_HSteamNetConnection);
            var listenHandle = new TransportConnectionHandle(callback.m_info.m_hListenSocket.m_HSteamListenSocket);

            OnConnectionStatusChanged?.Invoke(connHandle, mapped, listenHandle,
                callback.m_info.m_eEndReason, callback.m_info.m_szEndDebug);
        }

        #endregion
    }
}
