# Architecture

## Subsystems and static services

Threadlink has two kinds of service.

**Woven subsystems** are instances the core creates, owns and runs through a lifecycle. They derive from `ThreadlinkSubsystem<T>`; reach them with `T.TryGetSingleton(out var instance)`. The native ones, in weave order:

| Subsystem | Responsibility |
|---|---|
| `Sentinel` | Platform services across platforms: accounts, saves, achievements, friends and invitations |
| `Chronos` | Time, time scale, playtime, optional manual physics |
| `Dextra` | Input devices, action maps, UI stack, interactables |
| `Aura` | Audio mixing, spatial zones, listener transform |

**Static services** have no instance and no lifecycle, and are available from assembly load:

| Service | Responsibility |
|---|---|
| `Iris` | Events, including the update loop |
| `Nexus` | Scene loading, presentation and transitions |
| `Initium` | The preload, boot and initialise pipeline |
| `Scribe` | Logging |

Project systems become woven user subsystems through `WeavingFactory.User.cs` and `Subsystems.User.cs` (see [Implementing a Subsystem](implementing-a-subsystem.md)).

## Register hierarchy

`ThreadlinkSubsystem<T>` uses the curiously recurring generic pattern to give each subsystem a type-safe static singleton. A subsystem's `ID` is `ThreadlinkSubsystem<T>.TypeHash`, the xxHash32 of its type's full name. Three bases build on it:

| Base | Additions | Application |
|---|---|---|
| `Register<S, O>` | A `Dictionary<int, O>` keyed by `IIdentifiable.ID`; `HasLinked(id)`, `TryGetLinkedObject<T>(id, out T)` | Lookup tables |
| `Linker<S, O>` | `TryLink`, `TryDisconnect<T>(id, out T)`, `DisconnectAll(trimRegistry)` | Tracking objects created elsewhere |
| `Weaver<S, O>` | `TryWeave<T>(out T)`, `TryWeave<T>(original, out T)`, `TrySever(id)`, `SeverAll()` | Owning object lifecycles |

A `Weaver` creates objects through `WeavingFactory<T>`: `TryWeave<T>(out T)` through its `OnCreate` factory, and `TryWeave<T>(original, out T)` through its `OnCreateFrom` clone factory. Severing discards the object.

`Threadlink` is itself a `Weaver<Threadlink, IThreadlinkSubsystem>`. `Aura` is a `Linker<Aura, AuraSpatialObject>`.

In the Editor, every register also implements `IEditorOnlyRegister`, which exposes its contents (`EditorOnly_Count`, `EditorOnly_ObjectType`, `EditorOnly_Registry`) to tools such as the [Runtime Monitor](editor-tooling.md#runtime-monitor). It is not compiled into builds.

## Lifecycle contracts

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | Runs first. Loads dependencies. |
| `IBootable` | `void Boot()` | Like `Awake`. Order within the phase is not defined, so don't rely on other objects. |
| `IInitializable` | `void Initialize()` | Like `Start`. Other objects are ready to reference. |
| `IDiscardable` | `void Discard()` | Teardown. Unsubscribe here, then call `base.Discard()`. |
| `IDiscoverable` | Marker | A `LinkableBehaviour` placed in a scene boots when the scene loads and is discarded before it unloads, wherever it sits. Inactive objects don't boot. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Receives a dependency. |
| `IAsyncBinaryConsumer` | `UniTask ConsumeBinariesAsync()` | Reads binary data authored through `IBinaryAuthor` (see [Binary Authoring](configuration.md#binary-authoring)). Not part of the Initium pipeline: the consumer's owner awaits it. |

Boot and initialise run as batches, one frame apart. Order within a batch is not guaranteed. See [Deployment and Initialisation](deployment.md#initialisation-pipeline).

## Base object types

| Type | Domain | `Discard()` behaviour |
|---|---|---|
| `LinkableBehaviour` | Scene components | Raises `OnDiscard`, then destroys the GameObject. |
| `LinkableAsset` | ScriptableObjects, including Vault | Raises `OnDiscard`. Destroys the asset only if it was created at runtime through `LinkableAsset.TryCreate<T>(name, out T)`; project assets are never destroyed. |

Both have `ID`, `Name` and an `OnDiscard` event. `ID` is the xxHash32 of `Name`, which defaults to the object's name. Hashing trims the name and ignores case, so names that differ only in case share an ID. `LinkableBehaviour` also caches its `Transform` as `CachedTransform`, and `LinkableBehaviour.CreateFrom<T>(name)` creates a GameObject with a `T`.

Clean up by overriding `Discard()`. `OnDestroy()` runs outside the framework's ordering, so don't unsubscribe from Iris there.

Singleton variants publish their instance from `Boot()` and clear it in `Discard()`; `TryGetSingleton(out T)` reads it:

| Type | Base |
|---|---|
| `LinkableBehaviourSingleton<T>` | `LinkableBehaviour` |
| `LinkableAssetSingleton<T>` | `LinkableAsset` |
| `UserInterface<S>` | `UserInterface` (see [Dextra](dextra.md#the-ui-stack)) |

Their instances are cleared when Play Mode starts or ends, so with Domain Reload disabled no Play session sees the previous one's.

## Ownership and discarding

`Discard()` is called by whoever owns the object, never by the object itself, and never from an Iris event:

- **Nexus**, through `Initium.DiscardSceneObjects`, for the discoverables placed in a scene it unloads (see [Deployment and Initialisation](deployment.md) and [Nexus](nexus.md)).
- **The subsystem that created an object**, in its own `Discard()`. Dextra discards its interfaces this way.
- **An interface**, for the selectables and input icons in it.

The framework boots, initialises and discards every `IDiscoverable` it finds, and nothing else. Nesting is up to you: avoid nesting discoverables, or know how they nest and handle the consequences. You boot and discard everything that isn't discoverable.

The framework's own object types (`AuraSpatialObject`, `Interactable`, `EntityDetector`, `DextraInputIcon`, the selectables) are never discoverable, because gameplay decides how they are combined and controlled. Manage them from the object that owns them, or through a discoverable type of your own.

A `Linker` does not drop its links on scene events. Each linked object unlinks itself in `Discard()`, as `AuraSpatialObject` does, and its owner calls that in time.

## Subsystem access

```csharp
if (Dextra.TryGetSingleton(out var dextra))
    dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
```

`TryGetSingleton` checks that the instance exists and is still linked to the core, so it returns `false` during teardown. Don't keep a subsystem reference across a scene transition without checking it again. Subsystem singletons are also cleared when Play Mode starts or ends.

Reach the core the same way, with `Threadlink.TryGetSingleton(out var core)`. `core.IsDeployed` becomes `true` once every subsystem is woven and initialised, just before `OnCoreDeployed`. `Threadlink.Terminate()` discards the core, which uninstalls the update loop and quits the application (exits Play Mode in the Editor).
