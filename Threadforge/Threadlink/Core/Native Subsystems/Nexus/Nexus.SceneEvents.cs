namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Generated;
    using Iris;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        /// <summary>
        /// The payload of <see cref="ThreadlinkIDs.Iris.Events.OnSceneLoaded"/>: the scene Unity just activated, and how
        /// it was loaded.
        /// </summary>
        public readonly struct SceneLoad
        {
            public Scene Scene { get; }
            public LoadSceneMode Mode { get; }

            public SceneLoad(Scene scene, LoadSceneMode mode)
            {
                Scene = scene;
                Mode = mode;
            }
        }

        /// <summary>
        /// Publish Unity's scene activation and unloading through Iris, however a scene was loaded (Nexus, Addressables or
        /// <see cref="SceneManager"/>), so project code never subscribes to the engine's events:
        /// <list type="bullet">
        /// <item><see cref="ThreadlinkIDs.Iris.Events.OnSceneLoaded"/> (<c>Action&lt;Nexus.SceneLoad&gt;</c>) the moment a
        /// scene is activated: before it renders, and before Nexus boots its objects. This is the hook for presentation
        /// that must act before a scene's first frame, for example keeping a resident scene hidden while another one is
        /// presented. Listeners must not assume the scene is active or booted.</item>
        /// <item><see cref="ThreadlinkIDs.Iris.Events.OnSceneUnloaded"/> (<c>Action&lt;Scene&gt;</c>) once a scene has
        /// unloaded. The scene is no longer valid; use it only as a key.</item>
        /// </list>
        /// </summary>
        [OnEnteringPlayMode]
        private static void ListenForSceneEvents()
        {
            SceneManager.sceneLoaded -= PublishSceneLoaded;
            SceneManager.sceneLoaded += PublishSceneLoaded;
            SceneManager.sceneUnloaded -= PublishSceneUnloaded;
            SceneManager.sceneUnloaded += PublishSceneUnloaded;
        }

        private static void PublishSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Iris.Publish(ThreadlinkIDs.Iris.Events.OnSceneLoaded, new SceneLoad(scene, mode));
        }

        private static void PublishSceneUnloaded(Scene scene)
        {
            Iris.Publish(ThreadlinkIDs.Iris.Events.OnSceneUnloaded, scene);
        }
    }
}
