# Architecture

Threadlink owns the parts of a game that live for the whole session. Each part is a **subsystem**; the **core** creates, orders, links and shuts them down. Game code never decides where a framework object lives or when it starts.

## The session

| Moment | What happens |
| --- | --- |
| `RuntimeInitializeLoadType.SubsystemRegistration` | Every subsystem class registers itself. Generated code does this; nothing scans assemblies. |
| `RuntimeInitializeLoadType.BeforeSceneLoad` | The core deploys: each subsystem is created and initialized, dependencies first. |
| First scene's `Awake` and `Start` | Every subsystem is ready. Nothing waits. |
| Leaving Play mode, code reload, player quit | Subsystems shut down in reverse initialization order; the session's `Threadlink.Lifetime` token is cancelled. |

Initialization is synchronous. Work that is genuinely asynchronous, such as reading a save, loading a scene or calling a platform service, is an ordinary `UniTask` call the game makes when it needs it, never a precondition for `Start`.

## Subsystems

Derive from `ThreadlinkSubsystem<T>`. The bundled source generator registers every concrete subsystem in assemblies that reference Threadlink; mark a class `[ManualRegistration]` to keep it out of the session (tests and tools add such subsystems explicitly).

```csharp
using Threadforge.Threadlink;

public sealed class Wallet : ThreadlinkSubsystem<Wallet>
{
    public int Coins { get; private set; }

    protected override void OnInitialize() => Require<Iris>();

    public static void Add(int amount)
    {
        Instance.Coins += amount;
        Iris.Publish(new CoinsChanged(Instance.Coins));
    }

    public override void Report(MonitorReport report) => report.Summary(Coins + " coins");
}
```

| Member | Meaning |
| --- | --- |
| `OnInitialize()` | Runs once, before the first scene's Awake. Throwing fails only this subsystem and the ones that require it. |
| `OnShutdown()` | Runs once when the session ends, in reverse order. |
| `Require<T>()` | Returns another subsystem, initializing it first if needed. The dependency is recorded, ordered and shown in the Runtime monitor; cycles are rejected with the full chain. |
| `Instance` | The linked instance. Throws `SubsystemUnavailableException` with the reason when it is not linked. |
| `TryGetSingleton(out T)` | Whether the subsystem exists, is initialized and is linked to the current core. |
| `Lifetime` | Cancelled when the session ends. |
| `Report(MonitorReport)` | What the Runtime monitor shows. |

Framework subsystems follow the same pattern and expose their API as static methods on the class: `Iris.Publish`, `Nexus.LoadAsync`, `Aura.Play`, `Dextra.Player`, `Sentinel.Saves.SaveAsync`, `Vault.Get`. Content loading lives on the core: `Threadlink.LoadAsync`.

## The core

| API | Meaning |
| --- | --- |
| `Threadlink.TryGetSingleton(out Threadlink)` | Whether a core is deployed and linked. |
| `Threadlink.IsDeployed`, `Threadlink.Instance` | Deployment state and the current core. |
| `Threadlink.Lifetime` | Cancelled when the session ends. |
| `core.Subsystems` | Every subsystem in initialization order. |
| `Threadlink.Operations`, `Threadlink.Begin` | The session's operation log. See [Diagnostics](diagnostics.md). |
| `Threadlink.DeployIsolated()` | A separate core for tests and tools; it becomes current until disposed and starts empty (`core.Add<T>()`). |

## Design and engineering

| Kind | Lives in | Edited by |
| --- | --- | --- |
| Composition and behaviour | Code: subsystems, components, save types | Engineers |
| Engineering settings | `ThreadlinkSettings<T>` assets in `Engineering/Configs`, edited only in **Project Settings ▸ Threadlink**, read-only in the Inspector, preloaded in players | Engineers |
| Designer data | Assets and scene components: `AudioCue`, `SceneMusic`, `VaultAsset`, screen documents, tuning values | Designers |

Designer data holds values only: it never selects types, services or wiring.

## Asynchronous work

Every asynchronous API returns [UniTask](https://github.com/Cysharp/UniTask) (`UniTask` or `UniTask<T>`, namespace `Cysharp.Threading.Tasks`), which the core installs as a dependency. UniTask pools its async machinery, so steady-state asynchronous work does not allocate, and an `async UniTask` method that completes synchronously allocates nothing in release builds. Write your own listeners, steps and interactions as `async UniTask` methods and use UniTask's own helpers: `UniTask.CompletedTask`, `UniTask.FromResult`, `UniTask.WhenAll`, `UniTask.NextFrame`, `UniTask.WaitForSeconds`.

- Await a UniTask once. To await one result in several places, call `.Preserve()` first.
- Start work you do not await with `.Forget()`, so a failure is reported immediately; a discarded faulted UniTask is reported later as an unobserved exception.
- The Editor compiles scripts in Debug mode, where every `async` method allocates its state machine once per call. Measure allocations in a player build or with the Editor's code optimization set to Release.

## Outcomes

Operations report how they failed. Content requests, transitions, saves and platform calls return or throw explicit states (cancelled, failed, conflict, corrupt data, unsupported, unavailable) and never succeed silently.
