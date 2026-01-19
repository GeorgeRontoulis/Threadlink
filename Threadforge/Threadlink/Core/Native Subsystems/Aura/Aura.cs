namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Chronos;
    using Core;
    using Cysharp.Threading.Tasks;
    using Iris;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Audio;
    using Utilities.Mathematics;

    /// <summary>
    /// Subsystem responsible for Audio Mixing during Threadlink's runtime.
    /// Provides Spatial Mixing for BGM and Atmos, audio transitions, fades etc.
    /// </summary>
    public sealed class Aura : Linker<Aura, AuraSpatialObject>,
    IAddressablesPreloader,
    IDependencyConsumer<AuraConfig>,
    IDependencyConsumer<Transform>,
    IDependencyConsumer<AudioMixer>
    {
        public enum UISFX : byte { Cancel, Nagivate, Confirm }

        private AuraConfig Config { get; set; }
        private AudioMixer Mixer { get; set; }

        private AudioListener AudioListener { get; set; }
        private Transform AudioListenerTransform { get; set; }

        private AudioSource Music { get; set; }
        private AudioSource Atmos { get; set; }
        private AudioSource SFX { get; set; }

        private float CurrentMaxMusicVolume { get; set; }
        private float CurrentMaxAtmosVolume { get; set; }

        public override void Discard()
        {
            ReattachListenerToAura();
            base.Discard();
        }

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            var loadedResources = await Threadlink.Instance.NativeConfig.LoadAuraResourcesAsync();

            return TryConsumeDependency(loadedResources.Item2)
            && TryConsumeDependency(loadedResources.Item1)
            && TryConsumeDependency(loadedResources.Item3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(AudioMixer input) => (Mixer = input) != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(AuraConfig input) => (Config = input) != null;

        public bool TryConsumeDependency(Transform input)
        {
            if (input == null)
                return false;

            var components = UnityEngine.Object.Instantiate(input);

            components.name = input.name;
            components.gameObject.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(components.gameObject);

            Music = components.Find(nameof(Music)).GetComponent<AudioSource>();
            Atmos = components.Find(nameof(Atmos)).GetComponent<AudioSource>();
            SFX = components.Find(nameof(SFX)).GetComponent<AudioSource>();

            return Music != null && Atmos != null && SFX != null;
        }

        public override void Boot()
        {
            void CreateAudioListener()
            {
                var audioListenerType = typeof(AudioListener);

                AudioListener = new GameObject(audioListenerType.Name, audioListenerType)
                {
                    hideFlags = HideFlags.HideInHierarchy
                }
                .GetComponent<AudioListener>();

                UnityEngine.Object.DontDestroyOnLoad(AudioListener.gameObject);
                AudioListenerTransform = AudioListener.transform;
                ReattachListenerToAura();
            }

            #region Callbacks:
            void OnLoadingProcessFinished()
            {
                var spatialObjects = UnityEngine.Object.FindObjectsByType<AuraSpatialObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                if (spatialObjects != null)
                {
                    int length = spatialObjects.Length;

                    for (int i = 0; i < length; i++)
                        TryLink(spatialObjects[i]);
                }
            }

            void OnCoreDeployed(Threadlink core)
            {
                OnLoadingProcessFinished();
                Iris.Unsubscribe<Action<Threadlink>>(Iris.Events.OnCoreDeployed, OnCoreDeployed);
            }

            void DisconnectAllZones() => DisconnectAll();
            #endregion

            base.Boot();

            Music.volume = Atmos.volume = 0f;
            CreateAudioListener();

            Iris.Subscribe<Action>(Iris.Events.OnBeforeActiveSceneUnload, DisconnectAllZones);
            Iris.Subscribe<Action>(Iris.Events.OnLoadingProcessFinished, OnLoadingProcessFinished);
            Iris.Subscribe<Action<Threadlink>>(Iris.Events.OnCoreDeployed, OnCoreDeployed);
        }

        public override bool TryLink(AuraSpatialObject entity)
        {
            int previousCount = Registry.Count;
            bool linked = base.TryLink(entity);

            if (previousCount <= 0 && linked)
                Iris.Subscribe<Action>(Iris.Events.OnUpdate, CalculateSpatialInfluence);

            return linked;
        }

        public override bool TryDisconnect<T>(int linkID, out T disconnectedObject)
        {
            bool disconnected = base.TryDisconnect(linkID, out disconnectedObject);

            if (disconnected && Registry.Count <= 0)
                Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, CalculateSpatialInfluence);

            return disconnected;
        }

        public override void DisconnectAll(bool trimRegistry = false)
        {
            Iris.Unsubscribe<Action>(Iris.Events.OnUpdate, CalculateSpatialInfluence);
            base.DisconnectAll(trimRegistry);
        }

        private void CalculateSpatialInfluence()
        {
            var listenerPos = AudioListenerTransform.position;
            float totalInfluence = 0f;

            foreach (var entity in Registry.Values) totalInfluence += entity.GetSpatialInfluence(listenerPos);

            MoveTowardsVolume(Music, math.clamp(CurrentMaxMusicVolume - totalInfluence, 0f, 1f));
            MoveTowardsVolume(Atmos, math.clamp(CurrentMaxAtmosVolume - totalInfluence, 0f, 1f));
        }

        public static void AttachAudioListenerTo(LinkableBehaviour owner, Transform parent)
        {
            owner.OnDiscard += Instance.ReattachListenerToAura;
            Instance.AudioListenerTransform.SetParent(parent);
            Instance.ResetAudioListenerLocalPosition();
        }

        public static void SetGlobalVolumes(float musicVolume, float atmosVolume)
        {
#if THREADLINK_MATHEMATICS
            Instance.CurrentMaxMusicVolume = Unity.Mathematics.math.clamp(musicVolume, 0f, 1f);
            Instance.CurrentMaxAtmosVolume = Unity.Mathematics.math.clamp(atmosVolume, 0f, 1f);
#else
            Instance.CurrentMaxMusicVolume = Mathf.Clamp01(musicVolume);
            Instance.CurrentMaxAtmosVolume = Mathf.Clamp01(atmosVolume);
#endif
        }

        public static async UniTask FadeAudioListenerVolumeAsync(float targetVolume)
        {
            targetVolume = math.clamp(targetVolume, 0f, 1f);

            float speed = Instance.Config.VolumeFadeSpeed;

            while (!AudioListener.volume.IsSimilarTo(targetVolume))
            {
                AudioListener.volume = AudioListener.volume.MoveTowards(targetVolume, Chronos.UnscaledDeltaTime * speed);
                await UniTask.Yield();
            }
        }

        public static void PlayUISFX(UISFX uiSFX, float volume = 1f)
        {
            var config = Instance.Config;

            AudioClip sfx = uiSFX switch
            {
                UISFX.Cancel => Threadlink.LoadAsset<AudioClip>(config.CancelClipPointer),
                UISFX.Nagivate => Threadlink.LoadAsset<AudioClip>(config.NavClipPointer),
                UISFX.Confirm => Threadlink.LoadAsset<AudioClip>(config.ConfirmClipPointer),
                _ => null,
            };

            if (sfx != null)
                Instance.SFX.PlayOneShot(sfx, volume);
        }

        public static async UniTask TransitionToAudioScenarioAsync(AudioClip musicClip, AudioClip atmosClip, float musicVolume, float atmosVolume)
        {
            var musicSource = Instance.Music;
            var atmosSource = Instance.Atmos;

            await UniTask.WhenAll
            (
                Instance.FadeAudiosourceVolumeAsync(musicSource, 0f),
                Instance.FadeAudiosourceVolumeAsync(atmosSource, 0f)
            );

            musicSource.Stop();
            atmosSource.Stop();

            await Threadlink.WaitForFramesAsync(1);

            musicSource.clip = musicClip;
            atmosSource.clip = atmosClip;

            if (musicSource.clip != null)
                musicSource.Play();

            if (atmosSource.clip != null)
                atmosSource.Play();

            await Threadlink.WaitForFramesAsync(1);

            await UniTask.WhenAll
            (
                Instance.FadeAudiosourceVolumeAsync(musicSource, math.clamp(musicVolume, 0f, Instance.CurrentMaxMusicVolume)),
                Instance.FadeAudiosourceVolumeAsync(atmosSource, math.clamp(atmosVolume, 0f, Instance.CurrentMaxAtmosVolume))
            );
        }

        private async UniTask FadeAudiosourceVolumeAsync(AudioSource source, float targetVolume)
        {
            targetVolume = math.clamp(targetVolume, 0f, 1f);

            while (!source.volume.IsSimilarTo(targetVolume))
            {
                MoveTowardsVolume(source, targetVolume);
                await UniTask.Yield();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveTowardsVolume(AudioSource source, float targetVolume)
        {
            source.volume = source.volume.MoveTowards(targetVolume, Chronos.UnscaledDeltaTime * Config.VolumeFadeSpeed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetAudioListenerLocalPosition() => AudioListenerTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        private void ReattachListenerToAura(LinkableBehaviour owner = null)
        {
            ReattachListenerToAura();

            if (owner != null)
                owner.OnDiscard -= ReattachListenerToAura;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReattachListenerToAura()
        {
            AudioListenerTransform.SetParent(null);
            ResetAudioListenerLocalPosition();
        }
    }
}
