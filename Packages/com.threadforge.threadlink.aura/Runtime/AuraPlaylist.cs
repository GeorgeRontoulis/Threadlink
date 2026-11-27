namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>Explicit playlist/crossfade controller. Requires caller-retained cue definitions; no adaptive score graph is implied.</summary>
    public sealed class AuraPlaylist : MonoBehaviour, IDisposable
    {
        private AuraEngine engine;
        private AudioCue[] cues;
        private PlaybackHandle current;
        private readonly List<PlaybackHandle> playing = new List<PlaybackHandle>();
        private int index;
        private float crossfade;
        private bool repeat;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>Plays the cues in order, crossfading between them.</summary>
        public void StartPlaylist(
            AudioCue[] cues,
            float crossfadeSeconds = 0,
            bool repeat = true
        )
        {
            Dispose();
            var engine = Aura.Instance.Engine;
            if (cues == null || cues.Length == 0 || crossfadeSeconds < 0)
                throw new ArgumentException("Invalid playlist.");
            foreach (var cue in cues)
                cue.Validate();
            this.engine = engine;
            this.cues = (AudioCue[])cues.Clone();
            this.crossfade = crossfadeSeconds;
            this.repeat = repeat;
            index = 0;
            current =
                engine.Play(this.cues[index], loop: false)
                ?? throw new InvalidOperationException("Playlist voice rejected.");
            playing.Add(current);
            enabled = true;
        }

        private void Update()
        {
            playing.RemoveAll(voice => !voice.IsPlaying);
            if (current == null || (current.IsPlaying && current.RemainingSeconds > crossfade))
                return;
            if (index + 1 >= cues.Length && !repeat)
            {
                if (!current.IsPlaying)
                {
                    current = null;
                    enabled = false;
                }
                return;
            }
            var next = engine.Play(cues[(index + 1) % cues.Length], loop: false);
            if (next == null)
                return;
            if (crossfade > 0)
            {
                next.Gain = 0;
                next.Source.volume = 0;
                _ = next.FadeTo(1, crossfade);
                _ = current.FadeTo(0, crossfade, true);
            }
            else
                current.Stop();
            index = (index + 1) % cues.Length;
            current = next;
            playing.Add(next);
        }

        /// <summary>Stops all playlist-owned voices, including outgoing crossfades.</summary>
        public void Dispose()
        {
            foreach (var voice in playing)
                voice.Stop();
            playing.Clear();
            current = null;
            enabled = false;
        }

        private void OnDestroy() => Dispose();
    }

    /// <summary>Explicit layered ambience ownership. Each layer is an ordinary looped playback handle.</summary>
    public sealed class AmbienceLayers : IDisposable
    {
        private readonly List<PlaybackHandle> voices = new List<PlaybackHandle>();

        /// <summary>Starts every layer looping.</summary>
        public AmbienceLayers(params AudioCue[] layers)
        {
            var engine = Aura.Instance.Engine;
            try
            {
                foreach (var cue in layers)
                    voices.Add(
                        engine.Play(cue, loop: true)
                            ?? throw new InvalidOperationException("Ambience layer rejected.")
                    );
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Stops every layer.</summary>
        public void Dispose()
        {
            foreach (var voice in voices)
                voice.Stop();
            voices.Clear();
        }
    }
}
