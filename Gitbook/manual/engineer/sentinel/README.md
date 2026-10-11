# Sentinel: Platform Services

Sentinel gives one API for platform services on every platform. Unity decides the platform; `SentinelConfig` only picks the **distribution** (store) where a platform has several, such as Windows: Local, Steam, Microsoft Store, Epic or GOG. Runtime modules each handle one exact `SentinelPlatformMarker` / `SentinelDistribution` pair, and expose services according to their capabilities.

Sentinel covers what platforms provide: accounts, saves, achievements, presence, friends and invites. It is not a networking or game-state transport.

| Page | Covers |
|---|---|
| This page | Deployment, distributions, capabilities, services, accounts, results. |
| [Transactional Saving](saving.md) | `ISaveService` and save transactions. |
| [Social and Invitations](multiplayer.md) | `IMultiplayerService`: friends, presence, invites, join requests. |
| [Modules](modules.md) | The shipped Local and Steam modules, writing a module, the build pipeline. |

## Deployment

Sentinel deploys while preloading, moving through:

`Uninitialized → ResolvingPlatform → ResolvingModule → InitializingPlatform → Ready`

or `Failed`.

```csharp
if (Sentinel.TryGetSingleton(out var sentinel))
{
    bool ready = sentinel.State is Sentinel.DeploymentState.Ready;
    SentinelPlatformMarker platform = sentinel.ActivePlatform;
    SentinelDistribution distribution = sentinel.ActiveDistribution;
    SentinelCapability capabilities = sentinel.Capabilities;
    string module = sentinel.ActiveModuleDisplayName;   // and ActiveModuleID
    SentinelResult result = sentinel.InitializationResult;
}
```

