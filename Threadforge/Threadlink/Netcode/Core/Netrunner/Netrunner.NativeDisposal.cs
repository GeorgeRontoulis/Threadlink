namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;

    public partial class Netrunner
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            DisposeConnectivity(); // Uses transport before it's disposed below
            sessionFlowEventsBuffer.Dispose();

            if (transport != null)
            {
                transport.OnConnectionStatusChanged -= OnTransportConnectionStatusChanged;
                transport.Dispose();
                transport = null;
            }
        }
    }
}
