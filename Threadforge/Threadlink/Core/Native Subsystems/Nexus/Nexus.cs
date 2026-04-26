namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Iris;
    using Shared;

    /// <summary>
    /// System responsible for scene and player loading during Threadlink's runtime.
    /// </summary>
    public static partial class Nexus
    {
        public static async UniTask LoadSceneAsync(ISceneEntry sceneEntry)
        {
            var volumeTask = Aura.FadeAudioListenerVolumeAsync(0f);
            var faderTask = Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayFaderAsync);

            await UniTask.WhenAll(volumeTask, faderTask);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayLoadingScreenAsync);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideFaderAsync);

            var activeScene = Iris.Publish<ISceneEntry>(ThreadlinkIDs.Iris.Events.OnActiveSceneRequested);

            if (activeScene != null)
            {
                await activeScene.OnBeforeUnloadedAsync();
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnBeforeActiveSceneUnload);

                await Threadlink.UnloadSceneAsync(activeScene.ScenePointer);
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnActiveSceneFinishedUnloading);
            }

            await Threadlink.LoadSceneAsync(sceneEntry.ScenePointer, sceneEntry.LoadMode);

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNewSceneFinishedLoading, sceneEntry);

            await sceneEntry.OnFinishedLoadingAsync();

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished);

            await Threadlink.WaitForFramesAsync(1);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayFaderAsync);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideLoadingScreenAsync);

            volumeTask = Aura.FadeAudioListenerVolumeAsync(1f);
            faderTask = Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideFaderAsync);

            await UniTask.WhenAll(volumeTask, faderTask);
        }
    }
}