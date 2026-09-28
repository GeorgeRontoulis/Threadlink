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
| **Part III — Engineer Reference** | Programmers | Architecture, deployment pipeline, subsystem APIs, PlayerLoop integration, Sentinel platform services, deterministic tooling, code generation internals, custom subsystems, and performance. |

Both volumes describe the same systems from opposing ends. Where the designer volume specifies *"declare the identifier in `Vault.Fields.User.txt`"*, the engineer volume specifies the domain pipeline that emits `ThreadlinkIDs.Vault.Fields`, the manifest that stabilises its value, and the runtime API that consumes it.

> All API names, identifiers, menu paths, file paths, and asset-creation entries in this manual are drawn from framework source. Systems that are stubbed or under development are identified as such.

---
---

# PART I — Shared Foundations

## 1. Purpose

Threadlink is a modular runtime framework layered over Unity, providing a unified backbone for games and interactive applications. It supplies:

- A self-deploying core that needs no scene-placed initialiser. Its one scene requirement is a bootstrap scene: the build's entry point, which stays loaded while Nexus loads every other scene through Addressables (§3).
- Native subsystems covering event dispatch, time, scene management, input and UI, audio, and cross-platform platform services such as accounts, saves, achievements, and social/session affordances.
- A type-safe identifier system in which scenes, assets, prefabs, events, input modes, spawn points, RNG domains, and data fields are C# enumerations generated from plain-text declarations, eliminating string-keyed lookup.
- The **Vault**, a polymorphic data container for designer-authored game data.
- A deterministic arithmetic and stateless RNG toolkit for reproducible simulation, replay-sensitive logic, and procedural generation.

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
├── Deterministic/              ← Fixed point (FP, its vectors and rotations) and StatelessRNG
├── Editor/                     ← Domain code generation, Addressables tooling, formatter, inspectors
├── Generated/                  ← Generated output: ThreadlinkIDs enumerations and manifests
└── Plugins/                    ← SerializedReferenceInspector and bundled integrations

Threadlink User/                ← Project territory.
├── Design/
│   └── Custom Domain Definitions/ ← Project-defined identifier domains
├── Generated Addressables Injectors/ ← Mapping Window output; not hand-edited
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
    ├── Configs/                ← Configuration assets
    └── Binaries/               ← Editor-authored binary data
