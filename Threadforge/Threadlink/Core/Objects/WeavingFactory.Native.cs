namespace Threadlink.Core
{
    using NativeSubsystems.Chronos;
    using NativeSubsystems.Dextra;
    using NativeSubsystems.Sentinel;
    using Shared;
    using UnityEngine;

    internal static class NativeWeavingFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            //Native types here.
            //WeavingFactory<NativeSubsystem>.OnCreate += static () => new NativeSubsystem();

            WeavingFactory<Sentinel>.OnCreate += static () => new Sentinel();
            WeavingFactory<Chronos>.OnCreate += static () => new Chronos();
            WeavingFactory<Dextra>.OnCreate += static () => new Dextra();
        }
    }
}
