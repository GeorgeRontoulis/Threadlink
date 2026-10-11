# Architecture

## Subsystems and static services

Threadlink distinguishes two categories of framework service.

**Woven subsystems** are instances the core constructs, owns and drives through a lifecycle. They derive from `ThreadlinkSubsystem<T>` and are accessed via `T.TryGetSingleton(out var instance)`. The native set, in weave order:

| Subsystem | Responsibility |
|---|---|
| `Sentinel` | Cross-platform platform-service abstraction: accounts, saves, achievements, and platform social/session affordances |
| `Chronos` | Time, timescale, playtime accumulation, optional manual physics |
| `Dextra` | Input devices, action maps, UI stack, interactables |
| `Aura` | Audio mixing, spatial zones, listener transform |

**Static services** possess neither instance nor lifecycle and are available from assembly load:

| Service | Responsibility |
|---|---|
| `Iris` | Event dispatch and update-loop distribution |
| `Nexus` | Scene residency, presentation, transition sequencing |
| `Initium` | Preload, boot and initialise pipeline |
| `Scribe` | Logging |

Additional project systems are registered as woven user subsystems through `WeavingFactory.User.cs` and `Subsystems.User.cs` (see [Implementing a Subsystem](implementing-a-subsystem.md)).

## Register hierarchy

`ThreadlinkSubsystem<T>` employs the curiously recurring generic pattern to expose a type-safe static singleton per subsystem. A subsystem's `ID` is `ThreadlinkSubsystem<T>.TypeHash`, the xxHash32 of its type's full name. Three specialisations extend it:

| Base | Additions | Application |
|---|---|---|
| `Register<S, O>` | A `Dictionary<int, O>` keyed by `IIdentifiable.ID`; `HasLinked(id)`, `TryGetLinkedObject<T>(id, out T)` | Lookup tables |
| `Linker<S, O>` | `TryLink`, `TryDisconnect<T>(id, out T)`, `DisconnectAll(trimRegistry)` | Tracking externally-created objects |
| `Weaver<S, O>` | `TryWeave<T>(out T)`, `TryWeave<T>(original, out T)`, `TrySever(id)`, `SeverAll()` | Owning object lifecycles |

A `Weaver` creates objects through `WeavingFactory<T>`: `TryWeave<T>(out T)` through its `OnCreate` factory, and `TryWeave<T>(original, out T)` through its `OnCreateFrom` clone factory. Severing discards the object.

`Threadlink` is itself a `Weaver<Threadlink, IThreadlinkSubsystem>`. `Aura` is a `Linker<Aura, AuraSpatialObject>`.

## Lifecycle contracts

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | Executes first. Dependency acquisition. |
| `IBootable` | `void Boot()` | Awake equivalent. Execution order within the phase is non-deterministic; implementations must be self-contained. |
| `IInitializable` | `void Initialize()` | Start equivalent. Cross-object references are valid. |
| `IDiscardable` | `void Discard()` | Teardown. Unsubscription occurs here, followed by `base.Discard()`. |
| `IDiscoverable` | Marker | Scene-placed `LinkableBehaviour` implementations are booted automatically on scene load, and discarded before the scene unloads, wherever they sit in it. Inactive objects are not booted. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Dependency injection point. |
| `IAsyncBinaryConsumer` | `UniTask ConsumeBinariesAsync()` | Consumes binary data authored through `IBinaryAuthor` (see [Binary Authoring](configuration.md#binary-authoring)). Not part of the Initium pipeline: the consumer's owner awaits it. |

Boot and initialise execute as batches separated by a single-frame yield. Ordering within a batch is not guaranteed. See [Deployment and Initialisation](deployment.md#initialisation-pipeline).

## Base object types

| Type | Domain | `Discard()` behaviour |
|---|---|---|
| `LinkableBehaviour` | Scene components | Raises `OnDiscard`, then destroys the GameObject. |
| `LinkableAsset` | ScriptableObjects, including Vault | Raises `OnDiscard`. Destroys the asset only if it is a runtime instance created through `LinkableAsset.TryCreate<T>(name, out T)`; assets loaded from the project are never destroyed. |

Both expose `ID`, `Name` and an `OnDiscard` event. `ID` is the xxHash32 of `Name`, which defaults to the object's name; hashing trims the name and ignores case, so names that differ only in case share an ID. `LinkableBehaviour` also caches its `Transform` as `CachedTransform` and offers `LinkableBehaviour.CreateFrom<T>(name)`, which creates a GameObject carrying a `T`.

Cleanup is implemented by overriding `Discard()`. `OnDestroy()` executes outside framework ordering and is unsuitable for Iris unsubscription.

Singleton variants publish their instance from `Boot()` and clear it in `Discard()`; `TryGetSingleton(out T)` reads it:

| Type | Base |
|---|---|
| `LinkableBehaviourSingleton<T>` | `LinkableBehaviour` |
| `LinkableAssetSingleton<T>` | `LinkableAsset` |
| `UserInterface<S>` | `UserInterface` (see [Dextra](dextra.md#the-ui-stack)) |

Their static instances are cleared when Play Mode starts or ends, so no Play session sees the previous one's instance when Domain Reload is disabled.

## Ownership and discarding

`Discard()` is called by whoever owns the object, never by the object itself, and never from an Iris event:

- **Nexus**, through `Initium.DiscardSceneObjects`, for the discoverables placed in a scene it unloads (see [Deployment and Initialisation](deployment.md) and [Nexus](nexus.md)).
- **The subsystem that created an object**, in its own `Discard()`. Dextra discards its interfaces this way.
- **An interface**, for the selectables and input icons in it.

The framework boots, initialises and discards `IDiscoverable`s: every one it finds, and nothing else. How objects nest is the developer's concern. Either don't nest discoverables, or know how they nest and manage what that implies. Whatever isn't discoverable, the developer boots and discards.

The framework's own object types (`AuraSpatialObject`, `Interactable`, `EntityDetector`, `DextraInputIcon`, the selectables) are never discoverable. Gameplay decides how they are mixed and controlled, so the developer manages them: from the object that owns them, or through a discoverable type of their own.

A subsystem that links objects (a `Linker`) does not tear its links down on a scene event. Each linked object unlinks itself in `Discard()`, as `AuraSpatialObject` does, and its owner calls that in time.

## Subsystem access

```csharp
if (Dextra.TryGetSingleton(out var dextra))
    dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
```

`TryGetSingleton` verifies both instance existence and continued linkage to the core, returning `false` cleanly during teardown. Subsystem references must not be cached across a scene transition without revalidation. Subsystem singletons are also cleared when Play Mode starts or ends.

The core is reached the same way, `Threadlink.TryGetSingleton(out var core)`. `Threadlink.Terminate()` discards it, which uninstalls the update loop and quits the application (exits Play Mode in the Editor).
