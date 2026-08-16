# Threadlink Framework — Reference Manual

**Audience:** Game Designers & Engineers

**Scope:** Complete reference for the Threadlink runtime, editor tooling, and authoring workflow.

---

## Organisation

Threadlink enforces a strict separation between authored data and implemented behaviour. This manual mirrors that separation across two self-contained volumes sharing a common foundation chapter.

| Part | Audience | Contents |
|---|---|---|
| **Part I — Shared Foundations** | All | Framework purpose, project layout, deployment overview. |
| **Part II — Designer Reference** | Content authors | Vault authoring, identifier declaration, spatial audio, input prompts, configuration tuning. Requires no C#. |
| **Part III — Engineer Reference** | Programmers | Architecture, deployment pipeline, subsystem APIs, code generation internals, ECS, custom subsystems, netcode, performance. |

Both volumes describe the same systems from opposing ends. Where the designer volume specifies *"declare the identifier in `Vault.Fields.User.txt`"*, the engineer volume specifies the domain pipeline that emits `ThreadlinkIDs.Vault.Fields`, the manifest that stabilises its value, and the runtime API that consumes it.

> All API names, identifiers, menu paths, file paths, and asset-creation entries in this manual are drawn from framework source. Systems that are stubbed or under development are identified as such.

---
---

# PART I — Shared Foundations

## 1. Purpose

Threadlink is a modular runtime framework layered over Unity, providing a unified backbone for games and interactive applications. It supplies:

- A self-deploying core requiring neither a bootstrap scene nor a scene-placed initialiser.
- Native subsystems covering event dispatch, time, scene management, input and UI, audio, and persistence.
- A type-safe identifier system in which scenes, assets, prefabs, events, input modes, spawn points, RNG domains, and data fields are C# enumerations generated from plain-text declarations, eliminating string-keyed lookup.
- The **Vault**, a polymorphic data container for designer-authored game data.
- An unsafe, pointer-based ECS, a deterministic arithmetic and RNG toolkit, and a Steam-based netcode module.

Threadlink does not supersede Unity. Scenes, prefabs, and components are authored conventionally. Threadlink supersedes the intermediate infrastructure: manager singletons, event wiring, asset-reference bookkeeping, and persistence abstraction.

### 1.1 Authored Data, Implemented Behaviour

The framework maintains a single dividing line:

- **Designers author data.** They create Vault assets, populate configuration ScriptableObjects, place components in scenes, and declare identifiers in text files.
- **Engineers implement behaviour.** They author subsystems, implement scene logic, subscribe to events, and consume authored data.

Neither discipline encodes the other's concerns. Designers do not edit `.cs` files; engineers do not hard-code values belonging in a Vault.

### 1.2 Content-Addressed Identity

Generated identifier values derive from a hash of the entry name rather than from its position in a declaration list. Consequently, removing an entry cannot alter the value of any other entry, and removed entries persist as obsolete tombstones so that previously serialised references remain resolvable.

This property underpins the safety of the identifier workflow. A single domain — Iris events — operates on positional allocation instead, and is identified as such at every point where the distinction is material.

## 2. Project Layout

Threadlink occupies two top-level directories.

```
Threadlink/                     ← Framework. Not user-editable. Updated as a unit.
├── Core/
│   ├── Native Subsystems/      ← Aura, Chronos, Dextra, Iris, Nexus, Sentinel, Initium
│   └── Objects/                ← LinkableBehaviour, LinkableAsset, weaving factory
├── Shared/                     ← Contracts, Scribe, hashing, Addressables helpers
├── Collections/                ← Serialisable hash maps
├── Utilities/                  ← Extension-method libraries
├── Vault/                      ← Data container and Timeline integration
├── ECS/                        ← Entity component system
├── Deterministic/              ← Deterministic fixed point (DFP) and StatelessRNG
├── Netcode/                    ← Steam peer-to-peer networking
├── Editor/                     ← Domain code generation, Addressables tooling, inspectors
├── Generated/                  ← Generated output: ThreadlinkIDs enumerations and manifests
└── Plugins/                    ← SerializedReferenceInspector

Threadlink User/                ← Project territory.
├── Native Domain Injectors/    ← Plain-text identifier declarations
│   ├── Dextra.InputModes.User.txt
│   ├── Iris.Events.User.txt
│   ├── Nexus.SpawnPoints.User.txt
│   ├── StatelessRNG.Domains.User.txt
│   └── Vault.Fields.User.txt
└── Engineering/
    ├── Codebase/               ← Project code
    │   ├── Generated/          ← Generated output: project-defined domain enumerations
    │   ├── Constants.User.cs
    │   ├── Subsystems.User.cs
    │   ├── WeavingFactory.User.cs
    │   └── Threadlink.User.asmdef
    └── Configs/                ← Configuration assets
```

The `Threadlink/` directory constitutes the framework and is not hand-edited, `Threadlink/Generated/` included: that directory is owned exclusively by the code generator. All project authoring occurs under `Threadlink User/`.

### 2.1 Assembly Topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | Zero references; `noEngineReferences: true`. Referenceable from any assembly, including a deterministic simulation assembly. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers | — |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities | `allowUnsafeCode: true` |
| `Threadlink.Deterministic` | `DFP`, `StatelessRNG` | — |
| `Threadlink.ECS` | Entity component system | — |
| `Threadlink.Netcode` | Steam peer-to-peer networking | Opt-in |
| `Threadlink.Editor` | Code generation and editor tooling | Editor-only |
| `Threadlink.User` | Project code | — |
| `Threadlink.User.Generated` | Project-defined domain enumerations | Zero references |

Generated identifiers reside in namespace `Threadlink.Generated`; consuming code requires `using Threadlink.Generated;`. Isolating generated output in dedicated assemblies guarantees that neither project authoring nor third-party module installation produces a write within framework source.

## 3. Deployment Overview

The core deploys through Unity's `[RuntimeInitializeOnLoadMethod]` hooks. No bootstrap scene or scene-placed initialiser is required.

1. Assemblies load. Native and project subsystem factories register, and both tiers subscribe to their respective registration events.
2. Following first scene load, the core initialises Addressables, loads the **Native Config**, and loads the **User Config** referenced by it.
3. The core constructs itself, then registers and boots native subsystems followed by project subsystems.
4. The core publishes `OnCoreDeployed`.
5. Discoverable objects present in the loaded scene are booted.

Deployment requires two assets addressable through the Addressables system:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

Configuration is detailed in **Part III, §E20**.

---
---

# PART II — DESIGNER REFERENCE

> This volume covers data and identifier authoring. All operations are performed through text files, the Unity Inspector, and a small set of commands under the **Threadlink** menu.

## D1. Scope of the Role

Designers produce four categories of artefact:

