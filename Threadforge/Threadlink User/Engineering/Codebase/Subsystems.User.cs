namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Iris;
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
        /// Inject custom subsystems into <see cref="Threadlink"/>.
        /// Ensure a factory method exists in <see cref="UserWeavingFactory"/> for each subsystem before registering it.
        /// <para/>
        /// Use the following method to register your subsystems:
        /// <list type="bullet">
        /// <item> <see cref="Threadlink.Weave{T}()"/> </item>
        /// </list>
        /// </summary>
        /// <returns>The registered subsystems to be internally processed during deployment.</returns>
        private static List<IThreadlinkSubsystem> WeaveSubsystems()
        {
            var buffer = new List<IThreadlinkSubsystem>(/*[OPTIONAL] Your Subsystem Count*/)
            {
                //Threadlink.Weave<MyCustomSubsystem>(),
                //Add your custom subsystems here.
            };

            /// Call Threadlink.Netcode.ThreadlinkNetcode.WeaveSubsystems(buffer); here if you want to use Threadlink's Netcode.
            /// You must also call Threadlink.Netcode.ThreadlinkNetcode.RegistSubsystems(buffer); in <see cref="UserWeavingFactory"/>.
            /// Make sure the necessary assemblies are included in the user assembly definition file.

            Iris.Unsubscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
            return buffer;
        }
    }
}
