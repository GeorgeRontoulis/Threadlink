namespace Threadlink.Netcode
{
    using System;
    using System.Runtime.CompilerServices;

    public partial class Netrunner
    {
        private const byte MAX_MESSAGES = 32;

        public delegate void NetworkPayloadReceivedDelegate(TransportConnectionHandle sender, IntPtr dataPtr, int size);

        private readonly IntPtr[] MessagesBuffer = new IntPtr[MAX_MESSAGES];

        public event NetworkPayloadReceivedDelegate OnNetworkPayloadReceived = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ReceiveData()
        {
            if (!connections.IsCreated)
                return;

            int length = connections.Length;

            for (int i = 0; i < length; i++)
            {
                var connection = connections[i];

                if (!connection.IsValid)
                    continue;

                Array.Fill(MessagesBuffer, IntPtr.Zero);
                int messageCount = transport.ReceiveMessages(connection, MessagesBuffer, MAX_MESSAGES);

                for (int m = 0; m < messageCount; m++)
                {
                    ref var msgPtr = ref MessagesBuffer[m];

                    try
                    {
                        OnNetworkPayloadReceived?.Invoke(connection, transport.GetMessageData(msgPtr), transport.GetMessageSize(msgPtr));
                    }
                    finally
                    {
                        transport.ReleaseMessage(msgPtr);
                    }
                }
            }
        }
    }
}
