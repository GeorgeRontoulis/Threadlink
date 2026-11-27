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
