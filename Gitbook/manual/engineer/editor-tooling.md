# Editor Tooling

Every command lives under the **Threadlink** menu. The complete list is in [Menus and Asset Creation](../reference/menus-and-assets.md).

## Code generation and Addressables

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Forces a generation pass across every domain (see [Identifier Domains and Code Generation](code-generation.md)). |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers (see [Addressables Mapping Window](code-generation.md#addressables-mapping-window)). |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Registers framework assets with the Addressables system (see [Configuration](configuration.md#addressable-registration-of-native-assets)). |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Realigns addresses to asset paths. |

## Scene dropdown

A **Scenes** dropdown in the Editor's main toolbar lists every scene that ships: the enabled scenes in Build Settings, then, after a separator, the scenes that are entries of Addressable groups, marked `(Addressable)`. Selecting one offers to save modified scenes and opens it. The dropdown's label shows the active scene, and its context menu offers **Refresh** and **Open Build Settings...**. Scenes made Addressable only through a folder entry are not listed.

## Code formatter

**`Threadlink ▸ Code Formatter`** opens an editor window backed by CSharpier. Assign a project folder and select **Format C# Files** to recursively format every `.cs` file beneath it. Files with CSharpier compilation errors are skipped and reported; changed files are written in place, Unity's AssetDatabase is refreshed once at the end, and the window reports changed/failed/scanned counts.

## Diagnostics

**`Threadlink ▸ Registers Tracker`** inspects live register contents at runtime, reporting the objects each `Register`-derived subsystem currently holds: each entry's ID, name, type and reference. It requires Play Mode and a deployed core.

## Binaries

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file within a selected directory, which must be inside the project (see [Binary authoring](configuration.md#binary-authoring)).

## Inspectors

- **Hash maps** draw through `ThreadlinkHashMapDrawer`, or through Odin when installed; see the `[HashMapDrawer]` and `[ReadOnly]` attributes in [Collections and Utilities](collections-and-utilities.md#attributes).
- **`[MinMaxRange]`** fields draw as range sliders.
- **Sentinel Config** shows the current build target, its Sentinel platform, and the distribution choice the target allows.
- **`PopupInspector.TryInspect(asset)`** opens any asset's Inspector in a separate window, for editor tools.

## Build processors

| Processor | Stage | Function |
|---|---|---|
| `SentinelBuildProcessor` | Pre-build, first (`callbackOrder` −10000) | Resolves and validates the build's Sentinel platform, distribution and build module; fails the build on any error (see [Sentinel Modules](sentinel/modules.md#the-build-pipeline)). |
| Steam `SteamAppIdBuildProcessor` | Post-build, standalone targets | Stages `steam_appid.txt` for development Steam builds and `threadlink_steam_appid.txt` for release Steam builds, and removes them from other builds (see [Sentinel Modules](sentinel/modules.md#steam)). |
