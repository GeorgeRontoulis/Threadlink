# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
