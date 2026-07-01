namespace Threadlink.Core
{
    using NativeSubsystems.Aura;
    using NativeSubsystems.Chronos;
    using NativeSubsystems.Dextra;
    using NativeSubsystems.Sentinel;
    using Shared;
    using UnityEngine;

    internal static partial class NativeWeavingFactory
    {
        [OnEnteringPlayMode]
        private static void Register()
        {
            #region Native Subsystems:
            WeavingFactory.Register<Sentinel>();
            WeavingFactory.Register<Chronos>();
            WeavingFactory.Register<Dextra>();
            WeavingFactory.Register<Aura>();
            #endregion
        }
    }
}
