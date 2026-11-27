namespace Threadforge.Threadlink
{
    using System.IO;
    using UnityEngine;

    /// <summary>Engineering settings for Sentinel: where saves live and the default local profile.</summary>
    public sealed class SentinelSettings : ThreadlinkSettings<SentinelSettings>
    {
        /// <summary>Folder under Application.persistentDataPath/Threadlink for saves.</summary>
        [Tooltip("Save folder under persistentDataPath/Threadlink.")]
        public string SaveFolder = "Saves";

        /// <summary>Local profile selected at startup until a platform provider signs a player in.</summary>
        [Tooltip("Local profile used until a platform signs a player in.")]
        public string DefaultProfile = "Player 1";

        /// <summary>Largest save accepted, in bytes.</summary>
        [Min(1024)]
        [Tooltip("Largest save accepted, in bytes.")]
        public int MaximumSaveBytes = 16 * 1024 * 1024;

        /// <inheritdoc />
        public override string Validate() =>
            string.IsNullOrWhiteSpace(SaveFolder) || SaveFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "The save folder name is invalid." : null;
    }
}
