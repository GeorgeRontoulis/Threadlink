# Architecture

Threadlink owns the parts of a game that live for the whole session. Each part is a **subsystem**; the **core** creates, orders, links and shuts them down. Game code never decides where a framework object lives or when it starts.

## The session

| Moment | What happens |
| --- | --- |
| `RuntimeInitializeLoadType.SubsystemRegistration` | Every subsystem class registers itself. Generated code does this; nothing scans assemblies. |
| `RuntimeInitializeLoadType.BeforeSceneLoad` | The core deploys in two phases. Iris boots and initializes first. Then every other subsystem **boots**, then every one **initializes**. `ThreadlinkReady` is announced on Iris. |
| First scene's `Awake` and `Start` | Every subsystem is ready. Nothing waits. |
| Leaving Play mode, code reload, player quit | `SessionEnding` is announced, the session's `Threadlink.Lifetime` token is cancelled, every subsystem **deinitializes** in reverse initialization order, then every one **shuts down** in reverse boot order. |

Startup is synchronous. Work that is genuinely asynchronous, such as reading a save, loading a scene or calling a platform service, is an ordinary `UniTask` call the game makes when it needs it, never a precondition for `Start`.

### Two phases instead of a dependency order

Boot and initialization split what a subsystem does to itself from what it does with others, so no subsystem declares what it depends on and the order between them never matters.

| Phase | Hook | Does | May use |
| --- | --- | --- | --- |
| Boot | `OnSubsystemBoot()` | Sets up the subsystem's own state, creates its objects, subscribes to Iris events. | Only itself and Iris. Using any other subsystem throws, with the fix in the message. |
| Initialize | `OnSubsystemInit()` | Connects to other subsystems, registers with them, announces readiness on Iris. | Every subsystem, in any order: all have booted. |
| Deinitialize | `OnSubsystemDeinit()` | Disconnects from other subsystems. | Every subsystem: none has shut down yet. |
| Shut down | `OnSubsystemShutdown()` | Releases the subsystem's own state. | Itself and Iris. |

A subsystem that reaches a state only while it initializes (Steam signing in, a platform taking over accounts) announces it on Iris. Subscribe while booting, and the announcement arrives whichever subsystem initializes first. State another subsystem reaches during its initialization arrives through its events, not from `OnSubsystemInit`. An event reaches only the subscribers listening when it is announced; Iris never replays one. See [Framework Events](events.md).

Keep both phases to the work that has to happen before the first scene: wiring, subscriptions, starting an SDK that must start early. Loading, waiting and reading files belong to the asynchronous APIs, after startup.

## Subsystems

Derive from `ThreadlinkSubsystem<T>`. The bundled source generator registers every concrete subsystem in assemblies that reference Threadlink; mark a class `[ManualRegistration]` to keep it out of the session (tests and tools add such subsystems explicitly).

```csharp
using Threadforge.Threadlink;

public sealed class Wallet : ThreadlinkSubsystem<Wallet>
{
    private SubscriptionToken account;

    public int Coins { get; private set; }

    // Its own state, and its subscriptions: the account is announced while Sentinel and the platforms initialize.
    protected override void OnSubsystemBoot() => account = Iris.Subscribe<AccountChanged>(changed => Coins = 0);

    protected override void OnSubsystemShutdown() => account.Dispose();

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
| `OnSubsystemBoot()` | Startup phase one: set up the subsystem itself and subscribe to Iris. Throwing fails only this subsystem. |
| `OnSubsystemInit()` | Startup phase two: connect to other subsystems through their `Instance`. Throwing fails this subsystem; a subsystem whose initialization uses a failed one fails with the chain in its error. |
| `OnSubsystemDeinit()` | Shutdown phase one, in reverse initialization order: disconnect from other subsystems. |
| `OnSubsystemShutdown()` | Shutdown phase two, in reverse boot order: release the subsystem's own state. |
| `Instance` | The subsystem in the current session. Throws `SubsystemUnavailableException` with the reason when it is not part of the session or failed, and `InvalidOperationException` when used from another subsystem's boot. |
| `TryGetSingleton(out T)` | Whether the subsystem is part of the current session and available. It never starts one. |
| `State`, `Error` | `Registered`, `Booting`, `Booted`, `Initializing`, `Ready`, `Failed` or `ShutDown`, and why it failed. |
| `Dependencies` | The subsystems this one used while it initialized, shown in the Runtime monitor's graph. |
| `Lifetime` | Cancelled when the session ends. |
| `Report(MonitorReport)` | What the Runtime monitor shows. |

Framework subsystems follow the same pattern and expose their API as static methods on the class: `Iris.Publish`, `Nexus.LoadAsync`, `Aura.Play`, `Dextra.Player`, `Sentinel.Saves.SaveAsync`, `Vault.Get`. Content loading lives on the core: `Threadlink.LoadAsync`.

## The core

| API | Meaning |
| --- | --- |
| `Threadlink.TryGetSingleton(out Threadlink)` | Whether a core is deployed and linked. |
| `Threadlink.IsDeployed`, `Threadlink.Instance` | Deployment state and the current core. |
| `Threadlink.Lifetime` | Cancelled when the session ends. |
| `core.Subsystems` | Every subsystem in initialization order, then any that failed. |
| `Threadlink.Operations`, `Threadlink.Begin` | The session's operation log. See [Diagnostics](diagnostics.md). |
| `Threadlink.DeployIsolated()` | A separate core for tests and tools; it becomes current until disposed and starts empty. `core.Add<T>()` boots and initializes a subsystem; one that another uses joins on first use. |

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
