namespace Threadlink.Core.NativeSubsystems.Nexus
{
    using Aura;
    using Core;
    using Cysharp.Threading.Tasks;
    using Shared;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [CreateAssetMenu(menuName = "Threadlink/Nexus/Scene Entry")]
    public class SceneEntry : ScriptableObject
    {
        protected internal SceneIDs ScenePointer => scenePointer;

        [SerializeField] private SceneIDs scenePointer = default;

        [Space(10)]

        [SerializeField] internal LoadSceneMode loadingMode = LoadSceneMode.Additive;
        [SerializeField] internal Nexus.PlayerLoadingAction playerLoadingAction = Nexus.PlayerLoadingAction.Load;

        [Space(10)]

        [SerializeField] private AssetIDs musicClipPointer = default;
        [SerializeField] private AssetIDs atmosClipPointer = default;

        [Space(5)]

        [SerializeField] private float musicVolume = 1;
        [SerializeField] private float atmosVolume = 1;

        [Space(10)]

        [SerializeField] internal Vector3[] playerSpawnPoints = new Vector3[0];

        protected internal virtual UniTask OnBeforeUnloadedAsync() => UniTask.CompletedTask;

        protected internal virtual async UniTask OnFinishedLoadingAsync()
        {
            static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, float musicVolume, float atmosVolume)
            {
                Aura.SetGlobalVolumes(musicVolume, atmosVolume);

                await Aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
            }

            bool foundMusic = Threadlink.TryGetAssetReference(musicClipPointer, out var musicRef);
            bool foundAtmos = Threadlink.TryGetAssetReference(atmosClipPointer, out var atmosRef);

            if (!foundMusic && !foundAtmos) return;

            if (foundMusic && !foundAtmos)
            {
                var clip = await Threadlink.LoadAssetAsync<AudioClip>(musicRef);
                await TransitionToAudioScenario(clip, null, musicVolume, 0f);
                return;
            }
            else if (!foundMusic && foundAtmos)
            {
                var clip = await Threadlink.LoadAssetAsync<AudioClip>(atmosRef);
                await TransitionToAudioScenario(null, clip, 0f, atmosVolume);
                return;
            }
            else
            {
                var musicTask = Threadlink.LoadAssetAsync<AudioClip>(musicRef).Preserve();
                var atmosTask = Threadlink.LoadAssetAsync<AudioClip>(atmosRef).Preserve();

                await UniTask.WhenAll(musicTask, atmosTask);

                await TransitionToAudioScenario(musicTask.AsValueTask().Result, atmosTask.AsValueTask().Result, musicVolume, atmosVolume);
            }
        }
    }
}