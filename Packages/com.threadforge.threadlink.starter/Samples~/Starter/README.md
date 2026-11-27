# Threadlink Starter sample

1. Run **Threadlink ▸ Starter ▸ Add Starter Scenes To Build Profile**. It puts `Starter Bootstrap`, `Starter Menu` and `Starter Forest` first in the build scene list and keeps your other scenes.
2. Open `Scenes/Starter Bootstrap` and enter Play mode.

Importing the sample sets up Unity Localization for its string table (**Threadlink ▸ Workspace ▸ Localization ▸ Set up** does the same); add languages to the table to translate the game.

Threadlink deploys every subsystem, `StarterGame` included, before the bootstrap scene starts. The bootstrap scene's `StarterBootstrap` hands the game its designer data and shows the menu.

| Folder | Contents |
| --- | --- |
| `Configuration` | `StarterContent` (designer data: screens, sounds, first level, slots, autosave interval, text) and the input actions (Gameplay and UI maps). |
| `Definitions` | The typed `Forest` level definition: localized name, scene, music, spawn point and required lanterns. |
| `Audio` | Placeholder tones and their cues. |
| `UI` | UI Toolkit documents, stylesheet, theme and panel settings. |
| `Scenes` | Bootstrap, main menu and the forest. |
| `Localization` | The **Threadlink Starter** string table (English): screen labels, messages, level and item names, prompts. |

Saves are per local profile under `Application.persistentDataPath/Threadlink/Saves`; rebinds under `Threadlink/Input`. Open **Threadlink ▸ Runtime Monitor** in Play mode to watch the session.
