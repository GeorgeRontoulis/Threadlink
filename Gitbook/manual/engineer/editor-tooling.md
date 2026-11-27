# Editor Tooling

## Threadlink Workspace

Open **Threadlink ▸ Workspace** for a UI Toolkit window, in the Threadlink theme, whose pages come from the installed packages:

| Page | Package | Contents |
| --- | --- | --- |
| Hub | Core | Every Threadlink package by family with description, installed and release version, documentation, Install, Update and Remove, sample import, Install Complete Framework, the OpenUPM registry check and the Showcase |
| Validation | Core | Every installed validator; issues select the affected asset |
| Configuration | Core | Every configuration asset by category, where it is, the folders new ones go to, Create and Move to folder |
| Runtime | Core | The Runtime monitor (below) |
| Code Generation | Core | Every code generator: generated IDs and their sources, ID domains, the Content mapping and subsystem registration |
| Rendering | Core | The render pipeline: packages, the default and per-quality-level pipeline assets, pipeline and renderer assets |
| Content | Core | The Content mapping: every Addressables group with a toggle per entry, Apply, labels and problems |
| Vault | Core | Vault assets, schema and authored versions, pending migrations |
| Input | Dextra | Input action assets and the Input System settings |
| Localization | Dextra | Localization setup, locales, string tables and their folders |
| Starter | Starter | Sample import, build-scene setup and starter content checks |

The Hub opens first; in Play mode the Workspace switches to the Runtime page. The Hub's package list comes from a catalog shipped with the core for its release, so it lists packages that are not installed yet. Install and Update request the release version through the Package Manager; Remove is offered only for packages added directly to the project and not required by another installed Threadlink package. When `Packages/manifest.json` lacks the OpenUPM registry or the `com.threadforge` scope, **Add Registry** adds it after confirmation and leaves every other entry unchanged.

## Configuration assets and folders

The **Configuration** page lists every configuration asset the installed packages manage, by category:

| Category | Assets |
| --- | --- |
| Threadlink | Every engineering settings asset (`ThreadlinkSettings<T>`) |
| Content | Addressables settings, with their groups |
| Input | Player Actions (**Dextra ▸ Actions**), Input System settings, project-wide actions, Screen Panel Settings (**Dextra ▸ Panel**) |
| Localization | Localization settings; the Locales and String Tables folders |
| Rendering | The default render pipeline asset and the pipeline's global settings |

Each row shows where the asset is and offers **Select**, **Create** when it is missing, **Move to folder** when it is elsewhere, and **Folder…** to choose where new ones go. **Create** makes the project use the new asset: it registers the Input System settings, assigns Dextra's fields, activates the Localization settings and, after confirmation, makes a new pipeline asset the default. Project-wide actions and pipeline global settings are created by their packages; the page finds and moves them.

New assets go to one of two folders unless an asset has its own:

| Folder | Default | Used by |
| --- | --- | --- |
| Engineering configs | `Assets/Threadforge/Threadlink User/Engineering/Configs` | Settings, Addressables, input, Localization settings, render pipelines |
| Design configs | `Assets/Threadforge/Threadlink User/Design/Configs` | Locales and string tables |

The choices are stored in `ProjectSettings/ThreadlinkAssetLocations.asset`; keep it under version control. A new folder applies to assets created afterwards. **Move to folder**, or **Move all to their folders**, relocates existing ones with their GUIDs, so references keep working. Addressables settings move with their whole data folder; a pipeline asset moves with its renderer, the global settings with their volume profile, and the screen panel settings with their theme, when those sit beside them.

A package adds its own assets with a `ConfigSource`, discovered like pages:

```csharp
public sealed class WeatherConfigSource : ConfigSource
{
    public override IEnumerable<ConfigAsset> Assets => new[] { new WeatherProfileConfig() };
}

sealed class WeatherProfileConfig : ConfigAsset
{
    public WeatherProfileConfig()
        : base(new AssetLocation("game.weather.profile", "Weather Profile", "Rendering", ConfigRoot.Design)) { }

    public override Object Find() => AssetDatabase.FindAssets("t:WeatherProfile").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<WeatherProfile>).FirstOrDefault();

    protected override Object CreateIn(string folder) => Save(ScriptableObject.CreateInstance<WeatherProfile>(), folder, "Weather Profile.asset");
}
```

