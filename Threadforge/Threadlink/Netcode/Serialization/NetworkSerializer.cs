namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;
    using Threadlink.Core;

    public sealed class NetworkSerializer : ThreadlinkSubsystem<NetworkSerializer>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void Serialize<T>(NetworkPayloadIdentity identity, in T data, byte* destination) where T : unmanaged
        {
            *(NetworkPayloadIdentity*)destination = identity;
            *(T*)(destination + sizeof(NetworkPayloadIdentity)) = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryDeserialize<T>(IntPtr dataPtr, int size, out T result) where T : unmanaged
        {
            int requiredSize = sizeof(NetworkPayloadIdentity) + sizeof(T);

            if (size < requiredSize)
            {
                result = default;
                return false;
            }

            // Read directly from the memory block, offset by the header size
            result = *(T*)(dataPtr + sizeof(NetworkPayloadIdentity));
            return true;
        }
    }
}