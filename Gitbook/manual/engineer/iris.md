# Iris: Event Dispatch

`Iris` is a static class backed by `object[] EventRegistry`, dimensioned at type load to span the highest value of `ThreadlinkIDs.Iris.Events` (values can have gaps, see [Domain kinds](code-generation.md#domain-kinds)). Each slot holds a `DelegateList<T>` allocated on first subscription.

## Dispatch signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        (single listener)
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> (single listener)
```

Subscription and unsubscription state the delegate type explicitly:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
```

Subscribing a delegate that is already subscribed has no effect.

Diagnostic members: `TryGetListenerCount(eventID, out int)`, `ContainsListener<T>(eventID, listener)`, `Clear(eventID)`.

## Constraints

- **The delegate type constitutes the contract and is not enforced across subscribers at compile time.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type-mismatch error and discards the subscription. Publishing under a mismatched type is a no-op. Signature agreement remains a project discipline; the native signatures are listed in [Native Iris Events](../reference/iris-events.md).
- **`Func` events throw `InvalidOperationException` beyond a single listener.** They model a single provider rather than a broadcast. With no listener, a `Func` publication returns `default`.
- **Action dispatch iterates in reverse registration order with a fixed publication bound.** Removal or clearing immediately skips affected listeners; additions wait until the next publication (a nested publication counts as new). Slots are compacted after the outermost dispatch, including when a listener throws. Dispatch allocates no snapshot array.
- **Unsubscription belongs in `Discard()`.** A retained delegate keeps a destroyed object reachable and dispatches to stale state.
- Dispatch to an event without listeners is a no-op.
- Every listener is cleared when Play Mode exits.

## Update events

When the update loop is configured as `Native`, `ThreadlinkPlayerLoop` injects three marker systems into Unity's **current** `PlayerLoop` and publishes the Iris events directly; no `GameObject` or `MonoBehaviour` exists.

| Iris event | Native PlayerLoop position |
|---|---|
| `OnUpdate` | Immediately **before** `Update.ScriptRunBehaviourUpdate` |
| `OnFixedUpdate` | Immediately **after** `FixedUpdate.ScriptRunBehaviourFixedUpdate` |
| `OnLateUpdate` | Immediately **after** `PreLateUpdate.ScriptRunBehaviourLateUpdate` |

Installation removes stale Threadlink markers first, then fails fast if any expected Unity anchor cannot be found. The loop is uninstalled when the core is discarded or the application quits, and an entering-play-mode reset protects against stale PlayerLoop state when Domain Reload is disabled. `Custom` mode installs nothing; the project is responsible for publishing the three Iris update events (see [Update loop modes](configuration.md#update-loop-modes)).

## Declaring events

Declare the identifier in `Threadlink User/Native Domain Injectors/Iris.Events.User.txt` and save.

Iris events are the framework's sole **ordinal** domain: values are array indices allocated in source order, native entries preceding injector entries, and kept by the domain's manifest. Values are never serialised, so removal produces compilation failure at every subscription site. Tombstoning is disabled for this domain accordingly.
