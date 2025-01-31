namespace Threadlink.Core.Subsystems.Aura
{
	using Chronos;
	using Core;
	using Cysharp.Threading.Tasks;
	using Propagator;
	using System;
	using UnityEngine;

	/// <summary>
	/// System responsible for Audio Mixing during Threadlink's runtime.
	/// Provides Spatial Mixing for BGM and Atmos, audio transitions, fades etc.
	/// </summary>
	public sealed class Aura : Linker<Aura, AuraSpatialEntity>
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

		public override void Discard()
		{
			AudioListenerTransform.SetParent(cachedTransform);

			AudioListener = null;
			musicAudiosource = null;
			atmosAudiosource = null;
			sfxAudiosource = null;
			navigate = null;
			confirm = null;
			cancel = null;

			base.Discard();
		}

		public override void Boot()
		{
			void LinkAllAuraZonesInScene()
			{
				var entities = FindObjectsByType<AuraSpatialEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
				int length = entities.Length;

				for (int i = 0; i < length; i++) TryLink(entities[i]);
			}

			void DisconnectAllZones() => DisconnectAll();

			base.Boot();

			AudioListener = GetComponentInChildren<AudioListener>();
			musicAudiosource.volume = atmosAudiosource.volume = 0f;

			AudioListenerTransform.SetParent(cachedTransform);

			Propagator.Subscribe<Action>(PropagatorEvents.OnBeforeActiveSceneUnload, DisconnectAllZones);
			Propagator.Subscribe<Action>(PropagatorEvents.OnLoadingProcessFinished, LinkAllAuraZonesInScene);

			ResetAudioListenerLocalPosition();
		}

		public override bool TryLink(AuraSpatialEntity entity)
		{
			bool linked = base.TryLink(entity);

			if (linked && Registry.Count > 0)
				Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, CalculateSpatialInfluence);

			return linked;
		}

		public override bool TryDisconnect(Ulid linkID, out AuraSpatialEntity disconnectedEntity)
		{
			bool disconnected = base.TryDisconnect(linkID, out disconnectedEntity);

			if (disconnected && Registry.Count - 1 <= 0)
				Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, CalculateSpatialInfluence);

			return disconnected;
		}

		public override void DisconnectAll(bool trimRegistry = false)
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, CalculateSpatialInfluence);
			base.DisconnectAll(trimRegistry);
		}

		private void CalculateSpatialInfluence()
		{
			var listenerPos = AudioListenerTransform.position;
			float totalInfluence = 0f;

			foreach (var entity in Registry.Values) totalInfluence += entity.GetSpatialInfluence(listenerPos);

			MoveTowardsVolume(musicAudiosource, Mathf.Clamp01(CurrentMaxMusicVolume - totalInfluence));
			MoveTowardsVolume(atmosAudiosource, Mathf.Clamp01(CurrentMaxAtmosVolume - totalInfluence));
		}

		public static void AttachAudioListenerTo(LinkableBehaviour owner, Transform parent)
		{
			void ReattachListenerToAura()
			{
				AudioListenerTransform.SetParent(Instance.cachedTransform);
				owner.OnDiscard -= ReattachListenerToAura;
			}

			owner.OnDiscard += ReattachListenerToAura;
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

		public static async UniTask FadeAudioListenerVolumeAsync(float targetVolume)
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

			if (sfx != null) Instance.sfxAudiosource.PlayOneShot(sfx, volume);
		}

		public static async UniTask TransitionToAudioScenarioAsync(AudioClip musicClip, AudioClip atmosClip, Vector2 volumes)
		{
			var musicSource = Instance.musicAudiosource;
			var atmosSource = Instance.atmosAudiosource;

			await UniTask.WhenAll(FadeAudiosourceVolumeAsync(musicSource, 0f),
			FadeAudiosourceVolumeAsync(atmosSource, 0f));

			musicSource.Stop();
			atmosSource.Stop();

			await Threadlink.WaitForFrames(1);

			musicSource.clip = musicClip;
			atmosSource.clip = atmosClip;

			if (musicSource.clip != null) musicSource.Play();
			if (atmosSource.clip != null) atmosSource.Play();

			await Threadlink.WaitForFrames(1);

			volumes.x = Mathf.Clamp(volumes.x, 0f, Instance.CurrentMaxMusicVolume);
			volumes.y = Mathf.Clamp(volumes.y, 0f, Instance.CurrentMaxAtmosVolume);

			await UniTask.WhenAll(FadeAudiosourceVolumeAsync(musicSource, volumes.x),
			FadeAudiosourceVolumeAsync(atmosSource, volumes.y));
		}

		private static async UniTask FadeAudiosourceVolumeAsync(AudioSource source, float targetVolume)
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
