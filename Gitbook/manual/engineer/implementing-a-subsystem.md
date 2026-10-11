# Implementing a Subsystem

A project subsystem derives from `ThreadlinkSubsystem<T>` (or `Register`, `Linker` or `Weaver`, see [Architecture](architecture.md#register-hierarchy)) and implements whichever further lifecycle interfaces it needs. Every subsystem is bootable and discardable; overrides of `Boot()` and `Discard()` call the base implementation, which publishes and clears the singleton.

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

Registration spans two files.

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

The subsystem then traverses the preload, boot and initialise pipeline during deployment and is accessible through `InventorySystem.TryGetSingleton(out var instance)`. Only the required lifecycle interfaces need be implemented.

The order of the list is the weave order. Shutdown discards subsystems in reverse weave order, so a subsystem should be woven after the subsystems it depends on (see [Shutdown](deployment.md#shutdown)).

> Non-trivial construction is accommodated by assigning a factory delegate to `WeavingFactory<T>.OnCreate` in place of `Register<T>()`. `WeavingFactory<T>.OnCreateFrom` supplies the clone factory used by `TryWeave<T>(original, out T)`. Each holds a single delegate: adding one replaces the previous, and removing clears it. `ThreadlinkSubsystems.Register<T>()` (namespace `Threadlink.Utilities.Subsystems`) is equivalent to `WeavingFactory.Register<T>()`.
