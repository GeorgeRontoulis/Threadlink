namespace Threadlink.Utilities.Netcode
{
    using Steamworks;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Netcode;
    using Threadlink.Shared;
    using Threadlink.Utilities.Subsystems;

    public static class NetcodeUtils
    {
        public static unsafe NetworkEntity AsNetworkEntity(in this Entity entity)
        {
            if (ECSWorld.TryGetSingleton(out var world) && world.TryGetPointer(in entity, out NetworkEntity* netPtr))
                return *netPtr;

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasLocalAuthority(in this Entity entity)
        {
            return EntityOwnershipRegistry.TryGetSingleton(out var registry)
            && Netrunner.TryGetSingleton(out var netrunner)
            && registry.HasOwnership(netrunner.LocalPlayerIndex, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOwnedByHost(in this Entity entity)
        {
            return EntityOwnershipRegistry.TryGetSingleton(out var registry) && registry.HasOwnership(0, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNeutral(in this Entity entity)
        {
            return EntityOwnershipRegistry.TryGetSingleton(out var registry) && registry.IsNeutral(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidNetworkPayload(this byte[] payload)
        {
            return payload != null && payload.Length >= NetworkPayloadIdentity.Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidNetworkPayload(this ReadOnlySpan<byte> payload)
        {
            return !payload.IsEmpty && payload.Length >= NetworkPayloadIdentity.Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidNetworkPayload(this ReadOnlyMemory<byte> payload)
        {
            return !payload.IsEmpty && payload.Length >= NetworkPayloadIdentity.Size;
        }

        public static bool TryGetSteamUID(this TransportConnectionHandle handle, out CSteamID result)
        {
            return SteamTransportLayer.ToSteamConnection(handle).TryGetSteamUID(out result);
        }

        public static bool TryGetSteamUID(this TransportConnectionHandle handle, out ulong result)
        {
            if (SteamTransportLayer.ToSteamConnection(handle).TryGetSteamUID(out CSteamID id))
            {
                result = id.m_SteamID;
                return true;
            }

            result = 0;
            return false;
        }

        public static bool TryGetSteamUID(this HSteamNetConnection connectionHandle, out ulong result)
        {
            if (SteamNetworkingSockets.GetConnectionInfo(connectionHandle, out SteamNetConnectionInfo_t connectionInfo))
            {
                var remoteIdentity = connectionInfo.m_identityRemote;

                if (!remoteIdentity.IsInvalid())
                {
                    var uid = remoteIdentity.GetSteamID();
                    result = uid.m_SteamID;
                    return uid.IsValid();
                }
            }

            result = 0;
            return false;
        }

        public static bool TryGetSteamUID(this HSteamNetConnection connection, out CSteamID result)
        {
            if (SteamNetworkingSockets.GetConnectionInfo(connection, out SteamNetConnectionInfo_t connectionInfo))
            {
                var remoteIdentity = connectionInfo.m_identityRemote;

                if (!remoteIdentity.IsInvalid())
                {
                    result = remoteIdentity.GetSteamID();
                    return result.IsValid();
                }
            }

            result = CSteamID.Nil;
            return false;
        }
    }

    public static class ThreadlinkNetcode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterSubsystems()
        {
            ThreadlinkSubsystems.Register<ECSWorld>();
            ThreadlinkSubsystems.Register<Networld>();
            ThreadlinkSubsystems.Register<EntityOwnershipRegistry>();
            ThreadlinkSubsystems.Register<NetworkSerializer>();
            ThreadlinkSubsystems.Register<Netrunner>();
            ThreadlinkSubsystems.Register<Netflow>();
            ThreadlinkSubsystems.Register<NetworkRouter>();
            ThreadlinkSubsystems.Register<HandshakeSubsystem>();
            ThreadlinkSubsystems.Register<NetworkSpawningSubsystem>();
            ThreadlinkSubsystems.Register<NetworkTransformSubsystem>();
            ThreadlinkSubsystems.Register<NetworkClipLibrary>();
            ThreadlinkSubsystems.Register<NetworkAnimationSubsystem>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeaveSubsystems(List<IThreadlinkSubsystem> buffer)
        {
            buffer.Add(Threadlink.Weave<ECSWorld>());
            buffer.Add(Threadlink.Weave<Networld>());
            buffer.Add(Threadlink.Weave<EntityOwnershipRegistry>());
            buffer.Add(Threadlink.Weave<NetworkSerializer>());
            buffer.Add(Threadlink.Weave<Netrunner>());
            buffer.Add(Threadlink.Weave<Netflow>());
            buffer.Add(Threadlink.Weave<NetworkRouter>());
            buffer.Add(Threadlink.Weave<HandshakeSubsystem>());
            buffer.Add(Threadlink.Weave<NetworkSpawningSubsystem>());
            buffer.Add(Threadlink.Weave<NetworkTransformSubsystem>());
            buffer.Add(Threadlink.Weave<NetworkClipLibrary>());
            buffer.Add(Threadlink.Weave<NetworkAnimationSubsystem>());
        }

        public static bool IsHost
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Netrunner.TryGetSingleton(out var netrunner) && netrunner.IsHost;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetLocalSteamUID(out CSteamID result)
        {
            result = SteamUser.GetSteamID();
            return result.IsValid();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetLocalSteamUID(out ulong result)
        {
            var uid = SteamUser.GetSteamID();
            result = uid.m_SteamID;
            return uid.IsValid();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetHostSteamUID(out CSteamID result)
        {
            if (!Netrunner.TryGetSingleton(out var netrunner))
            {
                result = CSteamID.Nil;
                return false;
            }

            return netrunner.HostConnection.TryGetSteamUID(out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetHostSteamUID(out ulong result)
        {
            if (!Netrunner.TryGetSingleton(out var netrunner))
            {
                result = 0;
                return false;
            }

            return netrunner.HostConnection.TryGetSteamUID(out result);
        }
    }
}