```

The `Threadlink/` directory constitutes the framework and is not hand-edited, `Threadlink/Generated/` included: that directory is owned exclusively by the code generator. All project authoring occurs under `Threadlink User/`.

### 2.1 Assembly Topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | Zero references; `noEngineReferences: true`. Referenceable from any assembly, including a deterministic simulation assembly. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers | — |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities (including `FixedPointConversions`, between Unity's float types and the deterministic ones) | `allowUnsafeCode: true` |
| `Threadlink.Deterministic` | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, `StatelessRNG` | `noEngineReferences: true` |
| `Threadlink.Editor` | Code generation and editor tooling | Editor-only |
| `Threadlink.User` | Project code | — |
| `Threadlink.User.Generated` | Project-defined domain enumerations | Zero references |

Generated identifiers reside in namespace `Threadlink.Generated`; consuming code requires `using Threadlink.Generated;`. Isolating generated output in dedicated assemblies guarantees that neither project authoring nor third-party module installation produces a write within framework source.

## 3. Deployment Overview

The core deploys itself through Unity's `[RuntimeInitializeOnLoadMethod]` hooks, so no scene-placed initialiser is required. A **bootstrap scene** is:

- **Unity needs a scene as the entry point.** A player starts from the first scene in Build Settings.
- **An Addressable scene can't be that scene.** Unity keeps Addressable scenes out of the build's scene list, and Addressables only initialise once the first scene has loaded (step 2).
- **So every project needs one non-Addressable scene**, first and ideally only in Build Settings. That is the bootstrap scene. It stays loaded for the whole run, as the persistent scene every other scene loads beside (§E8), and every other scene is Addressable and loaded through Nexus.

Setting it up is covered in §E18.1. The deployment:

1. Assemblies load. Native and project subsystem factories register, and both tiers subscribe to their respective registration events.
2. Once the bootstrap scene has loaded, the core initialises Addressables, loads the **Native Config**, and loads the **User Config** referenced by it.
3. The core constructs itself, then registers and boots native subsystems followed by project subsystems.
4. The core publishes `OnCoreDeployed`.
5. Discoverable objects present in the bootstrap scene are booted.

Deployment requires two assets addressable through the Addressables system:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

Configuration is detailed in **Part III, §E18**.

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

Zones are linked automatically when their scene becomes the presented scene and disconnected when it is replaced or unloaded; placement is the only required action. Only the presented scene is audible, so zones in other loaded scenes (for example, scenes kept loaded for other players in a multiplayer session) have no effect. Zone names must be unique within a scene: a zone sharing another zone's name is not linked, and a warning is logged.

### D4.3 Per-Scene Audio

Each scene declares music and ambience tracks with target volumes through a scene entry implemented by engineering. Track selection and volume levels are design decisions and should be communicated as part of the scene specification.

## D5. Input Prompts

Threadlink resolves the correct button glyph for the active input device and substitutes it automatically on device change.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Configure the control it represents.

Inside one of Dextra's interfaces, that is all. Anywhere else, an engineer's component must boot and discard it.

The control-and-device to sprite mapping resides on the Dextra Config asset. Populating it is shared work: designers supply and assign sprites, engineers configure control paths. Sprites are Addressable assets; see §D7.

## D6. Configuration Assets

| Asset | Designer-tunable fields |
|---|---|
| **Aura Config** | Volume fade speed; navigation, confirm, and cancel clips. |
| **Dextra Config** | Input-icon sprite assignments; interface list, in conjunction with engineering. |
| **Chronos Config** | Iris Physics Update. **Engineering-owned; do not modify.** |
| **Sentinel Config** | Target distribution where a platform permits multiple storefront/ecosystem choices. **Engineering-owned; do not modify without coordination.** |

The physics toggle alters the simulation model for the entire application. Values of uncertain ownership should be confirmed with engineering before modification.

## D7. Registering Scenes, Prefabs, and Assets

Runtime-loaded content — scenes, prefabs, audio clips, sprites — is referenced through Addressables and surfaced as a dropdown identifier. Registration is performed through a dedicated tool.

Every scene is Addressable except the bootstrap scene, which is the only one in Build Settings (§3). A new scene therefore never goes into Build Settings.

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

> This volume assumes C# proficiency and familiarity with Unity, Addressables, and asynchronous programming. Threadlink uses **UniTask** exclusively in place of `System.Threading.Tasks`, relies on `[RuntimeInitializeOnLoadMethod]`, generic constraints, generated identifier domains, and direct Unity PlayerLoop integration.

## E1. Architecture

### E1.1 Subsystems and Static Services

Threadlink distinguishes two categories of framework service.

**Woven subsystems** are instances the core constructs, owns, and drives through a lifecycle. They derive from `ThreadlinkSubsystem<T>` and are accessed via `T.TryGetSingleton(out var instance)`. The native set, in weave order:

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
| `Nexus` | Scene loading, unloading, transition sequencing |
| `Initium` | Preload, boot, and initialise pipeline |
| `Scribe` | Logging |

Additional project systems are registered as woven user subsystems through `WeavingFactory.User.cs` and `Subsystems.User.cs`.

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
| `IDiscoverable` | Marker | Scene-placed `LinkableBehaviour` implementations are booted automatically on scene load, and discarded before the scene unloads, wherever they sit in it. Inactive objects are not booted. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Dependency injection point. |

Boot and initialise execute as batches separated by a single-frame yield. Ordering within a batch is not guaranteed.

### E1.4 Base Object Types

| Type | Domain | Discard behaviour |
|---|---|---|
| `LinkableBehaviour` | Scene components | Destroys the GameObject |
| `LinkableAsset` | ScriptableObjects, including Vault | Nulls fields; does not destroy |

Both expose `ID`, `Name`, and an `OnDiscard` event. Cleanup is implemented by overriding `Discard()`. `OnDestroy()` executes outside framework ordering and is unsuitable for Iris unsubscription.

`Discard()` is called by whoever owns the object, never by the object itself, and never from an Iris event:

- **Nexus**, through `Initium.DiscardSceneObjects`, for the discoverables placed in a scene it unloads (§E3, §E8).
- **The subsystem that created an object**, in its own `Discard()`. Dextra discards its interfaces this way.
- **An interface**, for the selectables and input icons in it.

The framework boots, initialises and discards `IDiscoverable`s: every one it finds, and nothing else. How objects nest is the developer's concern. Either don't nest discoverables, or know how they nest and manage what that implies. Whatever isn't discoverable, the developer boots and discards.

The framework's own object types (`AuraSpatialObject`, `Interactable`, `EntityDetector`, `DextraInputIcon`, the selectables) are never discoverable. Gameplay decides how they are mixed and controlled, so the developer manages them: from the object that owns them, or through a discoverable type of their own.

A subsystem that links objects (a `Linker`) does not tear its links down on a scene event. Each linked object unlinks itself in `Discard()`, as `AuraSpatialObject` does, and its owner calls that in time.

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
   - `Boot()` installs `ThreadlinkPlayerLoop` into Unity's current `PlayerLoop` when the update loop is configured as `Native`; no scene object or `MonoBehaviour` is created.
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

The input is materialised once, so every phase operates on the same objects: an object created during `Boot` is not swept into the `Initialize` phase without having been booted. All preloaders in the first phase are awaited together. If any preloader returns `false`, `PreloadBootAndInitAsync` throws `InvalidOperationException` naming the failed preloader and does not proceed to Boot/Initialize. Boot and Initialize remain batch-oriented and their intra-phase object ordering is not guaranteed.

At deployment, scene objects are discovered across every loaded scene via `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`.

Individual scenes are handled by a public pair:

| Member | Semantics |
|---|---|
| `Initium.BootAndInitSceneObjectsAsync(Scene)` | Preloads, boots and initialises the active `IDiscoverable` objects placed in the scene. |
| `Initium.DiscardSceneObjects(Scene)` | Discards every `IDiscoverable` placed in the scene, active or not, children before parents. Must run before the scene unloads. |

Scenes held and presented through Nexus (§E8) are processed automatically, whether one scene is resident or many. Scenes loaded by other means must call both members themselves.

Unity destroys scene objects without invoking `Discard()`, so a scene must never unload any other way: every Nexus unload discards its discoverables first. Scene objects release in `Discard` what they registered in `Boot`, and never discard themselves. Nor does any subsystem tear them down on a scene event: an object registered with a subsystem unregisters itself when Nexus discards it (§E1.4).

## E4. Iris — Event Dispatch

`Iris` is a static class backed by `object[] EventRegistry`, dimensioned at type load to span the highest value of `ThreadlinkIDs.Iris.Events` (values can have gaps, §E16.1). Each slot holds a `DelegateList<T>` allocated on first subscription.

### E4.1 Dispatch Signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        — single listener
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> — single listener
```

