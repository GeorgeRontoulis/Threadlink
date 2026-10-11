# Iris: Event Dispatch

`Iris` is a static class. Its `object[] EventRegistry` has a slot for every value up to the highest `ThreadlinkIDs.Iris.Events` value (values can have gaps; see [Domain kinds](code-generation.md#domain-kinds)). Each slot gets a `DelegateList<T>` on its first subscription.

## Dispatch signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        (single listener)
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> (single listener)
```

Subscribe and unsubscribe with an explicit delegate type:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
```

Subscribing a delegate that is already subscribed has no effect.

Diagnostics: `TryGetListenerCount(eventID, out int)`, `ContainsListener<T>(eventID, listener)`, `Clear(eventID)`. The [Runtime Monitor](editor-tooling.md#runtime-monitor) shows listener counts per event.

## Constraints

- **The delegate type is the contract, and the compiler doesn't check it across subscribers.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type-mismatch error and drops the subscription. Publishing with the wrong type does nothing. Keep signatures consistent; the native ones are in [Native Iris Events](../reference/iris-events.md).
- **A `Func` event allows one listener**, and throws `InvalidOperationException` beyond that: it is a single provider, not a broadcast. With no listener, publishing returns `default`.
- **`Action` listeners run newest first, over the listeners present when publishing began.** A listener removed or cleared during dispatch is skipped at once; one added waits for the next publication (a nested publication counts as new). Slots are compacted after the outermost dispatch, even if a listener throws. Dispatch allocates no snapshot.
- **Unsubscribe in `Discard()`.** A delegate left subscribed keeps a destroyed object alive and runs on stale state.
- Publishing an event with no listeners does nothing.
- Every listener is cleared when Play Mode exits.

## Update events

With the `Native` update loop, `ThreadlinkPlayerLoop` inserts three systems into Unity's **current** `PlayerLoop` that publish the Iris update events. No `GameObject` or `MonoBehaviour` is involved.

| Iris event | Native PlayerLoop position |
|---|---|
| `OnUpdate` | Immediately **before** `Update.ScriptRunBehaviourUpdate` |
| `OnFixedUpdate` | Immediately **after** `FixedUpdate.ScriptRunBehaviourFixedUpdate` |
| `OnLateUpdate` | Immediately **after** `PreLateUpdate.ScriptRunBehaviourLateUpdate` |

Installing first removes any old Threadlink systems, then fails at once if a Unity anchor is missing. The loop is uninstalled when the core is discarded or the application quits, and reset on entering Play Mode in case Domain Reload is disabled. `Custom` mode installs nothing; the project publishes the three update events itself (see [Update loop modes](configuration.md#update-loop-modes)).

## Declaring events

Declare the identifier in `Threadlink User/Native Domain Injectors/Iris.Events.User.txt` and save.

Iris events are the only **ordinal** domain: values are array indices, assigned in source order (native entries first, then the injector's) and kept stable by the domain's manifest. They are never serialised, so there are no tombstones: removing an event breaks compilation wherever it is used.
