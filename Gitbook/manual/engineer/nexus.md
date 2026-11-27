# Nexus: Scene Management

Nexus (`com.threadforge.threadlink.nexus`) moves between scenes. The next scene loads additively and stays hidden while it is prepared; only then is it presented and the previous scene unloaded. If loading or any preparation fails, the current scene stays on screen and nothing changes.

```csharp
await Nexus.LoadAsync(GameScenes.Forest);        // a scene in the active Build Profile
await Nexus.LoadAsync(ContentScenes.Levels.Forest); // a mapped Addressables scene

public sealed class Portal : MonoBehaviour
{
    [SerializeField] private SceneReference destination;   // a scene picker in the Inspector

    private void OnTriggerEnter(Collider other) => Nexus.LoadAsync(destination).Forget();
}
```

| API | Behaviour |
| --- | --- |
| `Nexus.LoadAsync(SceneId)` | Loads a Build Profile scene, prepares it, presents it, unloads the previous one. Returns the `SceneTransition`. A `SceneReference` converts to its `SceneId`. |
| `Nexus.LoadAsync(ContentSceneId)` | The same for a mapped Addressables scene (see [Resource Loading](resource-loading.md#content-mapping)). |
| `Nexus.HoldAsync(...)` | Keeps a scene loaded but hidden, for instant transitions later. Release the returned `SceneHold`. |
| `Nexus.CurrentScene` | The presented scene (or the active scene before the first transition). |
| `Nexus.IsTransitioning` | Whether a transition is running. |
| `Nexus.AddTransitionStep(prepare, committed)` | Joins every transition: `prepare` runs after the next scene loads and before it is shown (failing it keeps the current scene); `committed` runs after presentation. Dispose to leave. |

## Scene IDs

Threadlink generates `GameScenes` from the scenes in the active Build Profile (see [Generated IDs](editor-tooling.md#generated-ids)); it regenerates when the scene list changes. A `SceneId` is the scene asset's GUID plus its path, so moving or renaming a scene keeps references working. The generated class registers the scenes' current paths with `Nexus.AddScenes` before the first scene loads. Designer data uses a `SceneReference` field, which stores the same ID and shows a scene picker; `IsAssigned` tells whether one was chosen. A scene that is not in the active Build Profile fails the transition and the current scene stays.

After every presentation Nexus publishes `SceneChanged` on Iris.

## Presentation

A loaded scene's roots are deactivated until presentation and reactivated with their authored active state; the presented scene becomes the active scene. Author scene roots inactive if nothing may run before presentation. Transitions are serialized: one runs at a time.

## Joining transitions

Use a transition step for anything that must be ready when the scene appears:

```csharp
public sealed class LoadingCurtain : ThreadlinkSubsystem<LoadingCurtain>
{
    private IDisposable step;

    protected override void OnInitialize() =>
        step = Nexus.AddTransitionStep(async (transition, token) => await WarmUpAsync(transition.To, token));

    protected override void OnShutdown() => step.Dispose();
}
```

Aura's scene music is such a step: see [Aura](aura.md).
