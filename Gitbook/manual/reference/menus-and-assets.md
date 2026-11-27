# Menus and Asset Creation

## Threadlink menu

Entries appear only when their package is installed.

| Menu path | Package | Action |
|---|---|---|
| **Threadlink ▸ Workspace** | core | Opens the Workspace on the last page used (the Hub the first time; the Runtime page in Play mode). |
| **Threadlink ▸ Runtime Monitor** | core | Opens the workspace on the Runtime monitor. |
| **Threadlink ▸ Generate IDs** | core | Regenerates `GameScenes`, `GameInput`, `AudioCategories`, the platform IDs (`Achievements`, `Statistics`, `Leaderboards`, `Products`) and the ID domains; also runs automatically when their sources change. See [Generated IDs](../engineer/editor-tooling.md#generated-ids). |
| **Threadlink ▸ Content Mapping** | core | Opens the Workspace's Content page: tick the Addressables entries to map, then Apply. |
| **Assets ▸ Threadlink ▸ Map for Loading by ID** | core | Maps the selected addressable assets, as ticking them on the Content page does. |
| **Assets ▸ Threadlink ▸ Open in Vault Page** | core | Opens the selected definition on the Workspace's Vault page; also in a definition's Inspector context menu. |
| **Threadlink ▸ Starter ▸ Open Bootstrap Scene** | `starter` | Opens the imported starter's bootstrap scene. |
| **Threadlink ▸ Starter ▸ Add Starter Scenes To Build Profile** | `starter` | Adds the starter scenes to the active build profile. |

The Threadlink source repository adds development entries that are not part of any package: **Threadlink ▸ Validation ▸ Framework** (the maintained validation runner), **Threadlink ▸ Code Formatter** and **Threadlink ▸ Development ▸ Rebuild Showcase Sample**. See [Editor Tooling](../engineer/editor-tooling.md#framework-development-tools).

## Asset creation

Under **Assets ▸ Create ▸ Threadlink**:

| Entry | Package | Creates |
|---|---|---|
| **Aura ▸ Audio Cue** | `aura` | An `AudioCue`. |
| **ID Domain** | core | An `IdDomain`. |
| **Vault ▸ Field Set** | core | A `VaultFieldSetAsset`: named values designers author without code. |
| **Dextra ▸ Input Glyphs** | `dextra` | An `InputGlyphs`: button images by device family and control. |
| **Starter ▸ Starter Content** | `starter` | The starter's designer data. |
| **Starter ▸ Level Definition** | `starter` | A starter level definition. |

Other definition assets are game-defined types; their creation entries come from your project's code, and the Vault page's **New** lists them. Engineering settings and the Unity packages' configuration assets are created from **Project Settings ▸ Threadlink** or the Workspace's **Configuration** page, not from this menu.

## Components

Under **Add Component ▸ Threadlink**: **Aura ▸ Scene Music**, **Aura ▸ Audio Zone**, **Dextra ▸ Screen Sounds** (with Aura), **Dextra ▸ Input Prompt** (with uGUI) and **Dextra ▸ Interaction Sensor**.

The main toolbar's **Scenes** dropdown opens any Build Profile or Addressables scene.
