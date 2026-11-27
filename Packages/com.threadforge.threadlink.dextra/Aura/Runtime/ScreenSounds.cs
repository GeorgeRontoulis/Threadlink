namespace Threadforge.Threadlink
{
    using UnityEngine;

    /// <summary>
    /// Designer data: sounds for the screen this component is on. Sounds never fail or delay a screen; a rejected
    /// voice is simply not heard.
    /// </summary>
    [AddComponentMenu("Threadlink/Dextra/Screen Sounds")]
    [DisallowMultipleComponent]
    public sealed class ScreenSounds : MonoBehaviour, IScreenListener
    {
        [Tooltip("Played when the screen opens.")]
        [SerializeField]
        private AudioCue open;

        [Tooltip("Played when the screen closes.")]
        [SerializeField]
        private AudioCue close;

        [Tooltip("Played when focus returns to the screen after another screen closes.")]
        [SerializeField]
        private AudioCue focus;

        private bool shown;

        /// <summary>Number of sounds that could not play.</summary>
        public int Skipped { get; private set; }

        void IScreenListener.OnScreenVisible(bool visible)
        {
            if (visible == shown)
                return;
            shown = visible;
            Play(visible ? open : close);
        }

        void IScreenListener.OnScreenFocused(bool focused)
        {
            if (focused && shown)
                Play(focus);
        }

        private void Play(AudioCue cue)
        {
            if (cue == null || !Aura.TryGetSingleton(out _))
                return;
            try
            {
                if (Aura.Play(cue) == null)
                    Skipped++;
            }
            catch (System.Exception error)
            {
                Skipped++;
                Debug.LogException(error, this);
            }
        }
    }
}
