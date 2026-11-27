namespace Threadforge.Threadlink
{
    using UnityEngine;

    /// <summary>
    /// Designer data: the music of the scene this component is in. When the scene starts, Aura crossfades to it on the
    /// music channel. With Nexus, the cue is also prepared while the scene loads and starts as the scene is presented.
    /// </summary>
    [AddComponentMenu("Threadlink/Aura/Scene Music")]
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("Looping music for this scene. Leave empty to fade the current music out.")]
        [SerializeField]
        private AudioCue music;

        [Tooltip("Crossfade in seconds. Negative uses the Aura settings default.")]
        [SerializeField]
        private float crossfadeSeconds = -1f;

        /// <summary>The scene's music cue.</summary>
        public AudioCue Music => music;

        /// <summary>The crossfade to use, in seconds.</summary>
        public float CrossfadeSeconds => crossfadeSeconds >= 0 ? crossfadeSeconds : AuraSettings.Current.MusicCrossfadeSeconds;

        private void Start()
        {
            if (Aura.TryGetSingleton(out _))
                Aura.PlayMusic(music, CrossfadeSeconds);
        }

        private void OnValidate()
        {
            if (music != null && !music.Loop)
                Debug.LogWarning($"Scene music '{music.name}' does not loop.", this);
        }
    }
}
