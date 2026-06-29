namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Initium;
    using Iris;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine.ResourceManagement.ResourceProviders;

    /// <summary>
    /// System responsible for scene and player loading during Threadlink's runtime.
    /// </summary>
    public static partial class Nexus
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask FadeToLoadingScreenAsync()
        {
            await UniTask.WhenAll(FadeAudioAsync(0f), FadeFaderAsync(true));

            await FadeLoadingScreenAsync(true);

            await FadeFaderAsync(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask FadeToGameplayAsync()
        {
            await FadeFaderAsync(true);

            await FadeLoadingScreenAsync(false);

            await UniTask.WhenAll(FadeAudioAsync(1f), FadeFaderAsync(false));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask FadeAudioAsync(float targetVolume)
        {
            bool auraExists = Aura.TryGetSingleton(out var aura);

            await (auraExists ? aura.FadeAudioListenerVolumeAsync(targetVolume) : UniTask.CompletedTask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask FadeFaderAsync(bool faderVisible)
        {
            if (faderVisible)
                await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayFaderAsync);
            else
                await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideFaderAsync);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async UniTask FadeLoadingScreenAsync(bool screenVisible)
        {
            if (screenVisible)
                await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnDisplayLoadingScreenAsync);
            else
                await Iris.Publish<UniTask>(ThreadlinkIDs.Iris.Events.OnHideLoadingScreenAsync);
        }

        public static async UniTask UnloadActiveSceneAsync()
        {
            var activeSceneEntry = Iris.Publish<ISceneEntry>(ThreadlinkIDs.Iris.Events.OnActiveSceneRequested);

            if (activeSceneEntry != null)
            {
                await activeSceneEntry.OnBeforeUnloadedAsync();
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnBeforeActiveSceneUnload);

                if (Threadlink.TryGetSingleton(out var core))
                {
                    await core.UnloadSceneAsync(activeSceneEntry.ScenePointer);
                    Iris.Publish(ThreadlinkIDs.Iris.Events.OnActiveSceneFinishedUnloading);
                }
            }
        }

        public static async UniTask<SceneInstance> LoadNewSceneAsync(ISceneEntry sceneEntry)
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return default;

            var activeSceneInstance = await core.LoadSceneAsync(sceneEntry.ScenePointer, sceneEntry.LoadMode);

            await Initium.BootAndInitUnityObjectsAsync(activeSceneInstance.Scene);

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNewSceneFinishedLoading, sceneEntry);

            await sceneEntry.OnFinishedLoadingAsync();

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished);

            return activeSceneInstance;
        }
    }
}