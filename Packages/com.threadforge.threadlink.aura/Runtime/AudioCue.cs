namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;
    using UnityEngine.Audio;
    using UnityEngine.Serialization;

    /// <summary>Voice admission policy when a cue or category reaches its limit.</summary>
    public enum VoiceOverflow
    {
        Reject,
        StealLowestPriority,
    }

    public enum CueLoadingPolicy
    {
        SelectedVariation,
        PreloadVariations,
    }

    /// <summary>Typed Unity Audio cue authoring. Loaded cue retention is supplied explicitly by content integrations.</summary>
    [CreateAssetMenu(menuName = "Threadlink/Aura/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [Serializable]
        public sealed class Variation
        {
            [Tooltip("Audio clip for this variation.")]
            public AudioClip Clip;
            [Tooltip("Relative chance of being picked.")]
            public float Weight = 1;
        }

        [Tooltip("Clips picked at random, by weight, each time the cue plays.")]
        public Variation[] Variations = Array.Empty<Variation>();
        [Tooltip("Never pick the same variation twice in a row.")]
        public bool AvoidImmediateRepeat = true;
        [Tooltip("Random volume range: x is the minimum, y the maximum.")]
        public Vector2 Volume = Vector2.one;
        [Tooltip("Random pitch range: x is the minimum, y the maximum.")]
        public Vector2 Pitch = Vector2.one;
        [Tooltip("Repeat until stopped.")]
        public bool Loop;
        [AudioCategoryName]
        [FormerlySerializedAs("Category")]
        [Tooltip("Category for shared volume, mute and voice limits.")]
        public string CategoryName = "Effects";

        /// <summary>The cue's category, such as <c>AudioCategories.Music</c>.</summary>
        public AudioCategory Category
        {
            get => new AudioCategory(CategoryName);
            set => CategoryName = value.Name;
        }
        [Tooltip("Optional Audio Mixer group to route through.")]
        public AudioMixerGroup MixerGroup;

        [Range(0, 1)]
        [Tooltip("0 plays in 2D; 1 plays fully in 3D at the emitter.")]
        public float SpatialBlend;
        [Tooltip("Distance in meters within which the cue plays at full volume.")]
        public float MinimumDistance = 1;
        [Tooltip("Distance in meters beyond which the cue gets no quieter.")]
        public float MaximumDistance = 50;

        [Range(0, 256)]
        [Tooltip("Unity voice priority: 0 is most important, 256 least.")]
        public int Priority = 128;

        [Min(1)]
        [Tooltip("Most instances of this cue playing at once.")]
        public int VoiceLimit = 16;
        [Tooltip("At the limit: reject the new voice, or stop the lowest-priority one.")]
        public VoiceOverflow Overflow = VoiceOverflow.Reject;
        [Tooltip("Load only the variation being played, or preload them all.")]
        public CueLoadingPolicy LoadingPolicy;

        /// <summary>Requests Unity audio-data preparation without taking ownership of another consumer's clip data.</summary>
        public void WarmAudioData()
        {
            Validate();
            foreach (var variation in Variations)
                if (variation.Clip.loadState == AudioDataLoadState.Unloaded)
                    variation.Clip.LoadAudioData();
        }

        /// <summary>Checks unsupported or invalid authoring before any AudioSource is acquired.</summary>
        public void Validate()
        {
            if (
                Variations == null
                || Variations.Length == 0
                || VoiceLimit < 1
                || string.IsNullOrWhiteSpace(CategoryName)
                || Pitch.x <= 0
                || Pitch.y < Pitch.x
                || Pitch.y > 3
                || float.IsNaN(Pitch.x)
                || float.IsNaN(Pitch.y)
                || Volume.x < 0
                || Volume.y < Volume.x
                || Volume.y > 1
                || float.IsNaN(Volume.x)
                || float.IsNaN(Volume.y)
                || MinimumDistance < 0
                || MaximumDistance < MinimumDistance
            )
                throw new ArgumentException("Invalid audio cue settings.");
            foreach (var variation in Variations)
                if (
                    variation == null
                    || variation.Clip == null
                    || variation.Weight <= 0
                    || float.IsNaN(variation.Weight)
                    || float.IsInfinity(variation.Weight)
                )
                    throw new ArgumentException(
                        "Cue variation requires a clip and positive finite weight."
                    );
        }
    }
}
