namespace Threadforge.Threadlink
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>Engineering settings for Dextra: the input actions, which maps are gameplay and UI, and how screens render.</summary>
    public sealed class DextraSettings : ThreadlinkSettings<DextraSettings>
    {
        /// <summary>The project's input actions. Each player gets its own copy.</summary>
        [Tooltip("The game's input actions. Each player gets a copy. Generates GameInput.")]
        public InputActionAsset Actions;

        /// <summary>Action map enabled for gameplay (stored by map ID; a map name also works).</summary>
        [InputMapReference]
        [Tooltip("Action map enabled during gameplay.")]
        public string GameplayMap = "Gameplay";

        /// <summary>Action map enabled while a screen blocks gameplay input (stored by map ID; a map name also works).</summary>
        [InputMapReference]
        [Tooltip("Action map enabled while a screen blocks gameplay.")]
        public string InterfaceMap = "UI";

        /// <summary>The name of the map a setting refers to in <paramref name="actions"/>, or null when it has none.</summary>
        internal string MapName(InputActionAsset actions, string map)
        {
            if (actions == null || string.IsNullOrEmpty(map))
                return null;
            InputActionMap found;
            if (System.Guid.TryParse(map, out var id))
            {
                // A map chosen from the project's actions; a player with its own actions matches it by name.
                found = actions.FindActionMap(id);
                string name = found == null && Actions != null ? Actions.FindActionMap(id)?.name : null;
                if (!string.IsNullOrEmpty(name))
                    found = actions.FindActionMap(name);
            }
            else
                found = actions.FindActionMap(map);
            return found != null ? found.name : null;
        }

        /// <summary>Panel settings for UI Toolkit screens opened from a document.</summary>
        [Tooltip("Panel Settings for screens opened from a UI document.")]
        public PanelSettings Panel;

        /// <summary>Whether players' rebinds are saved and restored automatically.</summary>
        [Tooltip("Save each player's rebinds and restore them on the next launch.")]
        public bool SaveBindings = true;

        /// <summary>Button glyphs for prompts, by device family and control.</summary>
        [Tooltip("Button glyphs that prompts show for the device the player used last. Without them, prompts show the control's name.")]
        public InputGlyphs Glyphs;

        /// <summary>The action that goes back: it closes the focused screen (stored by action ID; a "Map/Action" path also works).</summary>
        [InputActionPicker]
        [Tooltip("Pressing it goes back from the focused screen: a screen set to close on Back closes, unless it handles Back itself (IScreenBackHandler).")]
        public string BackAction = "UI/Cancel";

        /// <summary>Whether Back closes screens. Off: call Dextra.Screens.BackAsync yourself.</summary>
        [Tooltip("React to the Back action. Screens close on it only when set to (Closes On Back, or ScreenHandle.ClosesOnBack).")]
        public bool BackClosesScreens = true;

        /// <summary>Whether the game pauses (Chronos) while a screen blocks gameplay input.</summary>
        [Tooltip("Pause the game with Chronos while a screen blocks gameplay input, such as a pause menu.")]
        public bool PauseWhileBlocked;

        /// <summary>The back action in <paramref name="actions"/>, or null.</summary>
        internal InputAction FindBack(InputActionAsset actions)
        {
            if (actions == null || string.IsNullOrEmpty(BackAction))
                return null;
            if (System.Guid.TryParse(BackAction, out var id))
            {
                var found = actions.FindAction(id);
                // A player with its own actions matches the project's action by path.
                if (found == null && Actions != null && Actions.FindAction(id) is InputAction source)
                    found = actions.FindAction(source.actionMap.name + "/" + source.name);
                return found;
            }
            return actions.FindAction(BackAction);
        }

        /// <inheritdoc />
        public override string Validate()
        {
            if (Actions == null)
                return "No input actions are assigned.";
            if (!string.IsNullOrEmpty(GameplayMap) && MapName(Actions, GameplayMap) == null)
                return "The input actions have no gameplay map '" + GameplayMap + "'.";
            if (!string.IsNullOrEmpty(InterfaceMap) && MapName(Actions, InterfaceMap) == null)
                return "The input actions have no interface map '" + InterfaceMap + "'.";
            return null;
        }
    }
}
