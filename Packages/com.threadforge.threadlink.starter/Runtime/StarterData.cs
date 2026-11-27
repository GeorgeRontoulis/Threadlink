namespace Threadforge.Threadlink.Starter
{
    using System.Collections.Generic;
    using MessagePack;

    /// <summary>One save slot of the starter game.</summary>
    [Save("threadlink-starter", 1)]
    [MessagePackObject]
    public sealed partial class StarterSave
    {
        /// <summary>Level definition the player is in.</summary>
        [Key(0)]
        public string Level { get; set; }

        /// <summary>Player position.</summary>
        [Key(1)]
        public float[] Position { get; set; }

        /// <summary>Identities of collected items.</summary>
        [Key(2)]
        public List<string> Collected { get; set; } = new List<string>();

        /// <summary>Seconds played.</summary>
        [Key(3)]
        public double PlaySeconds { get; set; }
    }

    /// <summary>Player preferences: category volumes and mute. Bindings are saved by Dextra.</summary>
    [Save("threadlink-starter-preferences", 1)]
    [MessagePackObject]
    public sealed partial class StarterPreferences
    {
        /// <summary>Volume per audio category, from 0 to 1.</summary>
        [Key(0)]
        public Dictionary<string, float> Volumes { get; set; } = new Dictionary<string, float> { ["Music"] = 0.8f, ["Effects"] = 1f, ["Interface"] = 1f };

        /// <summary>Whether all audio is muted.</summary>
        [Key(1)]
        public bool Muted { get; set; }

        /// <summary>Applies the preferences to Aura.</summary>
        public void Apply()
        {
            foreach (var pair in Volumes)
            {
                var category = new AudioCategory(pair.Key);
                Aura.SetVolume(category, pair.Value);
                Aura.SetMute(category, Muted);
            }
        }
    }
}
