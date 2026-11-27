# Iris: Event Dispatch

Iris is the session's event system, in the core package (`com.threadforge.threadlink`). Use it from the main thread; workers queue events with `TryPost`.

Iris boots and initializes before every other subsystem, and it is the one subsystem others may use while they boot. Subscribe in `OnSubsystemBoot` and you hear everything other subsystems announce while they initialize, whatever their order. The framework announces its own state changes on Iris: see [Framework Events](events.md).

## Synchronous events

```csharp
public readonly struct CoinsChanged
{
    public readonly int Total;
    public CoinsChanged(int total) => Total = total;
}

public sealed class CoinLabel : MonoBehaviour
{
    private void Start() => Iris.Subscribe<CoinsChanged>(this, change => text.text = change.Total.ToString());
}

Iris.Publish(new CoinsChanged(12));
```

| API | Behavior |
| --- | --- |
| `Iris.Subscribe<T>(callback)` | Returns a `SubscriptionToken`; dispose it to stop delivery. Copies and repeated disposal never remove another listener. |
| `Iris.Subscribe<T>(owner, callback)` | Ends when the `MonoBehaviour` owner is destroyed. |
| `Iris.Publish<T>(value)` | Delivers now, in subscription order. A listener that throws is logged and the others still run. |
| `Iris.TryPublish<T>(value)` | As `Publish` when Iris is available; returns false otherwise, such as in a test session without Iris. Framework events use it. |
| `Iris.TryPost<T>(value)` | From any thread: queues the event for the next Update. Returns false when the bounded queue is full or Iris is not linked. |
| `Iris.ListenerCount<T>()` | Current synchronous listeners. |

Additions during a publication take effect for the next publication; removals take effect immediately. Warm synchronous delivery allocates nothing.

## Frame callbacks

`Iris.OnUpdate(owner, deltaTime => …)`, `OnFixedUpdate` and `OnLateUpdate` run after the corresponding MonoBehaviour phase and end with their owner. Iris also publishes `UpdateFrame`, `FixedFrame` and `LateFrame` (each with `DeltaTime`) for `Iris.Subscribe`. Iris inserts its markers into the PlayerLoop while it is linked and removes only its own markers at shutdown; a test session inside Play mode reuses the running session's markers.

## Awaited events and requests

```csharp
Iris.SubscribeAsync<LevelCompleted>(this, async (done, token) => await fader.FadeOutAsync(token));
Iris.SubscribeAsync<LevelCompleted>(this, async (done, token) => await SaveAsync(token));
await Iris.PublishAsync(new LevelCompleted(3));     // each listener in order, each awaited

Iris.Respond<NextLevel, int>(this, (request, token) => UniTask.FromResult(request.After + 1));
int next = await Iris.RequestAsync<NextLevel, int>(new NextLevel(3));
```

| API | Behavior |
| --- | --- |
| `PublishAsync` | Awaits listeners one after another in subscription order. Cancellation is checked before each listener and passed to every listener. A failure stops the sequence and propagates. |
| `PublishConcurrentAsync` | Starts every listener and awaits all; one failure is rethrown, several are aggregated. |
| `Respond` / `RequestAsync` | Exactly one responder per request and response type. A request without a responder throws `NoResponderException`. |

Listeners return `UniTask`. Publication adds no allocation: warmed sequential and concurrent publications reuse their buffers, and listeners written as `async UniTask` use UniTask's pooled machinery.

