namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Aura;
    using Core.NativeSubsystems.Chronos;
    using Core.NativeSubsystems.Dextra;
    using Core.NativeSubsystems.Iris;
    using Core.NativeSubsystems.Sentinel;
    using Shared;
    using System;
    using UnityEngine;

    internal static class NativeSubsystemsConfig
    {
        private const Iris.Events REGISTRATION_EVENT = Iris.Events.OnNativeSubsystemRegistration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ListenForSubsystemRegistration()
        {
            Iris.Subscribe<Func<IThreadlinkSubsystem[]>>(REGISTRATION_EVENT, WeaveSubsystems);
        }

        private static IThreadlinkSubsystem[] WeaveSubsystems()
        {
            var buffer = new IThreadlinkSubsystem[]
            {
                Threadlink.Weave<Sentinel>(),
                Threadlink.Weave<Chronos>(),
                Threadlink.Weave<Dextra>(),
                Threadlink.Weave<Aura>(),
            };

            Iris.Unsubscribe<Func<IThreadlinkSubsystem[]>>(REGISTRATION_EVENT, WeaveSubsystems);
            return buffer;
        }
    }
}
