namespace Threadlink.Netcode
{
    using Core;
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Utilities.Netcode;

    public sealed class NetworkSerializer : ThreadlinkSubsystem<NetworkSerializer>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe Memory<byte> Serialize<T>(NetworkPayloadIdentity payloadIdentity, in T data) where T : unmanaged
        {
            int identitySize = NetworkPayloadIdentity.Size;
            int dataSize = sizeof(T);
            int totalSize = identitySize + dataSize;

            // 1. Rent the buffer
            var buffer = ArrayPool<byte>.Shared.Rent(totalSize);
            var span = buffer.AsSpan(0, totalSize);

            // 2. Write the Identity header
            MemoryMarshal.Write(span, ref payloadIdentity);

            // 3. Direct memory copy of the unmanaged struct, bypassing MessagePack
            MemoryMarshal.Write(span[identitySize..], ref Unsafe.AsRef(in data));

            return new Memory<byte>(buffer, 0, totalSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDeserialize<T>(in ReadOnlyMemory<byte> rawPayload, out T result) where T : unmanaged
        {
            if (!rawPayload.IsValidNetworkPayload())
            {
                result = default;
                return false;
            }

            // 1. Slice off the header
            var dataSpan = rawPayload.Span[NetworkPayloadIdentity.Size..];

            // 2. Direct memory read into the struct
            result = MemoryMarshal.Read<T>(dataSpan);
            return true;
        }
    }
}