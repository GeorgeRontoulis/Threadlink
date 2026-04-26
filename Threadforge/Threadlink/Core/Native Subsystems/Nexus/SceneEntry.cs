namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Shared;
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

            public virtual UniTask OnBeforeUnloadedAsync() => UniTask.CompletedTask;

            public virtual async UniTask OnFinishedLoadingAsync()
            {
                static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, float musicVolume, float atmosVolume)
                {
                    Aura.SetGlobalVolumes(musicVolume, atmosVolume);

                    await Aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
                }

                bool foundMusic = Threadlink.TryGetAssetReference(MusicClipPointer, out var musicRef);
                bool foundAtmos = Threadlink.TryGetAssetReference(AtmosClipPointer, out var atmosRef);

                if (!foundMusic && !foundAtmos) return;

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
                    var musicTask = Threadlink.LoadAssetAsync<AudioClip>(musicRef).Preserve();
                    var atmosTask = Threadlink.LoadAssetAsync<AudioClip>(atmosRef).Preserve();

                    await UniTask.WhenAll(musicTask, atmosTask);

                    await TransitionToAudioScenario(musicTask.AsValueTask().Result, atmosTask.AsValueTask().Result, MusicVolume, AtmosVolume);
                }
            }
        }
    }
}