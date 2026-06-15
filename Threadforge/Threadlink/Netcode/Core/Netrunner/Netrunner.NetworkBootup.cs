namespace Threadlink.Netcode
{
    public partial class Netrunner
    {
        private bool TryBootNetwork()
        {
            if (s_transportFactory == null)
                return false;

            var createdTransport = s_transportFactory();

            if (!createdTransport.TryInit())
            {
                createdTransport.Dispose();
                return false;
            }

            transport = createdTransport;
            transport.OnConnectionStatusChanged += OnTransportConnectionStatusChanged;
            return true;
        }
    }
}
