namespace Threadlink.Core
{
    using NativeSubsystems.Aura;
    using NativeSubsystems.Dextra;
    using NativeSubsystems.Sentinel;
    using Shared;
    using UnityEngine;

    internal static class NativeWeavingFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            #region Native Subsystems:
            WeavingFactory<Sentinel>.OnCreate += static () => new Sentinel();
            WeavingFactory<Dextra>.OnCreate += static () => new Dextra();
            WeavingFactory<Aura>.OnCreate += static () => new Aura();
            #endregion
        }
    }
}