Subscription and unsubscription state the delegate type explicitly:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
```

Diagnostic members: `TryGetListenerCount`, `ContainsListener<T>`, `Clear`.

### E4.2 Constraints

- **The delegate type constitutes the contract and is not enforced across subscribers at compile time.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type-mismatch error and discards the subscription. Publishing under a mismatched type is a no-op. Signature agreement remains a project discipline.
- **`Func` events throw `InvalidOperationException` beyond a single listener.** They model a single provider rather than a broadcast.
- **Dispatch iterates in reverse** (`Count - 1` to `0`), and `DelegateList.Remove` performs swap-with-last. A handler removing itself during dispatch is safe; a handler removing a different listener is not.
- **Unsubscription belongs in `Discard()`.** A retained delegate keeps a destroyed object reachable and dispatches to stale state.
- Dispatch to an event without listeners is a no-op.

### E4.3 Update Events

When the update loop is configured as `Native`, `ThreadlinkPlayerLoop` injects three marker systems into Unity's **current** `PlayerLoop` and publishes the Iris events directly; no `GameObject` or `MonoBehaviour` exists.

| Iris event | Native PlayerLoop position |
|---|---|
| `OnUpdate` | Immediately **before** `Update.ScriptRunBehaviourUpdate` |
| `OnFixedUpdate` | Immediately **after** `FixedUpdate.ScriptRunBehaviourFixedUpdate` |
| `OnLateUpdate` | Immediately **after** `PreLateUpdate.ScriptRunBehaviourLateUpdate` |

Installation removes stale Threadlink markers first, then fails fast if any expected Unity anchor cannot be found. The loop is uninstalled when the core is discarded or the application quits, and an entering-play-mode reset protects against stale PlayerLoop state when Domain Reload is disabled. `Custom` mode installs nothing; the project is responsible for publishing the three Iris update events.

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

`Nexus` is a static class with one scene workflow, for a game with one scene at a time or many at once:

- **Residency:** a scene is loaded while anything holds it.
- **Presentation:** the resident scene the local player sees and hears.
- **Transitions:** presentation behind the fader and loading screen.

Every scene loads additively beside the bootstrap scene (§3). The bootstrap scene stays loaded throughout, and Nexus never loads or unloads it.

### E8.1 The `ISceneEntry` Contract

```csharp
public interface ISceneEntry
{
    ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
    ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
    ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
    float MusicVolume { get; }
    float AtmosVolume { get; }
    LocalPhysicsMode PhysicsMode { get; }   // default: LocalPhysicsMode.None
    float UnloadDelay { get; }              // default: 0 seconds

    UniTask OnFinishedLoadingAsync();       // default: completed
    UniTask OnBeforeUnloadedAsync();        // default: completed
}
```

The interface may be implemented on any type: a ScriptableObject, a struct, or an asset type belonging to another framework. Every member after the volumes has a default implementation.

- **`PhysicsMode`:** `LocalPhysicsMode.None` shares the default physics scene. A scene resident alongside others should use its own (`LocalPhysicsMode.Physics2D` or `Physics3D`). Query it through `scene.GetPhysicsScene2D()` / `GetPhysicsScene()` and step it explicitly, since Unity does not simulate local physics scenes.
- **`UnloadDelay`:** how long the scene stays resident once nothing holds it. A hold within that time keeps it without reloading.

### E8.2 Residency: Holds

A scene is resident while anything holds it:

```csharp
Nexus.SceneHold farm = await Nexus.HoldAsync(farmEntry);   // resident and booted; null if it failed to load
Scene scene = farm.Scene;

farm.Release();                                            // unloads once nothing holds it, after entry.UnloadDelay
farm.Release(delaySeconds: 10f);                           // or after this long, unless it is held again meanwhile
```

- **Loading.** A scene loads once, boots its discoverables through Initium, and awaits `OnFinishedLoadingAsync`. Only then does any holder get it, and `OnSceneResident` is published. Holders arriving during the load share it.
- **Unloading**, whatever the reason, follows one path. It awaits `OnBeforeUnloadedAsync`, publishes `OnSceneUnloading`, and discards every `IDiscoverable` in the scene through `Initium.DiscardSceneObjects`, children before parents. Only then does it unload the scene. Scene objects therefore unregister in `Discard` whatever they registered in `Boot`, and never discard themselves.
- **Ordering.** Operations on one scene never overlap. A hold during an unload waits for it, then loads the scene again. A hold during a delayed unload cancels the unload.
- **Queries.** `Nexus.IsResident(entry)` and `Nexus.TryGetResidentScene(entry, out scene)` answer only for a scene that is loaded, booted and not unloading.

Release each hold exactly once, when its holder no longer needs the scene. Releasing it again does nothing. Residency changes nothing the player sees or hears.

### E8.3 Presentation and Transitions

```csharp
await Nexus.PresentAsync(entry);      // hold, activate, audio, OnScenePresented, then release the previous scene
await Nexus.TransitionAsync(entry);   // the same behind the fader and loading screen: the single-scene workflow
await Nexus.StopPresentingAsync();    // present nothing, releasing the presented scene

Nexus.ISceneEntry presented = Nexus.Presented;
Nexus.TryGetPresentedScene(out Scene scene);
```

- **Presenting holds the presented scene,** loading it if needed. It makes it the active scene, transitions to its audio scenario, and publishes `OnScenePresented`. Then it releases its hold on the scene presented before. So a scene held only by presentation unloads when something else is presented, while a scene another system holds stays resident.
- **Presentations run one at a time,** in call order.
- **`TransitionAsync`** wraps `PresentAsync` in `FadeToLoadingScreenAsync` and `FadeToGameplayAsync`, which drive four `Func<UniTask>` events, `OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync` and `OnHideLoadingScreenAsync`, plus Aura's listener volume fade. Each event requires exactly one provider.

A single-scene game needs nothing else:

```csharp
await Nexus.TransitionAsync(townEntry);   // the previous scene unloads, its objects discarded
```

A game with several resident scenes, such as a multiplayer host simulating every occupied area while presenting its own player's, holds the areas it simulates and presents one:

```csharp
var area = await Nexus.HoldAsync(entry);   // simulated: no visible or audible change
await Nexus.TransitionAsync(entry);        // the local view moves to it; it stays held by both
area.Release(gracePeriod);                 // later, once no one needs it
```

Objects booting in a resident scene must not assume it is the active scene. Objects instantiated at runtime must be placed in their scene explicitly. Hiding scenes that are resident but not presented (renderers, lights, canvases) is the project's rendering policy: `OnSceneLoaded` gives it the moment before a scene's first frame.

### E8.4 Events

| Event | Delegate | Raised |
|---|---|---|
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` (`Scene`, `Mode`) | Synchronously as Unity activates a scene: before it renders, and before its discoverables boot. |
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Once a scene has loaded, booted and run `OnFinishedLoadingAsync`, before any holder gets it. |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Once the presented scene is active and its audio transition has run. |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Before any unload, while the scene's objects are still there, undiscarded. For reading them, such as saving their state; their own `Discard` is their teardown. |
| `OnSceneUnloaded` | `Action<Scene>` | Once a scene has unloaded. The `Scene` is only a key by then. |

