# Nexus: Scene Management

`Nexus` is a static class with one scene workflow, whether a game has one scene at a time or many at once:

- **Residency:** a scene is loaded while anything holds it.
- **Presentation:** the resident scene the local player sees and hears.
- **Transitions:** presentation behind the fader and loading screen.

Every scene loads additively beside the bootstrap scene (see [Deployment Overview](../foundations/deployment-overview.md)), which stays loaded throughout. Nexus never loads or unloads the bootstrap scene.

## The ISceneEntry contract

```csharp
public interface ISceneEntry
{
    ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
    ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
    ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
    float MusicVolume { get; }
    float AtmosVolume { get; }
    LocalPhysicsMode PhysicsMode { get; }   // default: LocalPhysicsMode.None
    float UnloadDelay { get; }              // default: 0 seconds

    UniTask OnFinishedLoadingAsync();       // default: completed
    UniTask OnBeforeUnloadedAsync();        // default: completed
}
```

The interface is nested in `Nexus` (`Nexus.ISceneEntry`). Any type can implement it: a ScriptableObject, a struct, or another framework's asset type. Every member after the volumes has a default.

- **`PhysicsMode`:** `LocalPhysicsMode.None` shares the default physics scene. A scene resident alongside others should have its own (`LocalPhysicsMode.Physics2D` or `Physics3D`). Get it with `scene.GetPhysicsScene2D()` or `GetPhysicsScene()` and step it yourself: Unity does not simulate local physics scenes.
- **`UnloadDelay`:** how long the scene stays resident once nothing holds it. A hold within that time keeps it without reloading.
- **Audio:** an unmapped music or ambience pointer (such as `None`) plays nothing on that channel. If neither is mapped, presenting the scene leaves the audio as it is.

## Residency: holds

A scene is resident while anything holds it:

```csharp
Nexus.SceneHold farm = await Nexus.HoldAsync(farmEntry);   // resident and booted; null if it failed to load
Scene scene = farm.Scene;

farm.Release();                                            // unloads once nothing holds it, after entry.UnloadDelay
farm.Release(delaySeconds: 10f);                           // or after this long, unless it is held again meanwhile
```

A `SceneHold` exposes its `Entry`, its `Scene`, and `IsHeld`, which is `false` once released.

- **Loading.** A scene loads once, additively with the entry's `PhysicsMode`. Its discoverables boot through Initium and `OnFinishedLoadingAsync` is awaited; only then do holders get it, and `OnSceneResident` is published. Holders that arrive during the load share it.
- **Failed loads.** If the scene fails to load or its objects fail to boot, every holder gets `null` and an error is logged. A scene that loaded but failed to boot is discarded and unloaded; a later hold tries again.
- **Unloading** always takes the same path: await `OnBeforeUnloadedAsync`, publish `OnSceneUnloading`, discard every `IDiscoverable` in the scene through `Initium.DiscardSceneObjects` (children first), then unload. So scene objects unregister in `Discard` whatever they registered in `Boot`, and never discard themselves. If an unload fails, Nexus keeps the scene and retries on the next hold, release or shutdown.
- **Ordering.** Operations on one scene never overlap. A hold during an unload waits for it, then loads the scene again. A hold during a delayed unload cancels it. A scene whose last hold is released while it is still loading unloads once the load finishes.
- **Queries.** `Nexus.IsResident(entry)`, `Nexus.TryGetResidentScene(entry, out scene)` and `Nexus.TryGetResidentScene(scenePointer, out scene)` only succeed for a scene that is loaded, booted and not unloading.
- **Robustness.** An exception in an `OnSceneResident` or `OnSceneUnloading` listener is logged and can't stop a load or unload halfway.

Release each hold once, when its holder no longer needs the scene; releasing it again does nothing. Residency alone changes nothing the player sees or hears. The [Runtime Monitor](editor-tooling.md#runtime-monitor) lists held scenes with their state and hold count.

## Presentation and transitions

```csharp
bool presented = await Nexus.PresentAsync(entry);      // hold, activate, audio, OnScenePresented, then release the previous scene
bool transitioned = await Nexus.TransitionAsync(entry); // the same behind the fader and loading screen: the single-scene workflow
await Nexus.StopPresentingAsync();                     // present nothing, releasing the presented scene

Nexus.ISceneEntry current = Nexus.Presented;
Nexus.TryGetPresentedScene(out Scene scene);
```

- **Presenting holds the scene,** loading it if needed, makes it the active scene, switches to its audio, and publishes `OnScenePresented`. Then it releases its hold on the previously presented scene. So a scene held only by presentation unloads when something else is presented, while a scene something else holds stays resident.
- **The result** is `false` if the scene could not be loaded, and the current scene stays presented. Presenting the scene already presented does nothing and returns `true`.
- **Presentations run one at a time,** in call order, and `StopPresentingAsync` queues with them.
- **`TransitionAsync`** wraps `PresentAsync` in `FadeToLoadingScreenAsync` and `FadeToGameplayAsync`. These drive four `Func<UniTask>` events (`OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync`, `OnHideLoadingScreenAsync`) and Aura's listener volume fade. Each event needs exactly one provider.

The fade steps are public for custom sequences: `FadeToLoadingScreenAsync()`, `FadeToGameplayAsync()`, `FadeFaderAsync(bool visible)`, `FadeLoadingScreenAsync(bool visible)` and `FadeAudioAsync(float targetVolume)`.

A single-scene game needs nothing else:

```csharp
await Nexus.TransitionAsync(townEntry);   // the previous scene unloads, its objects discarded
```

A game with several resident scenes, such as a multiplayer host that simulates every occupied area but shows only its own player's, holds the areas it simulates and presents one:

```csharp
var area = await Nexus.HoldAsync(entry);   // simulated: no visible or audible change
await Nexus.TransitionAsync(entry);        // the local view moves to it; it stays held by both
area.Release(gracePeriod);                 // later, once no one needs it
```

Objects booting in a resident scene must not assume it is the active scene, and objects instantiated at runtime must be moved to their scene explicitly. Hiding resident scenes that aren't presented (renderers, lights, canvases) is up to the project; `OnSceneLoaded` fires just before a scene's first frame.

## Events

| Event | Delegate | Raised |
|---|---|---|
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` (`Scene`, `Mode`) | As Unity activates a scene: before it renders and before its discoverables boot. |
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Once a scene has loaded, booted and run `OnFinishedLoadingAsync`, before any holder gets it. |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Once the presented scene is active and its audio transition has run. |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Before any unload, while the scene's objects still exist. Use it to read them, for example to save their state; their own `Discard` handles teardown. |
| `OnSceneUnloaded` | `Action<Scene>` | Once a scene has unloaded. The `Scene` is only useful as a key by then. |

`OnSceneLoaded` and `OnSceneUnloaded` republish Unity's events for every scene, however it was loaded, so project code never needs to subscribe to `SceneManager`.

## Shutdown

When the application quits (in the Editor, when Play Mode exits), shutdown first releases every hold, so nothing is presented and each held scene's objects are discarded while their subsystems still exist. Then the subsystems are discarded (see [Shutdown](deployment.md#shutdown)). Unity finishes any unload that quitting cuts short.
