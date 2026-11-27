namespace Threadforge.Threadlink
{
    /// <summary>Announced on Iris when the music channel changes cue or stops.</summary>
    public readonly struct MusicChanged
    {
        /// <summary>The cue that was playing, or null.</summary>
        public readonly AudioCue Previous;

        /// <summary>The cue now playing, or null when the music stopped.</summary>
        public readonly AudioCue Current;

        /// <summary>Creates the event.</summary>
        public MusicChanged(AudioCue previous, AudioCue current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Announced on Iris when a category's player volume is set, or when a volume fade completes. Keeps settings menus in
    /// step with volumes changed elsewhere, such as from a loaded save.
    /// </summary>
    public readonly struct VolumeChanged
    {
        /// <summary>The category.</summary>
        public readonly AudioCategory Category;

        /// <summary>The player volume, from 0 to 1.</summary>
        public readonly float Volume;

        /// <summary>Creates the event.</summary>
        public VolumeChanged(AudioCategory category, float volume)
        {
            Category = category;
            Volume = volume;
        }
    }

    /// <summary>Announced on Iris when a category is muted or unmuted.</summary>
    public readonly struct MuteChanged
    {
        /// <summary>The category.</summary>
        public readonly AudioCategory Category;

        /// <summary>Whether it is now muted.</summary>
        public readonly bool Muted;

        /// <summary>Creates the event.</summary>
        public MuteChanged(AudioCategory category, bool muted)
        {
            Category = category;
            Muted = muted;
        }
    }
}
