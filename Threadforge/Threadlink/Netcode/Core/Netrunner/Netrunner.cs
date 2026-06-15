namespace Threadlink.Netcode
{
    using Core;
    using System;
    using System.Runtime.CompilerServices;

    public sealed partial class Netrunner : ThreadlinkSubsystem<Netrunner>, IDisposable
    {
        // Inject a custom transport before Boot() is called. Defaults to SteamTransportLayer.
        private static Func<ITransportLayer> s_transportFactory = static () => new SteamTransportLayer();

        private ITransportLayer transport;

        /// <summary>
        /// Override the transport used by every future <see cref="Netrunner"/> instance.
        /// Must be called before <see cref="Boot"/> (i.e. before Threadlink boots netcode).
        /// </summary>
        public static void UseTransport(Func<ITransportLayer> factory) => s_transportFactory = factory;

        #region Threadlink Lifecycle API

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Discard()
        {
            StopNetworkUpdateLoop();
            Dispose();
            base.Discard();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Boot()
        {
            AllocateNativeResources();
            base.Boot();

            if (TryBootNetwork())
            {
                BootConnectivity();
                BootNetworkUpdateLoop();
                StartNetworkUpdateLoop();
            }
        }

        #endregion
    }
}