`OnSceneLoaded` and `OnSceneUnloaded` republish Unity's own, however a scene was loaded, so project code never subscribes to `SceneManager`.

### E8.5 Shutdown

When the application quits, which in the Editor is when Play Mode exits, Threadlink's shutdown first releases every hold. Nothing is presented any more, and each held scene has its objects discarded while the subsystems they registered with still exist. Then the subsystems are discarded (§E1). Unity finishes the unloads that quitting cuts short.

## E9. Dextra — Input and UI

### E9.1 Device Resolution

`Dextra.InputDevice` enumerates `MouseAndKeyboard`, `Xbox`, `PlayStation`, and `Switch`. `CurrentInputDevice` updates automatically and publishes `OnInputDeviceChanged`. `TryGetInputIcon(device, controlPath, out Sprite)` resolves the corresponding glyph.

A `DextraInputIcon` is a bootable `LinkableBehaviour`, not a discoverable one. It shows the glyph of its control from `Boot` until it is discarded:

- In one of Dextra's interfaces, the interface boots it and discards it.
- Anywhere else, the developer's code that owns it does.

### E9.2 Input Modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The mode-to-`InputActionReference` mapping resides on the Dextra Config as a `FieldHashMap`.

### E9.3 The UI Stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and require a `CanvasGroup`. They are not discovered by Initium: Dextra instantiates them from `DextraConfig.interfacePointers` — Addressable prefab identifiers — marks them `DontDestroyOnLoad`, forces alpha to zero, and boots them. Dextra discards them in its own `Discard`. Each interface discards its selectables, and boots and discards its input icons.

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

`Interactable` derives from `LinkableBehaviour` and carries an `InteractableConfig`. Like every framework type it isn't discoverable: the developer's code discards it, which stops it answering `OnInteract`. `EntityDetector2D` and `EntityDetector3D` are trigger-collider components publishing `OnInteractableDetected` and `OnInteractableOutOfRange`. On detection, an interactable subscribes its `Interact` method as `Func<bool>` to `OnInteract`.

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

Aura links the `AuraSpatialObject`s (including `AuraZone`s) of the **presented** scene only: at deployment, and on every `OnScenePresented`, it disconnects all spatial objects and links those in the active scene. Spatial objects of other resident scenes are never linked, so a scene kept loaded for another player is inaudible.

Aura never tears its links down on a scene event. A spatial object unlinks itself when discarded. It isn't discoverable, so the developer's code that owns it discards it before its scene unloads; otherwise Aura keeps evaluating a destroyed object until the next presentation relinks. Aura's own `Discard` disconnects whatever is left, such as the persistent scene's.

The registry is keyed by `LinkableBehaviour.ID`, a hash of the object's name. Spatial object names must therefore be unique within a scene: a colliding object is not linked, and a warning names both objects.

## E11. Sentinel — Platform Services

Sentinel is Threadlink's cross-platform platform-service abstraction. Unity determines the hardware/platform; `SentinelConfig` only selects a **distribution** where that platform is ambiguous (for example Windows: Local, Steam, Microsoft Store, Epic, or GOG). Runtime modules register exact `SentinelPlatformMarker` / `SentinelDistribution` pairs and expose a capability-driven service graph.

### E11.1 Deployment

Sentinel deploys during the preloading phase and moves through:

`Uninitialized → ResolvingPlatform → ResolvingModule → InitializingPlatform → Ready`

or `Failed`.

```csharp
if (Sentinel.TryGetSingleton(out var sentinel))
{
    bool ready = sentinel.State is Sentinel.DeploymentState.Ready;
    SentinelPlatformMarker platform = sentinel.ActivePlatform;
    SentinelDistribution distribution = sentinel.ActiveDistribution;
    SentinelCapability capabilities = sentinel.Capabilities;
}
```

`SentinelModuleRegistry` requires an exact platform/distribution match. If no installed module implements the resolved key, deployment fails with `SentinelError.Unsupported`. `SentinelResult` / `SentinelResult<T>` carry `Succeeded`, `Error`, `Message`, and an optional native error code rather than relying on exceptions for normal service failures.

**A failed platform does not abort Threadlink.** Only a missing `SentinelConfig` fails Sentinel's preloader. Every platform-level failure leaves Sentinel in `Failed`, logs `InitializationResult` as an error during `Boot`, and lets deployment continue with no platform services: for example, Steam not running, a missing module, or a native initialisation error. `TryGetService` and `HasCapability` return `false` in that state. Whether a failed platform is acceptable (degraded play, an error screen, quitting) is the project's decision, made by checking `State` once deployment completes.

**Retrying, losing the platform, and state changes.**
- `RetryAsync()` deploys the platform again while Sentinel is `Failed`, for example after the player has started Steam, and returns whether it is `Ready`.
- A platform that goes away after initializing reports it: a module calls `SentinelPlatform.ReportLost(result)`, as the Steam module does on `SteamShutdown_t`.
  - Sentinel then moves to `Failed` with `SentinelError.PlatformLost`.
  - It publishes the state change at once, so code using the platform can release it synchronously.
  - It discards the platform one frame later, after the reporting callback has returned.
- Every time a deployment settles (`Ready` or `Failed`), Sentinel publishes the native Iris event `OnSentinelStateChanged` (`Action<Sentinel>`): after the first deployment, after a retry, and on a loss.
- Sentinel deploys before any subsystem initializes, so a subscriber reads `State` once when it subscribes, then follows the event.
- In the Editor and development builds, `SimulatePlatformLoss()` runs the loss path, for tests.

#### Distribution Override

`SentinelDistributionOverride` replaces the configured distribution at runtime. It is intended for instances that must not deploy the configured ecosystem, such as several development instances on one machine sharing a single Steam account. Sources, in order of precedence:

| Source | Availability |
|---|---|
| `SentinelDistributionOverride.Requested`, assigned by project code before deployment (for example at `BeforeSceneLoad`) | Always |
| Command-line argument `-threadlink-sentinel-distribution <Distribution>` | Editor and development builds |
| Environment variable `THREADLINK_SENTINEL_DISTRIBUTION` | Editor and development builds |

