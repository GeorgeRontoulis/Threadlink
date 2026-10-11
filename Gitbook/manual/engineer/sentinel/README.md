# Sentinel: Platform Services

Sentinel is Threadlink's cross-platform platform-service abstraction. Unity determines the hardware/platform; `SentinelConfig` only selects a **distribution** where that platform is ambiguous (for example Windows: Local, Steam, Microsoft Store, Epic or GOG). Runtime modules register exact `SentinelPlatformMarker` / `SentinelDistribution` pairs and expose a capability-driven service graph.

Sentinel provides platform-native affordances: accounts, saves, achievements, presence, friends and invites. It is not a transport or game-state networking layer.

| Page | Covers |
|---|---|
| This page | Deployment, distributions, capabilities, services, accounts, results. |
| [Transactional Saving](saving.md) | `ISaveService` and save transactions. |
| [Social and Invitations](multiplayer.md) | `IMultiplayerService`: friends, presence, invites, join requests. |
| [Modules](modules.md) | The shipped Local and Steam modules, writing a module, the build pipeline. |

## Deployment

Sentinel deploys during the preloading phase and moves through:

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

1. **Platform.** `SentinelRuntimePlatformResolver` maps Unity's runtime platform to a `SentinelPlatformMarker`. In Play Mode, the Editor host's platform is used (Windows, macOS or Linux).
2. **Distribution.** `SentinelConfig.GetDistribution(platform)` picks the distribution, after any [override](#distribution-override).
3. **Module.** `SentinelModuleRegistry` requires an exact platform/distribution match. If no installed module implements the resolved key, deployment fails with `SentinelError.Unsupported`.
4. **Platform initialisation.** The module creates its `ISentinelPlatform`, and Sentinel awaits its `InitializeAsync()`.

**A failed platform does not abort Threadlink.** Only a missing `SentinelConfig` fails Sentinel's preloader. Every platform-level failure leaves Sentinel in `Failed`, logs `InitializationResult` as an error during `Boot`, and lets deployment continue with no platform services: for example, Steam not running, a missing module, or a native initialisation error. `TryGetService` and `HasCapability` return `false` in that state. Whether a failed platform is acceptable (degraded play, an error screen, quitting) is the project's decision, made by checking `State` once deployment completes.

**Retrying, losing the platform, and state changes.**

- `RetryAsync()` deploys the platform again while Sentinel is `Failed`, for example after the player has started Steam, and returns whether it is `Ready`. It returns `true` at once if Sentinel is already `Ready`, and `false` while another retry is running.
- A platform that goes away after initialising reports it: a module calls the protected `SentinelPlatform.ReportLost(result)`, as the Steam module does on `SteamShutdown_t`.
  - Sentinel then moves to `Failed` with `SentinelError.PlatformLost`.
  - It publishes the state change at once, so code using the platform can release it synchronously.
  - It discards the platform one frame later, after the reporting callback has returned.
- Every time a deployment settles (`Ready` or `Failed`), Sentinel publishes the native Iris event `OnSentinelStateChanged` (`Action<Sentinel>`): after the first deployment, after a retry, and on a loss.
- Sentinel deploys before any subsystem initialises, so a subscriber reads `State` once when it subscribes, then follows the event.
- In the Editor and development builds, `SimulatePlatformLoss()` runs the loss path, for tests. It is not compiled into release builds.

## Distributions

`SentinelDistributionPolicy` defines which distributions each platform allows. Where only one is allowed, the platform resolves automatically and the Sentinel Config is not consulted; where several are, the config's choice is used, and an invalid choice resolves to `None`, which fails deployment.

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

The policy declares the ecosystems a project may target, but a target is usable only when a matching runtime module is installed and registered (see [Modules](modules.md)).

### Distribution override

`SentinelDistributionOverride` replaces the configured distribution at runtime. It is intended for instances that must not deploy the configured ecosystem, such as several development instances on one machine sharing a single Steam account. Sources, in order of precedence:

| Source | Availability |
|---|---|
| `SentinelDistributionOverride.Requested`, assigned by project code before deployment (for example at `BeforeSceneLoad`) | Always |
| Command-line argument `-threadlink-sentinel-distribution <Distribution>` (`COMMAND_LINE_ARGUMENT`) | Editor and development builds |
| Environment variable `THREADLINK_SENTINEL_DISTRIBUTION` (`ENVIRONMENT_VARIABLE`) | Editor and development builds |

Values are case-insensitive `SentinelDistribution` names (for example `Local`). An override must still be allowed for the active platform by `SentinelDistributionPolicy`; a disallowed or unknown value is ignored with a warning. An applied override is logged. `Requested` is reset on entering Play Mode.

## Capabilities

`SentinelCapability` is a flags enumeration. A platform module advertises only what it actually implements; `sentinel.HasCapability(flag)` is `true` only while Sentinel is `Ready`.

| Group | Flags |
|---|---|
| Accounts | `Accounts`, `AccountPicker`, `MultipleLocalAccounts` |
| Saving | `LocalSaves`, `CloudSaves`, `SaveTransactions` |
| Achievements | `Achievements`, `AchievementProgress` |
| Input | `InputOwnership` |
| Online | `Stats`, `Leaderboards`, `Presence`, `Friends`, `Entitlements`, `Commerce`, `Invites` |

`capabilities.Has(flag)` tests a set of flags directly.

## Services

Top-level services are obtained from `Sentinel`; account-scoped services are obtained from the resolved account:

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

`ISentinelAccount` carries `ID`, `DisplayName`, `State` (`Unknown`, `SignedOut`, `SigningIn`, `SignedIn`, `Suspended`) and `IsPrimary`, and is itself a service provider, allowing platform-specific account-scoped services such as saves, achievements and multiplayer/social affordances.

## Results

`SentinelResult` / `SentinelResult<T>` carry `Succeeded`, `Error`, `Message`, and an optional `NativeCode`, rather than relying on exceptions for normal service failures. The typed result adds `Value`, and `Untyped()` drops it.

`SentinelError` enumerates `None`, `NotInitialized`, `Unsupported`, `InvalidArgument`, `NotFound`, `Busy`, `Cancelled`, `PermissionDenied`, `NetworkUnavailable`, `QuotaExceeded`, `Conflict`, `CorruptData`, `AccountUnavailable`, `NativeFailure`, `Unknown` and `PlatformLost`. A failure created with `None` is reported as `Unknown`.
