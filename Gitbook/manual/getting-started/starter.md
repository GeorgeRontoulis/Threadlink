# Starter Project

`com.threadforge.threadlink.starter` is a small complete game on Threadlink: a main menu, a forest level with lanterns to collect, three save slots with recovery, settings with rebinding, music and screen sounds. Use it as a reference or as the starting point of a game.

## Install and play

1. Install `com.threadforge.threadlink.starter` (see [Installation](installation.md)), or select **Install** on its card in the Workspace Hub. Its dependencies install with it.
2. Import its **Starter** sample (Package Manager or Hub). The import sets up Unity Localization for the sample's English string table (see [Localizing Text](../designer/localization.md)).
3. Run **Threadlink ▸ Starter ▸ Add Starter Scenes To Build Profile**. It previews the list, puts `Starter Bootstrap`, `Starter Menu` and `Starter Forest` first and keeps your other scenes.
4. Open `Starter Bootstrap` (**Threadlink ▸ Starter ▸ Open Bootstrap Scene**) and enter Play mode.

Move with WASD or the left stick, interact with **E** or the south button, pause with **Escape** or Start.

## How it is built

The whole game flow is one subsystem, `StarterGame`. It uses Nexus, Aura, Dextra and Sentinel, which, like every subsystem, are running before the bootstrap scene starts. `StarterBootstrap` passes it the designer data and the menu appears at once; nothing waits for anything to become ready.

| Piece | Kind | Role |
| --- | --- | --- |
| `StarterGame` | Subsystem (code) | Menu, new game, continue, load, play, pause, save, autosave, settings, recovery |
| `StarterContent` | Designer data | Screens, sounds, first level, menu scene, slot count, autosave interval, input actions, panel and text |
| `LevelDefinition` | Designer data (Vault) | Localized level name, scene, music, spawn point, required lanterns |
| `StarterLevel`, `StarterPlayer`, `StarterCollectible`, `StarterExit` | Scene components | Restore and capture the level, move the player, lanterns and exit (both `Interactable`) |
| `StarterSave`, `StarterPreferences` | Save types | A slot's state; volumes and mute |

Screens are UI Toolkit documents opened with `Dextra.Screens.OpenAsync`; the game binds their controls through `ScreenHandle.Root` by element name. Levels and the menu name their scenes with `SceneReference` fields, which `Nexus.LoadAsync` loads; `StarterInput` holds the starter's input IDs (`StarterInput.Gameplay.Move`, `StarterInput.Maps.UI`), the same shape the generated `GameInput` has. When the project has no input actions in **Project Settings ▸ Threadlink ▸ Dextra**, the starter joins player 0 with its own actions.

## Saves, recovery and settings

- **New Game** uses the first empty slot; **Continue** loads the last played slot; **Load** lists every slot.
- Saving uses Sentinel's conflict detection: a slot changed elsewhere is reported, not overwritten.
- Autosave runs every `AutosaveSeconds` of play while no interaction, screen or transition is in progress.
- A damaged slot opens the recovery screen, which restores the previous version after validating it.
- Settings change volumes and mute immediately and save them when the screen closes; rebinding and **Reset** use Dextra, which saves bindings itself.

Saves are per local profile under `Application.persistentDataPath/Threadlink/Saves`. Open **Threadlink ▸ Runtime Monitor** in Play mode to watch the game subsystem, scenes, voices, screens and saves.

## Customizing

- Edit `Definitions/Forest` for the level, and `Configuration/StarterContent` for screens, sounds and tuning.
- Edit the UXML and USS under `UI`; keep the element names the game binds.
- Edit the text in the **Threadlink Starter** string table under `Localization`; screen labels bind to its entries, and `StarterContent` ▸ **Text** points at the entries the game fills in.
- Replace the placeholder tones in `Audio`.
- `StarterTint` colors the sample's primitives with the active render pipeline's default material. Replace it with authored materials in a real project.