1. **Platform.** `SentinelRuntimePlatformResolver` maps Unity's runtime platform to a `SentinelPlatformMarker`. In Play Mode it uses the Editor's host platform (Windows, macOS or Linux).
2. **Distribution.** `SentinelConfig.GetDistribution(platform)` picks the distribution, unless [overridden](#distribution-override).
3. **Module.** `SentinelModuleRegistry` needs a module for that exact platform and distribution. If none is installed, deployment fails with `SentinelError.Unsupported`.
4. **Platform initialisation.** The module creates its `ISentinelPlatform`, and Sentinel awaits its `InitializeAsync()`.

**A failed platform does not stop Threadlink.** Only a missing `SentinelConfig` fails Sentinel's preloader. Any platform failure (Steam not running, a missing module, a native error) leaves Sentinel `Failed`, logs `InitializationResult` as an error during `Boot`, and lets deployment continue without platform services. `TryGetService` and `HasCapability` then return `false`. The project decides what a failed platform means (degraded play, an error screen, quitting) by checking `State` after deployment. The [Runtime Monitor](../editor-tooling.md#runtime-monitor) shows the state, result and capabilities live.

**Retrying, losing the platform, and state changes.**

- `RetryAsync()` deploys the platform again while Sentinel is `Failed` (for example, after the player starts Steam) and returns whether it is now `Ready`. It returns `true` at once if already `Ready`, and `false` while another retry is running.
- A module reports a platform that goes away after initialising by calling the protected `SentinelPlatform.ReportLost(result)`, as the Steam module does on `SteamShutdown_t`.
  - Sentinel then moves to `Failed` with `SentinelError.PlatformLost`.
  - It publishes the state change at once, so code using the platform can release it synchronously.
  - It discards the platform one frame later, after the reporting callback has returned.
- Whenever deployment settles (`Ready` or `Failed`), Sentinel publishes the native Iris event `OnSentinelStateChanged` (`Action<Sentinel>`): after the first deployment, after a retry, and on a loss.
- Sentinel deploys before any subsystem initialises, so a subscriber reads `State` once when subscribing, then follows the event.
- In the Editor and development builds, `SimulatePlatformLoss()` runs the loss path, for tests. It does nothing in release builds.

## Distributions

`SentinelDistributionPolicy` defines which distributions each platform allows. If only one is allowed, it is used and the Sentinel Config is ignored. If several are, the config's choice is used; an invalid choice resolves to `None`, which fails deployment.

| Platform | Allowed distributions |
|---|---|
| Windows | Local, Steam, MicrosoftStore, Epic, GOG |
| MacOS | Local, Steam, MacAppStore |
| Linux | Local, Steam, GOG |
| Android | Local, GooglePlay, AmazonAppstore |
| IOS, TvOS, VisionOS | Local, AppleAppStore |
| Xbox, PlayStation, Nintendo | Native |
| WindowsStore | MicrosoftStore |
| WebGL | Local |

The policy lists what a project may target, but a target only works with a matching runtime module installed (see [Modules](modules.md)).

### Distribution override

`SentinelDistributionOverride` replaces the configured distribution at runtime. Use it for instances that must not use the configured store, such as several development instances on one machine sharing one Steam account. Sources, highest priority first:

| Source | Availability |
|---|---|
| `SentinelDistributionOverride.Requested`, assigned by project code before deployment (for example at `BeforeSceneLoad`) | Always |
| Command-line argument `-threadlink-sentinel-distribution <Distribution>` (`COMMAND_LINE_ARGUMENT`) | Editor and development builds |
| Environment variable `THREADLINK_SENTINEL_DISTRIBUTION` (`ENVIRONMENT_VARIABLE`) | Editor and development builds |

Values are `SentinelDistribution` names, in any case (for example `Local`). The active platform must allow the override; a disallowed or unknown value is ignored with a warning. An applied override is logged. `Requested` resets on entering Play Mode.

## Capabilities

`SentinelCapability` is a flags enumeration. A module advertises only what it implements; `sentinel.HasCapability(flag)` is only `true` while Sentinel is `Ready`.

| Group | Flags |
|---|---|
| Accounts | `Accounts`, `AccountPicker`, `MultipleLocalAccounts` |
| Saving | `LocalSaves`, `CloudSaves`, `SaveTransactions` |
| Achievements | `Achievements`, `AchievementProgress` |
| Input | `InputOwnership` |
| Online | `Stats`, `Leaderboards`, `Presence`, `Friends`, `Entitlements`, `Commerce`, `Invites` |

`capabilities.Has(flag)` tests a set of flags directly.

## Services

Get platform services from `Sentinel`, and account services from the account:

```csharp
if (Sentinel.TryGetSingleton(out var sentinel)
    && sentinel.TryGetService<IAccountService>(out var accounts))
{
    var accountResult = await accounts.GetPrimaryAccountAsync();

    if (accountResult.Succeeded
        && accountResult.Value.TryGetService<ISaveService>(out var saves))
    {
        var readResult = await saves.ReadAsync(saveID, fileID);
    }
}
```

| Service | Scope | Contract |
|---|---|---|
| `IAccountService` | Platform | Account list, primary account, refresh and picker operations, account lifecycle events. |
| `ISaveService` | Account | [Transactional Saving](saving.md) |
| `IAchievementService` | Account | `GetStateAsync(id)`, `SetProgressAsync(id, progress)`, `UnlockAsync(id)`. Progress is 0–1; `SentinelAchievementState.Unlocked` is progress at 1. Achievement ID 0 is reserved. |
| `IMultiplayerService` | Account | [Social and Invitations](multiplayer.md) |

## Accounts

`IAccountService` exposes `Accounts`, `PrimaryAccount`, `RefreshAsync()`, `GetPrimaryAccountAsync(allowUI)` and `PickAccountAsync()`, and the events `AccountAdded`, `AccountRemoved`, `PrimaryAccountChanged` and `AccountStateChanged`. `AccountStateChanged` reports an account's `State` changing, for example between `SignedIn` and `Suspended` as a platform loses and regains its servers.

`ISentinelAccount` has `ID`, `DisplayName`, `State` (`Unknown`, `SignedOut`, `SigningIn`, `SignedIn`, `Suspended`) and `IsPrimary`. It is also a service provider, for account services such as saves, achievements and social features.

## Results

Services report ordinary failures through `SentinelResult` / `SentinelResult<T>`, not exceptions. A result has `Succeeded`, `Error`, `Message` and an optional `NativeCode`. The typed result adds `Value`, and `Untyped()` drops it.

`SentinelError` values are `None`, `NotInitialized`, `Unsupported`, `InvalidArgument`, `NotFound`, `Busy`, `Cancelled`, `PermissionDenied`, `NetworkUnavailable`, `QuotaExceeded`, `Conflict`, `CorruptData`, `AccountUnavailable`, `NativeFailure`, `Unknown` and `PlatformLost`. A failure created with `None` is reported as `Unknown`.
