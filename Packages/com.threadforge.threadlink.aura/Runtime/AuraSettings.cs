namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;
    using UnityEngine.Audio;

    /// <summary>Engineering settings for Aura: audio categories and how many voices each may play.</summary>
    public sealed class AuraSettings : ThreadlinkSettings<AuraSettings>
    {
        /// <summary>One audio category.</summary>
        [Serializable]
        public sealed class Category
        {
            /// <summary>Name used by cues, such as "Music" or "Effects".</summary>
            [Tooltip("Name cues use, such as Music or Effects. Generates AudioCategories.")]
            public string Name = "Effects";

            /// <summary>Most voices the category plays at once.</summary>
            [Min(1)]
            [Tooltip("Most voices this category plays at once.")]
            public int VoiceLimit = 32;

            /// <summary>Optional mixer group for documentation and tooling; cues choose their own group.</summary>
            [Tooltip("Mixer group for reference and tooling. Cues choose their own group.")]
            public AudioMixerGroup Group;
        }

        /// <summary>The project's categories.</summary>
        public Category[] Categories =
        {
            new Category { Name = "Music", VoiceLimit = 4 },
            new Category { Name = "Effects", VoiceLimit = 32 },
            new Category { Name = "Interface", VoiceLimit = 8 },
            new Category { Name = "Ambience", VoiceLimit = 8 },
        };

        /// <summary>Default crossfade for scene music, in seconds.</summary>
        [Min(0)]
        [Tooltip("Default scene music crossfade, in seconds.")]
        public float MusicCrossfadeSeconds = 1.5f;

        internal static void Apply(AuraEngine engine)
        {
            foreach (var category in Current.Categories)
                if (category != null && !string.IsNullOrWhiteSpace(category.Name))
                    engine.SetVoiceLimit(category.Name, Math.Max(1, category.VoiceLimit));
        }

        /// <inheritdoc />
        public override string Validate()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var category in Categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Name))
                    return "A category has no name.";
                if (!seen.Add(category.Name))
                    return "Category '" + category.Name + "' is listed twice.";
            }
            return null;
        }
    }
}
