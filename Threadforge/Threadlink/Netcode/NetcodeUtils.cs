namespace Threadlink.Utilities.Netcode
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Threadlink.Core;
    using Threadlink.ECS;
    using Threadlink.Netcode;
    using Threadlink.Shared;
    using Threadlink.Utilities.Subsystems;

    public static class NetcodeUtils
    {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deallocate(ref this Memory<byte> payload)
        {
            if (MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> segment) && segment.Array != null)
                ArrayPool<byte>.Shared.Return(segment.Array);
        }
    }

    public static class ThreadlinkNetcode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterSubsystems()
        {
            ThreadlinkSubsystems.Register<ECSWorld>();
            ThreadlinkSubsystems.Register<NetworkSerializer>();
            ThreadlinkSubsystems.Register<Netrunner>();
            ThreadlinkSubsystems.Register<NetworkRouter>();
            ThreadlinkSubsystems.Register<SteamAuthenticator>();
            ThreadlinkSubsystems.Register<SteamMatchmaker>();
            ThreadlinkSubsystems.Register<Networld>();
            ThreadlinkSubsystems.Register<NetworkTransformSubsystem>();
            ThreadlinkSubsystems.Register<NetworkAnimationSubsystem>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WeaveSubsystems(List<IThreadlinkSubsystem> buffer)
        {
            buffer.Add(Threadlink.Weave<ECSWorld>());
            buffer.Add(Threadlink.Weave<NetworkSerializer>());
            buffer.Add(Threadlink.Weave<Netrunner>());
            buffer.Add(Threadlink.Weave<NetworkRouter>());
            buffer.Add(Threadlink.Weave<SteamAuthenticator>());
            buffer.Add(Threadlink.Weave<SteamMatchmaker>());
            buffer.Add(Threadlink.Weave<Networld>());
            buffer.Add(Threadlink.Weave<NetworkTransformSubsystem>());
            buffer.Add(Threadlink.Weave<NetworkAnimationSubsystem>());
        }
    }
}
