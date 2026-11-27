# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0]

### Added

- Workspace **Configuration** page: every configuration asset by category (Threadlink settings, Addressables, input, Localization, rendering) with Select, Create, Move to folder and a folder choice per asset kind.
- Asset folders (`AssetLocations`): new configuration assets go to the engineering or design configs folder, `Threadlink User/Engineering/Configs` and `Threadlink User/Design/Configs` by default, or a folder chosen per kind. The choices are stored in `ProjectSettings/ThreadlinkAssetLocations.asset`.
- `ConfigSource` and `ConfigAsset` for packages and projects to add their own configuration assets, and `ConfigUI` rows for Workspace pages.
- Workspace **Rendering** page: the active pipeline, the default and per-quality-level pipeline assets, installing the Universal or High Definition Render Pipeline, creating pipeline assets, and moving pipeline and renderer assets (`RenderPipelineKind`).
- Addressables settings can be created in the engineering configs folder from the Workspace.

### Changed

- `ThreadlinkSettingsAssets.Folder` is replaced by `ThreadlinkSettingsAssets.Location(type)`. Settings assets are found wherever they are in Assets, and new ones are created in their location's folder.
- `ThreadlinkSettingsAssets.Preload` is public.

### Fixed

- Opening a Threadlink page in Project Settings no longer leaves Unity's other settings pages with the Threadlink background. The theme applies to the page's own element (`ThreadlinkTheme.Host`), not to the root the Settings window shares between pages.

## [3.0.0]

### Changed

- Content loading moves from `Threadlink` to its own class: `Content.LoadAsync`, `LoadAllAsync`, `InstantiateAsync`, `LoadSceneAsync`, `Content.Map` (was `MapContent`) and `Content.UseBackend` (was `UseContentBackend`).
- `Iris.Publish` delivers to every listener and logs a listener that throws instead of stopping. `Iris.Notify` is removed; `Iris.TryNotify` is `Iris.TryPublish`.
- `Threadlink.Begin` is `Threadlink.BeginOperation`; `DiagnosticsOptions.Traces` is `RecordTraces`.
- `VaultRecord<T>.Version` is `SchemaVersion`, as on every Vault source.
- `InlineLayout.HEADER_BYTES` and `NativeAllocator.DEFAULT_ALIGNMENT` are `HeaderBytes` and `DefaultAlignment`.

### Removed

- `Threadlink.Get<T>()`: `Add<T>()` returns the subsystem already present.
- `Threadlink.Current`: use `Threadlink.Instance`, `TryGetSingleton` or `IsDeployed`.
- `ResourceRequest<T>.Release()`: dispose the request, or await `ReleaseAsync()`.
- `VaultFieldSet.Get<T>(string)`: read fields by their generated `VaultField`.
- `EventBus`, `AsyncEventBus` and `Iris.Bus`/`AwaitedBus` are internal.

## [2.0.0]

### Removed

- `OnInitialize`, `OnShutdown` and `Require<T>()`, obsolete since 1.1.0. Set up a subsystem in `OnSubsystemBoot`, use other subsystems through their `Instance` in `OnSubsystemInit`, and mirror both in `OnSubsystemDeinit` and `OnSubsystemShutdown`.
- `ResourceOwner` and `StableId`, which nothing in the framework used. Dispose resources with `using`, or bind subscriptions and content to their game object with the owner-bound APIs.

## [1.1.0]

### Added

- Two-phase startup: every subsystem boots (`OnSubsystemBoot`: its own state and Iris subscriptions), then every one initializes (`OnSubsystemInit`: connects to the others through `Instance`, in any order). Shutdown mirrors it with `OnSubsystemDeinit` and `OnSubsystemShutdown`. No subsystem declares a startup order.
- Iris boots and initializes before every other subsystem and is the one subsystem available while others boot. Using any other subsystem from `OnSubsystemBoot` fails with the fix in the message.
- Framework events on Iris: `ThreadlinkReady`, `SubsystemFailed`, `SessionEnding` and `ContentLoadFailed`.
- `Iris.TryNotify` delivers a notification when Iris is available and returns false otherwise.
- `SubsystemState` gains `Booting` and `Booted`. `Threadlink.DeployIsolated()` sessions start a subsystem another one uses on first use.

### Changed

- `TryGetSingleton` never starts a subsystem, and is true from the initialization phase on.
- The Runtime monitor's graph shows the subsystems each one uses while it initializes ("Uses").

### Deprecated

- `OnInitialize`, `OnShutdown` and `Require<T>()`. They still work: a subsystem that overrides `OnInitialize` is initialized as soon as another uses it, and `Require` still rejects cycles.

### Fixed

- A test session with Iris inside Play mode no longer removes the running session's frame callbacks when it ends.
- Content mapped while a subsystem boots joins the running catalog.

## [1.0.0]

First release.

### Added

- The Threadlink core: subsystem registration by source generator, synchronous deployment before the first scene, demand-driven dependencies with cycle detection, isolated cores for tests and tools.
- Iris: synchronous events, awaited events and requests on UniTask (zero-allocation pooled async), frame callbacks, owner-bound subscriptions.
- Content loading by generated IDs through Addressables, with owner-bound requests. The Content mapping (Workspace ▸ Content) ticks Addressables entries; three read-only dictionaries in Content settings map `ContentAssetId`, `ContentPrefabId` and `ContentSceneId` to Addressables references, generated as `ContentAssets`, `ContentPrefabs` and `ContentScenes`. Prefabs load or instantiate as a GameObject or a root component; Addressables labels generate `ContentLabels`. Addressables stays the source of truth for groups and delivery.
- Vault: typed definitions (`VaultAsset<T>`, `VaultVariant<T>`) with overrides, validation and typed field keys.
- Generated typed IDs for scenes, input, audio categories and achievements, and project-defined ID domains (enumerations, random domains and Vault fields) with stable values and tombstones.
- Engineering settings in Project Settings ▸ Threadlink, read-only in the Inspector and preloaded in players.
- The Workspace: Hub, Runtime monitor with animated data-flow lines, Code Generation, Content, Vault and Validation pages, in the Threadlink theme.
- Memory toolkits: native allocation, hashing and inline collections.
