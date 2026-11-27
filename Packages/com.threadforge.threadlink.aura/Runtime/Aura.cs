namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// Aura: audio for the session. Plays <see cref="AudioCue"/> assets with variations, categories, voice limits and
    /// fades on pooled Unity audio sources, plus one crossfading music channel. No audio component needs to be placed.
    /// </summary>
    /// <remarks>Playback needs exactly one active AudioListener. A rejected voice (category or cue limit) returns null.</remarks>
    public sealed class Aura : ThreadlinkSubsystem<Aura>
    {
        private GameObject host;
        private PlaybackHandle music;
        private AudioCue musicCue;

        private ContentLibrary content;

        internal AuraEngine Engine { get; private set; }

        /// <summary>The music cue playing on the music channel, if any.</summary>
        public static AudioCue CurrentMusic => Instance.music != null && Instance.music.IsPlaying ? Instance.musicCue : null;

        /// <inheritdoc />
        protected override void OnSubsystemBoot()
        {
            host = new GameObject("Threadlink Aura") { hideFlags = HideFlags.DontSave };
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(host);
            Engine = host.AddComponent<AuraEngine>();
            AuraSettings.Apply(Engine);
        }

        // Mapped cues load through the content library.
        /// <inheritdoc />
        protected override void OnSubsystemInit() => content = ContentLibrary.Instance;

        /// <inheritdoc />
        protected override void OnSubsystemDeinit() => content = null;

        /// <inheritdoc />
        protected override void OnSubsystemShutdown()
        {
            music = null;
            musicCue = null;
            if (host == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(host);
            else
                Object.DestroyImmediate(host);
        }

        /// <inheritdoc />
        public override void Report(MonitorReport report)
        {
            report.Summary(Engine.ActiveVoices + " voices");
            if (!report.Detailed)
                return;
            report.Section("Voices");
            report.Field("Playing", Engine.ActiveVoices);
            report.Field("Loading", Engine.LoadingVoices);
            report.Field("Pooled sources", Engine.PooledSources, MonitorTone.Muted);
            report.Field("Rejected", Engine.RejectedVoices, Engine.RejectedVoices > 0 ? MonitorTone.Caution : MonitorTone.Muted);
            report.Field("Stolen", Engine.StolenVoices, MonitorTone.Muted);
            report.Section("Music");
            report.Field("Playing", CurrentMusic != null ? CurrentMusic.name : "—");
            report.Field("Listeners", Engine.ActiveListeners, Engine.ActiveListeners == 1 ? MonitorTone.Good : MonitorTone.Bad);
        }

        /// <summary>Plays a cue without position (2D, or at the listener for 3D cues).</summary>
        public static PlaybackHandle Play(AudioCue cue) => Instance.Engine.Play(cue);

        /// <summary>Plays a cue at a world position.</summary>
        public static PlaybackHandle Play(AudioCue cue, Vector3 position) => Instance.Engine.Play(cue, position);

        /// <summary>Plays a cue that follows <paramref name="emitter"/>; it stops when the emitter is destroyed.</summary>
        public static PlaybackHandle Play(AudioCue cue, Transform emitter) =>
            Instance.Engine.Play(cue, emitter != null ? emitter.position : default, emitter, stopWithEmitter: emitter != null);

        /// <summary>Loads a mapped cue (a ContentAssets ID) and plays it; the cue stays loaded while it plays.</summary>
        public static async UniTask<PlaybackHandle> PlayAsync(ContentAssetId cue, CancellationToken token = default) =>
            await Instance.Engine.PlayCueAsync(
                (Instance.content ?? ContentLibrary.Instance).Provider,
                cue.IsAssigned ? new ResourceId<AudioCue>(cue.Identity) : throw new System.ArgumentException("The cue ID is empty.", nameof(cue)),
                token
            );

        /// <summary>Plays <paramref name="cue"/> looping on the music channel, crossfading from the current music.</summary>
        /// <remarks>Playing the cue that is already playing does nothing. Pass null to fade the music out.</remarks>
        public static void PlayMusic(AudioCue cue, float crossfadeSeconds = 1f)
        {
            var aura = Instance;
            if (cue == aura.musicCue && aura.music != null && aura.music.IsPlaying)
                return;
            var previous = CurrentMusic;
            aura.Silence(crossfadeSeconds);
            var next = cue != null ? aura.Engine.Play(cue, loop: true, initialGain: crossfadeSeconds > 0 ? 0 : 1) : null;
            AudioCue playing = null;
            if (next != null)
            {
                if (crossfadeSeconds > 0)
                    _ = next.FadeTo(1, crossfadeSeconds);
                aura.music = next;
                aura.musicCue = playing = cue;
            }
            if (previous != playing)
                Iris.TryNotify(new MusicChanged(previous, playing));
        }

        /// <summary>Fades the music channel out.</summary>
        public static void StopMusic(float fadeSeconds = 1f)
        {
            var aura = Instance;
            var previous = CurrentMusic;
            aura.Silence(fadeSeconds);
            if (previous != null)
                Iris.TryNotify(new MusicChanged(previous, null));
        }

        private void Silence(float fadeSeconds)
        {
            if (music == null)
                return;
            if (fadeSeconds > 0 && music.IsPlaying)
                _ = music.FadeTo(0, fadeSeconds, true);
            else
                music.Stop();
            music = null;
            musicCue = null;
        }

        /// <summary>Sets a category's player volume, from 0 to 1, such as <c>Aura.SetVolume(AudioCategories.Music, 0.8f)</c>.</summary>
        public static void SetVolume(AudioCategory category, float volume)
        {
            Instance.Engine.SetUserVolume(category.Name, volume);
            Iris.TryNotify(new VolumeChanged(category, volume));
        }

        /// <summary>Mutes or unmutes a category.</summary>
        public static void SetMute(AudioCategory category, bool muted)
        {
            Instance.Engine.SetMute(category.Name, muted);
            Iris.TryNotify(new MuteChanged(category, muted));
        }

        /// <summary>Fades a category's player volume. A later fade or <see cref="SetVolume"/> replaces it.</summary>
        /// <remarks><see cref="VolumeChanged"/> is announced when the fade completes; a replaced fade announces nothing.</remarks>
        public static async UniTask FadeVolumeAsync(AudioCategory category, float volume, float seconds)
        {
            await Instance.Engine.FadeCategoryAsync(category.Name, volume, seconds);
            Iris.TryNotify(new VolumeChanged(category, volume));
        }

        /// <summary>Sets how many voices a category may play at once.</summary>
        public static void SetVoiceLimit(AudioCategory category, int voices) => Instance.Engine.SetVoiceLimit(category.Name, voices);

        /// <summary>Makes <paramref name="listener"/> the only enabled AudioListener.</summary>
        public static void UseListener(AudioListener listener) => Instance.Engine.SelectListener(listener);
    }
}
