namespace Threadlink.Core
{
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public sealed partial class Threadlink
    {
        /// <summary>
        /// Woven subsystems in the order they were woven: native subsystems first, then user subsystems in the order
        /// they are listed. Shutdown discards them in reverse, so a subsystem is discarded before those it depends on,
        /// provided dependencies are woven first.
        /// </summary>
        private List<IThreadlinkSubsystem> WeaveOrder { get; } = new(16);

        public override bool TryWeave<T>(out T wovenObject)
        {
            if (!base.TryWeave(out wovenObject))
                return false;

            WeaveOrder.Add(wovenObject);
            return true;
        }

        public override bool TryWeave<T>(T original, out T wovenObject)
        {
            if (!base.TryWeave(original, out wovenObject))
                return false;

            WeaveOrder.Add(wovenObject);
            return true;
        }

        /// <summary>
        /// Shut the runtime down when the application quits, which in the Editor is when Play Mode exits: every woven
        /// subsystem is discarded, so <see cref="IDiscardable.Discard"/> is the one place a subsystem releases what it
        /// holds (sockets, native memory, platform sessions). Subscribed when the core boots.
        /// </summary>
        private static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;

            // First the scenes still held, whose objects are discarded while the subsystems they registered with still
            // exist, as in any unload (Nexus, D42); then the subsystems.
            NativeSubsystems.Nexus.Nexus.ReleaseAll();

            if (TryGetSingleton(out var core))
                core.DiscardSubsystems();
        }

        /// <summary>
        /// Discard every woven subsystem in reverse weave order: user subsystems, then native ones. A subsystem that
        /// fails to discard is reported and the others are still discarded.
        /// </summary>
        private void DiscardSubsystems()
        {
            for (int i = WeaveOrder.Count - 1; i >= 0; i--)
            {
                var subsystem = WeaveOrder[i];

                if (subsystem == null)
                    continue;

                try
                {
                    subsystem.Discard();
                }
                catch (Exception exception)
                {
                    this.Send("Discarding ", subsystem.GetType().Name, " during shutdown failed: ", exception.Message)
                    .ToUnityConsole(DebugType.Error);
                }
            }

            WeaveOrder.Clear();
            ClearRegistry();
        }
    }
}
