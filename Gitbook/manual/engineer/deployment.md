# Deployment and Initialisation

## Deployment sequence

| Phase | Hook | Action |
|---|---|---|
| 1 | `[OnEnteringPlayMode]` | `NativeWeavingFactory.Register()` registers factories for Sentinel, Chronos, Dextra and Aura. Nexus subscribes to Unity's scene events. |
| 1 | `SubsystemRegistration` | `UserWeavingFactory.Register()` registers project factories. The Sentinel module registry is cleared. |
| 2 | `AfterAssembliesLoaded` | `UserSubsystemsConfig` subscribes to `OnUserSubsystemRegistration`. Sentinel platform modules register with `SentinelModuleRegistry`. |
| 3 | `BeforeSceneLoad` | `NativeSubsystemsConfig` subscribes to `OnNativeSubsystemRegistration`. |
| 4 | `AfterSceneLoad` | `Threadlink.DeployCoreAsync()` runs. |

`DeployCoreAsync`:

1. In the Editor, returns at once if `ThreadlinkEditorDeployment.Suppressed` is set (see [Installation and Project Setup](../getting-started/installation.md#verify-the-deployment)).
2. Awaits `Addressables.InitializeAsync()`.
3. Loads `ThreadlinkNativeConfig` from the address in `NativeConstants.Addressables.NATIVE_CONFIG`.
4. Loads `ThreadlinkUserConfig` through the Native Config's `UserConfig` native resource.
5. Creates the core and runs `DeployAsync()`:
   - `Boot()` installs `ThreadlinkPlayerLoop` into Unity's `PlayerLoop` when the update loop is `Native`. No scene object or `MonoBehaviour` is created. It also hooks the core's shutdown to `Application.quitting`.
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` publishes the `Func<List<IThreadlinkSubsystem>>` event, runs the returned subsystems through the [initialisation pipeline](#initialisation-pipeline), and logs them.
   - The same happens for `OnUserSubsystemRegistration`. An empty list is reported and skipped.
   - `IsDeployed` becomes `true`, and `OnCoreDeployed` is published with the core as payload.
6. Starts the initialisation pipeline, without awaiting it, for the discoverable objects of every scene already loaded.

If either configuration asset fails to load, deployment stops with an error. A preloader that returns `false` throws, as described below. Sentinel is the exception: a failed platform does not fail its preloader (see [Sentinel](sentinel/README.md#deployment)).

## Initialisation pipeline

Initium's pipeline is how the core prepares both subsystems and discoverable objects. It sorts its input by interface and runs three phases, each finishing before the next starts:

1. `IAddressablesPreloader.TryPreloadAssetsAsync()`
2. `IBootable.Boot()`
3. `IInitializable.Initialize()`

The input is collected once, so every phase sees the same objects: an object created during `Boot` is not initialised without having been booted. Preloaders all run together. If any returns `false`, the pipeline throws an `InvalidOperationException` naming it and stops before Boot. Boot and Initialize run in batches: every object in the phase, then one frame. Order within a phase is not guaranteed.

At deployment, scene objects are found across every loaded scene with `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`.

The pipeline itself is internal. Two public members handle individual scenes:

| Member | Semantics |
|---|---|
| `Initium.BootAndInitSceneObjectsAsync(Scene)` | Preloads, boots and initialises the active `IDiscoverable` objects in the scene. |
| `Initium.DiscardSceneObjects(Scene)` | Discards every `IDiscoverable` in the scene, active or not, children first. If one throws, the error is logged and the rest are still discarded. Call it before the scene unloads. |

Nexus does both automatically for the scenes it holds, however many are resident. Code that loads scenes some other way must call both itself.

Unity destroys scene objects without calling `Discard()`, so never unload a scene behind Nexus's back: every Nexus unload discards its discoverables first. Scene objects release in `Discard` what they registered in `Boot`, and never discard themselves. Subsystems don't tear them down on scene events either: an object registered with a subsystem unregisters itself when Nexus discards it (see [Ownership and discarding](architecture.md#ownership-and-discarding)).

### Booting objects directly

Boot and initialise objects you own with Initium's direct members. None of them runs preloaders.

| Member | Semantics |
|---|---|
| `Boot(IBootable)`, `Initialize(IInitializable)` | One phase, synchronously. A null object is ignored. |
| `BootAndInit<T>(T)` | Both phases, synchronously. |
| `BootAsync(IBootable)`, `InitializeAsync(IInitializable)` | One phase, then a single frame. |
| `BootAndInitAsync<T>(T)` | Boot, a frame, initialise, a frame. |
| `Boot<T>(IReadOnlyList<T>)`, `Initialize<T>(IReadOnlyList<T>)` | Each object in order, with a frame after each. |
| `BootAndInit<T>(IReadOnlyList<T>)` | Boots the list in order, then initialises it in order. |

## Shutdown

Shutdown runs when the application quits, which in the Editor is when Play Mode exits:

1. **Scenes.** Nexus releases every hold, so nothing is presented. Each held scene's objects are discarded while the subsystems they registered with still exist, and its unload starts. Unity finishes any unload that quitting cuts short.
2. **Subsystems.** Woven subsystems are discarded in reverse weave order: user subsystems in reverse of their listed order, then Aura, Dextra, Chronos and Sentinel. So a subsystem is discarded before those it depends on, as long as its dependencies are woven first. If one subsystem's `Discard` throws, it is reported and the rest are still discarded.

So `Discard()` is the one place a subsystem releases what it holds: sockets, native memory, platform sessions.

The update loop is uninstalled when the core is discarded or the application quits. When Play Mode ends, Iris clears every listener, Nexus forgets its scenes and framework statics reset, so the next Play session starts clean even with Domain Reload disabled.
