namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static partial class Nexus
    {
        public interface ISceneEntry
        {
            public ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
            public LoadSceneMode LoadMode { get; }
            public ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
            public ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
            public float MusicVolume { get; }
            public float AtmosVolume { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public virtual UniTask OnBeforeUnloadedAsync() => UniTask.CompletedTask;

            public virtual async UniTask OnFinishedLoadingAsync()
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, float musicVolume, float atmosVolume)
                {
                    if (Aura.TryGetSingleton(out var aura))
                    {
                        aura.SetGlobalVolumes(musicVolume, atmosVolume);

                        await aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
                    }
                }

                if (!Threadlink.TryGetSingleton(out var core))
                    return;

                bool foundMusic = core.TryGetAssetReference(MusicClipPointer, out var musicRef);
                bool foundAtmos = core.TryGetAssetReference(AtmosClipPointer, out var atmosRef);

                if (!foundMusic && !foundAtmos)
                    return;

                if (foundMusic && !foundAtmos)
                {
                    var clip = await Threadlink.LoadAssetAsync<AudioClip>(musicRef);
                    await TransitionToAudioScenario(clip, null, MusicVolume, 0f);
                    return;
                }
                else if (!foundMusic && foundAtmos)
                {
                    var clip = await Threadlink.LoadAssetAsync<AudioClip>(atmosRef);
                    await TransitionToAudioScenario(null, clip, 0f, AtmosVolume);
                    return;
                }
                else
                {
                    var clips = await UniTask.WhenAll
                    (
                        Threadlink.LoadAssetAsync<AudioClip>(musicRef),
                        Threadlink.LoadAssetAsync<AudioClip>(atmosRef)
                    );

                    await TransitionToAudioScenario(clips.Item1, clips.Item2, MusicVolume, AtmosVolume);
                }
            }
        }
    }
}