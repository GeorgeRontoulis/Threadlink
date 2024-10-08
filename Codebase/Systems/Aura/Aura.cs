namespace Threadlink.Systems.Aura
{
	using Core;
	using Cysharp.Threading.Tasks;
	using Systems.Nexus;
	using UnityEngine;
	using Utilities.Events;

	/// <summary>
	/// System responsible for Audio Mixing during Threadlink's runtime.
	/// Provides Spatial Mixing for BGM and Atmos, audio transitions, fades etc.
	/// </summary>
	public sealed class Aura : UnitySystem<Aura, AuraSpatialEntity>
	{
		public enum UISFX { Cancel = -1, Nagivate, Confirm }

		private static AudioListener AudioListener { get; set; }
		private static Transform AudioListenerTransform => AudioListener.transform;

		private float CurrentMaxMusicVolume { get; set; }
		private float CurrentMaxAtmosVolume { get; set; }

		[Header("Music & Ambiance:")]
		[SerializeField] private AudioSource musicAudiosource = null;
		[SerializeField] private AudioSource atmosAudiosource = null;

		[Space(10)]

		[SerializeField] private float volumeFadeSpeed = 8;

		[Header("SFX:")]
		[SerializeField] private AudioSource sfxAudiosource = null;

		[Space(10)]

		[SerializeField] private AudioClip navigate = null;
		[SerializeField] private AudioClip confirm = null;
		[SerializeField] private AudioClip cancel = null;

		public override void Boot()
		{
			base.Boot();

			AudioListener = GetComponentInChildren<AudioListener>();
			musicAudiosource.volume = atmosAudiosource.volume = 0f;

			AudioListenerTransform.SetParent(selfTransform);

			VoidOutput LinkAllAuraZonesInScene(VoidInput _)
			{
				var entities = FindObjectsByType<AuraSpatialEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
				int length = entities.Length;

				for (int i = 0; i < length; i++) Link(entities[i]);

				return default;
			}

			VoidOutput DisconnectAllZones(VoidInput _)
			{
				DisconnectAll();
				return default;
			}

			Nexus.OnBeforeSceneUnload.TryAddListener(DisconnectAllZones);
			Nexus.OnSceneFinishedLoading.TryAddListener(LinkAllAuraZonesInScene);
		}

		public override void Initialize()
		{
			ResetAudioListenerLocalPosition();
		}

		public override Entity Link<Entity>(Entity instance, bool logAction = false)
		{
			var linkedEntity = base.Link(instance, logAction);

			if (LinkedEntities.Count > 0) Iris.SubscribeToUpdate(CalculateSpatialInfluence);

			return linkedEntity;
		}

		public override void Disconnect(AuraSpatialEntity instance, bool logAction = false)
		{
			if (LinkedEntities.Count - 1 <= 0) Iris.UnsubscribeFromUpdate(CalculateSpatialInfluence);

			base.Disconnect(instance, logAction);
		}

		public override void DisconnectAll()
		{
			Iris.UnsubscribeFromUpdate(CalculateSpatialInfluence);
			base.DisconnectAll();
		}

		private VoidOutput CalculateSpatialInfluence(VoidInput _)
		{
			var listenerPos = AudioListenerTransform.position;
			float totalInfluence = 0f;

			for (int i = LinkedEntities.Count - 1; i >= 0; i--)
				totalInfluence += LinkedEntities[i].GetSpatialInfluence(listenerPos);

			MoveTowardsVolume(musicAudiosource, Mathf.Clamp01(CurrentMaxMusicVolume - totalInfluence));
			MoveTowardsVolume(atmosAudiosource, Mathf.Clamp01(CurrentMaxAtmosVolume - totalInfluence));

			return default;
		}

		public static void AttachAudioListenerTo(LinkableBehaviour owner, Transform parent)
		{
			VoidOutput ReattachListenerToAura(VoidInput _ = default)
			{
				AudioListenerTransform.SetParent(Instance.selfTransform);
				owner.OnBeforeDiscarded.Remove(ReattachListenerToAura);
				return default;
			}

			owner.OnBeforeDiscarded.TryAddListener(ReattachListenerToAura);
			AudioListenerTransform.SetParent(parent);
			ResetAudioListenerLocalPosition();
		}

		public static void SetGlobalVolumes(Vector2 volumes)
		{
			volumes.x = Mathf.Clamp01(volumes.x);
			volumes.y = Mathf.Clamp01(volumes.y);
			Instance.CurrentMaxMusicVolume = volumes.x;
			Instance.CurrentMaxAtmosVolume = volumes.y;
		}

		public static async UniTask AsyncFadeAudioListenerVolumeTo(float targetVolume)
		{
			targetVolume = Mathf.Clamp01(targetVolume);

			float speed = Instance.volumeFadeSpeed;

			while (Mathf.Approximately(AudioListener.volume, targetVolume) == false)
			{
				AudioListener.volume = Mathf.MoveTowards(AudioListener.volume, targetVolume, Chronos.UnscaledDeltaTime * speed);
				await UniTask.Yield();
			}
		}

		public static void PlayUISFX(UISFX uiSFX, float volume = 1f)
		{
			AudioClip sfx = null;

			switch (uiSFX)
			{
				case UISFX.Cancel:
				sfx = Instance.cancel;
				break;
				case UISFX.Nagivate:
				sfx = Instance.navigate;
				break;
				case UISFX.Confirm:
				sfx = Instance.confirm;
				break;
			}

			Instance.sfxAudiosource.PlayOneShot(sfx, volume);
		}

		public static async UniTask TransitionToAudioScenarioAsync(AudioClip musicClip, AudioClip atmosClip, Vector2 volumes)
		{
			var musicSource = Instance.musicAudiosource;
			var atmosSource = Instance.atmosAudiosource;

			await UniTask.WhenAll(AsyncFadeAudiosourceVolumeTo(musicSource, 0f),
			AsyncFadeAudiosourceVolumeTo(atmosSource, 0f));

			musicSource.Stop();
			atmosSource.Stop();

			await UniTask.NextFrame();

			musicSource.clip = musicClip;
			atmosSource.clip = atmosClip;

			if (musicSource.clip != null) musicSource.Play();
			if (atmosSource.clip != null) atmosSource.Play();

			await UniTask.NextFrame();

			volumes.x = Mathf.Clamp(volumes.x, 0f, Instance.CurrentMaxMusicVolume);
			volumes.y = Mathf.Clamp(volumes.y, 0f, Instance.CurrentMaxAtmosVolume);

			await UniTask.WhenAll(AsyncFadeAudiosourceVolumeTo(musicSource, volumes.x),
			AsyncFadeAudiosourceVolumeTo(atmosSource, volumes.y));
		}

		private static async UniTask AsyncFadeAudiosourceVolumeTo(AudioSource source, float targetVolume)
		{
			targetVolume = Mathf.Clamp01(targetVolume);

			while (Mathf.Approximately(source.volume, targetVolume) == false)
			{
				MoveTowardsVolume(source, targetVolume);
				await UniTask.Yield();
			}
		}

		private static void MoveTowardsVolume(AudioSource source, float targetVolume)
		{
			source.volume = Mathf.MoveTowards(source.volume, targetVolume, Chronos.UnscaledDeltaTime * Instance.volumeFadeSpeed);
		}

		private static void ResetAudioListenerLocalPosition()
		{
			AudioListenerTransform.localPosition = Vector3.zero;
		}
	}
}
