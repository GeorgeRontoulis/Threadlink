# Editor Tooling

Every command is under the **Threadlink** menu. [Menus and Asset Creation](../reference/menus-and-assets.md) lists them all.

## Code generation and Addressables

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Runs a generation pass for every domain (see [Identifier Domains and Code Generation](code-generation.md)). |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers (see [Addressables Mapping Window](code-generation.md#addressables-mapping-window)). |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Makes the framework's own assets Addressable (see [Configuration](configuration.md#addressable-registration-of-native-assets)). |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Sets each address to its asset path. |

## Vault Browser

**`Threadlink ▸ Vault Browser`** lists every Vault in the project on the left. Select one to inspect and edit it on the right, with undo, through the same drawers as the Inspector.

- Search filters by name or folder. The arrow keys move the selection.
- The **+** button creates a Vault. Each row shows the Vault's field count.
- Right-click a row, or use **⋯**, to ping, select, reveal, copy the path of, duplicate or delete a Vault.
- To open a Vault in the browser, right-click it and choose **Threadlink ▸ Open in Vault Browser**, or use **Open in Vault Browser** in its Inspector context menu.
- Edits made in Play Mode change the asset and persist, as they do in the Inspector. The header says so while playing.

## Runtime Monitor

**`Threadlink ▸ Runtime Monitor`** shows the runtime as a graph: the core in the centre, with every woven subsystem and static service (Iris, Nexus, Initium, Scribe) around it. It replaces the Registers Tracker.

- **Nodes.** Filled nodes are woven subsystems; outlined nodes are static services; dark nodes are not running. Each caption gives a live summary, such as linked objects, listeners or Sentinel's state. A red dot marks a failed Sentinel or errors in the console.
- **Details.** Click a node to inspect it in the side panel:

  | Node | Shows |
  |---|---|
  | Threadlink | Deployment state, weave order, configs, and the frame, memory and rendering figures below. |
  | Any register (Aura, user Linkers and Weavers) | Every linked object: name, ID, and a ping link for Unity objects. |
  | Sentinel | State, platform, distribution, module, initialisation result, capabilities. |
  | Chronos | Delta times, time scale, playtime. |
  | Dextra | Input device, pointed object, UI stack. |
  | Iris | Listener counts per event. |
  | Nexus | Presented scene, held scenes with state and holds, loaded Unity scenes. |
  | Initium | Discoverable objects per loaded scene. |
  | Scribe | Messages, warnings and errors this session, and recent Threadlink messages. |

- **Metrics.** The bottom strip shows FPS, main-thread time against the frame budget, GC allocation per frame, managed heap, memory and batches. The budget comes from a target frame rate (**Auto** uses `Application.targetFrameRate`, then the display's refresh rate); change it in the core's panel, which also plots main-thread time over the last 12 seconds.

Outside Play Mode the graph shows the native subsystems and the user subsystems declared in the project, without live data. Editor figures include the Editor's own work and memory, so budget against a development build.

The monitor reads the runtime through editor-only members, which are not compiled into builds: `Register.EditorOnly_Registry` (through `IEditorOnlyRegister`), `Threadlink.EditorOnly_WeaveOrder`, `Nexus.EditorOnly_Scenes` and `Dextra.EditorOnly_StackedInterfaces`. `Threadlink.IsDeployed` is public in every build.

## Scene dropdown

A **Scenes** dropdown in the main toolbar lists every scene that ships: the enabled scenes in Build Settings, then the scenes in Addressable groups, marked `(Addressable)`. Choosing one offers to save modified scenes, then opens it. The label shows the active scene; the context menu offers **Refresh** and **Open Build Settings...**. Scenes that are Addressable only through a folder entry are not listed.

## Code formatter

**`Threadlink ▸ Code Formatter`** formats C# with CSharpier. Assign a project folder and select **Format C# Files** to format every `.cs` file beneath it. Files CSharpier cannot parse are skipped and reported. Changed files are written in place, the AssetDatabase refreshes once at the end, and the window reports how many files changed, failed and were scanned.

## Binaries

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file in a chosen folder inside the project (see [Binary authoring](configuration.md#binary-authoring)).

## Inspectors

- **Hash maps** draw through `ThreadlinkHashMapDrawer`, or Odin when installed. See the `[HashMapDrawer]` and `[ReadOnly]` attributes in [Collections and Utilities](collections-and-utilities.md#attributes).
- **`[MinMaxRange]`** fields draw as range sliders.
- **Sentinel Config** shows the current build target, its Sentinel platform, and the distributions that target allows.
- **`PopupInspector.TryInspect(asset)`** opens an asset's Inspector in its own window, for use by editor tools.

## Build processors

| Processor | Stage | Function |
|---|---|---|
| `SentinelBuildProcessor` | Pre-build, first (`callbackOrder` −10000) | Resolves and validates the build's Sentinel platform, distribution and build module, and fails the build on any error (see [Sentinel Modules](sentinel/modules.md#the-build-pipeline)). |
| Steam `SteamAppIdBuildProcessor` | Post-build, standalone targets | Copies `steam_appid.txt` into development Steam builds and `threadlink_steam_appid.txt` into release Steam builds, and removes both from other builds (see [Sentinel Modules](sentinel/modules.md#steam)). |