1. **Identifiers** — names for spawn points, input modes, and Vault data fields, declared in text files and surfaced as Inspector dropdowns.
2. **Vaults** — data assets holding tunable values keyed by those identifiers.
3. **Configuration values** — exposed fields on the framework's configuration assets.
4. **Scene authoring** — placement of Threadlink components including audio zones, interactables, and input-prompt icons.

The governing principle: no name is typed twice, and nothing is referenced by raw string. An identifier is declared once and thereafter selected from a dropdown.

## D2. The Identifier Workflow

Threadlink compiles plain-text declaration lists into C# enumerations. The declaration files reside in **`Threadlink User/Native Domain Injectors/`**. Three are designer-owned:

| File | Resulting dropdown | Purpose |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Control contexts (`Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Locations at which entities may be placed. |
| `Vault.Fields.User.txt` | Vault Fields | Data fields available to Vault assets. |

These files are termed **injectors**: each injects entries into an enumeration the framework declares. Two further injectors in the same directory are engineer-owned (`Iris.Events.User.txt`, `StatelessRNG.Domains.User.txt`).

### D2.1 Declaration Syntax

Each file opens with comment lines prefixed `///`. Declarations follow, one identifier per line:

```text
///Use this file to define custom player spawn points for Nexus as showcased below:
///
///UserDefinedSpawnPoint1
///UserDefinedSpawnPoint2
///...
PlayerStart
BossArenaEntrance
SecretRoom_North
CheckpointAlpha
```

Constraints:

- One identifier per line.
- Identifiers begin with a letter and comprise letters, digits, and underscores. Any other character is substituted with an underscore during generation; declaring `Checkpoint_Alpha` directly is preferred to relying on substitution.
- Lines prefixed `//`, including `///`, are treated as comments.
- Blank lines are ignored.
- Identifier comparison is case-insensitive: `Alpha` and `alpha` denote the same entry.

### D2.2 Regeneration

Saving the file is sufficient. The injector directory is monitored, generation runs automatically, and Unity recompiles. New identifiers appear in the corresponding dropdowns.

**`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a generation pass on demand.

### D2.3 Removal Semantics

Entries may be removed by deleting the corresponding line. Values are content-addressed, with the following consequences:

- Removing an entry does not alter the value of any other entry.
- The removed identifier is retained in the generated enumeration as an obsolete tombstone, preserving resolution for assets that still reference it. Code referencing it produces a compiler warning.
- Reordering declarations has no effect; declaration order is not significant.

Iris events constitute the sole exception and fall under engineering ownership. Their values are positional array indices, so removal produces compilation failures at every subscription site rather than a tombstone. This behaviour is intentional.

### D2.4 Rename Semantics

A rename is equivalent to a removal followed by an addition: the former identifier becomes a tombstone and the new identifier receives a distinct value. References to the former identifier require reassignment. Engineering must be notified of any rename affecting an identifier referenced in code.

Each generation pass emits a console summary enumerating additions, removals, and tombstones. Consulting this summary is the most direct means of detecting an unintended rename.

## D3. The Vault

A **Vault** is a polymorphic data asset holding a set of named, typed **fields** — the authoring unit for any game entity: an enemy, a weapon, a level, an item.

### D3.1 Creation

1. In the Project window, select **Create ▸ Threadlink ▸ Vault**.
2. Assign a descriptive name (`Vault_Enemy_Goblin`, `Vault_Weapon_Longsword`).
3. Select the asset to expose its data-field map in the Inspector.

### D3.2 Field Composition

Each field comprises two elements:

1. A **field identifier**, selected from the Vault Fields dropdown.
2. A **typed value**, whose type is selected from a dropdown prior to entry.

Available field types:

| Type | Representation |
|---|---|
| `Integer` | 32-bit integer |
| `Float` | Single-precision floating point |
| `Boolean` | Boolean |
| `Double` | Double-precision floating point |
| `Long` | 64-bit integer |
| `Integer2D` | Integer pair (`int2`) |
| `Float2D` | Float pair (`float2`) |
| `Vector2D` | Two-component vector |
| `Vector3D` | Three-component vector |
| `Rotation` | Quaternion |
| `UnityGameObject` | GameObject reference |
| `LocalizedText` | Localised string; requires the Unity Localization package |

Additional field types are introduced through a minor engineering task.

### D3.3 Serialised and Transient Backings

Each field is assigned one of two value backings:

- **Serialised** — persisted with the asset. Appropriate for authored values such as base health or weapon damage. This is the default selection for design data.
- **Transient** — runtime-only, never persisted, reset each session. Appropriate for scratch values populated during play.

Authored values are serialised; runtime scratch values are transient.

> Unity [Asset Presets](https://docs.unity3d.com/Manual/Presets.html) may be applied to guarantee that every Vault of a given class is instantiated with its complete field set.

### D3.4 Runtime Consumption

Engineering reads and writes fields by identifier at runtime. Renaming a field identifier invalidates those references; engineering must be notified.

## D4. Spatial and Interface Audio (Aura)

**Aura** is the audio subsystem, managing Music, Atmos, and SFX channels and supporting spatial audio zones.

### D4.1 Aura Configuration

| Field | Function |
|---|---|
| Volume Fade Speed | Rate at which music, ambience, and listener volumes transition. Higher values produce faster transitions. |
| Navigation Clip | Effect played on UI element traversal. |
| Confirm Clip | Effect played on UI confirmation. |
| Cancel Clip | Effect played on UI cancellation. |

Clips are selected from the Assets dropdown; §D7 covers the procedure for populating that dropdown.

### D4.2 Audio Zones

An **AuraZone** is a scene component producing a localised sound source that attenuates global music and ambience by inverse-distance influence as the listener approaches.

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same GameObject and assign its clip. Aura configures the source for looping playback on awake.
3. Configure two parameters:
   - **Radius Coefficient** (0–1) — scales influence radius relative to the AudioSource maximum distance.
   - **Influence** (0–1) — attenuation applied to global channels when the listener is fully within the zone.

Zones are linked automatically on scene load and disconnected on unload; placement is the only required action.

### D4.3 Per-Scene Audio

Each scene declares music and ambience tracks with target volumes through a scene entry implemented by engineering. Track selection and volume levels are design decisions and should be communicated as part of the scene specification.

## D5. Input Prompts

Threadlink resolves the correct button glyph for the active input device and substitutes it automatically on device change.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Configure the control it represents.

The control-and-device to sprite mapping resides on the Dextra Config asset. Populating it is shared work: designers supply and assign sprites, engineers configure control paths. Sprites are Addressable assets; see §D7.

## D6. Configuration Assets

| Asset | Designer-tunable fields |
|---|---|
| **Aura Config** | Volume fade speed; navigation, confirm, and cancel clips. |
| **Dextra Config** | Input-icon sprite assignments; interface list, in conjunction with engineering. |
| **Chronos Config** | Iris Physics Update. **Engineering-owned; do not modify.** |

The physics toggle alters the simulation model for the entire application. Values of uncertain ownership should be confirmed with engineering before modification.

## D7. Registering Scenes, Prefabs, and Assets

Runtime-loaded content — scenes, prefabs, audio clips, sprites — is referenced through Addressables and surfaced as a dropdown identifier. Registration is performed through a dedicated tool.

1. Ensure the asset belongs to an Addressable group.
2. Open **`Threadlink ▸ Addressables ▸ Mapping Window`**.
3. Locate the asset within its group and enable its checkbox.
4. Select **Apply**.

The window classifies assets by type automatically — scenes yield Scene identifiers, prefabs yield Prefab identifiers, all others yield Asset identifiers — and writes the corresponding reference into the User Config. The reference maps on the User Config are read-only in the Inspector and are owned exclusively by this window.

Two properties govern the resulting identifier:

- **The identifier derives from the asset name.** An audio clip named `Music_BossTheme` yields the identifier `Music_BossTheme`. Assets should carry stable, code-safe names prior to mapping.
- **The Addressable group participates in identity.** Two assets named `Splash` in distinct groups coexist; the second is qualified as `GroupName_Splash`. Relocating a mapped asset to a different group alters its identifier and invalidates existing references. Group assignment should be settled before mapping.

Disabling an asset's checkbox unmaps it. The identifier is retained as a tombstone.

## D8. Designer Procedures

**Declaring a spawn point**
1. Add the identifier to `Threadlink User/Native Domain Injectors/Nexus.SpawnPoints.User.txt`.
2. Save. Generation runs automatically.
3. The spawn point becomes selectable wherever spawn points are configured.

**Adding a tunable value to an entity**
1. Add the field identifier to `Vault.Fields.User.txt` and save.
2. Open the entity's Vault, add a field with that identifier, select its type, and enter the value.
3. Select the **Serialised** or **Transient** backing.

**Declaring an input context**
1. Add the mode identifier to `Dextra.InputModes.User.txt` and save.
2. Provide the identifier to engineering for binding to an input action map.

**Making an asset loadable**
1. Assign the asset to an Addressable group under a stable name.
2. Map it through **`Threadlink ▸ Addressables ▸ Mapping Window`** and select **Apply**.

### Operating Principles

- Nothing is referenced by raw string. Declare an identifier, save, and select from the dropdown.
- Removal is safe; renaming orphans existing references and requires coordination.
- Consult the console summary emitted by each generation pass.
- Assign stable names and final group membership to Addressable assets before mapping them.
- Notify engineering of any identifier rename that may be referenced in code.

---
---

# PART III — ENGINEER REFERENCE

> This volume assumes C# proficiency and familiarity with Unity, Addressables, and asynchronous programming. Threadlink uses **UniTask** exclusively in place of `System.Threading.Tasks`, and relies on `[RuntimeInitializeOnLoadMethod]`, generic constraints, and unsafe code within the ECS.

## E1. Architecture

### E1.1 Subsystems and Static Services

Threadlink distinguishes two categories of framework service.

**Woven subsystems** are instances the core constructs, owns, and drives through a lifecycle. They derive from `ThreadlinkSubsystem<T>` and are accessed via `T.TryGetSingleton(out var instance)`. The native set, in weave order:

| Subsystem | Responsibility |
|---|---|
| `Sentinel` | Environment-aware persistence IO |
| `Chronos` | Time, timescale, playtime accumulation, optional manual physics |
| `Dextra` | Input devices, action maps, UI stack, interactables |
| `Aura` | Audio mixing, spatial zones, listener transform |

**Static services** possess neither instance nor lifecycle and are available from assembly load:

| Service | Responsibility |
|---|---|
| `Iris` | Event dispatch and update-loop distribution |
| `Nexus` | Scene loading, unloading, transition sequencing |
| `Initium` | Preload, boot, and initialise pipeline |
| `Scribe` | Logging |

`ECSWorld` and `Netflow` are subsystems, registered by the project rather than natively.

### E1.2 Register Hierarchy

`ThreadlinkSubsystem<T>` employs the curiously recurring generic pattern to expose a type-safe static singleton per subsystem. Three specialisations extend it:

| Base | Additions | Application |
|---|---|---|
| `Register<S, O>` | `Dictionary<int, O>` keyed by `IIdentifiable.ID` | Lookup tables |
| `Linker<S, O>` | `TryLink`, `TryDisconnect`, `DisconnectAll` | Tracking externally-created objects |
| `Weaver<S, O>` | `TryWeave`, `TrySever`, `SeverAll` | Owning object lifecycles |

`Threadlink` is itself a `Weaver<Threadlink, IThreadlinkSubsystem>`. `Aura` is a `Linker<Aura, AuraSpatialObject>`.

### E1.3 Lifecycle Contracts

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | Executes first. Dependency acquisition. |
| `IBootable` | `void Boot()` | Awake equivalent. Execution order within the phase is non-deterministic; implementations must be self-contained. |
| `IInitializable` | `void Initialize()` | Start equivalent. Cross-object references are valid. |
| `IDiscardable` | `void Discard()` | Teardown. Unsubscription occurs here, followed by `base.Discard()`. |
| `IDiscoverable` | Marker | Scene-placed `LinkableBehaviour` implementations are booted automatically on scene load. Inactive objects are excluded. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Dependency injection point. |

Boot and initialise execute as batches separated by a single-frame yield. Ordering within a batch is not guaranteed.

### E1.4 Base Object Types

| Type | Domain | Discard behaviour |
|---|---|---|
| `LinkableBehaviour` | Scene components | Destroys the GameObject |
| `LinkableAsset` | ScriptableObjects, including Vault | Nulls fields; does not destroy |

Both expose `ID`, `Name`, and an `OnDiscard` event. Cleanup is implemented by overriding `Discard()`. `OnDestroy()` executes outside framework ordering and is unsuitable for Iris unsubscription.

## E2. Deployment Sequence

| Phase | Hook | Action |
|---|---|---|
| 1 | `[OnEnteringPlayMode]` | `NativeWeavingFactory.Register()` registers factories for Sentinel, Chronos, Dextra, and Aura. |
| 2 | `AfterAssembliesLoaded` | `UserSubsystemsConfig` subscribes to `OnUserSubsystemRegistration`. |
| 3 | `BeforeSceneLoad` | `NativeSubsystemsConfig` subscribes to `OnNativeSubsystemRegistration`. |
| 4 | `AfterSceneLoad` | `Threadlink.DeployCoreAsync()` executes. |

`DeployCoreAsync` performs:

1. `await Addressables.InitializeAsync()`.
2. Loading of `ThreadlinkNativeConfig` from the address in `NativeConstants.Addressables.NATIVE_CONFIG`.
3. Loading of `ThreadlinkUserConfig` through `NativeResources.UserConfig`.
4. Core construction and `DeployAsync()`:
   - `Boot()` instantiates the hidden `ThreadlinkLoop` GameObject when the update loop is configured as `Native`.
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` publishes the `Func<List<IThreadlinkSubsystem>>` event and passes the collected subsystems to `Initium.PreloadBootAndInitAsync`.
   - The sequence repeats for `OnUserSubsystemRegistration`.
   - `OnCoreDeployed` is published with the core as payload.
5. `Initium.BootAndInitUnityObjectsAsync()` is dispatched as fire-and-forget for scene objects already loaded.

Failure to load either configuration asset aborts deployment with an error.

## E3. Initialisation Pipeline

`Initium.PreloadBootAndInitAsync<T>(IEnumerable<T>)` is the single entry point. It partitions the input by interface and executes three phases, awaiting completion of each before proceeding:

1. `IAddressablesPreloader.TryPreloadAssetsAsync()`
2. `IBootable.Boot()`
3. `IInitializable.Initialize()`

Scene objects are discovered via `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`. The scene-scoped overload additionally filters by `gameObject.scene`; `Nexus.LoadNewSceneAsync` invokes it so that a newly loaded scene boots only its own objects.

No unload counterpart exists. Scene-placed objects are destroyed by Unity without `Discard()` being invoked. Objects subscribing to Iris must therefore arrange their own teardown, conventionally by subscribing to `OnBeforeActiveSceneUnload` and invoking `Discard()` from the handler.

## E4. Iris — Event Dispatch

`Iris` is a static class backed by `object[] EventRegistry`, dimensioned at type load from the cardinality of `ThreadlinkIDs.Iris.Events`. Each slot holds a `DelegateList<T>` allocated on first subscription.

### E4.1 Dispatch Signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        — single listener
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> — single listener
```

Subscription and unsubscription state the delegate type explicitly:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished, OnSceneReady);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnNexusLoadingFinished, OnSceneReady);
```

Diagnostic members: `TryGetListenerCount`, `ContainsListener<T>`, `Clear`.

### E4.2 Constraints

- **The delegate type constitutes the contract and is not enforced across subscribers at compile time.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type mismatch and discards the subscription. Publishing under a mismatched type returns without invocation. Both failures are silent at runtime; signature agreement is a project discipline.
- **`Func` events throw `InvalidOperationException` beyond a single listener.** They model a single provider rather than a broadcast.
- **Dispatch iterates in reverse** (`Count - 1` to `0`), and `DelegateList.Remove` performs swap-with-last. A handler removing itself during dispatch is safe; a handler removing a different listener is not.
- **Unsubscription belongs in `Discard()`.** A retained delegate keeps a destroyed object reachable and dispatches to stale state.
- Dispatch to an event without listeners is a no-op.

### E4.3 Update Events

`OnUpdate`, `OnFixedUpdate`, and `OnLateUpdate` are published by the hidden `ThreadlinkLoop` MonoBehaviour when the update loop is configured as `Native`. `OnLateUpdate` is the correct target for camera-relative state, as it dispatches after camera transformation.

### E4.4 Declaring Events

Declare the identifier in `Threadlink User/Native Domain Injectors/Iris.Events.User.txt` and save.

Iris events are the framework's sole **ordinal** domain: values are dense array indices allocated in source order, native entries preceding injector entries. Values are never serialised, so removal produces compilation failure at every subscription site. Tombstoning is disabled for this domain accordingly.

## E5. Subsystem Access

```csharp
if (Dextra.TryGetSingleton(out var dextra))
    dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
```

`TryGetSingleton` verifies both instance existence and continued linkage to the core, returning `false` cleanly during teardown. Subsystem references must not be cached across a scene transition without revalidation.

## E6. Scribe — Logging

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

this.Send("Loaded ", count, " entries.").ToUnityConsole();
Scribe.Send<MySystem>("Static context message.").ToUnityConsole(DebugType.Error);
```

`Send` accepts `params object[]` and appends each element to a ZString builder, so multi-argument invocation avoids materialising an interpolated string. The message prefix is a type name: the runtime type of the receiver for the extension form, or the supplied type argument for the static form. `DebugType` enumerates `Info`, `Warning`, and `Error`.

Scribe is the logging path throughout the framework, including editor tooling.

## E7. Chronos — Time

| Member | Semantics |
|---|---|
| `TimeScale` | Accepts only 0 or 1. Assignment publishes `OnGamePaused` or `OnGameResumed`. |
| `RawTimeScale` | Identical restriction; publishes nothing. |
| `DeltaTime`, `SmoothDeltaTime`, `UnscaledDeltaTime`, `FixedDeltaTime` | Cached once per tick. |
| `CurrentFramerate`, `CurrentTimeSinceDeployment` | Derived values. |
| `TotalPlaytime`, `CountTotalPlaytime`, `PlaytimeCountingMode`, `ClearTotalPlaytime()` | `PlaytimeCountMode` enumerates `Scaled` and `Unscaled`. |
| `Start()`, `Stop()` | Subscribe and unsubscribe the internal tick handlers. |

Playtime accumulation publishes `OnPlaytimeCountTick` with the running total each frame while `CountTotalPlaytime` is set.

### E7.1 Manual Physics Simulation

When `ChronosConfig.IrisPhysicsUpdate` is enabled, `Chronos.Boot()` assigns `Physics.simulationMode = SimulationMode.Script` and steps `Physics.Simulate(Time.fixedDeltaTime)` on each `OnFixedUpdate`.

The setting must remain disabled where another framework owns the simulation. The simulation mode is global state: any additional code assigning `SimulationMode.Script` and stepping independently produces double stepping. No runtime guard enforces exclusivity.

## E8. Nexus — Scene Management

`Nexus` is a static class.

### E8.1 The `ISceneEntry` Contract

```csharp
public interface ISceneEntry
{
    ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
    LoadSceneMode LoadMode { get; }
    ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
    ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
    float MusicVolume { get; }
    float AtmosVolume { get; }

    UniTask OnFinishedLoadingAsync();   // default: completed
    UniTask OnBeforeUnloadedAsync();    // default: completed
}
```

The interface may be implemented on any type: a ScriptableObject, a struct, or an asset type belonging to another framework. Both asynchronous members carry default implementations.

### E8.2 Transition Sequence

```csharp
await Nexus.FadeToLoadingScreenAsync();
await Nexus.UnloadActiveSceneAsync();
await Nexus.LoadNewSceneAsync(entry);
await Nexus.FadeToGameplayAsync();
```

`UnloadActiveSceneAsync` obtains the current entry via `Iris.Publish<ISceneEntry>(OnActiveSceneRequested)`. The project must supply a provider for that `Func` event; without one, unloading is a no-op.

Event order across a complete transition:

| Order | Event | Payload |
|---|---|---|
| 1 | `OnActiveSceneRequested` | returns `ISceneEntry` |
| 2 | `OnBeforeActiveSceneUnload` | `ISceneEntry` |
| 3 | `OnActiveSceneFinishedUnloading` | `ISceneEntry` |
| 4 | `OnNewSceneFinishedLoading` | `ISceneEntry` |
| 5 | `OnNexusLoadingFinished` | `ISceneEntry` |

Between phases 4 and 5, `LoadNewSceneAsync` activates the loaded scene, boots its discoverables through Initium, and executes the audio transition concurrently with `OnFinishedLoadingAsync`.

`FadeToLoadingScreenAsync` and `FadeToGameplayAsync` drive four `Func<UniTask>` events — `OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync`, `OnHideLoadingScreenAsync` — in addition to Aura's listener volume fade. Each requires exactly one provider.

### E8.3 Teardown Ordering

Iris dispatches in reverse subscription order; objects subscribing latest are notified first. Scene-placed components boot during `LoadNewSceneAsync` and therefore execute their `OnBeforeActiveSceneUnload` handlers before subsystems that subscribed at deployment. Teardown logic must not assume the inverse ordering.

## E9. Dextra — Input and UI

### E9.1 Device Resolution

`Dextra.InputDevice` enumerates `MouseAndKeyboard`, `Xbox`, `PlayStation`, and `Switch`. `CurrentInputDevice` updates automatically and publishes `OnInputDeviceChanged`. `TryGetInputIcon(device, controlPath, out Sprite)` resolves the corresponding glyph.

### E9.2 Input Modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The mode-to-`InputActionReference` mapping resides on the Dextra Config as a `FieldHashMap`.

### E9.3 The UI Stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and require a `CanvasGroup`. They are not discovered by Initium: Dextra instantiates them from `DextraConfig.interfacePointers` — Addressable prefab identifiers — marks them `DontDestroyOnLoad`, forces alpha to zero, and boots them.

Introducing a stacked interface therefore requires constructing the prefab, mapping it through the Addressables Mapping Window, and adding its identifier to `interfacePointers`.

```csharp
dextra.Stack<PauseMenuUI>();
dextra.Stack<ShopUI, ShopData>(data);   // implements IStackingDataPreprocessor<ShopData>
dextra.PopTopInterface();
dextra.Cancel();
dextra.ClearStackedInterfaces();
Dextra.IsTopInterface<PauseMenuUI>();
```

Marker interfaces:

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Receives `OnCancelled()` and `OnSubPanelCancelled()` |
| `IPersistentInterface` | Exempt from concealment when overlaid |
| `IInteractableInterface`, `IInteractableInterface<T>` | Declares selectable content; the generic form exposes the collection |
| `IStackingDataPreprocessor<T>` | Preprocesses the stacking payload |

Scene-placed interfaces outside the stack are not managed by it. `ClearStackedInterfaces()` does not affect them, and `DontDestroyOnLoad` instances persist across transitions. Concealment must be invoked explicitly.

### E9.4 Interactables

`Interactable` derives from `LinkableBehaviour` and carries an `InteractableConfig`. `EntityDetector2D` and `EntityDetector3D` are trigger-collider components publishing `OnInteractableDetected` and `OnInteractableOutOfRange`. On detection, an interactable subscribes its `Interact` method as `Func<bool>` to `OnInteract`.

`OnInteract` is a `Func` event, so at most one interactable may be in range at any time. Overlapping active areas violate this constraint and throw.

## E10. Aura — Audio

```csharp
aura.DriveAudioListener(position, rotation);     // position-only and rotation-only overloads exist
aura.PlayUISFX(Aura.UISFX.Confirm);
await aura.FadeAudioListenerVolumeAsync(0f);
await aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
aura.SetGlobalVolumesMax(musicVolume, atmosVolume);
aura.TryGetMixerValue(name, out float value);
aura.TrySetMixerValue(name, value);
await aura.FadeAudiosourceVolumeAsync(source, target);
aura.MoveTowardsVolume(source, target);
```

The AudioListener transform is driven rather than parented: some component must invoke `DriveAudioListener` each frame, conventionally a camera controller on `OnLateUpdate`. Absent a driver, the listener retains its last assigned transform. Scene transitions require particular attention, as the previous driver is destroyed before its successor exists.

`AuraZone` components link automatically through `TryLink` and disconnect on scene unload.

## E11. Sentinel — Persistence

Sentinel is an environment-aware IO subsystem operating exclusively on byte arrays. Text-based schemes such as JSON are outside its scope.

```csharp
await sentinel.DeployEnvironmentAsync();
bool written = await sentinel.TryWriteToStorageAsync(folderID, fileID, bytes);
byte[] data = await sentinel.ReadFromStorageAsync(folderID, fileID);
sentinel.DeleteStoredData(folderID, fileID);
```

`CurrentOperationState` enumerates `Idle`, `Deploying`, `Reading`, and `Writing`. `EnvironmentDeployed` reports readiness.

The environment is a `[SerializeReference]` field on the Sentinel Config deriving from `Sentinel.Environment`:

| Environment | Status |
|---|---|
| `Steam` | Implemented |
| `XBOX` | Implemented via GDK, covering Microsoft Store; requires `THREADLINK_SENTINEL_XBOX` |
| `PlayStation` | Stubbed; members declared, not implemented |
| `NintendoSwitch` | Stubbed; members declared, not implemented |

Serialisation is the caller's responsibility. `Threadlink.TrySerialize<T>` and `TryDeserialize<T>` provide MessagePack wrappers.

## E12. Vault — Runtime API

```csharp
vault.Has(fieldID);
vault.TryGetDataField(fieldID, out DataField field);
vault.TryGetGenericDataField<float>(fieldID, out DataField<float> field);
vault.TryGetConcreteDataField<Float>(fieldID, out Float field);
vault.TryGet<float>(fieldID, out float value);
vault.TrySet<float>(fieldID, value);
```

Fields are stored in a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`, backed by `[SerializeReference]` to permit polymorphic field types.

`DataField<T>` exposes `Value` and an `OnValueChanged` event. The backing is either `SerializedValue<T>` or `TransientValue<T>`; the transient variant is `[NonSerialized]` and resets per session. As a Vault is a ScriptableObject, transient state persists across editor play-mode sessions unless explicitly reset.

Under `THREADLINK_TIMELINE`, `VaultMarker`, `VaultTrack`, and `VaultReceiver` permit a Timeline to write into a Vault.

## E13. Resource Loading

Two parallel APIs exist: one keyed by generated identifier, one accepting an `AssetReference` directly.

```csharp
core.LoadAsset<T>(ThreadlinkIDs.Addressables.Assets id);
Threadlink.LoadAsset<T>(AssetReference reference);
await core.LoadAssetAsync<T>(id);
await Threadlink.LoadAssetAsync<T>(reference);

core.LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs id);       // T : Component
await core.LoadPrefabAsync<T>(id);

await core.LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes id, LoadSceneMode mode);
await core.UnloadSceneAsync(id);

core.ReleaseAsset(id);
core.ReleasePrefab(id);
```

Query and validation:

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.CheckIDValidity(id);
```

All lookups resolve through the User Config's keyed maps and validate `RuntimeKeyIsValid()`, reporting failure through Scribe.

The `AssetReference` overloads exist for portable modules: a module may hold its own references without depending on the consuming project's generated `Assets` enumeration.

## E14. Implementing a Subsystem

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

The subsystem then traverses the preload, boot, and initialise pipeline during deployment and is accessible through `InventorySystem.TryGetSingleton(out var instance)`. Only the required lifecycle interfaces need be implemented.

> Non-trivial construction is accommodated by assigning a factory delegate to `WeavingFactory<T>.OnCreate` in place of `Register<T>()`.

Enabling netcode requires invoking `ThreadlinkNetcode.WeaveSubsystems(buffer)` in the weave method, `ThreadlinkNetcode.RegisterSubsystems()` in the factory, and adding the netcode assembly to `Threadlink.User.asmdef`.

## E15. Entity Component System

An unsafe, pointer-based, allocation-free world for high-throughput simulation. `ECSWorld` is a project-registered subsystem.

### E15.1 Components

```csharp
[RuntimeComponent]
public struct Position : IComponent { public float3 Value; public readonly void Dispose() { } }
```

`IComponent` requires `Dispose()`. `[RuntimeComponent]` causes `ComponentRegistry.Hydrate()` to assign a bit index at boot. Indices derive from sorting component types by hash and are therefore stable across runs and machines.

> IL2CPP targets additionally require `[UnityEngine.Scripting.Preserve]`. The registry emits a warning at boot when it is absent.

### E15.2 Entities

```csharp
ECSWorld.TryGetSingleton(out var world);

Entity entity = world.CreateNewEntity();
Position* position = world.Add<Position>(entity);
if (world.TryGetPointer<Velocity>(entity, out Velocity* velocity)) velocity->Value = new float3(1, 0, 0);
bool present = world.Has<Position>(entity);
world.Destroy(entity);
```

`Entity` carries an identifier and a recycling generation counter. Destroyed identifiers are reissued with an incremented generation, causing stale handles to fail `IsValid`.

### E15.3 Iteration

`ForEach` accepts function pointers rather than delegates, rendering closures impossible by construction:

```csharp
world.ForEach<Position, Velocity>(&Integrate);

static void Integrate(in Entity entity, Position* position, Velocity* velocity)
    => position->Value += velocity->Value;
```

Overloads accept one through four component types, with and without an `ECSFilter`. Filters should be constructed once in `Boot()` and retained.

`EntityCommandBuffer` defers structural modification, permitting creation and destruction to be queued during iteration.

## E16. Deterministic Toolkit

### E16.1 `DFP`

A software fixed-point type providing deterministic arithmetic, transcendental functions, and trigonometry. Required for any computation whose results must be identical across machines: networked simulation, replay, and procedural generation.

### E16.2 `StatelessRNG`

Stateless by construction: identical seed and inputs yield identical output, independent of call order or thread.

```csharp
StatelessRNG.Boot(seed);

using var scope = StatelessRNG.CreateScope(ThreadlinkIDs.StatelessRNG.Domains.LootTables);
int roll = scope.Range(1, 100);
bool critical = scope.Probability((DFP)0.15f);
DFP fraction = scope.Float01();
int index = scope.Index(itemCount);

var next = scope.Advance();
```

`CreateScope<C>(domain, in C context)` mixes an additional `unmanaged` context structure into the scope identity, yielding independent streams per entity, per room, or per tick from a single domain.

Domains partition streams: systems drawing from a common seed under distinct domains do not interfere. New domains are declared in `StatelessRNG.Domains.User.txt`.

> Domain values are name hashes. Renaming a domain alters its stream, causing divergence in anything reproducing a prior sequence from a stored seed or replay. Domain names constitute part of the save format.

## E17. Netcode

An opt-in Steam peer-to-peer module.

| Type | Responsibility |
|---|---|
| `Netflow` | Lobby flow subsystem: `HostLobby()`, `JoinLobby(id)`, `AutoJoinHostLobby()` |
| `Netrunner` | Connection, session, ingress and egress, native allocation, network update loop |
| `Networld` | Networked world state; binds ECS entities to scene players |
| `TransportLayer`, `SteamTransportLayer` | Transport abstraction and Steam implementation |
| `NetworkRouter`, `NetworkPayload`, `NetworkSerializer` | Message routing and wire format |
| `HandshakeSubsystem`, `NetworkSpawningSubsystem`, `NetworkTransformSubsystem`, `NetworkAnimationSubsystem` | Feature subsystems |
| `NetworkTransform`, `NetworkPlayableAnimator`, `NetworkClipLibrary` | Unity bridge components |

Flow providers `LocalSteamFlowProvider` and `RemoteSteamFlowProvider` implement `IFlowProvider`, permitting lobby behaviour substitution for local testing.

The module is experimental and under active development. Its API is not stable.

## E18. Identifier Domains and Code Generation

A single pipeline produces every enumeration under `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

### E18.1 Domain Kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | `xxHash32` of the scope-qualified key | Retained as `[Obsolete]` tombstone with value preserved | All domains except Iris |
| **Ordinal** | Dense index allocated in source order | Removed outright | `Iris.Events` |

Identity is the default kind, guaranteeing that removal cannot shift another entry's value. Iris is ordinal because dispatch indexes `EventRegistry` with the value directly. As Iris values are never serialised, outright removal producing compilation failure is the correct failure mode.

### E18.2 Sources

Domain entries originate from up to three sources, merged in order:

1. **Native entries** — a framework-owned `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`).
2. **Injectors** — files named `{DomainName}.{Injector}.txt` within the injector directory. The injector name becomes the **scope**, folded into each entry's hash key. `Iris.Events.User.txt` is the injector named `User`.
3. **Domain definitions** — a `.txt` within the definitions directory declares an entirely new enumeration named after the file, emitted through the shared `CustomDomain.Shell.txt` into namespace `Threadlink.User`.

