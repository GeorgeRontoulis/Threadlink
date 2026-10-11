# Nexus: Scene Management

`Nexus` is a static class with one scene workflow, for a game with one scene at a time or many at once:

- **Residency:** a scene is loaded while anything holds it.
- **Presentation:** the resident scene the local player sees and hears.
- **Transitions:** presentation behind the fader and loading screen.

Every scene loads additively beside the bootstrap scene (see [Deployment Overview](../foundations/deployment-overview.md)). The bootstrap scene stays loaded throughout, and Nexus never loads or unloads it.

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

The interface is nested in `Nexus` (`Nexus.ISceneEntry`) and may be implemented on any type: a ScriptableObject, a struct, or an asset type belonging to another framework. Every member after the volumes has a default implementation.

- **`PhysicsMode`:** `LocalPhysicsMode.None` shares the default physics scene. A scene resident alongside others should use its own (`LocalPhysicsMode.Physics2D` or `Physics3D`). Query it through `scene.GetPhysicsScene2D()` / `GetPhysicsScene()` and step it explicitly, since Unity does not simulate local physics scenes.
- **`UnloadDelay`:** how long the scene stays resident once nothing holds it. A hold within that time keeps it without reloading.
- **Audio:** a music or ambience pointer that is not mapped (for example `None`) plays nothing on that channel. If neither is mapped, presenting the scene leaves the audio as it is.

## Residency: holds

A scene is resident while anything holds it:

```csharp
Nexus.SceneHold farm = await Nexus.HoldAsync(farmEntry);   // resident and booted; null if it failed to load
Scene scene = farm.Scene;

farm.Release();                                            // unloads once nothing holds it, after entry.UnloadDelay
farm.Release(delaySeconds: 10f);                           // or after this long, unless it is held again meanwhile
```

A `SceneHold` exposes its `Entry`, its `Scene`, and `IsHeld`, which is `false` once released.

- **Loading.** A scene loads once, additively and with the entry's `PhysicsMode`, boots its discoverables through Initium, and awaits `OnFinishedLoadingAsync`. Only then does any holder get it, and `OnSceneResident` is published. Holders arriving during the load share it.
- **Failed loads.** If the scene cannot be loaded or its objects fail to boot, every holder gets `null` and an error is logged. A scene that loaded but failed to boot is discarded and unloaded again; a later hold tries afresh.
- **Unloading**, whatever the reason, follows one path. It awaits `OnBeforeUnloadedAsync`, publishes `OnSceneUnloading`, and discards every `IDiscoverable` in the scene through `Initium.DiscardSceneObjects`, children before parents. Only then does it unload the scene. Scene objects therefore unregister in `Discard` whatever they registered in `Boot`, and never discard themselves. An unload that fails leaves the scene owned by Nexus, and the next hold, release or shutdown retries it.
- **Ordering.** Operations on one scene never overlap. A hold during an unload waits for it, then loads the scene again. A hold during a delayed unload cancels the unload. A scene whose last hold is released while it is still loading unloads once the load completes.
- **Queries.** `Nexus.IsResident(entry)`, `Nexus.TryGetResidentScene(entry, out scene)` and `Nexus.TryGetResidentScene(scenePointer, out scene)` answer only for a scene that is loaded, booted and not unloading.
- **Robustness.** An exception thrown by `OnSceneResident` or `OnSceneUnloading` listeners is logged and cannot stop a load or unload halfway.

Release each hold exactly once, when its holder no longer needs the scene. Releasing it again does nothing. Residency changes nothing the player sees or hears.

## Presentation and transitions

```csharp
bool presented = await Nexus.PresentAsync(entry);      // hold, activate, audio, OnScenePresented, then release the previous scene
bool transitioned = await Nexus.TransitionAsync(entry); // the same behind the fader and loading screen: the single-scene workflow
await Nexus.StopPresentingAsync();                     // present nothing, releasing the presented scene

Nexus.ISceneEntry current = Nexus.Presented;
Nexus.TryGetPresentedScene(out Scene scene);
```

- **Presenting holds the presented scene,** loading it if needed. It makes it the active scene, transitions to its audio scenario, and publishes `OnScenePresented`. Then it releases its hold on the scene presented before. So a scene held only by presentation unloads when something else is presented, while a scene another system holds stays resident.
- **The result** is `false` if the scene could not be loaded; what was presented stays presented. Presenting the scene already presented does nothing and returns `true`.
- **Presentations run one at a time,** in call order. `StopPresentingAsync` takes its turn with them.
- **`TransitionAsync`** wraps `PresentAsync` in `FadeToLoadingScreenAsync` and `FadeToGameplayAsync`, which drive four `Func<UniTask>` events, `OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync` and `OnHideLoadingScreenAsync`, plus Aura's listener volume fade. Each event requires exactly one provider.

The fade steps are public for custom sequences: `FadeToLoadingScreenAsync()`, `FadeToGameplayAsync()`, `FadeFaderAsync(bool visible)`, `FadeLoadingScreenAsync(bool visible)` and `FadeAudioAsync(float targetVolume)`.

A single-scene game needs nothing else:

```csharp
await Nexus.TransitionAsync(townEntry);   // the previous scene unloads, its objects discarded
```

A game with several resident scenes, such as a multiplayer host simulating every occupied area while presenting its own player's, holds the areas it simulates and presents one:

```csharp
var area = await Nexus.HoldAsync(entry);   // simulated: no visible or audible change
await Nexus.TransitionAsync(entry);        // the local view moves to it; it stays held by both
area.Release(gracePeriod);                 // later, once no one needs it
```

Objects booting in a resident scene must not assume it is the active scene. Objects instantiated at runtime must be placed in their scene explicitly. Hiding scenes that are resident but not presented (renderers, lights, canvases) is the project's rendering policy: `OnSceneLoaded` gives it the moment before a scene's first frame.

## Events

| Event | Delegate | Raised |
|---|---|---|
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` (`Scene`, `Mode`) | Synchronously as Unity activates a scene: before it renders, and before its discoverables boot. |
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Once a scene has loaded, booted and run `OnFinishedLoadingAsync`, before any holder gets it. |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Once the presented scene is active and its audio transition has run. |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Before any unload, while the scene's objects are still there, undiscarded. For reading them, such as saving their state; their own `Discard` is their teardown. |
| `OnSceneUnloaded` | `Action<Scene>` | Once a scene has unloaded. The `Scene` is only a key by then. |

`OnSceneLoaded` and `OnSceneUnloaded` republish Unity's own, however a scene was loaded, so project code never subscribes to `SceneManager`.

## Shutdown

When the application quits, which in the Editor is when Play Mode exits, Threadlink's shutdown first releases every hold. Nothing is presented any more, and each held scene has its objects discarded while the subsystems they registered with still exist. Then the subsystems are discarded (see [Shutdown](deployment.md#shutdown)). Unity finishes the unloads that quitting cuts short.
