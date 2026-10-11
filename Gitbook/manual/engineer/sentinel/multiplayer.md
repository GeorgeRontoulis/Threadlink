# Social and Invitations

The account-scoped `IMultiplayerService` exposes platform-native social and session affordances only. Networking owns the creation, serialisation and interpretation of join payloads; Sentinel carries them as opaque strings. The contract lives in the `Threadlink.Sentinel.Multiplayer.Contracts` assembly, which project code references explicitly.

| Member | Semantics |
|---|---|
| `GetFriendsAsync()` | The friend list as `SentinelFriend` values: `ID`, `DisplayName`, `Presence`, `IsPlayingThisGame`, `IsJoinable`, and the friend's public `JoinPayload`. |
| `SetSessionPresenceAsync(SentinelSessionPresence)` | Publishes the local player's session: a public `JoinPayload`, plus optional `Status`, `GroupID` and `GroupSize`. |
| `ClearSessionPresenceAsync()` | Clears the published session. |
| `InviteFriendAsync(friendID)` | Invites a friend to the published session. |
| `InviteFriendAsync(friendID, joinPayload)` | Sends a private opaque payload to one friend without publishing it. |
| `JoinRequested`, `TryDequeueJoinRequest(out SentinelJoinRequest)` | Join requests: `SourceUserID`, `SourceDisplayName` and the `JoinPayload` to join with. |

`SentinelFriendPresence` values are `Unknown`, `Offline`, `Online`, `Busy`, `Away`, `Snooze`, `LookingToTrade` and `LookingToPlay`.

## Private invitations

`InviteFriendAsync(friendID)` uses the published session presence; `InviteFriendAsync(friendID, joinPayload)` sends a private payload instead. The caller owns token creation, authorisation, expiration and revocation. Both use the same `SentinelJoinRequest` delivery, including cold launches.

A `SentinelSessionPresence` with a null or empty `JoinPayload` retains status and group information but clears the joinable connect key, making the session non-joinable.

## Join requests

A join request goes to `JoinRequested` if it has a subscriber, and is queued otherwise, so a request that arrives before the game is ready isn't lost. Once ready, the game drains the queue with `TryDequeueJoinRequest`. A game launched by accepting an invitation (a cold launch) receives the request the same way; its source user is unknown.

## Steam

Steam remains the only current provider. The join payload is published as the `connect` rich-presence value, encoded as `+threadlink_join <payload>` with the payload in URL-safe Base64, and must fit within Steam's rich-presence value limit. Cold-launch requests are recovered from Steam's launch command line. `status`, `steam_player_group` and `steam_player_group_size` carry the optional presence fields. Sentinel continues to own the Steam lifecycle and its callbacks.
