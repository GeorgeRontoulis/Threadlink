namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Audio;

    /// <summary>Explicit playback completion reason.</summary>
    public enum PlaybackEnd
    {
        Completed,
        Stopped,
        Stolen,
        EngineDestroyed,
        Failed,
    }

    /// <summary>Optional audio backend extension contract. Implementations own admitted retention obligations.</summary>
    internal interface IAudioBackend
    {
        PlaybackHandle Play(
            AudioCue cue,
            Vector3 position,
            Transform emitter,
            bool stopWithEmitter,
            double dspStart,
            bool? loop,
            IDisposable retention
        );
    }

    /// <summary>One playing voice. Stop it, fade it, or wait for it to end.</summary>
    public sealed class PlaybackHandle : IDisposable
    {
        internal AuraEngine Engine;
        internal AudioSource Source;
        internal AudioCue Cue;
        internal IDisposable Retention;
        internal Transform Emitter;
        internal bool FollowEmitter,
            StopWithEmitter,
            Loop;
        internal bool Started;
        internal float BaseVolume,
            Gain = 1;
        internal double StartDsp,
            EndDsp;
        internal long Order;
        internal UniTaskCompletionSource<PlaybackEnd> Completed =
            new UniTaskCompletionSource<PlaybackEnd>();
        internal UniTaskCompletionSource<bool> Fade;
        internal float FadeStart,
            FadeTarget,
            FadeElapsed,
            FadeDuration;
        internal bool StopAfterFade;
        internal PlaybackHandle() { }

        internal UniTask<PlaybackEnd> Completion => Completed.Task;

        /// <summary>Waits until the voice ends and returns how it ended.</summary>
        public UniTask<PlaybackEnd> WaitAsync() => Completed.Task;
        public bool IsPlaying => Engine != null;
        public bool IsLoading => Engine != null && !Started;
        public Exception Error { get; internal set; }
        public double RemainingSeconds =>
            !IsPlaying ? 0
            : Loop ? double.PositiveInfinity
            : Math.Max(0, EndDsp - AudioSettings.dspTime);
        public Exception CleanupError { get; internal set; }

        /// <summary>Fades this voice's gain. A later fade replaces this one, which then completes as cancelled.</summary>
        public UniTask FadeToAsync(float gain, float seconds, bool stopWhenFinished = false) =>
            FadeTo(gain, seconds, stopWhenFinished);

        internal UniTask FadeTo(float gain, float seconds, bool stopWhenFinished = false)
        {
            if (Engine == null)
                return UniTask.CompletedTask;
            if (gain < 0 || gain > 1 || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(gain));
            var replaced = Fade;
            var fade = Fade = new UniTaskCompletionSource<bool>();
            FadeStart = Gain;
            FadeTarget = gain;
            FadeElapsed = 0;
            FadeDuration = seconds;
            StopAfterFade = stopWhenFinished;
            Engine.CancelFade(replaced);
            return fade.Task;
        }

        public void Stop() => Engine?.Stop(this, PlaybackEnd.Stopped);

        public void Dispose() => Stop();
    }

    /// <summary>Unity Audio backend with pooled sources, explicit cue/category concurrency and retained content ownership.</summary>
    [AddComponentMenu("")]
    internal sealed class AuraEngine : MonoBehaviour, IAudioBackend
    {
        private sealed class Category
        {
            internal int Limit = 32;
            internal float Volume = 1;
            internal bool Muted;
            internal UniTaskCompletionSource<bool> Fade;
            internal float Start,
                Target,
                Elapsed,
                Duration;
        }

        private readonly List<PlaybackHandle> active = new List<PlaybackHandle>();
        private readonly Stack<AudioSource> pool = new Stack<AudioSource>();
        private readonly Dictionary<string, Category> categories = new Dictionary<string, Category>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<EntityId, int> previousVariation =
            new Dictionary<EntityId, int>();
        private readonly Queue<EntityId> variationOrder = new Queue<EntityId>();
        private int poolCapacity = 64;
        private readonly System.Random random = new System.Random();
        private long nextOrder;
        private bool destroyed;

        private void Awake()
        {
            enabled = false;
        }

        public int ActiveVoices => active.Count;
        public int PooledSources => pool.Count;

        /// <summary>Maximum idle source retention. Reducing it immediately trims the idle pool.</summary>
        public int PoolCapacity
        {
            get => poolCapacity;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                poolCapacity = value;
                TrimPool(value);
            }
        }

        public void TrimPool(int retain = 0)
        {
            if (retain < 0)
                throw new ArgumentOutOfRangeException(nameof(retain));
            while (pool.Count > retain)
            {
                var source = pool.Pop();
                if (source != null)
                    Release(source.gameObject);
            }
        }

        public int LoadingVoices
        {
            get
            {
                int count = 0;
                foreach (var voice in active)
                    if (voice.IsLoading)
                        count++;
                return count;
            }
        }
        public long RejectedVoices { get; private set; }
        public long StolenVoices { get; private set; }
        public int RetainedContentVoices
        {
            get
            {
                int count = 0;
                foreach (var voice in active)
                    if (voice.Retention != null)
                        count++;
                return count;
            }
        }
        public int ActiveListeners
        {
            get
            {
                int count = 0;
                foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>())
                    if (listener.enabled && listener.gameObject.activeInHierarchy)
                        count++;
                return count;
            }
        }

        private Category GetCategory(string name)
        {
            if (!categories.TryGetValue(name, out var category))
                categories.Add(name, category = new Category());
            return category;
        }

        public void ConfigureCategory(
            string category,
            int voiceLimit,
            float userVolume = 1,
            bool muted = false
        )
        {
            if (
                string.IsNullOrWhiteSpace(category)
                || voiceLimit < 1
                || userVolume < 0
                || userVolume > 1
            )
                throw new ArgumentException("Invalid audio category.");
            var settings = GetCategory(category);
            settings.Limit = voiceLimit;
            settings.Volume = userVolume;
            settings.Muted = muted;
            ApplyVolumes();
        }

        internal void SetVoiceLimit(string category, int limit)
        {
            if (string.IsNullOrWhiteSpace(category) || limit < 1)
                throw new ArgumentException("Invalid audio category limit.");
            GetCategory(category).Limit = limit;
        }

        public void SetUserVolume(string category, float volume)
        {
            if (volume < 0 || volume > 1 || float.IsNaN(volume))
                throw new ArgumentOutOfRangeException(nameof(volume));
            var settings = GetCategory(category);
            var replaced = settings.Fade;
            settings.Fade = null;
            settings.Volume = volume;
            ApplyVolumes();
            CancelFade(replaced);
        }

        public void SetMute(string category, bool muted)
        {
            GetCategory(category).Muted = muted;
            ApplyVolumes();
        }

        /// <summary>Fades user category volume. A later category fade supersedes the previous one.</summary>
        public UniTask FadeCategoryAsync(string category, float volume, float seconds)
        {
            if (volume < 0 || volume > 1 || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(volume));
            var settings = GetCategory(category);
            var replaced = settings.Fade;
            if (active.Count == 0 || seconds == 0)
            {
                settings.Fade = null;
                settings.Volume = volume;
                ApplyVolumes();
                CancelFade(replaced);
                return UniTask.CompletedTask;
            }
            settings.Start = settings.Volume;
            settings.Target = volume;
            settings.Elapsed = 0;
            settings.Duration = seconds;
            var fade = settings.Fade = new UniTaskCompletionSource<bool>();
            enabled = true;
            CancelFade(replaced);
            return fade.Task;
        }

        public void TransitionSnapshot(AudioMixerSnapshot snapshot, float seconds)
        {
            if (snapshot == null || seconds < 0)
                throw new ArgumentException("Invalid mixer snapshot transition.");
            snapshot.TransitionTo(seconds);
        }

        /// <summary>Explicit advanced listener selection. The default engine never creates, replaces or disables scene listeners.</summary>
        public void SelectListener(AudioListener selected)
        {
            if (selected == null)
                throw new ArgumentNullException(nameof(selected));
            foreach (
                var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include
                )
            )
                listener.enabled = listener == selected;
            selected.enabled = true;
        }

        /// <summary>Plays a cue, optionally following an emitter or starting on the DSP timeline. Retention transfers on admission.</summary>
        /// <param name="initialGain">Starting voice gain in [0, 1]; use 0 with <see cref="PlaybackHandle.FadeToAsync"/> for fade-ins.</param>
        public PlaybackHandle Play(
            AudioCue cue,
            Vector3 position = default,
            Transform emitter = null,
            bool stopWithEmitter = false,
            double dspStart = 0,
            bool? loop = null,
            IDisposable retention = null,
            float initialGain = 1
        )
        {
            if (float.IsNaN(initialGain) || initialGain < 0 || initialGain > 1)
                throw new ArgumentOutOfRangeException(nameof(initialGain));
            if (destroyed)
                throw new ObjectDisposedException(nameof(AuraEngine));
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            if (
                double.IsNaN(dspStart)
                || double.IsInfinity(dspStart)
                || dspStart < 0
                || (stopWithEmitter && emitter == null)
            )
                throw new ArgumentException("Invalid scheduled start or emitter lifetime policy.");
            cue.Validate();
            if (ActiveListeners != 1)
                throw new InvalidOperationException(
                    "Select exactly one active AudioListener before playback."
                );
            if (cue.LoadingPolicy == CueLoadingPolicy.PreloadVariations)
                cue.WarmAudioData();
            var category = GetCategory(cue.CategoryName);
            int cueCount = 0,
                categoryCount = 0;
            foreach (var voice in active)
            {
                if (voice.Cue == cue)
                    cueCount++;
                if (voice.Cue.CategoryName == cue.CategoryName)
                    categoryCount++;
            }
            if (cueCount >= cue.VoiceLimit || categoryCount >= category.Limit)
            {
                PlaybackHandle victim = null;
                if (cue.Overflow == VoiceOverflow.StealLowestPriority)
                    foreach (var voice in active)
                    {
                        bool eligible =
                            cueCount >= cue.VoiceLimit
                                ? voice.Cue == cue
                                : voice.Cue.CategoryName == cue.CategoryName;
                        if (
                            eligible
                            && voice.Cue.Priority >= cue.Priority
                            && (
                                victim == null
                                || voice.Cue.Priority > victim.Cue.Priority
                                || (
                                    voice.Cue.Priority == victim.Cue.Priority
                                    && voice.Order < victim.Order
                                )
                            )
                        )
                            victim = voice;
                    }
                if (victim == null)
                {
                    RejectedVoices++;
                    return null;
                }
                StolenVoices++;
                Stop(victim, PlaybackEnd.Stolen);
            }
            EntityId cueKey = cue.GetEntityId();
            int previous = previousVariation.TryGetValue(cueKey, out var last) ? last : -1;
            double total = 0;
            for (int i = 0; i < cue.Variations.Length; i++)
                if (!(cue.AvoidImmediateRepeat && cue.Variations.Length > 1 && i == previous))
                    total += cue.Variations[i].Weight;
            double selectedWeight = random.NextDouble() * total;
            int index = 0;
            for (int i = 0; i < cue.Variations.Length; i++)
            {
                if (cue.AvoidImmediateRepeat && cue.Variations.Length > 1 && i == previous)
                    continue;
                index = i;
                selectedWeight -= cue.Variations[i].Weight;
                if (selectedWeight <= 0)
                    break;
            }
            if (!previousVariation.ContainsKey(cueKey))
            {
                if (previousVariation.Count >= 1024)
                    previousVariation.Remove(variationOrder.Dequeue());
                variationOrder.Enqueue(cueKey);
            }
            previousVariation[cueKey] = index;
            var clip = cue.Variations[index].Clip;
            var source =
                pool.Count > 0
                    ? pool.Pop()
                    : new GameObject("Aura Voice").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.gameObject.SetActive(true);
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = loop ?? cue.Loop;
            source.outputAudioMixerGroup = cue.MixerGroup;
            source.priority = cue.Priority;
            source.spatialBlend = cue.SpatialBlend;
            source.minDistance = cue.MinimumDistance;
            source.maxDistance = cue.MaximumDistance;
            source.pitch = Mathf.Lerp(cue.Pitch.x, cue.Pitch.y, (float)random.NextDouble());
            source.transform.position = emitter != null ? emitter.position : position;
            var handle = new PlaybackHandle
            {
                Engine = this,
                Source = source,
                Cue = cue,
                Retention = retention,
                Emitter = emitter,
                FollowEmitter = emitter != null,
                StopWithEmitter = stopWithEmitter,
                Loop = source.loop,
                BaseVolume = Mathf.Lerp(cue.Volume.x, cue.Volume.y, (float)random.NextDouble()),
                Order = ++nextOrder,
                StartDsp = Math.Max(AudioSettings.dspTime, dspStart),
                Gain = initialGain,
            };
            handle.EndDsp = double.PositiveInfinity;
            active.Add(handle);
            ApplyVolume(handle);
            enabled = true;
            if (clip.loadState == AudioDataLoadState.Loaded)
                BeginVoice(handle);
            else
                clip.LoadAudioData();
            return handle;
        }

        private void BeginVoice(PlaybackHandle handle)
        {
            handle.Started = true;
            handle.StartDsp = Math.Max(AudioSettings.dspTime + 0.01, handle.StartDsp);
            handle.EndDsp = handle.StartDsp + handle.Source.clip.length / handle.Source.pitch;
            handle.Source.PlayScheduled(handle.StartDsp);
        }

        PlaybackHandle IAudioBackend.Play(
            AudioCue cue,
            Vector3 position,
            Transform emitter,
            bool stopWithEmitter,
            double dspStart,
            bool? loop,
            IDisposable retention
        ) => Play(cue, position, emitter, stopWithEmitter, dspStart, loop, retention);

        internal void Stop(PlaybackHandle voice, PlaybackEnd reason)
        {
            settling++;
            try
            {
                StopVoice(voice, reason);
            }
            finally
            {
                EndSettling();
            }
        }

        private void StopVoice(PlaybackHandle voice, PlaybackEnd reason)
        {
            if (voice.Engine != this)
                return;
            voice.Engine = null;
            active.Remove(voice);
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.outputAudioMixerGroup = null;
            voice.Source.gameObject.SetActive(false);
            if (pool.Count < poolCapacity)
                pool.Push(voice.Source);
            else
                Release(voice.Source.gameObject);
            voice.Source = null;
            if (voice.Fade != null)
                cancelledFades.Add(voice.Fade);
            voice.Fade = null;
            var retention = voice.Retention;
            voice.Retention = null;
            try
            {
                retention?.Dispose();
            }
            catch (Exception error)
            {
                voice.CleanupError = error;
            }
            voice.Cue = null;
            voice.Emitter = null;
            endedVoices.Add((voice.Completed, reason));
            if (active.Count == 0)
            {
                foreach (var category in categories.Values)
                    if (category.Fade != null)
                    {
                        category.Volume = category.Target;
                        finishedFades.Add(category.Fade);
                        category.Fade = null;
                    }
                enabled = false;
            }
        }

        private void ApplyVolume(PlaybackHandle voice)
        {
            var category = GetCategory(voice.Cue.CategoryName);
            voice.Source.volume = category.Muted
                ? 0
                : voice.BaseVolume * voice.Gain * category.Volume;
        }

        private void ApplyVolumes()
        {
            foreach (var voice in active)
                ApplyVolume(voice);
        }

        private void Update()
        {
            settling++;
            try
            {
                Tick();
            }
            finally
            {
                EndSettling();
            }
        }

        private void Tick()
        {
            foreach (var category in categories.Values)
                if (category.Fade != null)
                {
                    category.Elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(category.Elapsed / category.Duration);
                    category.Volume = Mathf.Lerp(category.Start, category.Target, progress);
                    if (progress >= 1)
                    {
                        finishedFades.Add(category.Fade);
                        category.Fade = null;
                    }
                }
            ApplyVolumes();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var voice = active[i];
                if (!voice.Started)
                {
                    if (voice.Source.clip.loadState == AudioDataLoadState.Failed)
                    {
                        voice.Error = new InvalidOperationException(
                            "Unity failed to prepare the clip's audio data."
                        );
                        Stop(voice, PlaybackEnd.Failed);
                        continue;
                    }
                    if (voice.Source.clip.loadState == AudioDataLoadState.Loaded)
                        BeginVoice(voice);
                }
                if (voice.FollowEmitter)
                {
                    if (voice.Emitter != null)
                        voice.Source.transform.position = voice.Emitter.position;
                    else if (voice.StopWithEmitter)
                    {
                        Stop(voice, PlaybackEnd.Stopped);
                        continue;
                    }
                }
                if (voice.Fade != null)
                {
                    voice.FadeElapsed += Time.unscaledDeltaTime;
                    float progress =
                        voice.FadeDuration == 0
                            ? 1
                            : Mathf.Clamp01(voice.FadeElapsed / voice.FadeDuration);
                    voice.Gain = Mathf.Lerp(voice.FadeStart, voice.FadeTarget, progress);
                    ApplyVolume(voice);
                    if (progress >= 1)
                    {
                        finishedFades.Add(voice.Fade);
                        voice.Fade = null;
                        if (voice.StopAfterFade)
                        {
                            StopVoice(voice, PlaybackEnd.Stopped);
                            continue;
                        }
                    }
                }
                if (!voice.Loop && AudioSettings.dspTime >= voice.EndDsp && !voice.Source.isPlaying)
                    StopVoice(voice, PlaybackEnd.Completed);
            }
        }

        // Completions resolve only once the engine's state is settled, so code awaiting a fade or a voice never sees a
        // half-updated voice list and may play, stop or fade again from its continuation. The lists are reused, so
        // settling allocates nothing once warm.
        private readonly List<UniTaskCompletionSource<bool>> finishedFades = new List<UniTaskCompletionSource<bool>>();
        private readonly List<UniTaskCompletionSource<bool>> cancelledFades = new List<UniTaskCompletionSource<bool>>();
        private readonly List<(UniTaskCompletionSource<PlaybackEnd> Source, PlaybackEnd End)> endedVoices =
            new List<(UniTaskCompletionSource<PlaybackEnd>, PlaybackEnd)>();
        private int settling;

        internal void CancelFade(UniTaskCompletionSource<bool> fade)
        {
            if (fade == null)
                return;
            cancelledFades.Add(fade);
            if (settling == 0)
                Resolve();
        }

        private void EndSettling()
        {
            if (--settling == 0)
                Resolve();
        }

        private void Resolve()
        {
            settling++;
            try
            {
                int ended = 0,
                    finished = 0,
                    cancelled = 0;
                while (ended < endedVoices.Count || finished < finishedFades.Count || cancelled < cancelledFades.Count)
                {
                    while (ended < endedVoices.Count)
                    {
                        var voice = endedVoices[ended++];
                        voice.Source.TrySetResult(voice.End);
                    }
                    while (finished < finishedFades.Count)
                        finishedFades[finished++].TrySetResult(true);
                    while (cancelled < cancelledFades.Count)
                        cancelledFades[cancelled++].TrySetCanceled();
                }
            }
            finally
            {
                endedVoices.Clear();
                finishedFades.Clear();
                cancelledFades.Clear();
                settling--;
            }
        }

        // Engines may be torn down by service shutdown after leaving Play mode, where Destroy is not allowed.
        private static void Release(GameObject target)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            destroyed = true;
            while (active.Count > 0)
                Stop(active[active.Count - 1], PlaybackEnd.EngineDestroyed);
            while (pool.Count > 0)
            {
                var source = pool.Pop();
                if (source != null)
                    Release(source.gameObject);
            }
            previousVariation.Clear();
            variationOrder.Clear();
            categories.Clear();
        }
    }
}
