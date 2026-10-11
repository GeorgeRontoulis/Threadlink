# Engineering Procedures

## Introducing a subsystem

- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`, after the subsystems it depends on.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`, then call `base.Discard()`.

See [Implementing a Subsystem](implementing-a-subsystem.md). Check it in the [Runtime Monitor](editor-tooling.md#runtime-monitor): it should appear in the graph, linked to the core.

## Introducing an Iris event

- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document its delegate signature and use it everywhere. A mismatched subscription logs an error; a mismatched publication does nothing.

## Introducing a scene

- [ ] Map the scene through the Addressables Mapping Window, and keep it out of Build Settings: only the bootstrap scene belongs there (see [Configuration](configuration.md#bootstrap-scene)).
- [ ] Implement `Nexus.ISceneEntry` with its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Choose its `PhysicsMode`: a scene resident alongside others needs its own physics scene.
- [ ] Provide a listener for the four fader and loading-screen `Func` events.
- [ ] Load it only through Nexus: `TransitionAsync` or `PresentAsync` to show it, `HoldAsync` to keep it resident. Nexus discards its objects whenever it unloads (see [Nexus](nexus.md#residency-holds)). Release every hold.

## Introducing a stacked interface

- [ ] Build the prefab with a `CanvasGroup` and a component derived from `UserInterface` or `UserInterface<S>`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

## Using a custom update loop

- [ ] Set the User Config update-loop mode to `Custom`.
- [ ] Do not install `ThreadlinkPlayerLoop` yourself; it is internal, and Native mode owns it.
- [ ] Publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` from your driver at the right points in the frame.
- [ ] Keep the event contract Chronos, Aura, Dextra and project listeners expect.

## Introducing a Sentinel runtime module

- [ ] Implement an `ISentinelPlatform` / `SentinelPlatform` for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair.
- [ ] Register a `SentinelModuleDescriptor` through `SentinelModuleRegistry.Register` at `AfterAssembliesLoaded`.
- [ ] Advertise only the capabilities it implements.
- [ ] Register platform-level services on the platform and account-scoped services on `ISentinelAccount` implementations.
- [ ] Provide an `ISentinelBuildModule` for the same pair; without it, builds for that pair fail.

See [Sentinel Modules](sentinel/modules.md).
