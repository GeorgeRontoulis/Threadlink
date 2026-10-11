# Modules

A **module** implements Sentinel for exact platform/distribution pairs. It has a runtime half, which registers a platform factory with `SentinelModuleRegistry`, and an editor half, an `ISentinelBuildModule` that configures and validates builds for the same pairs.

## Shipped modules

| Module | ID | Keys | Capabilities |
|---|---|---|---|
| **Threadlink Local** | `threadlink.local` | Windows, macOS, Linux / `Local` | `Accounts`, `LocalSaves`, `SaveTransactions`, `Achievements`, `AchievementProgress` |
| **Steam** | `threadlink.steam` | Windows, macOS, Linux / `Steam` | `Accounts`, `LocalSaves`, `SaveTransactions`, `Achievements`, `AchievementProgress`, `Presence`, `Friends`, `Invites`; `CloudSaves` when Steam Cloud is enabled for the app and the account |

### Local

The Local module needs no platform SDK or storefront configuration.

- **One implicit account**, `local-user` ("Local User"), always `SignedIn` and primary. `PickAccountAsync` fails with `Unsupported`, and the account events never fire.
- **Saves** are generation-based files under `Application.persistentDataPath/Sentinel` (see [Transactional Saving](saving.md#storage-by-module)).
- **Achievements** are held in memory for the session and are not persisted. Progress only increases.

### Steam

The Steam module requires Steamworks.NET (`com.rlabrecque.steamworks.net`, validated against 2025.165.0) and the `STEAMWORKS_NET` scripting define; its assembly compiles only for standalone platforms and the Editor.

- **Sole lifecycle owner.** The module initialises and shuts down the Steam API itself, and pumps Steam callbacks every frame from a hidden persistent object. If Steamworks.NET was initialised by other code first, deployment fails with `Conflict`.
- **The account follows the client's connection to Steam's servers.** It is `SignedIn` when connected and `Suspended` in offline mode or without a connection, and `AccountStateChanged` reports each change.
- **The Steam client shutting down** reports the platform lost.
- **App ID:** the project-root `steam_appid.txt` is the one App ID source.
  - At startup the expected App ID is resolved from the `SteamAppId` environment variable (which Steam sets when it launches the game), then `steam_appid.txt` in the working directory, then beside the executable. If one resolves and Steam initialises under a different App ID, deployment fails.
  - Development builds get a copy of `steam_appid.txt` beside the executable.
  - Release builds ship without it, as Valve requires, and get the App ID in `<Data>/threadlink_steam_appid.txt` instead: a file the Steam client never reads.
  - With it, a release build started outside Steam calls `SteamAPI.RestartAppIfNecessary` and Steam relaunches it.
- **Saves** use Steam Remote Storage (see [Transactional Saving](saving.md#storage-by-module)).
- **Achievements.** A Threadlink achievement ID maps to the Steam API name `TL_ACH_<ID as 8 hex digits>` unless registered otherwise. Partial progress is persisted only through a registered progress stat; without one, `SetProgressAsync` below 1 fails with `Unsupported`. Progress only increases.

`SteamSentinelSettings` (in `Threadlink.Sentinel.Steam.Shared`) configures the module in code, before Sentinel deploys:

| Member | Default | Semantics |
|---|---|---|
| `RestartAppIfNecessary` | `true` | Whether a player build started outside Steam asks Steam to relaunch it. |
| `RetainedSaveGenerations` | 3 | Published save generations kept per save; at least 1. |
| `RegisterAchievement(id, apiName, progressStatAPIName, progressMaximum)` | — | Maps an achievement ID to an explicit Steam API name and, optionally, an integer progress stat with its maximum. |
| `RemoveAchievement(id)`, `ClearAchievementOverrides()` | — | Remove explicit mappings. |
| `ResolveAchievement(id)`, `BuildDefaultAchievementAPIName(id)` | — | The mapping in effect, and the default name. |

## Writing a runtime module

1. Derive a platform from `SentinelPlatform` (or implement `ISentinelPlatform`) for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair. Implement `Capabilities` and `InitializeAsync()`, which returns a `SentinelResult`.
2. In `InitializeAsync`, register platform-level services with the protected `RegisterService<T>`, and account-scoped services on `SentinelAccount` implementations the same way. `ReplaceService<T>` swaps a service, discarding the previous one; `UnregisterService<T>` removes one.
3. Advertise only capabilities actually implemented.
4. If the platform can go away after initialising, call `ReportLost(result)`; it is safe to call from an SDK callback.
5. Register a `SentinelModuleDescriptor` (platform, distribution, module ID, display name, platform factory) through `SentinelModuleRegistry.Register` from an `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]` hook. The registry is cleared at `SubsystemRegistration`, and Sentinel resolves it during preloading.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
private static void Register()
{
    SentinelModuleRegistry.Register(new SentinelModuleDescriptor
    (
        SentinelPlatformMarker.Windows,
        SentinelDistribution.Epic,
        "studio.epic",
        "Epic Games",
        static () => new EpicSentinelPlatform()
    ));
}
```

`Register` returns a failed `SentinelResult` for an unknown platform or `None` distribution, a pair the distribution policy does not allow, an empty module ID or display name, or a key another module has already registered (`Conflict`).

## The build pipeline

Before every build, Sentinel resolves the target the same way the runtime does: Unity build target to `SentinelPlatformMarker`, `SentinelConfig` to `SentinelDistribution`, and the exact key to exactly one installed `ISentinelBuildModule`. The build fails if:

- the build target is not supported;
- there is not exactly one `SentinelConfig` asset, or the Native Config's `SentinelConfig` resource does not reference it;
- the config holds no valid distribution for the platform;
- no build module, or more than one, implements the key;
- the module throws, or reports an error diagnostic.

An `ISentinelBuildModule` exposes `ModuleID`, `DisplayName`, `Implements(in SentinelModuleKey)`, `ConfigureBuild` and `ValidateBuild`. The last two receive a `SentinelBuildContext` (the `BuildReport`, the key, the output path, and `GetSubtarget<T>()`) and append `SentinelBuildDiagnostic.Info`, `Warning` or `Error` entries, which are logged. Build modules are found through the `TypeCache` and must expose a public parameterless constructor.

The Steam build module checks that Steamworks.NET is installed and that `steam_appid.txt` holds a valid App ID, and warns if it is Valve's test App ID 480.

The Sentinel Config's Inspector shows the current build target, its Sentinel platform, and the distribution choice the target allows.