Values are case-insensitive `SentinelDistribution` names (for example `Local`). An override must still be allowed for the active platform by `SentinelDistributionPolicy`; a disallowed or unknown value is ignored with a warning. An applied override is logged. `Requested` is reset on entering Play Mode.

### E11.2 Capabilities and Services

Capability flags include accounts/account selection, local/cloud saves and save transactions, achievements/progress, input ownership, statistics, leaderboards, presence, friends, entitlements, commerce, and invites. A platform module advertises only what it actually implements.

Top-level services are obtained from `Sentinel`; account-scoped services are obtained from the resolved account:

```csharp
if (Sentinel.TryGetSingleton(out var sentinel)
    && sentinel.TryGetService<IAccountService>(out var accounts))
{
    var accountResult = await accounts.GetPrimaryAccountAsync();

    if (accountResult.Succeeded
        && accountResult.Value.TryGetService<ISaveService>(out var saves))
    {
        var readResult = await saves.ReadAsync(saveID, fileID);
    }
}
```

`IAccountService` exposes the account list, primary account, refresh/picker operations, and account lifecycle events. `AccountStateChanged` reports an account's `State` changing, for example between `SignedIn` and `Suspended` as a platform loses and regains its servers. `ISentinelAccount` is itself a service provider, allowing platform-specific account-scoped services such as saves, achievements, and multiplayer/social affordances.

### E11.3 Transactional Saving

`ISaveService` operates exclusively on byte arrays:

```csharp
var transactionResult = await saves.BeginTransactionAsync(saveID);

if (transactionResult.Succeeded)
{
    var transaction = transactionResult.Value;

    var write = await transaction.WriteAsync(fileID, bytes);

    if (write.Succeeded)
        await transaction.CommitAsync();

    transaction.Discard();
}
```

A transaction prepares a complete logical save while leaving the currently published generation untouched. `CommitAsync()` atomically publishes the new generation; failed or abandoned transactions leave the previous published state intact. `DeleteSaveAsync(saveID)` provides the same atomic publication guarantee for logical deletion.

Serialisation remains the caller's responsibility. `Threadlink.TrySerialize<T>` and `TryDeserialize<T>` provide MessagePack wrappers, but Sentinel itself stores opaque bytes.

### E11.4 Shipped Modules

| Module | Current capabilities |
|---|---|
| **Local** | One implicit local account; local saves; save transactions; achievements and progress |
| **Steam** | Steam account; local saves; save transactions; achievements/progress; presence, friends and invites; cloud saves when Steam Cloud is enabled |

The distribution policy declares additional target ecosystems, but a target is usable only when a matching runtime module is installed and registered. Sentinel provides platform-native social/session affordances; it is not a transport or game-state networking layer.

**Steam specifics:**
- **The account follows the client's connection to Steam's servers.** It is `SignedIn` when connected and `Suspended` in offline mode or without a connection, and `AccountStateChanged` reports each change.
- **The Steam client shutting down** reports the platform lost.
- **App ID:** the project-root `steam_appid.txt` is the one App ID source.
  - Development builds get a copy beside the executable.
  - Release builds ship without it, as Valve requires, and get the App ID in `<Data>/threadlink_steam_appid.txt` instead: a file the Steam client never reads.
  - With it, a release build started outside Steam calls `SteamAPI.RestartAppIfNecessary` and Steam relaunches it.
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
await core.LoadSceneAsync(id, new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D));
await core.UnloadSceneAsync(id);

core.ReleaseAsset(id);
core.ReleasePrefab(id);
```

Query and validation:

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes id, out Scene scene);
core.CheckIDValidity(id);
```

All lookups resolve through the User Config's keyed maps and validate `RuntimeKeyIsValid()`, reporting failure through Scribe.

Scene loads are "load or get": loading a scene that is already loaded (or loading) returns that instance, whatever parameters are passed. `UnloadSceneAsync` completes once the scene has actually unloaded. A failed scene load or unload is reported through Scribe and returns a default `SceneInstance` instead of throwing.

`SceneAssetReference` tracks its load through `SceneOperation` rather than `AssetReference.OperationHandle`, which Addressables only populates for `LoadSceneMode` loads. `LoadSceneAsync(LoadSceneParameters)`, `UnloadSceneAsync(bool autoReleaseHandle)` and `TryGetLoadedScene(out Scene)` extend the base API. The base `LoadSceneAsync(LoadSceneMode)`, `UnLoadScene()` and `ReleaseAsset()` are routed through the same operation.

The `AssetReference` overloads exist for portable modules: a module may hold its own references without depending on the consuming project's generated `Assets` enumeration.

For framework-owned resource batches, `ThreadlinkNativeConfig.LoadNativeResourcesAsync` accepts an `AddressablesRequest<NativeResources>` and fills a caller-provided dictionary. The request uses a fixed-capacity `UnsafeList<T>` and the loader batches Addressables operations through pooled task storage before collecting results.

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


## E15. Deterministic Toolkit

Everything a simulation needs to give the same result on every machine, runtime and compiler: numbers, vectors and rotations in fixed point, and the framework's one source of randomness. Networked simulation, lockstep, replays and procedural generation build on it. The assembly references no engine type.

### E15.1 `FP`

A Q32.32 fixed-point number: 32 integer and 32 fractional bits in a `long`, for a range of about ±2.1 billion at a uniform resolution of 2^-32 (about 2.3e-10). Every operation is integer arithmetic.