`AssetLocations.GetFolder(location)` and `AssetLocations.NewAssetPath(location, fileName)` give the folder for an asset kind, and `ConfigUI.Category(context, "Rendering", context.Refresh)` shows a category's rows on a page.

## Rendering

The **Rendering** page shows the active pipeline, or the Built-in Render Pipeline when no pipeline asset is the default, and:

- the default pipeline asset, and one per quality level (empty: the level uses the default);
- the Universal and High Definition Render Pipeline packages, which ship with the Editor: **Install** adds one after confirmation, **New asset** creates a pipeline asset (Universal's with its renderer) in the Render Pipeline folder without making it the default;
- every pipeline and renderer asset in the project, with **Use as default** and **Move to folder**.

## Runtime monitor

**Threadlink ▸ Runtime Monitor** opens the Workspace on the Runtime page:

- a graph that reads left to right: the core, then the subsystems that use no other, then each subsystem one level after the latest subsystem it uses. A subsystem uses another when its initialization reaches it through `Instance`. Each card shows a subsystem's name, summary and state (a dot that is green when ready and amber on alerts). Arrowed dashed lines run from each used subsystem to the one that uses it; in Play mode the dashes flow in that direction. Hovering or selecting a card highlights its lines and fades the rest. Levels order their cards to keep lines from crossing, tall levels wrap into extra columns, and cards shrink to their names in narrow windows;
- a panel with the selected node's report: sections, fields, meters, references that ping their object, and notes, refreshed four times a second;
- a strip with frames per second, main-thread time against the frame budget, total memory, GC allocation per frame and a frame-time history.

Outside Play mode it shows the subsystems that will deploy. The core's report includes the session's operation log.

## Generated IDs

Threadlink generates typed ID classes into `Assets/Threadforge/Threadlink User/Engineering/Codebase/Generated`, so code refers to scenes, input and settings-defined names without strings:

| Class | Source | Members |
| --- | --- | --- |
| `GameScenes` | Scenes in the active Build Profile (Nexus) | `SceneId` per scene, registered with Nexus at startup |
| `GameInput` | Input actions in **Project Settings ▸ Threadlink ▸ Dextra** | `GameInput.Maps.<Map>` (`InputMapId`) and `GameInput.<Map>.<Action>` (`InputId<T>`) |
| `AudioCategories` | Categories in **Project Settings ▸ Threadlink ▸ Aura** | `AudioCategory` per category |
| `ContentAssets`, `ContentPrefabs`, `ContentScenes` | Addressables entries ticked in the [Content mapping](resource-loading.md#content-mapping), nested by group | `ContentAssetId`, `ContentPrefabId`, `ContentSceneId` per entry |
| `ContentLabels` | Labels in the Addressables settings | `ContentLabel` per label |
| `Achievements`, `Statistics`, `Leaderboards`, `Products` | What each installed platform authors, such as **Project Settings ▸ Threadlink ▸ Steam** | `AchievementId`, `StatisticId`, `LeaderboardId`, `ProductId` per entry |

Generation runs automatically when a scene, input actions asset, ID domain or engineering setting changes, or when the Build Profile scene list changes; **Threadlink ▸ Generate IDs** runs it on demand. The Workspace's **Code Generation** page lists every generator with its source, output and ID counts, and opens each source. Names become PascalCase identifiers (`first-chest` becomes `FirstChest`). Each member keeps a stable key (a GUID for scenes and input, the name for categories and achievements) recorded in a `.ids.json` ledger beside the class. When a key disappears, its member stays as an `[Obsolete]` tombstone, so code that still uses it warns instead of breaking; see [Identifiers and Tombstones](../designer/identifiers.md). Never edit generated classes or ledgers by hand, and keep them under version control.

A package adds a generator by implementing `IIdGenerator` in an Editor assembly; Threadlink finds it with `TypeCache` and calls `GeneratedIds.Write(className, summary, members)` (or `WriteEnum` for an enumeration). Implement `Title`, `Source`, `Outputs` and `OpenSource` to describe it on the Code Generation page.

## ID domains

An **ID domain** is a project-defined list of IDs: create one on the Code Generation page or with **Create ▸ Threadlink ▸ ID Domain**. Each domain is one asset, in `Threadlink User/Design/Identifiers` when designers maintain it or `Threadlink User/Engineering/Identifiers` when engineers do.

| Generates | Result | Example |
| --- | --- | --- |
| Its own enumeration | `public enum <AssetName> : int` with `None = 0` and one member per entry | `EnemyKind.Goblin`; a serialized `EnemyKind` field is a dropdown in the Inspector |
| Random domains (StatelessRNG) | Members of `RandomDomains`, each a `StatelessRNG.Domain` | `rng.CreateStream(RandomDomains.Loot, chestID)` |
| Vault fields | Members of `VaultFields`, each a `VaultField` | `fields.Get<int>(VaultFields.Damage)` |

- Each entry receives a value when it is added, starting from a hash of its name (for random domains, the value `StatelessRNG.Domain.Named` would give). Renaming an entry keeps its value, so serialized enum fields, saves and replays stay valid.
- Removing an entry keeps it as an obsolete tombstone, and its value is never handed out again. Deleting a domain keeps its type with every member obsolete.
- Empty names, duplicate names and an entry called `None` are reported in the Inspector and stop that domain from generating.
- Domains that extend a native domain may come from several assets; their entries join one class. Generating `RandomDomains` adds the `Threadforge.Threadlink.Deterministic` reference to the generated folder's assembly.

A package adds a native domain by implementing `IIdDomainTarget` in an Editor assembly. Iris events need no IDs: an event's struct type is its identity and carries its payload.

## Engineering settings

**Project Settings ▸ Threadlink** itself is a read-only overview: the framework version, the session state, and every settings page with its owning package and status (defaults, configured, or the problem its validation reports). The core has no settings of its own. Below it, Threadlink has one page per settings type (Aura, Dextra, Sentinel, Steam and any project type deriving from `ThreadlinkSettings<T>`). The first edit creates the asset in `Assets/Threadforge/Threadlink User/Engineering/Configs`; the asset is read-only in the Inspector and added to the player's preloaded assets at build. Problems reported by `Validate()` show on the page and in the Validation page.

## Extending the Workspace

Derive a public class from `WorkspacePage` (implement `IFillsWorkspace` for a page that fills the window instead of scrolling) or implement `IWorkspaceValidator`, with a public parameterless constructor, in an Editor assembly that references `Threadforge.Threadlink.Editor`. Use `WorkspaceUI` and `ThreadlinkTheme.Apply` for consistent styling. Pages and validators are discovered with `TypeCache`; runtime assemblies are never scanned. Validators report issues and never modify assets; `WorkspaceValidation.RunAll()` runs them from scripts and continuous integration.

## Framework development tools

The Threadlink source repository contains Editor tools under `Threadforge/Development` that are not part of any package:

- **Threadlink ▸ Validation ▸ Framework ▸ Run All Checks** runs the maintained Editor, Mono runtime and IL2CPP player suites of every installed package and writes results and status to `Logs/FrameworkValidation`. The IL2CPP player runs headless. A missing or empty package suite fails the run. **Run Runtime Checks** runs only the runtime suites in Play mode; **Run Runtime Checks Without Domain Reload** runs them with domain reload disabled, as Unity's CoreCLR Editor requires, and restores the project's Enter Play Mode settings.
- **Threadlink ▸ Code Formatter** formats C# files in a selected folder with the bundled CSharpier.
- **Threadlink ▸ Development ▸ Rebuild Showcase Sample** authors the Showcase scene, panel settings and theme.
- `Tools/release.py` applies the release version and package metadata, regenerates the starter compatibility set and the Hub catalog, checks every package, packs archives and writes OpenUPM metadata.
- `Tools/Generators/build.py` builds the subsystem registration source generator shipped in the core package.
