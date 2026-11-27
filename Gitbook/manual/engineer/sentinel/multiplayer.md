# Social and Invitations

Sentinel carries platform-native social affordances only. Networking owns lobbies, matchmaking, transport and the meaning of join data; Sentinel passes join data as opaque strings.

| Contract | Member | Meaning |
|---|---|---|
| `IPresence` | `SetAsync(state, token)` | Publishes the local player's presence or rich-presence state. |
| `IFriends` | `ListAsync(token)` | The friend list as provider-qualified `AccountIdentity` values. |
| `IInvites` | `SendAsync(recipient, joinData, token)` | Invites one friend with opaque join data. |
| `IJoinIntents` | `TryTake(out JoinIntent)`, `Pending` | Takes the oldest captured request to join; counts waiting requests. |

Presence, friends and invites return a `PlatformResult<T>`. Resolve the contract through `Sentinel.Resolve<T>()` or `Sentinel.TryGet<T>()`: an unsupported capability reports Unsupported, and a supported one that is not ready reports Unavailable.

## Join intents

A `JoinIntent` carries the provider, an identity and the opaque payload. Providers capture intents as they arrive; the Steam provider also captures the connect data a game was launched with. With the local provider, the game captures intents explicitly. `JoinIntentBuffer` holds them until the game is ready: `Capture` adds one, `TryTake` removes the oldest, `Pending` and `Rejected` report the queue, and the capacity (32 by default) bounds it. Recent duplicates are suppressed. The game decides when to consume intents and whether to join.

The caller owns the join data: create, authorize, expire and revoke it in the networking layer. Treat received payloads as untrusted input.

## Steam

`SteamProvider` implements presence, friends, invites and join intents. Launch connect data and accepted invites are captured as join intents; `SendAsync` invites a Steam friend with a connect string. See [Providers](modules.md#steam-provider).