- **Exact arithmetic.** `+`, `-`, `*`, `/` and `%` give the true result, rounded to nearest with ties away from zero, so results are symmetric in sign: `(-a) * b == -(a * b)`.
- **Saturation.** Results beyond the range stop at `MaxValue` or `MinValue` instead of wrapping to the opposite sign.
- **Nothing throws.** Dividing by zero saturates toward the dividend's sign (zero over zero is zero). Each function states its value outside its domain: `Sqrt` of a negative value is zero, and `Log` of zero is `MinValue`.
- **Functions.** `Sqrt` is exact: the true root, rounded to nearest. `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `Exp`, `Log`, `Log2` and `Pow` are evaluated in Q3.61 and rounded once, to within about one raw unit of the true value. Trigonometric arguments are reduced exactly, so `Sin(1e6)` is as accurate as `Sin(1)`.
- **Rounding.** `Floor`, `Ceiling`, `Round` (halves away from zero), `Truncate`, `Frac`, and `FloorToInt`, `CeilToInt` and `RoundToInt`.
- **Helpers.** `Abs`, `Sign`, `Min`, `Max`, `Clamp`, `Clamp01`, `Lerp`, `LerpUnclamped`, `InverseLerp` and `MoveTowards`.

```csharp
FP speed = 3;                           // integers convert implicitly and exactly
FP chance = FP.FromFraction(3, 20);     // exactly the nearest value to 0.15
FP friction = (FP)0.92;                 // a literal, converted once: deterministic
FP angle = FP.Atan2(dy, dx) * FP.Rad2Deg;
```

Converting a literal or an authored value (a serialized field) is deterministic, since its bits are the same everywhere. A float computed at runtime is not, so it never flows into simulated state. Serialize and hash `RawValue`, never a converted float. `(float)value` is for presentation.

### E15.2 Vectors and Rotations

`FPVector2`, `FPVector3` and `FPQuaternion` are mutable structs of `FP` components, with the usual operators.

- **Exact lengths.** `Magnitude` is the true length of the raw components (a 128-bit sum of squares and an exact root), rounded once. `Normalized` scales the components exactly before measuring, so a vector one raw unit long normalizes as well as one a million units long.
- **`FPVector2`.** `Dot`, `Cross`, `Distance`, `Lerp`, `MoveTowards`, `ClampMagnitude`, `Perpendicular`, `Rotate`, `FromAngle`, `Angle` and `SignedAngle`.
- **`FPVector3`.** `Dot`, `Cross`, `Distance`, `Lerp`, `MoveTowards`, `ClampMagnitude`, `Angle`, `Project` and `ProjectOnPlane`. Axes follow Unity: y up, z forward.
- **`FPQuaternion`.** Unit rotations: `FromAxisAngle`, composition with `*`, `Rotate` (or `rotation * vector`), `Conjugate`, `Inverse`, `Lerp` (normalized), `Slerp` and `Angle`.

`FixedPointConversions`, in `Threadlink.Runtime`, converts between these and Unity's types: `vector.ToFP()` for authored data, and `position.ToVector3()` for presentation.

### E15.3 `StatelessRNG`

The one source of random numbers for gameplay, simulation, procedural generation and presentation alike. A **stream** is a pure function of the world's seed, a **domain** (what the numbers are for) and **identity parts** (whose, and when: an entity, a tick, a map cell), folded in order. The same inputs give the same numbers on every machine, in any order and on any thread, however many draws happen elsewhere. Nothing is shared or advanced globally.

```csharp
StatelessRNG.SetSeed(worldSeed);   // once per world; the world's save keeps it

var loot = StatelessRNG.CreateStream(ThreadlinkIDs.StatelessRNG.Domains.Loot, chestID, day);

int gold = loot.Range(10, 50);                    // [10, 50), every value equally likely
bool rare = loot.Chance(FP.FromFraction(1, 20));  // exact to the raw unit
bool coin = loot.Chance(1, 2);                    // integer odds
FP scale = loot.Range(FP.Half, (FP)1.5);
int pick = loot.WeightedIndex(weights);           // in proportion to the weights
loot.Shuffle(deck);                               // Fisher–Yates, every order equally likely
FPVector2 scatter = loot.InsideUnitCircle();

var item = loot.Fork(itemIndex);                  // a finer identity: independent of loot's own draws
```

- **Streams.** Each draw advances the stream copy it is made on. Create streams where you roll, one per purpose, entity and tick, so a result never depends on how many draws came before it. Two streams created from the same identity give the same numbers.
- **Identity.** Parts fold in order through SplitMix64's finalizer, with each part mixed on its own first. Order matters, equal parts don't cancel, a zero seed with zero parts isn't degenerate, and a domain's whole 32-bit id counts. `CreateStream` takes up to three parts, a span of any number, or an `IContext`, a readonly struct that adds its parts in a fixed order. `StatelessRNG.Part(text)` turns a string into a part; hash fixed strings once.
- **Samplers.** Every sampler is unbiased (Lemire's method, redrawing the rare biased case), and makes the same draws whatever its arguments' values: a probability of zero advances the stream as much as any other.
- **Not for secrets or uniqueness.** Session tokens and nonces come from `System.Security.Cryptography.RandomNumberGenerator`, and unique ids from `Guid`.

Domains partition streams: systems drawing from one seed under distinct domains do not interfere. New domains are declared in `StatelessRNG.Domains.User.txt`.

> Domain values are name hashes. Renaming a domain alters its stream, causing divergence in anything reproducing a prior sequence from a stored seed or replay. Domain names constitute part of the save format, as does the stream format itself, which the Edit Mode tests pin with golden values.


## E16. Identifier Domains and Code Generation

A single pipeline produces every enumeration under `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

### E16.1 Domain Kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | `xxHash32` of the scope-qualified key | Retained as `[Obsolete]` tombstone with value preserved | All domains except Iris |
| **Ordinal** | Index allocated in source order and kept by the manifest; removing an entry leaves a gap | Removed outright | `Iris.Events` |

Identity is the default kind, guaranteeing that removal cannot shift another entry's value. Iris is ordinal because dispatch indexes `EventRegistry` with the value directly; the registry spans the highest value, so the gaps removals leave are harmless. As Iris values are never serialised, outright removal producing compilation failure is the correct failure mode.

### E16.2 Sources

Domain entries originate from up to three sources, merged in order:

1. **Native entries** — a framework-owned `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`).
2. **Injectors** — files named `{DomainName}.{Injector}.txt` within the injector directory. The injector name becomes the **scope**, folded into each entry's hash key. `Iris.Events.User.txt` is the injector named `User`.
3. **Domain definitions** — a `.txt` within the definitions directory declares an entirely new enumeration named after the file, emitted through the shared `CustomDomain.Shell.txt` into namespace `Threadlink.User`.

