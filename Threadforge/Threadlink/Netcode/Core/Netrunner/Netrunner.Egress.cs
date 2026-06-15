namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;

    public partial class Netrunner
    {
        public unsafe void SendRaw(ReadOnlySpan<byte> rawPayload, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        {
            if (rawPayload.IsEmpty) return;

            uint payloadSize = (uint)rawPayload.Length;
            int length = connections.Length;

            fixed (byte* dataPtr = rawPayload)
            {
                for (int i = 0; i < length; i++)
                {
                    var connection = connections[i];

                    if (connection.IsValid)
                        transport.SendMessage(connection, (IntPtr)dataPtr, payloadSize, reliability);
                }
            }
        }

        public unsafe void SendRawTo(int playerIndex, ReadOnlySpan<byte> rawPayload, NetMsgReliability reliability = NetMsgReliability.Unreliable)
        {
            if (rawPayload.IsEmpty || !TryGetPlayerConnectionAt(playerIndex, out var connection)) return;

            uint payloadSize = (uint)rawPayload.Length;

            fixed (byte* dataPtr = rawPayload)
                transport.SendMessage(connection, (IntPtr)dataPtr, payloadSize, reliability);
        }

        public unsafe void Send<T>(in NetworkPayloadIdentity payloadIdentity, in T payloadData, NetMsgReliability reliability = NetMsgReliability.Unreliable)
            where T : unmanaged
        {
            if (!NetworkSerializer.TryGetSingleton(out var serializer)) return;

            int totalSize = sizeof(NetworkPayloadIdentity) + sizeof(T);
            byte* buffer = stackalloc byte[totalSize];

            serializer.Serialize(payloadIdentity, in payloadData, buffer);

            int length = connections.Length;

            for (int i = 0; i < length; i++)
            {
                var connection = connections[i];

                if (connection.IsValid)
                    transport.SendMessage(connection, (IntPtr)buffer, (uint)totalSize, reliability);
            }
        }

        public unsafe void SendTo<T>(int playerIndex, in NetworkPayloadIdentity payloadIdentity,
            in T payloadData, NetMsgReliability reliability = NetMsgReliability.Unreliable)
            where T : unmanaged
        {
            if (!NetworkSerializer.TryGetSingleton(out var serializer)
             || !TryGetPlayerConnectionAt(playerIndex, out var connection)) return;

            int totalSize = sizeof(NetworkPayloadIdentity) + sizeof(T);
            byte* buffer = stackalloc byte[totalSize];

            serializer.Serialize(payloadIdentity, in payloadData, buffer);

            transport.SendMessage(connection, (IntPtr)buffer, (uint)totalSize, reliability);
        }
    }
}
