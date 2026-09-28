namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        /// <summary>
        /// One claim on a resident scene, from <see cref="HoldAsync"/>. The scene stays loaded while any hold on it is
        /// held, and unloads once the last one is released. Release each hold exactly once, when its holder no longer
        /// needs the scene; releasing it again does nothing.
        /// </summary>
        public sealed class SceneHold
        {
            private SceneRecord record;

            internal SceneHold(SceneRecord record)
            {
                this.record = record;
                Entry = record.Entry;
                Scene = record.Scene;
            }

            public ISceneEntry Entry { get; }

            /// <summary>The resident scene, booted.</summary>
            public Scene Scene { get; }

            /// <summary>False once released.</summary>
            public bool IsHeld => record != null;

            /// <summary>
            /// Let the scene go. If this was its last hold, it unloads after its entry's
            /// <see cref="ISceneEntry.UnloadDelay"/>.
            /// </summary>
            public void Release()
            {
                var released = record;

                record = null;

                if (released != null)
                    Nexus.Release(released, released.Entry.UnloadDelay);
            }

            /// <summary>
            /// Let the scene go. If this was its last hold, it unloads after <paramref name="delaySeconds"/> unless it is held
            /// again meanwhile, which keeps it without reloading.
            /// </summary>
            public void Release(float delaySeconds)
            {
                var released = record;

                record = null;

                if (released != null)
                    Nexus.Release(released, delaySeconds);
            }
        }
    }
}
