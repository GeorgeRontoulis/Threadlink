namespace Threadlink.Netcode
{
    using Steamworks;
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;
    using Threadlink.Core;
    using Threadlink.Utilities.ECS;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    public sealed class SteamAuthenticator : ThreadlinkSubsystem<SteamAuthenticator>, IDisposable
    {
        public event Action<HSteamNetConnection, ulong> OnRemoteClientAuthenticated;
        public event Action<int> OnLocalClientAuthenticationAccepted;
        public event Action<string> OnAuthenticationFailed;

        private Callback<ValidateAuthTicketResponse_t> authResponseCallback;

        // Maps the pending SteamID to the unverified network socket
        private UnsafeHashMap<ulong, HSteamNetConnection> pendingVerifications;
        private HAuthTicket localAuthTicket = HAuthTicket.Invalid;

        public void Dispose()
        {
            if (localAuthTicket != HAuthTicket.Invalid)
            {
                SteamUser.CancelAuthTicket(localAuthTicket);
                localAuthTicket = HAuthTicket.Invalid;
            }

            pendingVerifications.DisposeSafely();
        }

        public override void Discard()
        {
            Dispose();
            base.Discard();
        }

        public override void Boot()
        {
            base.Boot();
            pendingVerifications = new UnsafeHashMap<ulong, HSteamNetConnection>(16, Allocator.Persistent);
            authResponseCallback = Callback<ValidateAuthTicketResponse_t>.Create(OnAuthTicketValidated);

            if (NetworkRouter.TryGetSingleton(out var router))
            {
                router.Register(SystemsPayloadHeader.AuthTicketSubmission, ProcessAuthTicketSubmission);
                router.Register(SystemsPayloadHeader.AuthTicketVerification, ProcessAuthTicketVerification);
                router.Register(SystemsPayloadHeader.AuthTicketRejection, ProcessAuthTicketRejection);
            }
        }

        #region Client Flow:
        public unsafe void AuthenticateToServer()
        {
            if (!Netrunner.TryGetSingleton(out var netrunner) || netrunner.HostConnection == HSteamNetConnection.Invalid)
            {
                Debug.LogError("[Authenticator] Cannot authenticate. No active host connection found.");
                return;
            }

            // 1. Retrieve the Host's identity from the active P2P socket
            SteamNetworkingSockets.GetConnectionInfo(netrunner.HostConnection, out var info);
            var targetHostIdentity = info.m_identityRemote;

            var ticketBuffer = ArrayPool<byte>.Shared.Rent(1024);

            // 2. Generate the securely locked Auth Ticket using the corrected API
            localAuthTicket = SteamUser.GetAuthSessionTicket(ticketBuffer, 1024, out uint ticketLength, ref targetHostIdentity);

            if (localAuthTicket == HAuthTicket.Invalid)
            {
                Debug.LogError("[Authenticator] Steam failed to generate a valid Auth Ticket.");
                ArrayPool<byte>.Shared.Return(ticketBuffer);
                return;
            }

            int totalSize = NetworkPayloadIdentity.Size + (int)ticketLength;
            var payloadBuffer = ArrayPool<byte>.Shared.Rent(totalSize);
            var span = payloadBuffer.AsSpan(0, totalSize);

            // 3. Stamp using the internal System Header
            var identity = NetworkPayloadIdentity.ForSystemsPayload(SystemsPayloadHeader.AuthTicketSubmission);
            MemoryMarshal.Write(span, ref identity);

            // 4. Append variable-length ticket bytes
            ticketBuffer.AsSpan(0, (int)ticketLength).CopyTo(span[NetworkPayloadIdentity.Size..]);

            // 5. Transmit the payload reliably
            netrunner.SendRaw(new ReadOnlyMemory<byte>(payloadBuffer, 0, totalSize), NetMsgReliability.Reliable);

            ArrayPool<byte>.Shared.Return(payloadBuffer);
            ArrayPool<byte>.Shared.Return(ticketBuffer);
        }

        private unsafe void ProcessAuthTicketVerification(HSteamNetConnection sender, ReadOnlyMemory<byte> payload)
        {
            // Extract the Assigned Network ID stamped by the Host
            int assignedNetworkID = MemoryMarshal.Read<int>(payload.Span[NetworkPayloadIdentity.Size..]);
            OnLocalClientAuthenticationAccepted?.Invoke(assignedNetworkID);
        }

        private void ProcessAuthTicketRejection(HSteamNetConnection sender, ReadOnlyMemory<byte> payload)
        {
            OnAuthenticationFailed?.Invoke("Host rejected authentication.");
        }
        #endregion

        #region Host Flow:
        private unsafe void ProcessAuthTicketSubmission(HSteamNetConnection sender, ReadOnlyMemory<byte> payload)
        {
            if (Netrunner.TryGetSingleton(out var netrunner) && !netrunner.IsHost)
                return;

            SteamNetworkingSockets.GetConnectionInfo(sender, out var info);
            ulong steamID = info.m_identityRemote.GetSteamID().m_SteamID;

            var ticketSpan = payload.Span[NetworkPayloadIdentity.Size..];
            var ticketArray = ArrayPool<byte>.Shared.Rent(ticketSpan.Length);
            ticketSpan.CopyTo(ticketArray);

            pendingVerifications.Add(steamID, sender);

            var result = SteamUser.BeginAuthSession(ticketArray, ticketSpan.Length, info.m_identityRemote.GetSteamID());
            ArrayPool<byte>.Shared.Return(ticketArray);

            if (result != EBeginAuthSessionResult.k_EBeginAuthSessionResultOK)
            {
                pendingVerifications.Remove(steamID);
                RejectClient(sender);
            }
        }

        private void OnAuthTicketValidated(ValidateAuthTicketResponse_t callback)
        {
            if (Netrunner.TryGetSingleton(out var netrunner) && !netrunner.IsHost)
                return;

            ulong steamID = callback.m_SteamID.m_SteamID;

            if (pendingVerifications.TryGetValue(steamID, out var connection))
            {
                pendingVerifications.Remove(steamID);

                if (callback.m_eAuthSessionResponse is EAuthSessionResponse.k_EAuthSessionResponseOK)
                {
                    OnRemoteClientAuthenticated?.Invoke(connection, steamID);
                }
                else
                {
                    Debug.LogWarning($"[Auth] Steam rejected client {steamID}. Reason: {callback.m_eAuthSessionResponse}");
                    RejectClient(connection);
                }
            }
        }

        public unsafe void AcceptClient(HSteamNetConnection client, int assignedNetworkID)
        {
            int totalSize = NetworkPayloadIdentity.Size + sizeof(int);
            var buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            var span = buffer.AsSpan(0, totalSize);

            // 1. Stamp with the Verification/Accept Header
            var identity = NetworkPayloadIdentity.ForSystemsPayload(SystemsPayloadHeader.AuthTicketVerification);
            MemoryMarshal.Write(span, ref identity);

            // 2. Pack the assigned Network ID
            MemoryMarshal.Write(span[NetworkPayloadIdentity.Size..], ref assignedNetworkID);

            // 3. Send securely ONLY to the target client using SendRawTo
            if (Netrunner.TryGetSingleton(out var netrunner))
                netrunner.SendRawTo(client, new ReadOnlyMemory<byte>(buffer, 0, totalSize), NetMsgReliability.Reliable);

            ArrayPool<byte>.Shared.Return(buffer);
        }

        private void RejectClient(HSteamNetConnection client)
        {
            // Implementation routes AuthTicketReject payload then severs the socket.
            SteamNetworkingSockets.CloseConnection(client, 0, "Auth Failed", false);
        }
        #endregion
    }
}