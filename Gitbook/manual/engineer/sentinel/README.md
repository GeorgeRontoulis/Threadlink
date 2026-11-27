# Sentinel: Platform Services

Sentinel (`com.threadforge.threadlink.sentinel`) owns accounts, platform capabilities and saves for the session. A local profile is selected before the first scene; a platform package such as [Steam](modules.md) adds its provider and capabilities route to it. Saves are described in [Transactional Saving](saving.md).

```csharp
var account = Sentinel.Account;                                   // provider-qualified identity, or null
Sentinel.SelectProfile("Player 2");                                // local profiles; saves follow the profile

var unlocked = await Sentinel.UnlockAchievementAsync(Achievements.FirstChest);
if (!unlocked.Succeeded)
    Debug.Log(unlocked.Error);                                     // Unsupported, Unavailable, ...

if (Sentinel.TryGet<IStatistics>(out var statistics))
    await statistics.WriteAsync(Statistics.LanternsFound, lanterns, Sentinel.Accounts.Capture().Token);
```

| API | Behaviour |
| --- | --- |
| `Sentinel.Account`, `Sentinel.Accounts` | The signed-in account and the `AccountSession`. `Accounts.Capture()` returns a lease whose token is cancelled when the player switches account. |
| `Sentinel.SelectProfile(name)` | Selects a local profile. The default profile is set in **Project Settings ▸ Threadlink ▸ Sentinel**. |
| `Sentinel.Status(capability)` | Whether a capability is supported and ready. |
| `Sentinel.Resolve<T>()`, `TryGet<T>(out T)` | A capability contract (`IAchievements`, `IStatistics`, `ILeaderboards`, `IPresence`, `IFriends`, `IInvites`, `IEntitlements`, `ICommerce`, `ICloudStorage`, `IJoinIntents`, `IAccounts`) or the reason it is unavailable. Never a silent no-op. |
| `Sentinel.UnlockAchievementAsync`, `SetAchievementProgressAsync` | Achievement shortcuts by `AchievementId`, returning `PlatformResult<Unit>`. |
| `Sentinel.AddProvider(provider, capabilities)` | Adds a platform provider and routes capabilities to it. Platform packages call this from their own subsystem; Sentinel disposes providers at shutdown. |

## Platform IDs

Sentinel does not know where platform data comes from. Each platform authors its own achievements, statistics, leaderboards and products in its own settings (for Steam, **Project Settings ▸ Threadlink ▸ Steam**), and game code refers to them through platform-agnostic IDs:

| ID | Generated class | Used by |
| --- | --- | --- |
| `AchievementId` | `Achievements` | `IAchievements`, `Sentinel.UnlockAchievementAsync`, `SetAchievementProgressAsync` |
| `StatisticId` | `Statistics` | `IStatistics` |
| `LeaderboardId` | `Leaderboards` | `ILeaderboards` |
| `ProductId` | `Products` | `IEntitlements`, `ICommerce`, `CatalogItem.Product`, `PurchaseReceipt.Product` |

Sentinel passes an ID to the selected provider unchanged, and the provider resolves it against its own authoring, much as a boxed value is unboxed to its concrete type. An ID a platform does not author fails there with `NotFound`, so the same game code runs on every platform. Products the provider reports (catalog items, receipts) come back translated to `ProductId`.

The generated classes are the union of what installed platforms author (see [Generated IDs](../editor-tooling.md#generated-ids)): an entry named `first-chest` becomes `Achievements.FirstChest`, and its documentation lists each platform's native name. Give an achievement the same name on every platform; the **Platform services** validator warns when an ID is authored on some installed platforms but not others. `new AchievementId(name)` and its siblings cover names known only at runtime.

A platform package reports its authoring to the generator by implementing `IPlatformAuthoring` in an Editor assembly.

## Accounts

`AccountIdentity` pairs a provider and an account string; equal strings from different providers are different accounts. Changing account invalidates earlier leases; adapters observe the cancellation and recheck before applying results. Operations that may already have committed natively are reconciled, never blindly retried.

## Events

Sentinel announces on Iris (see [Framework Events](../events.md)):

| Event | When |
| --- | --- |
| `PlatformReady` | A platform provider is added, such as Steam once it has started; it carries the routed capabilities. |
| `AccountChanged` | The signed-in account changes, and once for the startup account during the initialization phase. |
| `AchievementUnlocked` | An achievement is unlocked through Sentinel: show an in-game toast. |
| `SaveWritten`, `SaveFailed`, `SaveRecovered` | A save is written; a save cannot be written, read, deleted or restored (an empty slot and a cancelled operation are not failures); a slot is restored to its previous version. |

## Providers

A provider implements `ISentinelProvider` and the capability contracts it supports. Its `Status(capability)` reports Ready, Offline, AccountRequired, Initializing, TemporarilyUnavailable or Failed separately from support. Provider contracts and the Sentinel API return `UniTask`.

`JoinIntentBuffer` holds launch and invite join requests captured before the game is ready; the game takes them when it chooses, and `JoinIntentReceived` announces each one on Iris. Lobbies, matchmaking and transport are separate networking integrations. See [Social and Invitations](multiplayer.md).