Injectors may extend domain definitions on identical terms to native domains, permitting a third-party module to supply content for a project-defined domain.

Modules use this mechanism. A module requiring its own Iris events ships `Iris.Events.MyModule.txt`; placing it in the injector directory constitutes the entire installation procedure. Module entries are appended after project entries and cannot displace them.

> A module's injector filename forms part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` alters the scope and therefore every identity-domain value that injector contributes.

### E18.3 Manifests

Each domain maintains a `{DomainName}.manifest.json` adjacent to its generated script, recording every entry's key, member name, scope, and value. Manifests are version-controlled artefacts and establish identity stability:

- An assigned member name survives regeneration unchanged.
- Name collisions resolve deterministically: a second entry claiming `Splash` is qualified as `GroupName_Splash`, and the incumbent is never displaced.
- Removed entries are flagged as tombstoned rather than discarded.
- Hash collisions are detected and rehashed under a recorded seed offset, rendering the resolution reproducible.

Each pass emits an addition, removal, rename, rescope, and collision summary through Scribe.

### E18.4 Shells

A **shell** supplies the C# scaffolding for a generated file — namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` substitution token. `CustomDomain.Shell.txt` carries an additional `{DOMAIN_NAME}` token, permitting one template to serve every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator is configured to reserve those values. Shells do not declare generated entries.

