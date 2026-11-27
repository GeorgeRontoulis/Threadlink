namespace Threadforge.Threadlink.Starter
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UIElements;

    /// <summary>UI Toolkit documents used by the starter screens.</summary>
    [Serializable]
    public sealed class StarterScreenAssets
    {
        [Tooltip("Main menu screen.")]
        public VisualTreeAsset MainMenu;
        [Tooltip("Save slot list.")]
        public VisualTreeAsset Slots;
        [Tooltip("Shown when a save needs recovery.")]
        public VisualTreeAsset Recovery;
        [Tooltip("Settings screen.")]
        public VisualTreeAsset Settings;
        [Tooltip("Input rebinding screen.")]
        public VisualTreeAsset Rebind;
        [Tooltip("Pause menu.")]
        public VisualTreeAsset Pause;
        [Tooltip("In-game HUD.")]
        public VisualTreeAsset Hud;
        [Tooltip("Short message dialog.")]
        public VisualTreeAsset Message;
        [Tooltip("Shown when a rebind conflicts with another action.")]
        public VisualTreeAsset Conflict;
        [Tooltip("Shown while a level loads.")]
        public VisualTreeAsset Loading;
    }

    /// <summary>Cues used by the starter's menus and gameplay.</summary>
    [Serializable]
    public sealed class StarterSounds
    {
        [Tooltip("Music for the menus.")]
        public AudioCue MenuMusic;
        [Tooltip("Played when a screen opens.")]
        public AudioCue ScreenOpen;
        [Tooltip("Played when a screen closes.")]
        public AudioCue ScreenClose;
        [Tooltip("Played when a screen regains focus.")]
        public AudioCue ScreenFocus;
        [Tooltip("Played when a lantern is collected.")]
        public AudioCue Pickup;
    }

    /// <summary>
    /// Designer data for the starter: screens, sounds, the first level and tuning. The bootstrap scene references it;
    /// nothing here selects code or services.
    /// </summary>
    [CreateAssetMenu(menuName = "Threadlink/Starter/Starter Content")]
    public sealed class StarterContent : ScriptableObject
    {
        [Tooltip("Input actions with Gameplay (Move, Interact, Pause) and UI maps, used when Dextra has none.")]
        public InputActionAsset Input;

        [Tooltip("Panel settings for the starter's screens.")]
        public PanelSettings Panel;

        [Tooltip("UI documents for each starter screen.")]
        public StarterScreenAssets Screens = new StarterScreenAssets();

        [Tooltip("Audio cues for menus and pickups.")]
        public StarterSounds Sounds = new StarterSounds();

        [Tooltip("The level a new game starts in.")]
        public LevelDefinition FirstLevel;

        [Tooltip("Build Profile scene of the main menu.")]
        public SceneReference MenuScene;

        [Range(1, 9)]
        [Tooltip("Number of save slots offered.")]
        public int SlotCount = 3;

        [Tooltip("Seconds of play between autosaves. 0 turns autosave off.")]
        [Min(0)]
        public float AutosaveSeconds = 120;

        /// <summary>Lists missing data, or returns null when complete.</summary>
        public string Validate()
        {
            if (Input == null || Input.FindActionMap("Gameplay") == null || Input.FindActionMap("UI") == null)
                return "Input actions with Gameplay and UI maps are required.";
            foreach (InputId action in new InputId[] { StarterInput.Gameplay.Move, StarterInput.Gameplay.Interact, StarterInput.Gameplay.Pause })
                if (action.Find(Input) == null)
                    return "Input action " + action + " is missing.";
            if (Panel == null)
                return "Panel settings are required.";
            if (Screens == null || Screens.MainMenu == null || Screens.Slots == null || Screens.Settings == null || Screens.Pause == null || Screens.Hud == null || Screens.Message == null || Screens.Recovery == null)
                return "A screen document is missing.";
            if (FirstLevel == null)
                return "The first level is missing.";
            return !MenuScene.IsAssigned ? "The menu scene is missing." : null;
        }
    }
}
