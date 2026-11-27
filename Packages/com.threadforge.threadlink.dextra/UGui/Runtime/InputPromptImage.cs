namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;

    /// <summary>
    /// Designer data for uGUI: shows the control for an action on the device the player used last, as its glyph from the
    /// Input Glyphs in Project Settings ▸ Threadlink ▸ Dextra, or as the control's name when there is no glyph.
    /// </summary>
    [AddComponentMenu("Threadlink/Dextra/Input Prompt")]
    public sealed class InputPromptImage : MonoBehaviour
    {
        [Tooltip("The action whose control is shown.")]
        [SerializeField]
        private InputActionReference action;

        [Tooltip("The player whose controls are shown.")]
        [Min(0)]
        [SerializeField]
        private int player;

        [Tooltip("Shows the glyph. Hidden when there is none.")]
        [SerializeField]
        private Image image;

        [Tooltip("Shows the control's name when there is no glyph. Optional.")]
        [SerializeField]
        private Text label;

        private SubscriptionToken controls,
            bindings;

        /// <summary>The action whose control is shown.</summary>
        public InputActionReference Action
        {
            get => action;
            set
            {
                action = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            if (Iris.TryGetSingleton(out _))
            {
                controls = Iris.Subscribe<ControlsChanged>(_ => Refresh());
                bindings = Iris.Subscribe<BindingsChanged>(_ => Refresh());
            }
            Refresh();
        }

        private void OnDisable()
        {
            controls.Dispose();
            bindings.Dispose();
            controls = default;
            bindings = default;
        }

        /// <summary>Shows the current glyph or control name again.</summary>
        public void Refresh()
        {
            Sprite sprite = null;
            string text = string.Empty;
            if (action != null && action.action != null && Dextra.TryGetSingleton(out _) && player < Dextra.Players.Count)
            {
                try
                {
                    var context = Dextra.Players[player];
                    var id = InputId.Of(action.action);
                    sprite = context.Glyph(id);
                    if (sprite == null)
                        text = context.Prompt(id);
                }
                catch (ArgumentException error)
                {
                    text = action.action.name;
                    Debug.LogWarning(error.Message, this);
                }
            }
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
            if (label != null)
            {
                label.text = text;
                label.enabled = sprite == null;
            }
        }
    }
}
