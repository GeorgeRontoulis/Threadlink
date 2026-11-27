# Lifetimes

The Threadlink core owns every subsystem for the session (see [Architecture](architecture.md)). Inside game objects, bind what you subscribe to or load to the object that needs it: the subscription or the content is released when that object is destroyed, so a destroyed component never receives events or keeps content loaded.

```csharp
public sealed class CoinLabel : MonoBehaviour
{
    private async void Start()
    {
        Iris.Subscribe<CoinsChanged>(this, change => label.text = change.Total.ToString());
        var icon = await Content.LoadAsync<Sprite>(ContentAssets.Interface.Coin, this);
        image.sprite = icon.Asset;
    }
}
```

| API | Released when the owner is destroyed |
| --- | --- |
| `Iris.Subscribe<T>(owner, callback)`, `Iris.SubscribeAsync<T>(owner, listener)`, `Iris.Respond(owner, responder)` | The subscription or responder. |
| `Iris.OnUpdate(owner, …)`, `OnFixedUpdate`, `OnLateUpdate` | The frame callback. |
| `Content.LoadAsync(id, owner)`, `LoadAllAsync(label, owner)` | The loaded content. |
| `Content.InstantiateAsync(id, owner)` | The instance and the content it came from. |

Without an owner, the call returns what you release yourself: dispose the `SubscriptionToken`, the responder or the `ResourceRequest`, usually with `using` or in `OnDestroy`.
