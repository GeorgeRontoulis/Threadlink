namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Iris;
    using Shared;
    using System;
    using UnityEngine;

    internal static class UserSubsystemsConfig
    {
        private const Iris.Events REGISTRATION_EVENT = Iris.Events.OnUserSubsystemRegistration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ListenForSubsystemRegistration()
        {
            Iris.Subscribe<Func<IThreadlinkSubsystem[]>>(REGISTRATION_EVENT, WeaveSubsystems);
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
        private static IThreadlinkSubsystem[] WeaveSubsystems()
        {
            var buffer = new IThreadlinkSubsystem[]
            {
                //Threadlink.Weave<MyCustomSubsystem>(),
                //Add your custom subsystems here.
            };

            Iris.Unsubscribe<Func<IThreadlinkSubsystem[]>>(REGISTRATION_EVENT, WeaveSubsystems);
            return buffer;
        }
    }
}
