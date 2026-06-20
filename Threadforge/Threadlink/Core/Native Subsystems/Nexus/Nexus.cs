namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Initium;
    using Iris;
    using Shared;
    using UnityEngine.ResourceManagement.ResourceProviders;

    /// <summary>
    /// System responsible for scene and player loading during Threadlink's runtime.
    /// </summary>
    public static partial class Nexus
    {
        public static async UniTask<SceneInstance> LoadSceneAsync(ISceneEntry sceneEntry)
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return default;

            bool auraExists = Aura.TryGetSingleton(out var aura);

            var volumeTask = auraExists ? aura.FadeAudioListenerVolumeAsync(0f) : UniTask.CompletedTask;
            var faderTask = Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayFaderAsync);

            await UniTask.WhenAll(volumeTask, faderTask);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayLoadingScreenAsync);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideFaderAsync);

            var activeSceneEntry = Iris.Publish<ISceneEntry>(ThreadlinkIDs.Iris.Events.OnActiveSceneRequested);

            if (activeSceneEntry != null)
            {
                await activeSceneEntry.OnBeforeUnloadedAsync();
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnBeforeActiveSceneUnload);

                await core.UnloadSceneAsync(activeSceneEntry.ScenePointer);
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnActiveSceneFinishedUnloading);
            }

            var activeSceneInstance = await core.LoadSceneAsync(sceneEntry.ScenePointer, sceneEntry.LoadMode);

            await Initium.BootAndInitUnityObjectsAsync();

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNewSceneFinishedLoading, sceneEntry);

            await sceneEntry.OnFinishedLoadingAsync();

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished);

            await Threadlink.WaitForFramesAsync(1);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayFaderAsync);

            await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideLoadingScreenAsync);

            volumeTask = auraExists ? aura.FadeAudioListenerVolumeAsync(1f) : UniTask.CompletedTask;
            faderTask = Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideFaderAsync);

            await UniTask.WhenAll(volumeTask, faderTask);

            return activeSceneInstance;
        }
    }
}