### E18.5 Generation Triggers and Guards

An `AssetPostprocessor` monitors every native-entries file and all three directories, regenerating on any `.txt` modification. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a pass.

The pipeline enforces:

- Output confinement to the configured generated directories, preventing a misconfigured domain from overwriting hand-authored source.
- Abort on two domains resolving to the same output path.
- Diagnostic reporting, by filename, of any injector whose prefix matches no declared domain.
- Rejection of injectors carrying more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the sole accepted form.
- Emission of a sentinel-only enumeration, with warning, for a domain yielding zero entries.

### E18.6 Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** enumerates every writable Addressable group with its member assets and a per-asset selection toggle. **Apply** performs:

1. Emission of one injector per group per reference kind — `Addressables.Assets.{Group}.txt` and equivalents — into the Addressables injector directory.
2. Regeneration of the three Addressables domains.
3. Reconstruction of the User Config reference maps from the resulting manifests.

Group names are sanitised into scopes: `Test Assets` yields `Test_Assets`. Two groups sanitising to an identical scope are reported by warning, as entries sharing a name across them would collide.

Apply purges before rewriting, so deselection unmaps. Injector files in that directory are generated output and are not hand-edited.

## E19. Collections and Utilities

**Serialisable maps** in `Threadlink.Collections`, both deriving from `ThreadlinkHashMap<TKey, TValue>` — bucket-indexed, allocation-free, driven by `ISerializationCallbackReceiver`:

