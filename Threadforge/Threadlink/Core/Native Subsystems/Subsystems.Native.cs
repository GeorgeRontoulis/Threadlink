namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Aura;
    using Core.NativeSubsystems.Dextra;
    using Core.NativeSubsystems.Iris;
    using Core.NativeSubsystems.Sentinel;
    using Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    internal static class NativeSubsystemsConfig
    {
        private const ThreadlinkIDs.Iris.Events REGISTRATION_EVENT = ThreadlinkIDs.Iris.Events.OnNativeSubsystemRegistration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ListenForSubsystemRegistration()
        {
            Iris.Subscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
        }

        private static List<IThreadlinkSubsystem> WeaveSubsystems()
        {
            var buffer = new List<IThreadlinkSubsystem>(3)
            {
                Threadlink.Weave<Sentinel>(),
                Threadlink.Weave<Dextra>(),
                Threadlink.Weave<Aura>(),
            };

            Iris.Unsubscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
            return buffer;
        }
    }
}
