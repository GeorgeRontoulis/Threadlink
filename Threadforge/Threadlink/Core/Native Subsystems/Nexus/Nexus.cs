namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Chronos;
    using Core;
    using Cysharp.Threading.Tasks;
    using Dextra;
    using Initium;
    using Iris;
    using Utilities.Collections;

    /// <summary>
    /// System responsible for scene and player loading during Threadlink's runtime.
    /// </summary>
    public sealed class Nexus : ThreadlinkSubsystem<Nexus>
    {
        internal enum PlayerLoadingAction : byte { None, Unload, Load }

        public static async UniTask LoadSceneAsync(SceneEntry sceneEntry, int entranceIndex = -1)
        {
            Dextra.Instance.CurrentInputMode = Dextra.InputMode.Unresponsive;
            Chronos.Instance.RawTimeScale = 0;

            var volumeTask = Aura.FadeAudioListenerVolumeAsync(0f);
            var faderTask = Iris.Publish<UniTask>(Iris.Events.OnDisplayFaderAsync);

            await UniTask.WhenAll(volumeTask, faderTask);

            await Iris.Publish<UniTask>(Iris.Events.OnDisplayLoadingScreenAsync);

            await Iris.Publish<UniTask>(Iris.Events.OnHideFaderAsync);

            var playerLoadingAction = sceneEntry.playerLoadingAction;

            if (Iris.Publish<bool>(Iris.Events.OnPlayerLoadStateCheck)
            && playerLoadingAction is PlayerLoadingAction.Unload)
            {
                Iris.Publish(Iris.Events.OnUnloadPlayer);
            }

            var activeScene = Iris.Publish<SceneEntry>(Iris.Events.OnActiveSceneRequested);

            if (activeScene != null)
            {
                await activeScene.OnBeforeUnloadedAsync();
                Iris.Publish(Iris.Events.OnBeforeActiveSceneUnload);

                await Threadlink.UnloadSceneAsync(activeScene.SceneIndexInDatabase);
                Iris.Publish(Iris.Events.OnActiveSceneFinishedUnloading);
            }

            await Threadlink.LoadSceneAsync(sceneEntry.SceneIndexInDatabase, sceneEntry.loadingMode);

            Iris.Publish(Iris.Events.OnNewSceneFinishedLoading, sceneEntry);

            if (!Iris.Publish<bool>(Iris.Events.OnPlayerLoadStateCheck)
            && playerLoadingAction is PlayerLoadingAction.Load)
            {
                await Iris.Publish<UniTask>(Iris.Events.OnLoadPlayerAsync);
            }

            bool playerIsLoaded = Iris.Publish<bool>(Iris.Events.OnPlayerLoadStateCheck);

            if (playerIsLoaded)
            {
                var spawnPoints = sceneEntry.playerSpawnPoints;
                Iris.Publish(Iris.Events.OnPlacePlayer, entranceIndex.IsWithinBoundsOf(spawnPoints) ? spawnPoints[entranceIndex] : default);
            }

            await Initium.BootAndInitUnityObjectsAsync();

            await sceneEntry.OnFinishedLoadingAsync();

            Iris.Publish(Iris.Events.OnLoadingProcessFinished);
            Chronos.Instance.RawTimeScale = 1;

            await Iris.Publish<UniTask>(Iris.Events.OnDisplayFaderAsync);

            await Iris.Publish<UniTask>(Iris.Events.OnHideLoadingScreenAsync);

            volumeTask = Aura.FadeAudioListenerVolumeAsync(1f);
            faderTask = Iris.Publish<UniTask>(Iris.Events.OnHideFaderAsync);

            //Re-enable player input, if appropriate.
            Dextra.Instance.CurrentInputMode = playerIsLoaded ? Dextra.InputMode.Player : Dextra.InputMode.UI;

            await UniTask.WhenAll(volumeTask, faderTask);
        }
    }
}