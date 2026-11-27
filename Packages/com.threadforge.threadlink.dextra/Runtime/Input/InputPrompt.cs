namespace Threadforge.Threadlink
{
    using System;
    using UnityEngine.UIElements;

    /// <summary>
    /// A UI Toolkit element that shows the control for an action on the device the player used last: its glyph from the
    /// Input Glyphs in Project Settings ▸ Threadlink ▸ Dextra, or the control's name. It follows device changes and rebinds.
    /// </summary>
    /// <example><c>&lt;Threadforge.Threadlink.InputPrompt action="Gameplay/Jump" /&gt;</c></example>
    [UxmlElement]
    public partial class InputPrompt : VisualElement
    {
        /// <summary>The element's USS class.</summary>
        public const string UssClassName = "threadlink-input-prompt";

        /// <summary>The glyph's USS class.</summary>
        public const string GlyphUssClassName = UssClassName + "__glyph";

        /// <summary>The control name's USS class.</summary>
        public const string LabelUssClassName = UssClassName + "__label";

        private readonly VisualElement glyph = new VisualElement();
        private readonly Label label = new Label();
        private SubscriptionToken controls,
            bindings;
        private string action = string.Empty;
        private int player;

        /// <summary>The action, as "Map/Action".</summary>
        [UxmlAttribute]
        public string Action
        {
            get => action;
            set
            {
                action = value ?? string.Empty;
                Refresh();
            }
        }

        /// <summary>The player whose controls are shown; 0 by default.</summary>
        [UxmlAttribute]
        public int Player
        {
            get => player;
            set
            {
                player = Math.Max(0, value);
                Refresh();
            }
        }

        /// <summary>Whether a glyph is shown rather than the control's name.</summary>
        public bool ShowsGlyph => glyph.style.display != DisplayStyle.None;

        /// <summary>The text shown when there is no glyph.</summary>
        public string Text => label.text;

        /// <summary>Creates the element.</summary>
        public InputPrompt()
        {
            AddToClassList(UssClassName);
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            glyph.AddToClassList(GlyphUssClassName);
            glyph.style.width = 32;
            glyph.style.height = 32;
            glyph.style.display = DisplayStyle.None;
            glyph.pickingMode = PickingMode.Ignore;
            label.AddToClassList(LabelUssClassName);
            label.pickingMode = PickingMode.Ignore;
            Add(glyph);
            Add(label);
            RegisterCallback<AttachToPanelEvent>(_ => Attach());
            RegisterCallback<DetachFromPanelEvent>(_ => Detach());
        }

        private void Attach()
        {
            Detach();
            if (Iris.TryGetSingleton(out _))
            {
                controls = Iris.Subscribe<ControlsChanged>(_ => Refresh());
                bindings = Iris.Subscribe<BindingsChanged>(_ => Refresh());
            }
            Refresh();
        }

        private void Detach()
        {
            controls.Dispose();
            bindings.Dispose();
            controls = default;
            bindings = default;
        }

        /// <summary>Shows the current glyph or control name again.</summary>
        public void Refresh()
        {
            UnityEngine.Sprite sprite = null;
            string text = action;
            if (action.Length > 0 && Dextra.TryGetSingleton(out _) && player < Dextra.Players.Count)
            {
                try
                {
                    var context = Dextra.Players[player];
                    var id = new InputId(Guid.Empty, action);
                    sprite = context.Glyph(id);
                    text = sprite == null ? context.Prompt(id) : string.Empty;
                }
                catch (ArgumentException)
                {
                    // An action the player's input does not have shows its path, so the mistake is visible.
                }
            }
            glyph.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
            glyph.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            label.text = text;
            label.style.display = sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