| Type | Value backing | Application |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` | Value types and Unity object references |
| `RefHashMap<K,V>` | `[SerializeReference]` | Polymorphic managed values, including Vault's `DataField` |

Editor-only mutation is exposed through `EditorOnly_TryAdd`, `EditorOnly_Remove`, and the indexer. `OnAfterDeserialize` clamps a serialised entry count exceeding the backing arrays and reports the discrepancy rather than throwing from a deserialisation callback.

**Extension libraries** under `Threadlink.Utilities`:

```csharp
using Threadlink.Utilities.Mathematics;   // float.IsSimilarTo(b), MoveTowards(target, maxDelta)
using Threadlink.Utilities.Vectors;       // Vector3.IsSimilarTo(b)
using Threadlink.Utilities.Strings;       // string.ToAbsolutePath(), string.ToProjectRelativePath()
using Threadlink.Utilities.UniTask;       // List<UniTask>.AwaitAllThenClear(trim)
using Threadlink.Utilities.Collections;   // IDisposable.PreventEditorMemoryLeaks()
using Threadlink.Utilities.Flags;         // HasFlagUnsafe
using Threadlink.Utilities.Attributes;    // [MinMaxRange], [ReadOnly]
```

`[ReadOnly]` is a marker attribute without an associated drawer; supplying one would displace the hash-map drawer, as attribute drawers take precedence over type drawers. `ThreadlinkHashMapDrawer` reads the attribute from `fieldInfo` and renders the map without addition, removal, or reordering controls.

## E20. Configuration and Project Setup

### E20.1 Configuration Assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`. Must reside at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset, and prefab reference maps; binaries directory. |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector, and definition directories. |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Mixer, fade rate, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | The `[SerializeReference]` persistence environment. |
| **Netflow Config** | `… ▸ Netflow Config` | Netcode flow parameters. |

