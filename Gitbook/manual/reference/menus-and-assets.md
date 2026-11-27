# Menus and Asset Creation

## Threadlink menu

Entries appear only when their package is installed.

| Menu path | Package | Action |
|---|---|---|
| **Threadlink ▸ Workspace** | core | Opens the workspace window on the Hub (on the Runtime page in Play mode). |
| **Threadlink ▸ Runtime Monitor** | core | Opens the workspace on the Runtime monitor. |
| **Threadlink ▸ Generate IDs** | core | Regenerates `GameScenes`, `GameInput`, `AudioCategories`, the platform IDs (`Achievements`, `Statistics`, `Leaderboards`, `Products`) and the ID domains; also runs automatically when their sources change. See [Generated IDs](../engineer/editor-tooling.md#generated-ids). |
| **Threadlink ▸ Content Mapping** | core | Opens the Workspace's Content page: tick the Addressables entries to map, then Apply. |
| **Assets ▸ Threadlink ▸ Map for Loading by ID** | core | Maps the selected addressable assets, as ticking them on the Content page does. |
| **Threadlink ▸ Starter ▸ Open Bootstrap Scene** | `starter` | Opens the imported starter's bootstrap scene. |
| **Threadlink ▸ Starter ▸ Add Starter Scenes To Build Profile** | `starter` | Adds the starter scenes to the active build profile. |

The Threadlink source repository adds development entries that are not part of any package: **Threadlink ▸ Validation ▸ Framework** (the maintained validation runner), **Threadlink ▸ Code Formatter** and **Threadlink ▸ Development ▸ Rebuild Showcase Sample**. See [Editor Tooling](../engineer/editor-tooling.md#framework-development-tools).

## Asset creation

Under **Assets ▸ Create ▸ Threadlink**:

| Entry | Package | Creates |
|---|---|---|
| **Aura ▸ Audio Cue** | `aura` | An `AudioCue`. |
| **ID Domain** | core | An `IdDomain`. |
| **Starter ▸ Starter Content** | `starter` | The starter's designer data. |
| **Starter ▸ Level Definition** | `starter` | A starter level definition. |

Definition assets are game-defined types; their creation entries come from your project's code. Engineering settings are created from **Project Settings ▸ Threadlink**, not from this menu.

## Components

Under **Add Component ▸ Threadlink**: **Aura ▸ Scene Music**, **Dextra ▸ Screen Sounds** (with Aura) and **Dextra ▸ Interaction Sensor**.
