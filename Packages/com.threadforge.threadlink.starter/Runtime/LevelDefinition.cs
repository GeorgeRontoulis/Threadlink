namespace Threadforge.Threadlink.Starter
{
    using System;
    using UnityEngine;

    /// <summary>Read-only runtime level data prepared from a <see cref="LevelDefinition"/>.</summary>
    /// <remarks>The music cue is a presentation reference kept for the Unity adapter; gameplay state lives in saves.</remarks>
    public sealed class PreparedLevel
    {
        /// <summary>Stable level identity, also used as the Nexus scene identity.</summary>
        public Guid Identity { get; }

        /// <summary>Player-facing name.</summary>
        public string DisplayName { get; }

        /// <summary>Build-profile scene name.</summary>
        public SceneId Scene { get; }

        /// <summary>Looping music for the level, or null.</summary>
        public AudioCue Music { get; }

        /// <summary>Where a new game starts.</summary>
        public Vector3 Spawn { get; }

        /// <summary>Items needed to open the exit.</summary>
        public int RequiredItems { get; }

        internal PreparedLevel(Guid identity, string displayName, SceneId scene, AudioCue music, Vector3 spawn, int requiredItems)
        {
            Identity = identity;
            DisplayName = displayName;
            Scene = scene;
            Music = music;
            Spawn = spawn;
            RequiredItems = requiredItems;
        }
    }

    /// <summary>Typed authored level definition (schema version 1).</summary>
    [CreateAssetMenu(menuName = "Threadlink/Starter/Level Definition")]
    public sealed class LevelDefinition : VaultAsset<PreparedLevel>
    {
        [Tooltip("Level name shown in menus and saves.")]
        public string DisplayName = "Forest";
        [Tooltip("Build Profile scene to load.")]
        public SceneReference Scene;
        [Tooltip("Music played in the level.")]
        public AudioCue Music;
        [Tooltip("Where the player starts.")]
        public Vector3 Spawn;

        [Min(0)]
        [Tooltip("Lanterns needed to open the exit.")]
        public int RequiredItems = 3;

        /// <inheritdoc />
        public override Guid Schema => new Guid("0d6f6a77-2b9e-4b55-9f7b-3d4f3e7d5c10");

        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <summary>Prepares this definition alone.</summary>
        public PreparedLevel Prepare() => Vault.Get(this);

        /// <inheritdoc />
        protected override void ValidateAuthoring()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || !Scene.IsAssigned)
                throw new InvalidOperationException("A level needs a display name and scene.");
            if (RequiredItems < 0)
                throw new InvalidOperationException("Required items cannot be negative.");
            Music?.Validate();
        }

        /// <inheritdoc />
        protected override PreparedLevel BuildRuntime(VaultContext context) =>
            new PreparedLevel(Identity, DisplayName, Scene, Music, Spawn, RequiredItems);
    }
}