Additional creation paths: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply the following native resources: `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraComponentsPrefab`, `ChronosConfig`, `NetflowConfig`.

The three reference maps on the User Config are read-only in the Inspector and are owned by the Addressables Mapping Window.

### E20.2 Editor Config Composition

Each entry in the `nativeDomains` array declares a domain name, an output filename, a shell, optional native entries, and three flags:

| Flag | Semantics |
|---|---|
| `ordinalValues` | Dense positional allocation. Applies to `Iris.Events` exclusively. |
| `reserveZero` | The shell declares a sentinel at zero that the allocator must not issue. |
| `sourcedFromAddressables` | Draws injectors from the Addressables injector directory. |

`domainName` is the prefix injector filenames must match. No validation constrains it, so a mistyped name renders the corresponding injector unread. The orphaned-injector diagnostic addresses this case.

### E20.3 Addressable Registration of Native Assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** reads the Native Config and marks every referenced native asset, together with the Native Config itself, as Addressable within the "Threadlink Assets" group, assigning each asset's path as its address.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** realigns addresses that have diverged from their asset paths.

### E20.4 Update Loop Modes

Configured on the User Config:

- **Native** — Threadlink instantiates the hidden `ThreadlinkLoop`, publishing `OnUpdate`, `OnFixedUpdate`, and `OnLateUpdate`.
- **Custom** — Threadlink instantiates nothing. The project publishes those events from its own driver, installed in response to `OnCoreDeployed`.

