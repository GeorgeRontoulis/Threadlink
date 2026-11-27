# Engineering Procedures

## Adding a subsystem

1. Derive a `public sealed class` from `ThreadlinkSubsystem<T>` with a public parameterless constructor. It registers automatically.
2. Set up its own state and subscribe to Iris events in `OnSubsystemBoot`; use other subsystems through their `Instance` in `OnSubsystemInit`. Undo them in reverse: disconnect from other subsystems in `OnSubsystemDeinit`, release your own state and subscriptions in `OnSubsystemShutdown`. See [Architecture](architecture.md#two-phases-instead-of-a-dependency-order).
3. Expose its API as static methods built on `Instance`, and describe it in `Report`.
4. Check **Threadlink ▸ Runtime Monitor** in Play mode: the node, its edges and its report.

## Adding engineering settings

1. Derive `public sealed class MySettings : ThreadlinkSettings<MySettings>` with serialized fields, in its own file.
2. Read `MySettings.Current` from code; override `Validate()` to report problems.
3. Open **Project Settings ▸ Threadlink ▸ My** and select **Create settings** (or **Create** on the Workspace's Configuration page). The asset goes to the engineering configs folder.

## Adding an event

1. Declare a payload type, usually a `readonly struct` named in the past tense.
2. Subscribe with `Iris.Subscribe<T>(owner, callback)` so the subscription ends with its owner. A subsystem subscribes in `OnSubsystemBoot`, so it hears what other subsystems announce while they initialize.
3. Publish a change with `Iris.TryPublish`, as the framework does. An event reaches only the subscribers listening at that moment, so a subsystem publishes from `OnSubsystemInit` or later, after every subsystem has subscribed in `OnSubsystemBoot`.
4. For work that must finish before the caller continues, use `Iris.PublishAsync`; for a single answer, `Iris.Respond`/`RequestAsync`.

## Adding a scene

1. Add the scene to the Build Profile, or make it addressable and map it in **Threadlink ▸ Content Mapping**.
2. Move to it with `Nexus.LoadAsync`. Add a transition step for anything that must be ready before it is shown.
3. Add a **Scene Music** component for its music.

## Adding a screen

1. Author a UI Toolkit document, or a prefab with `ToolkitScreen` or `UGuiScreen` mapped in the Content mapping.
2. Open it with `Dextra.Screens.OpenAsync` and bind controls through `handle.Root`.
3. Close it with `Dextra.Screens.CloseAsync`; focus and input return automatically.

## Adding a definition type

1. Define an immutable runtime type.
2. Derive the authoring class from `VaultAsset<T>` or `VaultVariant<T>` with a new schema GUID and version 1, and map fields in `BuildRuntime`.
3. Add a `CreateAssetMenu` entry for designers; read values with `Vault.Get(definition)`.
4. When the layout changes, raise `SchemaVersion` and implement `MigrateAuthoring`.

## Adding a save type

1. Declare a `[Save("name", 1)]`, `[MessagePackObject]` partial class with stable `[Key]` indices.
2. Save and load with `Sentinel.Saves`.
3. Never change the name. Raise the version and register `Sentinel.Saves.AddMigration` when the payload changes.

## Adding a platform provider

1. Implement `ISentinelProvider` and the capability interfaces the platform supports; report `Supported` and `Status` honestly.
2. Add a subsystem whose `OnSubsystemInit` starts the SDK and calls `Sentinel.AddProvider(provider, capabilities)`, which announces `PlatformReady`; give it engineering settings that keep it off until enabled.
3. Verify every advertised capability against the real platform before treating it as stable.

## Testing

Tests and tools deploy their own core: `using var core = Threadlink.DeployIsolated(); core.Add<Wallet>();`. Mark test subsystems `[ManualRegistration]` so they never join the game's session.
