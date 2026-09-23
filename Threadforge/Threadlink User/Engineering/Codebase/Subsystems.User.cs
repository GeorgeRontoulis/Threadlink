namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Iris;
    using Generated;
    using Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    internal static class UserSubsystemsConfig
    {
        private const ThreadlinkIDs.Iris.Events REGISTRATION_EVENT = ThreadlinkIDs.Iris.Events.OnUserSubsystemRegistration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ListenForSubsystemRegistration()
        {
            Iris.Subscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
        }

        /// <summary>
        /// Inject custom subsystems into <see cref="Threadlink"/>'s core.
        /// Ensure a factory method exists in <see cref="UserWeavingFactory"/> for each subsystem before weaving it here.
        /// <para/>
        /// Use the following method to inject your subsystems:
        /// <list type="bullet">
        /// <item> <see cref="Threadlink.Weave{T}()"/> </item>
        /// </list>
        /// </summary>
        /// <returns>The injected subsystems to be internally processed during deployment.</returns>
        private static List<IThreadlinkSubsystem> WeaveSubsystems()
        {
            var buffer = new List<IThreadlinkSubsystem>(/*[OPTIONAL] Your Subsystem Count*/)
            {
                //Threadlink.Weave<MyCustomSubsystem>(),
                //Add your custom subsystems here.
            };

            Iris.Unsubscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
            return buffer;
        }
    }
}