Custom mode applies where Threadlink renders the view for a simulation owned by another framework.

### E20.5 Scripting Defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline ≥ 1.8.10` | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization ≥ 1.5.9` | `LocalizedText` Vault field, localisation utilities |
| `THREADLINK_SENTINEL_XBOX` | `com.unity.microsoft.gdk ≥ 1.4.5` | XBOX/GDK Sentinel environment and achievements |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps and inspectors |

### E20.6 Binary Authoring

Types implementing `IBinaryAuthor` serialise authoring data to `.bytes` files within the project, subsequently loaded through Addressables and consumed via `IAsyncBinaryConsumer`. **`Threadlink ▸ Clear all Binaries`** empties the `.bytes` files within a selected in-project directory during format iteration.

### E20.7 Diagnostics

**`Threadlink ▸ Registers Tracker`** inspects live register contents at runtime, reporting the objects each `Register`-derived subsystem currently holds.

## E21. Performance Constraints

| Practice | Rationale |
|---|---|
| Cache `Chronos.DeltaTime` into a local once per tick. | Eliminates repeated static property access in hot loops. |
| Construct `ECSFilter` instances in `Boot()` and retain them. | Per-frame allocation is avoidable. |
| Declare `ForEach` callbacks as `static` methods. | The ECS prohibits closures by construction. |
| Log through `Scribe` rather than `Debug.Log`. | ZString composition is allocation-free, and the prefix identifies the source. |
| Use `UniTask` exclusively; avoid `System.Threading.Tasks` and coroutines in framework code. | Mixing violates the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | Retained delegates keep destroyed objects reachable and dispatch to stale state. |
| Confine networked and replayed simulation to `DFP` and `StatelessRNG`. | Hardware floating point and `UnityEngine.Random` are non-deterministic. |
| Prefer `OnLateUpdate` for camera-relative computation. | Dispatch occurs after camera transformation. |

