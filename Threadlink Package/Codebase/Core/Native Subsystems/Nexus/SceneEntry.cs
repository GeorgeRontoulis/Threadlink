namespace Threadlink.Core.Subsystems.Nexus
{
	using Addressables;
	using Aura;
	using Core;
	using Cysharp.Threading.Tasks;
	using UnityEngine;
	using UnityEngine.SceneManagement;

	public abstract class SceneEntry : ScriptableObject
	{
		internal AddressableScene AddressableScene
		{
			get
			{
				Threadlink.TryGetAddressableScene(sceneInfo.assetAddress, out var scene);
				return scene;
			}
		}

		[SerializeField] private AddressablePointer sceneInfo = new();

		[Space(10)]

		[SerializeField] internal LoadSceneMode loadingMode = LoadSceneMode.Additive;
		[SerializeField] internal Nexus.PlayerLoadingAction playerLoadingAction = Nexus.PlayerLoadingAction.Load;

		[Space(10)]

		[SerializeField] private AddressablePointer musicClipInfo = new();
		[SerializeField] private AddressablePointer atmosClipInfo = new();

		[Space(5)]

		[SerializeField] private float musicVolume = 1;
		[SerializeField] private float atmosVolume = 1;

		[Space(10)]

		[SerializeField] internal Vector3[] playerSpawnPoints = new Vector3[0];

		public virtual UniTask OnBeforeUnloadedAsync() { return UniTask.CompletedTask; }

		public virtual async UniTask OnFinishedLoadingAsync()
		{
			static async UniTask TransitionToAudioScenario(AudioClip music, AudioClip atmos, Vector2 volumes)
			{
				Aura.SetGlobalVolumes(volumes);

				await Aura.TransitionToAudioScenarioAsync(music, atmos, volumes);
			}

			Threadlink.TryGetAddressableAsset<AudioClip>(musicClipInfo.assetAddress, out var musicClipAddressable);
			Threadlink.TryGetAddressableAsset<AudioClip>(atmosClipInfo.assetAddress, out var atmosClipAddressable);

			bool foundMusic = musicClipAddressable != null;
			bool foundAtmos = atmosClipAddressable != null;

			if (foundMusic == false && foundAtmos == false) return;

			if (foundMusic && foundAtmos == false)
			{
				await musicClipAddressable.LoadAsync();
				await TransitionToAudioScenario(musicClipAddressable.Result, null, new(musicVolume, 0f));
				return;
			}
			else if (foundMusic == false && foundAtmos)
			{
				await atmosClipAddressable.LoadAsync();
				await TransitionToAudioScenario(null, atmosClipAddressable.Result, new(0f, atmosVolume));
				return;
			}
			else
			{
				await UniTask.WhenAll(musicClipAddressable.LoadAsync(), atmosClipAddressable.LoadAsync());
				await TransitionToAudioScenario(musicClipAddressable.Result, atmosClipAddressable.Result, new(musicVolume, atmosVolume));
			}
		}
	}
}