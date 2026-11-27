namespace Threadforge.Threadlink.Starter
{
    using System;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.Localization;
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
    /// Text the starter's code shows, as entries of the Starter string table. Entries with <c>{0}</c> receive a value:
    /// a slot number, a time or an error. Text written in the screen documents binds to the same table there.
    /// </summary>
    [Serializable]
    public sealed class StarterText
    {
        [Tooltip("Table for labels made from names: \"volume-<category>\" and \"action-<action>\" entries. Missing entries show the name.")]
        public LocalizedStringTable Table = new LocalizedStringTable();
        [Tooltip("A save slot that holds a game. {0}: slot number.")]
        public LocalizedString Slot = new LocalizedString();
        [Tooltip("An empty save slot. {0}: slot number.")]
        public LocalizedString EmptySlot = new LocalizedString();
        [Tooltip("HUD note after an autosave. {0}: time.")]
        public LocalizedString Autosaved = new LocalizedString();
        [Tooltip("HUD note after a save from the pause menu. {0}: time.")]
        public LocalizedString Saved = new LocalizedString();
        [Tooltip("HUD note when a save fails. {0}: error.")]
        public LocalizedString SaveFailedNote = new LocalizedString();
        [Tooltip("Pause menu note after saving. {0}: slot number.")]
        public LocalizedString SavedToSlot = new LocalizedString();
        [Tooltip("Title of the message when a save fails.")]
        public LocalizedString SaveFailed = new LocalizedString();
        [Tooltip("Body of the message when a slot changed elsewhere since it was loaded.")]
        public LocalizedString SaveConflict = new LocalizedString();
        [Tooltip("Title of the message when a level fails to load.")]
        public LocalizedString LevelFailed = new LocalizedString();
        [Tooltip("Title of the message when a slot cannot be loaded.")]
        public LocalizedString NothingToLoad = new LocalizedString();
        [Tooltip("Body of that message. {0}: slot number, {1}: error.")]
        public LocalizedString NothingToLoadDetail = new LocalizedString();
        [Tooltip("Title of the recovery screen. {0}: slot number.")]
        public LocalizedString Damaged = new LocalizedString();
        [Tooltip("Recovery screen text when the store gave no detail.")]
        public LocalizedString DamagedDetail = new LocalizedString();
        [Tooltip("Recovery screen button.")]
        public LocalizedString RestorePrevious = new LocalizedString();
        [Tooltip("Title of the message when no earlier version exists.")]
        public LocalizedString NothingToRestore = new LocalizedString();

        internal LocalizedString[] All =>
            new[] { Slot, EmptySlot, Autosaved, Saved, SaveFailedNote, SavedToSlot, SaveFailed, SaveConflict, LevelFailed, NothingToLoad, NothingToLoadDetail, Damaged, DamagedDetail, RestorePrevious, NothingToRestore };
    }

    /// <summary>
    /// Designer data for the starter: screens, sounds, text, the first level and tuning. The bootstrap scene references it;
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

        [Tooltip("Text the starter's code shows, from the Starter string table.")]
        public StarterText Text = new StarterText();

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
            if (Text == null || Text.Table.IsEmpty)
                return "The Starter string table is missing.";
            foreach (var text in Text.All)
                if (text == null || text.IsEmpty)
                    return "A Starter text entry is not assigned.";
            if (FirstLevel == null)
                return "The first level is missing.";
            return !MenuScene.IsAssigned ? "The menu scene is missing." : null;
        }
    }
}
