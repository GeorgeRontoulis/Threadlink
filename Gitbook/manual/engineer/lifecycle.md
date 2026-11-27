# Ownership

## Disposable ownership

Subsystems are owned by the Threadlink core for the session (see [Architecture](architecture.md)). Inside game objects, prefer owner-bound APIs: `Iris.Subscribe(owner, …)`, `Iris.OnUpdate(owner, …)`, `Threadlink.LoadAsync(id, owner)` and `InstantiateAsync(id, owner)` end when the owner is destroyed. For plain C# resources, the core provides `ResourceOwner` and `StableId`.

```csharp
using var resources = new ResourceOwner();
var reader = resources.Own(new System.IO.StreamReader(path));
string text = reader.ReadToEnd();
```

Ownership is optional. Ordinary `using` statements remain appropriate for individual resources. Each `Own` call transfers one cleanup obligation. Disposal closes admission, then attempts every cleanup in reverse order and aggregates failures. Repeated and reentrant disposal is harmless. Concurrent disposal calls do not wait for another caller's cleanup. Admission after closure immediately disposes the supplied resource and throws; if that cleanup fails, its exception propagates. Do not admit a non-idempotent resource twice.

`StableId` holds an existing nonempty GUID independently of an asset's name or path. Persist its canonical `ToString()` representation or `Value`, never its hash code. `TryParse` accepts canonical GUID text; an uninitialized identity cannot be persisted.