## E22. Engineering Procedures

**Introducing a subsystem**
- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`.

**Introducing an Iris event**
- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document the delegate signature and apply it consistently; mismatches fail silently.

**Introducing an ECS component**
- [ ] Declare an `unmanaged struct : IComponent` implementing `Dispose()`.
- [ ] Apply `[RuntimeComponent]`, and `[Preserve]` for IL2CPP targets.

**Introducing a scene**
- [ ] Map the scene through the Addressables Mapping Window.
- [ ] Implement `ISceneEntry` binding it to its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Provide a listener for `OnActiveSceneRequested` and the four fader and loading-screen `Func` events.
- [ ] Provide a teardown path for scene-placed `IDiscoverable` implementations; Initium boots them but does not discard them.

**Introducing a stacked interface**
- [ ] Construct the prefab with a `CanvasGroup`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

---
---

# Appendices

## Appendix A — Native Iris Events

Values are ordinals allocated in the order listed.

| Event | Delegate | Publisher |
|---|---|---|
| `OnNativeSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment |
| `OnUserSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment |
| `OnCoreDeployed` | `Action<Threadlink>` | Core deployment |
| `OnUpdate` | `Action` | `ThreadlinkLoop` |
| `OnFixedUpdate` | `Action` | `ThreadlinkLoop` |
| `OnLateUpdate` | `Action` | `ThreadlinkLoop` |
| `OnPlaytimeCountTick` | `Action<float>` | Chronos |
| `OnGamePauseRequested` | `Action` | Project code |
| `OnGameResumeRequested` | `Action` | Project code |
| `OnGamePaused` | `Action` | Chronos |
| `OnGameResumed` | `Action` | Chronos |
| `OnInputDeviceChanged` | `Action<Dextra.InputDevice>` | Dextra |
| `OnUICancelled` | `Action` | Dextra |
| `OnUIElementSelected` | `Action<GameObject>` | Dextra |
| `OnInteract` | `Func<bool>` | Dextra |
| `OnInteractableDetected` | `Action<Interactable…>` | Entity detectors |
| `OnInteractableOutOfRange` | `Action<Interactable…>` | Entity detectors |
| `OnActiveSceneRequested` | `Func<Nexus.ISceneEntry>` | Nexus |
| `OnBeforeActiveSceneUnload` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnActiveSceneFinishedUnloading` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnNewSceneFinishedLoading` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnDisplayFaderAsync` | `Func<UniTask>` | Nexus |
| `OnHideFaderAsync` | `Func<UniTask>` | Nexus |
| `OnDisplayLoadingScreenAsync` | `Func<UniTask>` | Nexus |
| `OnHideLoadingScreenAsync` | `Func<UniTask>` | Nexus |
| `OnNexusLoadingFinished` | `Action<Nexus.ISceneEntry>` | Nexus |

## Appendix B — Service Access

| Service | Category | Access |
|---|---|---|
| `Threadlink` | Core (Weaver) | `Threadlink.TryGetSingleton(out var core)` |
| `Iris` | Static | `Iris.Publish(...)` |
| `Nexus` | Static | `Nexus.LoadNewSceneAsync(...)` |
| `Initium` | Static | `Initium.BootAndInitAsync(...)` |
| `Scribe` | Static | `this.Send(...)`, `Scribe.Send<T>(...)` |
| `Sentinel` | Native subsystem | `Sentinel.TryGetSingleton(out var sentinel)` |
| `Chronos` | Native subsystem | `Chronos.TimeScale`, `Chronos.DeltaTime` |
| `Dextra` | Native subsystem | `Dextra.TryGetSingleton(out var dextra)` |
| `Aura` | Native subsystem (Linker) | `Aura.TryGetSingleton(out var aura)` |
| `ECSWorld` | Project subsystem | `ECSWorld.TryGetSingleton(out var world)` |
| `Netflow` | Project subsystem | `Netflow.TryGetSingleton(out var netflow)` |
| `Vault` | Asset | `LinkableAsset` instance |

## Appendix C — Identifier Domains

| Enumeration | Kind | Native entries | Injector | Ownership |
|---|---|---|---|---|
| `ThreadlinkIDs.Iris.Events` | Ordinal | `Iris.Events.Native.txt` | `Iris.Events.User.txt` | Engineering |
| `ThreadlinkIDs.StatelessRNG.Domains` | Identity | — | `StatelessRNG.Domains.User.txt` | Engineering |
| `ThreadlinkIDs.Dextra.InputModes` | Identity | — | `Dextra.InputModes.User.txt` | Design |
| `ThreadlinkIDs.Nexus.SpawnPoints` | Identity | — | `Nexus.SpawnPoints.User.txt` | Design |
| `ThreadlinkIDs.Vault.Fields` | Identity | — | `Vault.Fields.User.txt` | Design |
| `ThreadlinkIDs.Addressables.Scenes` | Identity | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Assets` | Identity | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Prefabs` | Identity | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.NativeResources` | Identity | `Addressables.NativeResources.Native.txt` | — | Framework |

All reside in namespace `Threadlink.Generated`, assembly `Threadlink.Generated`.

## Appendix D — Menu Reference

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Forces a generation pass across every domain. |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers. |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Registers framework assets with the Addressables system. |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Realigns addresses to asset paths. |
| `Threadlink ▸ Clear all Binaries` | Empties `.bytes` files within a selected directory. |
| `Threadlink ▸ Registers Tracker` | Inspects live register contents. |

## Appendix E — Terminology

| Term | Definition |
|---|---|
| **Core** | The `Threadlink` singleton; a Weaver of subsystems. |
| **Subsystem** | A service the core owns and drives through a lifecycle. |
| **Weaving** | Construction and lifecycle ownership of an object. |
| **Linking** | Tracking of an externally-constructed object. |
| **Domain** | A generated enumeration of identifiers. |
| **Injector** | A declaration file appending entries to an existing domain; its name supplies the entries' scope. |
| **Domain definition** | A declaration file declaring an entirely new enumeration. |
| **Shell** | The C# scaffolding into which a generated enumeration is emitted. |
| **Manifest** | The JSON record of a domain's assigned member names and values. |
| **Tombstone** | A removed entry retained as `[Obsolete]` to preserve resolution of existing references. |
| **Scope** | The qualifier folded into an entry's hash key: an injector name or an Addressable group. |
| **Scene entry** | An `ISceneEntry` implementation binding a scene to its load mode and audio scenario. |
| **Discoverable** | A scene component booted automatically by Initium. |
| **DFP** | Deterministic fixed-point numeric type. |

*Threadlink Framework — Reference Manual. Developed and maintained by Threadforge.*

*Lead Developer: George Rontoulis*