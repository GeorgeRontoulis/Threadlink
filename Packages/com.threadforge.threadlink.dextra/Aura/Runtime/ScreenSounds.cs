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

        [Tooltip("Played when keyboard or gamepad navigation moves between controls (UI Toolkit screens).")]
        [SerializeField]
        private AudioCue navigate;

        [Tooltip("Played when a control is submitted or a button is clicked (UI Toolkit screens).")]
        [SerializeField]
        private AudioCue submit;

        private bool shown;
        private UnityEngine.UIElements.VisualElement root;

        /// <summary>Number of sounds that could not play.</summary>
        public int Skipped { get; private set; }

        void IScreenListener.OnScreenVisible(bool visible)
        {
            if (visible == shown)
                return;
            shown = visible;
            Play(visible ? open : close);
            Listen(visible);
        }

        void IScreenListener.OnScreenFocused(bool focused)
        {
            if (focused && shown)
                Play(focus);
        }

        // UI Toolkit navigation reaches the screen's root as events; listening only while visible keeps hidden screens silent.
        private void Listen(bool visible)
        {
            if (root != null)
            {
                root.UnregisterCallback<UnityEngine.UIElements.NavigationMoveEvent>(OnNavigate, UnityEngine.UIElements.TrickleDown.TrickleDown);
                root.UnregisterCallback<UnityEngine.UIElements.NavigationSubmitEvent>(OnSubmit, UnityEngine.UIElements.TrickleDown.TrickleDown);
                root.UnregisterCallback<UnityEngine.UIElements.ClickEvent>(OnClick, UnityEngine.UIElements.TrickleDown.TrickleDown);
                root = null;
            }
            if (!visible || (navigate == null && submit == null) || !TryGetComponent<UnityEngine.UIElements.UIDocument>(out var document) || document.rootVisualElement == null)
                return;
            root = document.rootVisualElement;
            root.RegisterCallback<UnityEngine.UIElements.NavigationMoveEvent>(OnNavigate, UnityEngine.UIElements.TrickleDown.TrickleDown);
            root.RegisterCallback<UnityEngine.UIElements.NavigationSubmitEvent>(OnSubmit, UnityEngine.UIElements.TrickleDown.TrickleDown);
            root.RegisterCallback<UnityEngine.UIElements.ClickEvent>(OnClick, UnityEngine.UIElements.TrickleDown.TrickleDown);
        }

        private void OnNavigate(UnityEngine.UIElements.NavigationMoveEvent _) => Play(navigate);

        private void OnSubmit(UnityEngine.UIElements.NavigationSubmitEvent _) => Play(submit);

        private void OnClick(UnityEngine.UIElements.ClickEvent click)
        {
            if (click.target is UnityEngine.UIElements.Button)
                Play(submit);
        }

        private void OnDestroy() => Listen(false);

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