Injectors may extend domain definitions on identical terms to native domains, permitting a third-party module to supply content for a project-defined domain.

Modules use this mechanism. A module requiring its own Iris events ships `Iris.Events.MyModule.txt`; placing it in the injector directory constitutes the entire installation procedure. Module entries are appended after project entries and cannot displace them.

> A module's injector filename forms part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` alters the scope and therefore every identity-domain value that injector contributes.

### E16.3 Manifests

Each domain maintains a `{DomainName}.manifest.json` adjacent to its generated script, recording every entry's key, member name, scope, and value. Manifests are version-controlled artefacts and establish identity stability:

- An assigned member name survives regeneration unchanged.
- Name collisions resolve deterministically: a second entry claiming `Splash` is qualified as `GroupName_Splash`, and the incumbent is never displaced.
- Removed entries are flagged as tombstoned rather than discarded.
- Hash collisions are detected and rehashed under a recorded seed offset, rendering the resolution reproducible.

Each pass emits an addition, removal, rename, rescope, and collision summary through Scribe.

### E16.4 Shells

A **shell** supplies the C# scaffolding for a generated file — namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` substitution token. `CustomDomain.Shell.txt` carries an additional `{DOMAIN_NAME}` token, permitting one template to serve every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator is configured to reserve those values. Shells do not declare generated entries.

### E16.5 Generation Triggers and Guards

An `AssetPostprocessor` monitors every native-entries file and all three directories, regenerating on any `.txt` modification. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a pass.

The pipeline enforces:

- Output confinement to the configured generated directories, preventing a misconfigured domain from overwriting hand-authored source.
- Abort on two domains resolving to the same output path.
- Diagnostic reporting, by filename, of any injector whose prefix matches no declared domain.
- Rejection of injectors carrying more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the sole accepted form.
- Emission of a sentinel-only enumeration, with warning, for a domain yielding zero entries.

### E16.6 Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** enumerates every writable Addressable group with its member assets and a per-asset selection toggle. **Apply** performs:

1. Emission of one injector per group per reference kind — `Addressables.Assets.{Group}.txt` and equivalents — into the Addressables injector directory.
2. Regeneration of the three Addressables domains.
3. Reconstruction of the User Config reference maps from the resulting manifests.

Group names are sanitised into scopes: `Test Assets` yields `Test_Assets`. Two groups sanitising to an identical scope are reported by warning, as entries sharing a name across them would collide.

Apply purges before rewriting, so deselection unmaps. Injector files in that directory are generated output and are not hand-edited.

Editor tools map through the same logic with `ThreadlinkAddressablesMapping` (`Threadlink.Editor.CodeGen`). A tool that creates Addressable content maps it without a manual step:

```csharp
// Exactly what ticking the rows and selecting Apply does; existing mappings are kept.
if (ThreadlinkAddressablesMapping.Map("Assets/Content/Scenes/Farm/Farm.unity") is false)
    return; // Not an entry of an editable Addressable group; the reason is logged and nothing is applied.

bool mapped = ThreadlinkAddressablesMapping.IsMapped("Assets/Content/Scenes/Farm/Farm.unity");
```

`Map` applies nothing when every entry is mapped already. Applying regenerates the domains and recompiles scripts, so code that needs the new identifiers runs after the domain reload, for example from an `[InitializeOnLoad]` reconciler. An open Mapping Window refreshes itself.

## E17. Collections and Utilities

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

## E18. Configuration and Project Setup

### E18.1 Bootstrap Scene

Every project needs a bootstrap scene, the build's entry point (§3):

- **Create one scene that is not Addressable**, and make it the first, and ideally the only, scene in Build Settings. Every other scene is Addressable and mapped (§D7).
- **Keep it minimal.** It stays loaded for the whole run, so anything placed in it lives for the whole run. Its discoverables boot at deployment, and nothing ever unloads it. Gameplay content belongs in the scenes Nexus loads.
- **Start from it in the Editor too.** Play Mode starts from whichever scene is open, and that scene then takes the bootstrap scene's place: it stays loaded, and if it is an Addressable gameplay scene, Nexus loads a second copy of it beside the first. Set `EditorSceneManager.playModeStartScene` to the bootstrap scene, so Play boots as a build does.

### E18.2 Configuration Assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`. Must reside at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset, and prefab reference maps; binaries directory. |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector, and definition directories. |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Mixer, fade rate, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | Distribution choice for platforms where Unity's target does not uniquely identify the storefront/ecosystem. |

Additional creation paths: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply the following native resources: `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraComponentsPrefab`, and `ChronosConfig`.

The three reference maps on the User Config are read-only in the Inspector and are owned by the Addressables Mapping Window.

### E18.3 Editor Config Composition

Each entry in the `nativeDomains` array declares a domain name, an output filename, a shell, optional native entries, and three flags:

| Flag | Semantics |
|---|---|
| `ordinalValues` | Dense positional allocation. Applies to `Iris.Events` exclusively. |
| `reserveZero` | The shell declares a sentinel at zero that the allocator must not issue. |
| `sourcedFromAddressables` | Draws injectors from the Addressables injector directory. |

`domainName` is the prefix injector filenames must match. No validation constrains it, so a mistyped name renders the corresponding injector unread. The orphaned-injector diagnostic addresses this case.

### E18.4 Addressable Registration of Native Assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** reads the Native Config and marks every referenced native asset, together with the Native Config itself, as Addressable within the "Threadlink Assets" group, assigning each asset's path as its address.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** realigns addresses that have diverged from their asset paths.

### E18.5 Update Loop Modes

Configured on the User Config:

- **Native** — Threadlink injects `ThreadlinkPlayerLoop` marker systems into Unity's current PlayerLoop and publishes `OnUpdate`, `OnFixedUpdate`, and `OnLateUpdate` directly.
- **Custom** — Threadlink installs no update callbacks. The project publishes those events from its own driver, commonly configured in response to `OnCoreDeployed`.

