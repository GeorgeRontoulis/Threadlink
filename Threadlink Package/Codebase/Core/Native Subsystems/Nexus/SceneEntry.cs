namespace Threadlink.Core.Subsystems.Nexus
{
	using Addressables;
	using Aura;
	using Core;
	using Cysharp.Threading.Tasks;
	using Unity.Mathematics;
	using UnityEngine;
	using UnityEngine.SceneManagement;

	public abstract class SceneEntry : ScriptableObject
	{
		public int SceneIndexInDatabase => scenePointer.IndexInDatabase;

		[SerializeField] private ScenePointer scenePointer = new();

		[Space(10)]

		[SerializeField] internal LoadSceneMode loadingMode = LoadSceneMode.Additive;
		[SerializeField] internal Nexus.PlayerLoadingAction playerLoadingAction = Nexus.PlayerLoadingAction.Load;

		[Space(10)]

		[SerializeField] private GroupedAssetPointer musicClipPointer = new();
		[SerializeField] private GroupedAssetPointer atmosClipPointer = new();

		[Space(5)]

		[SerializeField] private float musicVolume = 1;
		[SerializeField] private float atmosVolume = 1;

		[Space(10)]

		[SerializeField] internal Vector3[] playerSpawnPoints = new Vector3[0];

		public virtual UniTask OnBeforeUnloadedAsync() => UniTask.CompletedTask;

		public virtual async UniTask OnFinishedLoadingAsync()
		{
			static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, float2 volumes)
			{
				Aura.SetGlobalVolumes(volumes);

				await Aura.TransitionToAudioScenarioAsync(music, atmos, volumes);
			}

			bool foundMusic = Threadlink.TryGetAssetReference(musicClipPointer.Group, musicClipPointer.IndexInDatabase, out var musicReference);
			bool foundAtmos = Threadlink.TryGetAssetReference(atmosClipPointer.Group, atmosClipPointer.IndexInDatabase, out var atmosReference);

			if (foundMusic == false && foundAtmos == false) return;

			if (foundMusic && !foundAtmos)
			{
				var clip = await musicReference.LoadAssetAsync<AudioClip>().ToUniTask();
				await TransitionToAudioScenario(clip, null, new(musicVolume, 0f));
				return;
			}
			else if (!foundMusic && foundAtmos)
			{
				var clip = await atmosReference.LoadAssetAsync<AudioClip>().ToUniTask();
				await TransitionToAudioScenario(null, clip, new(0f, atmosVolume));
				return;
			}
			else
			{
				await UniTask.WhenAll(musicReference.LoadAssetAsync<AudioClip>().ToUniTask(),
				atmosReference.LoadAssetAsync<AudioClip>().ToUniTask());

				await TransitionToAudioScenario(musicReference.Asset as AudioClip,
				atmosReference.Asset as AudioClip, new(musicVolume, atmosVolume));
			}
		}
	}
}