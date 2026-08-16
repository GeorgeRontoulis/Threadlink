namespace Threadlink.Core.NativeSubsystems.Aura
{
    using Chronos;
    using Core;
    using Cysharp.Threading.Tasks;
    using Generated;
    using Iris;
    using NativeSubsystems.Nexus;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;
    using UnityEngine;
    using Utilities.Mathematics;
    using NativeResources = Generated.ThreadlinkIDs.Addressables.NativeResources;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// Subsystem responsible for Audio Mixing during Threadlink's runtime.
    /// Provides Spatial Mixing for BGM and Atmos, audio transitions, fades etc.
    /// </summary>
    public sealed class Aura : Linker<Aura, AuraSpatialObject>,
    IAddressablesPreloader,
    IDependencyConsumer<AuraConfig>,
    IDependencyConsumer<Transform>
    {
        public enum UISFX : byte { Cancel, Navigate, Confirm }

        private AuraConfig Config { get; set; } = null;

        private AudioListener AudioListener { get; set; } = null;
        private Transform AudioListenerTransform { get; set; } = null;

        private AudioSource Music { get; set; } = null;
        private AudioSource Atmos { get; set; } = null;
        private AudioSource SFX { get; set; } = null;

        private float CurrentMaxMusicVolume { get; set; } = 0f;
        private float CurrentMaxAtmosVolume { get; set; } = 0f;

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return false;

            var nativeConfig = core.NativeConfig;

            var loadedResources = await UniTask.WhenAll
            (
                nativeConfig.LoadNativeResourceAsync<GameObject>(NativeResources.AuraComponentsPrefab),
                nativeConfig.LoadNativeResourceAsync<AuraConfig>(NativeResources.AuraConfig)
            );

            return TryConsumeDependency(loadedResources.Item1.transform) && TryConsumeDependency(loadedResources.Item2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(AuraConfig input) => (Config = input) != null;

        public bool TryConsumeDependency(Transform input)
        {
            if (input == null)
                return false;

            var components = UnityObject.Instantiate(input);

            components.name = input.name;
            //components.gameObject.hideFlags = HideFlags.HideInHierarchy;
            UnityObject.DontDestroyOnLoad(components.gameObject);

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

                UnityObject.DontDestroyOnLoad(AudioListener.gameObject);
                AudioListenerTransform = AudioListener.transform;
            }

            #region Callbacks:
            void OnLoadingProcessFinished(Nexus.ISceneEntry _ = null)
            {
                var spatialObjects = UnityObject.FindObjectsByType<AuraSpatialObject>(FindObjectsInactive.Exclude);

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
                Iris.Unsubscribe<Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
            }

            void DisconnectAllZones(Nexus.ISceneEntry _) => DisconnectAll();
            #endregion

            base.Boot();

            Music.volume = Atmos.volume = 0f;
            CreateAudioListener();

            Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnBeforeActiveSceneUnload, DisconnectAllZones);
            Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished, OnLoadingProcessFinished);
            Iris.Subscribe<Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
        }

        public override bool TryLink(AuraSpatialObject entity)
        {
            int previousCount = Registry.Count;
            bool linked = base.TryLink(entity);

            if (previousCount <= 0 && linked)
                Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, CalculateSpatialInfluence);

            return linked;
        }

        public override bool TryDisconnect<T>(int linkID, out T disconnectedObject)
        {
            bool disconnected = base.TryDisconnect(linkID, out disconnectedObject);

            if (disconnected && Registry.Count <= 0)
                Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, CalculateSpatialInfluence);

            return disconnected;
        }

        public override void DisconnectAll(bool trimRegistry = false)
        {
            Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, CalculateSpatialInfluence);
            base.DisconnectAll(trimRegistry);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DriveAudioListener(Vector3 worldPosition, Quaternion worldRotation)
        {
            AudioListenerTransform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DriveAudioListener(Vector3 worldPosition)
        {
            AudioListenerTransform.position = worldPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DriveAudioListener(quaternion worldRotation)
        {
            AudioListenerTransform.rotation = worldRotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMixerValue(string parameterName, out float result)
        {
            return Config.TryGetMixerValue(parameterName, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetMixerValue(string parameterName, float input)
        {
            return Config.TrySetMixerValue(parameterName, input);
        }

        private void CalculateSpatialInfluence()
        {
            var listenerPos = AudioListenerTransform.position;
            float totalInfluence = 0f;

            foreach (var entity in Registry.Values)
                totalInfluence += entity.GetSpatialInfluence(listenerPos);

            MoveTowardsVolume(Music, math.clamp(CurrentMaxMusicVolume - totalInfluence, 0f, 1f));
            MoveTowardsVolume(Atmos, math.clamp(CurrentMaxAtmosVolume - totalInfluence, 0f, 1f));
        }

        public void SetGlobalVolumesMax(float maxMusicVolume, float maxAtmosVolume)
        {
#if THREADLINK_MATHEMATICS
            CurrentMaxMusicVolume = Unity.Mathematics.math.clamp(musicVolume, 0f, 1f);
            CurrentMaxAtmosVolume = Unity.Mathematics.math.clamp(atmosVolume, 0f, 1f);
#else
            CurrentMaxMusicVolume = Mathf.Clamp01(maxMusicVolume);
            CurrentMaxAtmosVolume = Mathf.Clamp01(maxAtmosVolume);
#endif
        }

        public async UniTask FadeAudioListenerVolumeAsync(float targetVolume)
        {
            targetVolume = math.clamp(targetVolume, 0f, 1f);

            float speed = Config.VolumeFadeSpeed;

            while (!AudioListener.volume.IsSimilarTo(targetVolume))
            {
                AudioListener.volume = AudioListener.volume.MoveTowards(targetVolume, Chronos.UnscaledDeltaTime * speed);
                await Threadlink.WaitForFramesAsync(1);
            }
        }

        public void PlayUISFX(UISFX uiSFX, bool oneShot = true, float volume = 1f)
        {
            if (!Threadlink.TryGetSingleton(out var core))
                return;

            AudioClip sfx = uiSFX switch
            {
                UISFX.Cancel => core.LoadAsset<AudioClip>(Config.CancelClipPointer),
                UISFX.Navigate => core.LoadAsset<AudioClip>(Config.NavClipPointer),
                UISFX.Confirm => core.LoadAsset<AudioClip>(Config.ConfirmClipPointer),
                _ => null,
            };

            if (sfx != null)
            {
                if (!oneShot)
                    SFX.Stop();

                SFX.PlayOneShot(sfx, volume);
            }
        }

        public async UniTask TransitionToAudioScenarioAsync(AudioClip musicClip, AudioClip atmosClip, float musicVolume, float atmosVolume)
        {
            await UniTask.WhenAll
            (
                FadeAudiosourceVolumeAsync(Music, 0f),
                FadeAudiosourceVolumeAsync(Atmos, 0f)
            );

            Music.Stop();
            Atmos.Stop();

            await Threadlink.WaitForFramesAsync(1);

            Music.clip = musicClip;
            Atmos.clip = atmosClip;

            if (Music.clip != null)
                Music.Play();

            if (Atmos.clip != null)
                Atmos.Play();

            await Threadlink.WaitForFramesAsync(1);

            await UniTask.WhenAll
            (
                FadeAudiosourceVolumeAsync(Music, math.clamp(musicVolume, 0f, CurrentMaxMusicVolume)),
                FadeAudiosourceVolumeAsync(Atmos, math.clamp(atmosVolume, 0f, CurrentMaxAtmosVolume))
            );
        }

        public async UniTask FadeAudiosourceVolumeAsync(AudioSource source, float targetVolume)
        {
            targetVolume = math.clamp(targetVolume, 0f, 1f);

            while (!source.volume.IsSimilarTo(targetVolume))
            {
                MoveTowardsVolume(source, targetVolume);
                await Threadlink.WaitForFramesAsync(1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveTowardsVolume(AudioSource source, float targetVolume)
        {
            source.volume = source.volume.MoveTowards(targetVolume, Chronos.UnscaledDeltaTime * Config.VolumeFadeSpeed);
        }
    }
}
