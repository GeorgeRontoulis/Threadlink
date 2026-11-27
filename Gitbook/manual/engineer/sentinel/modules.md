# Modules

## Provider requirements

Native integrations implement `ISentinelProvider` and the selected typed capability interfaces. Keep SDK imports in optional adapters. A build should validate its actual selected capabilities through `ProviderRequirements.ValidateSelection`, then apply adapter-specific SDK/configuration validation. Compiling a contract or finding SDK files does not verify a native capability.

| Target | Implementation boundary | Required verification |
| --- | --- | --- |
| Windows Local | SDK-free profiles, optional local storage adapter | Target filesystem atomic replacement, account isolation and recovery |
| Linux / macOS Local | Same ordinary C# local adapter | Native target runtime/filesystem checks |
| Steam | `sentinel.steam` with a Unity Steamworks API or Steamworks.NET backend | Native libraries, a running signed-in client, configured App ID and per-capability Steamworks configuration tests |
| Android Local | Application-private local storage adapter | Android module/device and filesystem checks |
| Google Play | Separate optional adapter | Target SDK/device, application/account configuration and service-specific tests |
| Apple local / services | Local storage or separate service adapter | Apple toolchain/device, sandbox, identifiers, entitlements and selected services |
| Microsoft Store Windows | Separate optional adapter | Store/package identity, relevant SDK, account and service tests |
| Public PC GDK | Separate optional adapter | GDK/runtime, title/account/backend configuration and selected capability tests |
| GOG | Separate optional adapter | Authorized Galaxy SDK/account and game-service configuration |
| Xbox Console | Extension contracts and integration groundwork | Authorized console tooling/title/hardware plus native capability tests |
| Nintendo / Sony | Extension contracts/test doubles | Separately supplied authorized native implementation and device tests |

The public requirement catalog describes these integration boundaries; it does not mark unavailable integrations as installed providers. Test doubles exercise cancellation, conflicts, account isolation and transaction handling without claiming storefront/backend/device verification.

Steam's initialization requirements include native SDK integration, a usable client and a configured application/account. See the [Steamworks API overview](https://partner.steamgames.com/doc/sdk/api). PC GDK setup also requires the relevant development/runtime environment; see [Microsoft's development PC requirements](https://learn.microsoft.com/gaming/gdk/docs/gdk-dev/get-started/overviews/set-up-dev-pc). Console and service access must be verified independently of public documentation or SDK installation.

## Steam provider

`com.threadforge.threadlink.sentinel.steam` supplies `SteamProvider`, which implements accounts, achievements (with optional progress statistics), statistics, leaderboards, rich presence, friends, invites, DLC entitlements, join intents and Steam Cloud slots. It works with either Steam wrapper through `ISteamBackend`:

| Wrapper | Package | Adapter assembly | Backend |
| --- | --- | --- | --- |
| Unity Steamworks API | `com.unity.steamworks-api` | `Threadforge.Threadlink.Sentinel.Steam.Unity` | `UnitySteamworksBackend` |
| Steamworks.NET | `com.rlabrecque.steamworks.net` (2024.8.0 or later) | `Threadforge.Threadlink.Sentinel.Steam.SteamworksNet` | `SteamworksNetBackend` |

Each adapter compiles only when its wrapper is installed; the provider core has no Unity or SDK reference. Choose one wrapper per project, because each brings its own native `steam_api` libraries. With Unity's package, import the SDK libraries through **Project Settings ▸ Steamworks** and keep the SDK folder itself outside `Assets`. Steamworks.NET ships its libraries and needs `steam_appid.txt` during development.

Steam is configured in **Project Settings ▸ Threadlink ▸ Steam**: enable it, set the App ID, and author the game's Steam achievements, statistics, leaderboards and DLC. Each entry pairs the name game code uses (which generates `Achievements.Name`, `Statistics.Name`, `Leaderboards.Name` or `Products.Name`) with its Steamworks name; an empty Steamworks name uses the entry's name. It is off by default, so tests and the Editor never touch Steam unless a project enables it. When enabled, the `SteamPlatform` subsystem starts Steam with the session through the installed wrapper and adds the provider to Sentinel, so the code below talks to Steam:

```csharp
await Sentinel.UnlockAchievementAsync(Achievements.FirstChest);
if (Sentinel.TryGet<IStatistics>(out var statistics))
    await statistics.WriteAsync(Statistics.LanternsFound, lanterns, Sentinel.Accounts.Capture().Token);
var provider = SteamPlatform.Provider;        // null while Steam is disabled or did not start
```

`SteamPlatform` owns the Steam API lifecycle: nothing initializes on installation, Steam starts once with the session, in the initialization phase, and shuts down with it. When it has started, Sentinel announces `PlatformReady` and `AccountChanged` on Iris, so subsystems that subscribed while booting react whichever subsystem initializes first. A start that reports `ClientNotRunning`, `RestartRequired`, `AppIdMismatch`, `AlreadyInitialized` or `Failed` is logged and leaves Sentinel on the local provider; a `RestartRequired` player should quit. On success the signed-in user becomes the `steam` account.

`Status` keeps supported and available separate. Before `Start` capabilities are `Initializing`; without a client they are `TemporarilyUnavailable`; offline users are `Offline`; another selected account means `AccountRequired`; Steam Cloud is unavailable when disabled for the account or app. Commerce is unsupported, because Steam microtransactions require a trusted server.

`SteamProvider` resolves each platform-agnostic ID against this authoring; an ID not authored for Steam reports `NotFound`. Partial achievement progress requires a progress statistic and never moves backwards. Stores wait for Steam's `UserStatsStored` result. A store cancelled after it was requested reports `Cancelled` with `MayHaveCommitted`, because Steam may still persist it. EResult codes map to Sentinel errors, and nothing retries automatically.

Launch connect data and accepted invites are captured in `JoinIntents` for the game to consume when ready. `SendAsync` invites a Steam friend with a connect string. `SteamCloudStorage` stores each slot as one versioned file (`{CloudFilePrefix}{slot}.tlc`) with compare-and-publish. Steam Cloud keeps one copy, so `ExposesDivergentVersions` is false and stale publications fail with `Conflict`. Use it with `CloudSynchronization`; it accepts only the signed-in Steam account's addresses.

Verification uses scripted backends for every capability contract, and clean Mono and IL2CPP player builds for each wrapper. Native Steam behavior needs a running signed-in client, a configured App ID and the matching Steamworks achievements, statistics, leaderboards, DLC and Cloud settings. Verify each capability your game uses against Steam.
