namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Generated;
    using Initium;
    using Iris;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

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
                Iris.Publish(ThreadlinkIDs.Iris.Events.OnBeforeActiveSceneUnload, activeSceneEntry);

                if (Threadlink.TryGetSingleton(out var core))
                {
                    await core.UnloadSceneAsync(activeSceneEntry.ScenePointer);
                    Iris.Publish(ThreadlinkIDs.Iris.Events.OnActiveSceneFinishedUnloading, activeSceneEntry);
                }
            }
        }

        public static async UniTask<SceneInstance> LoadNewSceneAsync<T>(T sceneEntry) where T : ISceneEntry
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return default;

            var activeSceneInstance = await core.LoadSceneAsync(sceneEntry.ScenePointer, sceneEntry.LoadMode);

            SceneManager.SetActiveScene(activeSceneInstance.Scene);

            await Initium.BootAndInitUnityObjectsAsync(activeSceneInstance.Scene);

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNewSceneFinishedLoading, sceneEntry as ISceneEntry);

            var audioTransitionTask = TransitionAudioAsync(sceneEntry);
            var onFinishedLoadingTask = sceneEntry.OnFinishedLoadingAsync();

            await UniTask.WhenAll(audioTransitionTask, onFinishedLoadingTask);

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished, sceneEntry as ISceneEntry);

            return activeSceneInstance;
        }

        private static async UniTask TransitionAudioAsync<T>(T entry) where T : ISceneEntry
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, float musicVolume, float atmosVolume)
            {
                if (Aura.TryGetSingleton(out var aura))
                {
                    aura.SetGlobalVolumesMax(musicVolume, atmosVolume);
                    await aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
                }
            }

            if (!Threadlink.TryGetSingleton(out var core)) return;

            bool foundMusic = core.TryGetAssetReference(entry.MusicClipPointer, out var musicRef);
            bool foundAtmos = core.TryGetAssetReference(entry.AtmosClipPointer, out var atmosRef);

            if (!foundMusic && !foundAtmos) return;

            if (foundMusic && !foundAtmos)
            {
                var clip = await Threadlink.LoadAssetAsync<AudioClip>(musicRef);
                await TransitionToAudioScenario(clip, null, entry.MusicVolume, 0f);
            }
            else if (!foundMusic && foundAtmos)
            {
                var clip = await Threadlink.LoadAssetAsync<AudioClip>(atmosRef);
                await TransitionToAudioScenario(null, clip, 0f, entry.AtmosVolume);
            }
            else
            {
                var clips = await UniTask.WhenAll
                (
                    Threadlink.LoadAssetAsync<AudioClip>(musicRef),
                    Threadlink.LoadAssetAsync<AudioClip>(atmosRef)
                );

                await TransitionToAudioScenario(clips.Item1, clips.Item2, entry.MusicVolume, entry.AtmosVolume);
            }
        }
    }
}