Custom mode applies where Threadlink renders the view for a simulation owned by another framework.

### E18.6 Scripting Defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline ≥ 1.8.10` | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization ≥ 1.5.9` | `LocalizedText` Vault field, localisation utilities |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps and inspectors |

### E18.7 Binary Authoring

Types implementing `IBinaryAuthor` serialise authoring data to `.bytes` files within the project, subsequently loaded through Addressables and consumed via `IAsyncBinaryConsumer`. **`Threadlink ▸ Clear all Binaries`** empties the `.bytes` files within a selected in-project directory during format iteration.

### E18.8 Diagnostics

**`Threadlink ▸ Registers Tracker`** inspects live register contents at runtime, reporting the objects each `Register`-derived subsystem currently holds.

### E18.9 Code Formatter

**`Threadlink ▸ Code Formatter`** opens an editor window backed by CSharpier. Assign a project folder and select **Format C# Files** to recursively format every `.cs` file beneath it. Files with CSharpier compilation errors are skipped and reported; changed files are written in place, Unity's AssetDatabase is refreshed once at the end, and the window reports changed/failed/scanned counts.

## E19. Performance Constraints

| Practice | Rationale |
|---|---|
| Cache `Chronos.DeltaTime` into a local once per tick. | Eliminates repeated static property access in hot loops. |
| Log through `Scribe` rather than `Debug.Log`. | ZString composition is allocation-free, and the prefix identifies the source. |
| Use `UniTask` exclusively; avoid `System.Threading.Tasks` and coroutines in framework code. | Mixing violates the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | Retained delegates keep destroyed objects reachable and dispatch to stale state. |
| Confine replay-sensitive and cross-machine deterministic logic to `FP` and `StatelessRNG`. | Hardware floating point and `UnityEngine.Random` are not suitable for Threadlink's deterministic contract. |
| Prefer `OnLateUpdate` for camera-relative computation. | Dispatch occurs after camera transformation. |

## E20. Engineering Procedures

**Introducing a subsystem**
- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`.

**Introducing an Iris event**
- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document the delegate signature and apply it consistently; mismatched subscriptions log an error, while mismatched publications are a no-op.

**Introducing a scene**
- [ ] Map the scene through the Addressables Mapping Window, and keep it out of Build Settings: only the bootstrap scene belongs there (§E18.1).
- [ ] Implement `ISceneEntry` binding it to its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Choose its `PhysicsMode`: a scene resident alongside others needs its own physics scene.
- [ ] Provide a listener for the four fader and loading-screen `Func` events.
- [ ] Reach it through Nexus only: `TransitionAsync` / `PresentAsync` to show it, `HoldAsync` to keep it resident. Nexus discards its objects whenever it unloads (§E8.2); release every hold.

**Introducing a stacked interface**
- [ ] Construct the prefab with a `CanvasGroup`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

**Using a custom update loop**
- [ ] Set the User Config update-loop mode to `Custom`.
- [ ] Do not call `ThreadlinkPlayerLoop.Install()`; Native mode owns that implementation.
- [ ] Publish `OnUpdate`, `OnFixedUpdate`, and `OnLateUpdate` from the external driver at the intended phases.
- [ ] Preserve the same event contract expected by Chronos, Aura, Dextra, and project listeners.

**Introducing a Sentinel runtime module**
- [ ] Implement an `ISentinelPlatform`/`SentinelPlatform` for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair.
- [ ] Register a `SentinelModuleDescriptor` during subsystem registration.
- [ ] Advertise only capabilities actually implemented.
- [ ] Register platform-level services on the platform and account-scoped services on `ISentinelAccount` implementations.
- [ ] If the target needs editor build configuration/validation, provide the corresponding Sentinel build module.

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
| `OnUpdate` | `Action` | `ThreadlinkPlayerLoop` |
| `OnFixedUpdate` | `Action` | `ThreadlinkPlayerLoop` |
| `OnLateUpdate` | `Action` | `ThreadlinkPlayerLoop` |
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
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnDisplayFaderAsync` | `Func<UniTask>` | Nexus |
| `OnHideFaderAsync` | `Func<UniTask>` | Nexus |
| `OnDisplayLoadingScreenAsync` | `Func<UniTask>` | Nexus |
| `OnHideLoadingScreenAsync` | `Func<UniTask>` | Nexus |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Nexus |
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` | Nexus |
| `OnSceneUnloaded` | `Action<Scene>` | Nexus |

## Appendix B — Service Access

| Service | Category | Access |
|---|---|---|
| `Threadlink` | Core (Weaver) | `Threadlink.TryGetSingleton(out var core)` |
| `Iris` | Static | `Iris.Publish(...)` |
| `Nexus` | Static | `Nexus.TransitionAsync(...)`, `Nexus.HoldAsync(...)` |
| `Initium` | Static | `Initium.BootAndInitAsync(...)` |
| `Scribe` | Static | `this.Send(...)`, `Scribe.Send<T>(...)` |
| `Sentinel` | Native subsystem | `Sentinel.TryGetSingleton(out var sentinel)` |
| `Chronos` | Native subsystem | `Chronos.TimeScale`, `Chronos.DeltaTime` |
| `Dextra` | Native subsystem | `Dextra.TryGetSingleton(out var dextra)` |
| `Aura` | Native subsystem (Linker) | `Aura.TryGetSingleton(out var aura)` |
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
| `Threadlink ▸ Code Formatter` | Recursively formats C# files in a selected project folder using CSharpier. |
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
| **Scene entry** | An `ISceneEntry` implementation binding a scene to its audio scenario, physics mode and unload delay. |
| **Bootstrap scene** | The one non-Addressable scene, first in Build Settings: the build's entry point, loaded for the whole run (§3, §E18.1). |
| **Discoverable** | A scene component Initium boots automatically when its scene loads and discards before it unloads. |
| **FP** | Deterministic Q32.32 fixed-point numeric type (§E15). |

*Threadlink Framework — Reference Manual. Developed and maintained by Threadforge.*

*Lead Developer: George Rontoulis*