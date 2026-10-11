# Engineering Procedures

## Introducing a subsystem

- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`, after the subsystems it depends on.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`, then call `base.Discard()`.

See [Implementing a Subsystem](implementing-a-subsystem.md).

## Introducing an Iris event

- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document the delegate signature and apply it consistently; mismatched subscriptions log an error, while mismatched publications are a no-op.

## Introducing a scene

- [ ] Map the scene through the Addressables Mapping Window, and keep it out of Build Settings: only the bootstrap scene belongs there (see [Configuration](configuration.md#bootstrap-scene)).
- [ ] Implement `Nexus.ISceneEntry` binding it to its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Choose its `PhysicsMode`: a scene resident alongside others needs its own physics scene.
- [ ] Provide a listener for the four fader and loading-screen `Func` events.
- [ ] Reach it through Nexus only: `TransitionAsync` / `PresentAsync` to show it, `HoldAsync` to keep it resident. Nexus discards its objects whenever it unloads (see [Nexus](nexus.md#residency-holds)); release every hold.

## Introducing a stacked interface

- [ ] Construct the prefab with a `CanvasGroup`, deriving its component from `UserInterface` or `UserInterface<S>`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

## Using a custom update loop

- [ ] Set the User Config update-loop mode to `Custom`.
- [ ] Do not install `ThreadlinkPlayerLoop` yourself; it is internal, and Native mode owns it.
- [ ] Publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` from the external driver at the intended phases.
- [ ] Preserve the same event contract expected by Chronos, Aura, Dextra and project listeners.

## Introducing a Sentinel runtime module

- [ ] Implement an `ISentinelPlatform` / `SentinelPlatform` for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair.
- [ ] Register a `SentinelModuleDescriptor` through `SentinelModuleRegistry.Register` at `AfterAssembliesLoaded`.
- [ ] Advertise only capabilities actually implemented.
- [ ] Register platform-level services on the platform and account-scoped services on `ISentinelAccount` implementations.
- [ ] Provide an `ISentinelBuildModule` for the same pair; without one, builds for that pair fail.

See [Sentinel Modules](sentinel/modules.md).
