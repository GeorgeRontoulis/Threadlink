# Deployment and Initialisation

## Deployment sequence

| Phase | Hook | Action |
|---|---|---|
| 1 | `[OnEnteringPlayMode]` | `NativeWeavingFactory.Register()` registers factories for Sentinel, Chronos, Dextra and Aura. Nexus subscribes to Unity's scene events. |
| 1 | `SubsystemRegistration` | `UserWeavingFactory.Register()` registers project factories. The Sentinel module registry is cleared. |
| 2 | `AfterAssembliesLoaded` | `UserSubsystemsConfig` subscribes to `OnUserSubsystemRegistration`. Sentinel platform modules register with `SentinelModuleRegistry`. |
| 3 | `BeforeSceneLoad` | `NativeSubsystemsConfig` subscribes to `OnNativeSubsystemRegistration`. |
| 4 | `AfterSceneLoad` | `Threadlink.DeployCoreAsync()` executes. |

`DeployCoreAsync` performs:

1. In the Editor, an early return if `ThreadlinkEditorDeployment.Suppressed` is set (see [Installation and Project Setup](../getting-started/installation.md#verify-the-deployment)).
2. `await Addressables.InitializeAsync()`.
3. Loading of `ThreadlinkNativeConfig` from the address in `NativeConstants.Addressables.NATIVE_CONFIG`.
4. Loading of `ThreadlinkUserConfig` through the Native Config's `UserConfig` native resource.
5. Core construction and `DeployAsync()`:
   - `Boot()` installs `ThreadlinkPlayerLoop` into Unity's current `PlayerLoop` when the update loop is configured as `Native`; no scene object or `MonoBehaviour` is created. It also subscribes the core's shutdown to `Application.quitting`.
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` publishes the `Func<List<IThreadlinkSubsystem>>` event and passes the collected subsystems through the [initialisation pipeline](#initialisation-pipeline), then logs the woven subsystems.
   - The sequence repeats for `OnUserSubsystemRegistration`. An empty list is reported and skipped.
   - `OnCoreDeployed` is published with the core as payload.
6. The initialisation pipeline is dispatched as fire-and-forget for the discoverable objects of every scene already loaded.

Failure to load either configuration asset aborts deployment with an error. A preloader returning `false` throws, as described below; Sentinel is the exception, since a failed platform does not fail its preloader (see [Sentinel](sentinel/README.md#deployment)).

## Initialisation pipeline

Initium's pipeline is the one path through which the core prepares subsystems and discoverable objects. It partitions its input by interface and executes three phases, awaiting completion of each before proceeding:

1. `IAddressablesPreloader.TryPreloadAssetsAsync()`
2. `IBootable.Boot()`
3. `IInitializable.Initialize()`

The input is materialised once, so every phase operates on the same objects: an object created during `Boot` is not swept into the `Initialize` phase without having been booted. All preloaders in the first phase are awaited together. If any preloader returns `false`, the pipeline throws `InvalidOperationException` naming the failed preloader and does not proceed to Boot/Initialize. Boot and Initialize are batch-oriented: every object in the phase runs, then a single frame passes; intra-phase ordering is not guaranteed.

At deployment, scene objects are discovered across every loaded scene via `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`.

The pipeline entry point itself is internal. Individual scenes are handled by a public pair:

| Member | Semantics |
|---|---|
| `Initium.BootAndInitSceneObjectsAsync(Scene)` | Preloads, boots and initialises the active `IDiscoverable` objects placed in the scene. |
| `Initium.DiscardSceneObjects(Scene)` | Discards every `IDiscoverable` placed in the scene, active or not, children before parents. An exception thrown by one object is logged and the rest are still discarded. Must run before the scene unloads. |

Scenes held and presented through [Nexus](nexus.md) are processed automatically, whether one scene is resident or many. Scenes loaded by other means must call both members themselves.

Unity destroys scene objects without invoking `Discard()`, so a scene must never unload any other way: every Nexus unload discards its discoverables first. Scene objects release in `Discard` what they registered in `Boot`, and never discard themselves. Nor does any subsystem tear them down on a scene event: an object registered with a subsystem unregisters itself when Nexus discards it (see [Ownership and discarding](architecture.md#ownership-and-discarding)).

### Booting objects directly

Objects the developer owns are booted and initialised through Initium's direct members. None of them runs preloaders.

| Member | Semantics |
|---|---|
| `Boot(IBootable)`, `Initialize(IInitializable)` | One phase, synchronously. A null object is ignored. |
| `BootAndInit<T>(T)` | Both phases, synchronously. |
| `BootAsync(IBootable)`, `InitializeAsync(IInitializable)` | One phase, then a single frame. |
| `BootAndInitAsync<T>(T)` | Boot, a frame, initialise, a frame. |
| `Boot<T>(IReadOnlyList<T>)`, `Initialize<T>(IReadOnlyList<T>)` | Sequential: one object, then a frame, for each object in order. |
| `BootAndInit<T>(IReadOnlyList<T>)` | Sequential boot of the list, then sequential initialisation. |

## Shutdown

Shutdown runs when the application quits, which in the Editor is when Play Mode exits:

1. **Scenes.** Nexus releases every hold. Nothing is presented any more, and each held scene has its objects discarded while the subsystems they registered with still exist, and its unload started. Unity finishes the unloads that quitting cuts short.
2. **Subsystems.** Every woven subsystem is discarded in reverse weave order: user subsystems in reverse of the order they are listed, then the native ones (Aura, Dextra, Chronos, Sentinel). A subsystem is therefore discarded before those it depends on, provided its dependencies are woven first. A subsystem whose `Discard` throws is reported, and the others are still discarded.

`Discard()` is therefore the one place a subsystem releases what it holds: sockets, native memory, platform sessions.

The update loop is uninstalled when the core is discarded or the application quits. When Play Mode ends, Iris clears every listener, Nexus forgets its scenes, and framework statics are reset, so a following Play session starts clean even with Domain Reload disabled.
