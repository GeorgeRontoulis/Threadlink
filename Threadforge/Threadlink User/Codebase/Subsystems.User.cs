namespace Threadlink.User
{
    using Core;
    using Core.NativeSubsystems.Chronos;
    using Core.NativeSubsystems.Iris;
    using Core.NativeSubsystems.Sentinel;
    using Shared;
    using System;
    using Threadlink.Core.NativeSubsystems.Dextra;
    using UnityEngine;

    internal static class SubsystemsConfig
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ListenForSubsystemRegistration()
        {
            Iris.Subscribe<Func<IThreadlinkSubsystem[]>>(Iris.Events.OnSubsystemRegistration, WeaveSubsystems);
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
                Threadlink.Weave<Sentinel>(),
                Threadlink.Weave<Dextra>(),
                Threadlink.Weave<Chronos>(),
                //Add your custom subsystems here.
            };

            Iris.Unsubscribe<Func<IThreadlinkSubsystem[]>>(Iris.Events.OnSubsystemRegistration, WeaveSubsystems);
            return buffer;
        }
    }
}
