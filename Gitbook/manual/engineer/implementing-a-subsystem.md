# Implementing a Subsystem

A project subsystem derives from `ThreadlinkSubsystem<T>` (or `Register`, `Linker` or `Weaver`; see [Architecture](architecture.md#register-hierarchy)) and implements whichever other lifecycle interfaces it needs. Every subsystem can boot and be discarded. Overrides of `Boot()` and `Discard()` must call the base method, which sets and clears the singleton.

```csharp
public sealed class InventorySystem : ThreadlinkSubsystem<InventorySystem>, IInitializable
{
    public void Initialize()
    {
        Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
    }

    public override void Discard()
    {
        Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
        base.Discard();
    }

    private void Tick() { }
}
```

Register it in two files.

**Factory registration** in `WeavingFactory.User.cs`:

```csharp
internal static class UserWeavingFactory
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        WeavingFactory.Register<InventorySystem>();   // requires a public parameterless constructor
    }
}
```

**Weaving** in `Subsystems.User.cs`:

```csharp
private static List<IThreadlinkSubsystem> WeaveSubsystems()
{
    var buffer = new List<IThreadlinkSubsystem>
    {
        Threadlink.Weave<InventorySystem>(),
    };

    Iris.Unsubscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
    return buffer;
}
```

During deployment the subsystem then goes through preload, boot and initialise, and `InventorySystem.TryGetSingleton(out var instance)` returns it. Implement only the lifecycle interfaces you need.

The list order is the weave order. Shutdown discards in reverse, so weave a subsystem after the ones it depends on (see [Shutdown](deployment.md#shutdown)). The [Runtime Monitor](editor-tooling.md#runtime-monitor) shows the weave order, and each subsystem's state: override `Report` to describe yours (see [Monitoring](monitoring.md)).

> For custom construction, assign a factory delegate to `WeavingFactory<T>.OnCreate` instead of calling `Register<T>()`. `WeavingFactory<T>.OnCreateFrom` is the clone factory used by `TryWeave<T>(original, out T)`. Each holds one delegate: adding one replaces the previous, and removing clears it. `ThreadlinkSubsystems.Register<T>()` (namespace `Threadlink.Utilities.Subsystems`) is the same as `WeavingFactory.Register<T>()`.
