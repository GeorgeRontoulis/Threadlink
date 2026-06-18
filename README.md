# Threadlink Framework — User Manual

**Audience:** Game Designers & Engineers

**Scope:** Complete reference for the Threadlink runtime, editor tooling, and authoring workflow.

---

## How to Read This Manual

Threadlink enforces a hard separation between **design data** and **engineering code**, and this manual mirrors that separation. It is split into two self‑contained books that share one short foundation chapter:

| Part | Read this if you are… | Covers |
|---|---|---|
| **Part I — Shared Foundations** | Everyone | What Threadlink is, the project layout, how the framework starts itself. |
| **Part II — The Designer's Manual** | A designer / content author | Authoring data in Vaults, defining design IDs, audio zones, input prompts, tuning configs. No C# required. |
| **Part III — The Engineer's Manual** | A programmer | Architecture, the boot pipeline, every subsystem's runtime API, the ECS, custom subsystems, netcode, performance. |

The two books reference the same systems from opposite ends. Where a designer reads *"add a line to `Vault.DataFields.User.txt` and run the menu,"* the engineer reads *"here is the codegen that turns that file into the `ThreadlinkIDs.Vault.Fields` enum and here is the runtime API that consumes it."*

> **Accuracy note:** Every API name, enum, menu path, file path, and asset‑menu entry in this manual is taken directly from the framework source. Method signatures are reproduced as they exist in code. Where a system is a stub or partially implemented, the feature is either experimental, or in development, and this is stated explicitly.

---
---

# PART I — Shared Foundations

## 1. What Threadlink Is

Threadlink is a scalable, production-grade, modular runtime framework that sits on top of Unity and provides a unified backbone for games and interactive applications. It supplies:

- A **self‑deploying core** that boots before your first scene with no bootstrap GameObject to place.
- A set of **native subsystems** — event bus, time, scenes, input/UI, audio, persistence, logging.
- A **type‑safe ID system**: scenes, assets, events, input modes, spawn points, and data fields are all C# enums generated from plain‑text files, so there is no "stringly‑typed" lookup anywhere.
- A designer‑facing **Vault** data container for authoring game data as assets.
- A high‑performance unsafe **ECS**, a **deterministic math + RNG** toolkit, and a Steam‑based **netcode** module.

Threadlink does **not** replace Unity. You still build scenes, prefabs, and components as usual. Threadlink replaces the *plumbing* — the manager singletons, the event wiring, the asset‑reference bookkeeping, and the save/load abstraction.

### 1.1 The Central Idea: Data is Authored, Behavior is Coded

The framework draws one firm line:

- **Designers author data.** They create Vault assets, fill in configuration ScriptableObjects, place components in scenes, and declare IDs by typing names into text files.
- **Engineers author behavior.** They write subsystems, implement scene logic, subscribe to events, and consume the data designers produce.

Neither side hard‑codes the other's concerns. A designer never edits a `.cs` file; an engineer never hard‑codes a tuning value that belongs in a Vault.

## 2. Project Layout

Threadlink ships as two top‑level folders. Knowing which folder you belong in is the first step.

```
Threadlink/                 ← THE FRAMEWORK. Do not edit. Updated as a unit.
├── Core/                   ← The core, native subsystems, base objects, configs
│   ├── Native Subsystems/  ← Iris, Chronos, Nexus, Dextra, Aura, Sentinel, Initium
│   └── Objects/            ← LinkableBehaviour, LinkableAsset, weaving factory
├── Shared/                 ← Contracts (interfaces), ThreadlinkIDs domains, Scribe
├── Collections/            ← Serializable hash maps
├── Utilities/              ← Extension-method libraries
├── Vault/                  ← The Vault data container + Timeline integration
├── ECS/                    ← Entity Component System
├── Deterministic/          ← Deterministic float (DFP) + StatelessRNG
├── Netcode/                ← Steam P2P networking
├── Editor/                 ← Codegen, custom inspectors, addressable tools
└── Plugins/                ← UniTask, ZString, MessagePack, SerializedReferenceInspector

Threadlink User/            ← YOUR PROJECT. This is where you work.
├── Design/                 ← DESIGNER territory — plain-text ID definitions
│   ├── Dextra.InputModes.User.txt
│   ├── Nexus.SpawnPoints.User.txt
│   └── Vault.DataFields.User.txt
└── Engineering/            ← ENGINEER territory
    ├── Codebase/           ← User code + engineering ID definitions
    │   ├── Subsystems.User.cs
    │   ├── WeavingFactory.User.cs
    │   ├── Constants.User.cs
    │   ├── Iris.Events.User.txt
    │   ├── StatelessRNG.Domains.User.txt
    │   └── Threadlink.User.asmdef
    └── Configs/            ← The config assets (Aura, Chronos, Dextra, Sentinel, …)
```

**The golden rule of the layout:** designers live in `Threadlink User/Design/` and in the Inspector; engineers live in `Threadlink User/Engineering/`. The `Threadlink/` folder is the framework itself and is never edited by either.

## 3. How the Framework Starts (the 30‑second version)

There is **no bootstrap scene and no "Threadlink" GameObject to drag in.** The core deploys itself automatically using Unity's `[RuntimeInitializeOnLoadMethod]` hooks. In order:

1. Unity loads assemblies. Threadlink registers its subsystem **factories** and the Iris event bus initializes.
2. Threadlink listens for subsystem registration.
3. After the first scene loads, the core:
   - initializes Addressables,
   - loads the **Native Config** and the **User Config**,
   - constructs itself,
   - registers and boots all **native** subsystems, then all **user** subsystems,
   - announces readiness via the `OnCoreDeployed` event,
   - then boots any discoverable objects already present in the scene.

Designers don't need more than this. Engineers get the exact, step‑by‑step timeline in **Part III, §2**.

The only things a project *must* provide for the core to deploy are two assets, wired through Addressables:
- a **`ThreadlinkConfig.Native.asset`** (the Native Config) at the Addressable address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`, and
- a **`ThreadlinkConfig.User.asset`** (the User Config) referenced by the Native Config.

Setting these up is a one‑time engineering task covered in **Part III, §20**.

---
---

# PART II — THE DESIGNER'S MANUAL

> You author data and IDs. You will not write or read C#. Everything here is done through text files and the Unity Inspector, plus a couple of menu commands under the **Threadlink** menu.

## D1. Your Role and Mental Model

As a designer you produce four kinds of things:

1. **IDs** — names for spawn points, input modes, and Vault data fields. You type these into text files in `Threadlink User/Design/`, then run a menu command that turns them into safe, selectable dropdown values for engineers and for your own assets.
2. **Vaults** — data assets that hold tunable values (health, speed, prices, flags, curves…) keyed by those IDs.
3. **Config tuning** — adjusting the exposed values on the framework's configuration assets (audio fade speeds, UI sound effects, etc.).
4. **Scene authoring** — placing Threadlink components such as audio zones, interactables, spawn points, and input‑prompt icons.

The throughline: **you never type a name twice and you never reference anything by a raw string.** You declare a name once in a text file, regenerate, and from then on it appears as a dropdown everywhere it's needed.

## D2. The ID Workflow (Your Most Important Skill)

Threadlink turns plain‑text lists into C# `enum` dropdowns. As a designer you own three of these "domains":

| You edit this file (in `Threadlink User/Design/`) | It becomes this dropdown | Used for |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Naming control contexts (e.g. `Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Naming places the player/objects can spawn. |
| `Vault.DataFields.User.txt` | Vault Fields | Naming the data fields your Vault assets can hold. |

### D2.1 How to edit a domain file

Open the file. You'll see comment lines starting with `///`. Add your own names below them, **one name per line**:

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

Rules for names:
- One identifier per line.
- A name must start with a letter and contain only letters, digits, and underscores. Spaces and dashes are not allowed (use `Checkpoint_Alpha`, not `Checkpoint Alpha`).
- Lines beginning with `//` or `///` are comments and are ignored.
- Blank lines are ignored.

### D2.2 Regenerating

After editing one or more domain files, run **either**:

- **`Threadlink ▸ Run All CodeGens`** — regenerates every domain at once (recommended), or
- the specific command under **`Threadlink ▸ CodeGen ▸ …`** for just the one you changed (e.g. `Run Nexus Spawn Points CodeGen`).

Unity will recompile, and your new names will appear as dropdown options in the relevant Inspectors. That's it.

### D2.3 ⚠️ The two rules you must never break

Two of these domains feed systems where **the order of entries is permanent**:

- **Spawn Points** (`Nexus.SpawnPoints.User.txt`)
- *(Engineering also has RNG Domains with the same rule.)*

> **Do not reorder or delete existing entries after they've been generated and used.** Reordering silently remaps every saved reference and corrupts spawn‑point/determinism data in production. If you no longer need an entry, **do not delete it** — rename it to something obviously dead like `Unused_SpawnPoint_3` and leave it in place, then regenerate. Always **add new entries at the bottom.**

Vault Fields and Input Modes are more forgiving, but the safe habit — **append, don't reorder** — is worth applying everywhere.

## D3. The Vault — Authoring Game Data

The **Vault** is your primary tool. A Vault is a data asset (a ScriptableObject) that holds a set of **fields**: named, typed values. Think of it as a strongly‑typed spreadsheet row for any game entity — an enemy, a weapon, a level, an item.

### D3.1 Creating a Vault

1. In the Project window, right‑click ▸ **Create ▸ Threadlink ▸ Vault**.
2. Name it meaningfully (e.g. `Enemy_Goblin`, `Weapon_Longsword`).
3. Select it. In the Inspector you'll see its data‑fields map.

### D3.2 Adding fields

Each field has two parts:

1. **A field ID** — chosen from the **Vault Fields** dropdown (the names you declared in `Vault.DataFields.User.txt`).
2. **A typed value** — you pick the field's *type* from a dropdown, then enter the value.

The built‑in field types you can choose from:

| Type name | Holds |
|---|---|
| `Integer` | whole number (int) |
| `Float` | decimal number |
| `Boolean` | true / false |
| `Double` | high‑precision decimal |
| `Long` | large whole number |
| `Integer2D` | a pair of whole numbers |
| `Float2D` | a pair of decimals |
| `Vector2D` | a 2D vector |
| `Vector3D` | a 3D vector |
| `Rotation` | a rotation (quaternion) |
| `UnityGameObject` | a reference to a GameObject |
| `LocalizedText` | a localized string *(only if the Unity Localization package is installed)* |

> If you need a field type that isn't in this list, ask an engineer — adding new field types is a small one‑time code task on their side.

### D3.3 Serialized vs. Transient fields — a critical choice

When you add a value to a field, you choose how it is *stored*. For each field you pick one of two backings:

- **Serialized** — the value is saved with the asset and persists. Use this for authored, designed values (an enemy's base health, a weapon's damage). This is the normal choice.
- **Transient** — the value exists only at runtime and is **never saved**. It always resets. Use this for scratch values the game fills in while playing (a runtime counter you don't want persisted).

Rule of thumb: **author values are Serialized; runtime scratch values are Transient.**

### D3.4 What engineers do with your Vaults

At runtime, engineers read and write your fields by ID — for example, "get the `Float` field `MoveSpeed` from this enemy's Vault." You don't need to know the code, but it helps to know the contract: **if you rename a field ID, regenerate, and tell the engineering team**, because their code refers to the same generated names.

## D4. Audio for Designers (Aura)

Threadlink's audio subsystem is **Aura**. It manages three channels — **Music**, **Atmos** (ambience), and **SFX** — and supports spatial audio zones. As a designer you touch Aura in three places.

### D4.1 The Aura Config (UI sounds & fade speed)

Open the **Aura Config** asset (in `Threadlink User/Engineering/Configs/Aura/`). The values you can safely tune:

| Field | Meaning |
|---|---|
| Volume Fade Speed | How fast music/ambience/listener volumes fade during transitions. Higher = snappier. |
| Navigation Clip | The SFX played when the player moves between UI elements. |
| Confirm Clip | The SFX for confirming/accepting in UI. |
| Cancel Clip | The SFX for backing out/cancelling in UI. |

The three UI clips are chosen from the **Assets** dropdown (Addressable audio assets — see D7).

### D4.2 Spatial audio zones (`AuraZone`)

An **AuraZone** is a component you place in a scene to create a localized sound source that *ducks* the global music and ambience as the listener gets close (an inverse‑distance influence). To set one up:

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same object and assign its clip (this is the zone's own sound). Aura automatically configures the source to loop and play on awake when you edit it.
3. Tune the two sliders:
   - **Radius Coefficient** (0–1) — scales the influence radius relative to the AudioSource's Max Distance.
   - **Influence** (0–1) — how strongly this zone ducks the global music/ambience when the listener is fully inside.

Zones are automatically discovered and linked when a scene finishes loading, and disconnected when it unloads. You just place them.

### D4.3 Per‑scene audio (music & ambience)

Each scene can declare its own music and ambience tracks and their target volumes. The *wiring* of a scene to its audio is done by an engineer (via a "scene entry"), but the **choices** — which music track, which ambience, how loud — are design decisions. Communicate them as part of the scene's spec, or fill them in if the engineer exposes them on a component.

## D5. Input Prompts for Designers (`DextraInputIcon`)

Threadlink can display the correct button glyph for whatever device the player is currently using (keyboard/mouse, Xbox, PlayStation, or Switch Pro Controller), and swap it automatically when they change devices.

To place a context‑sensitive prompt:

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Configure which input action / control it represents.

The mapping from *control + device → icon sprite* lives in the **Dextra Config** asset (the **Input Icons** map). Populating that map — pairing each control path with the right sprite for each device family — is shared work: a designer typically supplies and assigns the sprites; an engineer wires up the control paths. The sprites themselves are Addressable assets (see D7).

## D6. Tuning the Config Assets

Threadlink's configs are ScriptableObjects in `Threadlink User/Engineering/Configs/`. Most of their setup is engineering, but several exposed values are fair game for designers:

| Config | Designer‑tunable values |
|---|---|
| **Aura Config** | Volume fade speed; UI navigate / confirm / cancel clips. |
| **Chronos Config** | "Iris Physics Update" toggle — leave this to engineering unless told otherwise. |
| **Dextra Config** | Input‑icon sprite assignments; UI screen list (with engineering). |

If you're unsure whether a value is yours to change, ask. A wrong toggle here (like the physics one) can change how the whole game ticks.

## D7. Registering Scenes, Prefabs, and Assets

Anything Threadlink loads at runtime — a scene, a prefab, an audio clip, a sprite — is referenced through Unity's **Addressables** and surfaced as a dropdown ID. The list of these references lives on the **User Config** asset (`ThreadlinkConfig.User.asset`).

As a designer you may be asked to **add entries** to one of three lists on that asset:

- **Scene References** — scenes the game can load.
- **Asset References** — loose assets (audio clips, sprites, data).
- **Prefab References** — prefabs that get spawned.

After adding references, an engineer (or you, if comfortable) runs **`Threadlink ▸ CodeGen ▸ Run Addressables CodeGen`**. This turns each reference into a dropdown entry **named after the asset itself**. So an audio clip named `Music_BossTheme` becomes the selectable ID `Music_BossTheme`.

> **Naming matters:** because the generated ID comes from the asset's name, give your Addressable assets clear, stable, code‑safe names *before* generating. Renaming an asset later means regenerating and re‑selecting it wherever it was used.

## D8. Designer Checklists & Golden Rules

### Adding a new spawn point
1. Add the name to `Threadlink User/Design/Nexus.SpawnPoints.User.txt` (**append at the bottom**).
2. Run `Threadlink ▸ Run All CodeGens`.
3. The new spawn point is now selectable wherever spawn points are chosen.

### Adding a new tunable value to an entity
1. Add the field name to `Vault.DataFields.User.txt`; regenerate.
2. Open the entity's Vault; add a field with that ID; choose its type; enter the value.
3. Mark it **Serialized** (authored value) or **Transient** (runtime scratch).

### Adding a new input context
1. Add the mode name to `Dextra.InputModes.User.txt`; regenerate.
2. Hand the mode name to engineering to bind it to an input action map in the Dextra Config.

### The golden rules
- **Never reference anything by raw text.** Declare an ID, regenerate, pick from the dropdown.
- **Append, never reorder.** Especially for spawn points. Rename dead entries instead of deleting.
- **Regenerate after every edit** to a `.txt` domain file.
- **Give Addressable assets clean names** before generating their IDs.
- **Tell engineering when you rename an ID** they might reference in code.

---
---

# PART III — THE ENGINEER'S MANUAL

> This book assumes C# fluency and familiarity with Unity, Addressables, and async/await. Threadlink uses **UniTask** (not `System.Threading.Tasks`) throughout, and makes heavy use of `[RuntimeInitializeOnLoadMethod]`, generics, and (in the ECS) unsafe code.

## E1. Architecture Overview

### E1.1 Everything is a subsystem

The unit of architecture in Threadlink is the **subsystem**: a singleton object the core owns. Subsystems derive from a small CRTP (Curiously Recurring Template Pattern) base that gives each one a type‑safe static singleton accessor.

```csharp
public abstract class ThreadlinkSubsystem<Singleton> : IThreadlinkSubsystem<Singleton>
where Singleton : ThreadlinkSubsystem<Singleton>
{
    public static readonly int TypeHash = HashFunctions.ToXxHash32(typeof(Singleton).FullName);
    public virtual int ID => TypeHash;

    public virtual void Boot()    => Instance = this as Singleton;   // register the singleton
    public virtual void Discard() => Instance = null;               // tear it down

    public static bool TryGetSingleton(out Singleton result) { /* … */ }
}
```

Key facts:
- The singleton is accessed via **`TryGetSingleton(out var x)`**, not a `.Singleton` property. `TryGetSingleton` returns `false` until the subsystem has actually been linked into the running core, which protects you from touching a half‑deployed system.
- Each subsystem's `ID` is an xxHash32 of its type name (`TypeHash`).
- `Boot()` is where a subsystem assigns its static `Instance`; always call `base.Boot()` when you override it.

### E1.2 The three subsystem flavors

Three layers build on the base, each adding capability:

| Base class | Adds | Use when… |
|---|---|---|
| `ThreadlinkSubsystem<S>` | Singleton + ID only | You want a plain manager with no managed collection. |
| `Register<S, O>` | A `Dictionary<int, O>` of `IIdentifiable` objects (`HasLinked`, `TryGetLinkedObject`) | You need to track a keyed set of objects. |
| `Linker<S, O>` | `TryLink` / `TryDisconnect` / `DisconnectAll` | You link **existing** scene/runtime objects without owning their lifecycle. |
| `Weaver<S, O>` | `TryWeave` / `TrySever` / `SeverAll` via a factory | You **create and own** the lifecycle of objects. |

The core itself, `Threadlink`, is a `Weaver<Threadlink, IThreadlinkSubsystem>` — it weaves subsystems. (Aura, for example, is a `Linker<Aura, AuraSpatialObject>` because it links zones it doesn't own.)

### E1.3 The lifecycle contracts

Objects opt into the boot pipeline by implementing these interfaces (all in `Threadlink.Shared`):

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | **Phase 1.** Load Addressable assets/configs this object needs. |
| `IBootable` | `void Boot()` | **Phase 2.** "Awake." Set up internal state only — **no cross‑references**. |
| `IInitializable` | `void Initialize()` | **Phase 3.** "Start." Wire cross‑references to other subsystems. |
| `IDiscardable` | `void Discard()` | Teardown. Release resources and **unsubscribe from Iris**. |
| `IDiscoverable` | *(marker)* | Makes a scene `LinkableBehaviour` auto‑discovered by the pipeline. |

> **Boot and Initialize are synchronous `void` methods**, but the pipeline runs each *phase* asynchronously across all participants and **does not guarantee ordering within a phase**. Never assume another subsystem booted before you in your own `Boot()`. Cross‑reference work belongs in `Initialize()`, which runs only after every participant has finished `Boot()`.

### E1.4 The two base object classes

| Class | Base | For |
|---|---|---|
| `LinkableBehaviour` | `MonoBehaviour` | Any Threadlink‑aware component. Provides `CachedTransform`, an `OnDiscard` event, `ID` (= `GetInstanceID()`), `Name`, and a `Discard()` that fires `OnDiscard` and destroys the GameObject. Requires a `Transform`. Static `CreateFrom<T>(name)` factory. |
| `LinkableAsset` | `ScriptableObject` | Any Threadlink‑aware data asset. Tracks `IsInstance`, has an `OnDiscard` event, and a `Discard()` that destroys the object only if it's a runtime instance. Static `TryCreate<T>(name, out T)` factory. |

Derive your components from `LinkableBehaviour` and your data assets from `LinkableAsset` (the Vault already does).

## E2. The Deployment Timeline (Exact)

Threadlink deploys via three `[RuntimeInitializeOnLoadMethod]` stages. Understanding this order is essential for knowing when it's safe to touch what.

**Stage 1 — `SubsystemRegistration`**
- `Iris.Observe()` clears the event registry.
- `NativeWeavingFactory.Register()` registers factory delegates (`() => new T()`) for the native subsystems: **Sentinel, Chronos, Dextra, Aura**.
- `UserWeavingFactory.Register()` (in your code) registers factory delegates for *your* subsystems.

**Stage 2 — `AfterAssembliesLoaded`**
- `NativeSubsystemsConfig.ListenForSubsystemRegistration()` subscribes a `Func<List<IThreadlinkSubsystem>>` to the `OnNativeSubsystemRegistration` event.
- `UserSubsystemsConfig.ListenForSubsystemRegistration()` subscribes another to `OnUserSubsystemRegistration`.

**Stage 3 — `AfterSceneLoad` — `Threadlink.DeployCoreAsync()`**
1. `await Addressables.InitializeAsync()`.
2. Load the **Native Config** from the address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
3. From it, load the **User Config** (`NativeResources.UserConfig`).
4. Construct the core: `new Threadlink { NativeConfig = …, UserConfig = … }`.
5. `await core.DeployAsync()`:
   - `Boot()` — assigns the core singleton and, if the User Config's update loop is **Native**, instantiates the hidden `ThreadlinkLoop` GameObject (`DontDestroyOnLoad`).
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` — **publishes** the event, which returns the `List<IThreadlinkSubsystem>` of woven native subsystems, then runs them through the init pipeline (`Initium.PreloadBootAndInitAsync`).
   - `RegisterSubsystemsAsync(OnUserSubsystemRegistration)` — same, for your subsystems.
   - Logs "Core successfully deployed."
   - Publishes **`OnCoreDeployed`** (payload: the `Threadlink` core).
6. `Initium.BootAndInitUnityObjectsAsync().Forget()` — finds every active `LinkableBehaviour` in the loaded scene that implements `IDiscoverable` and runs them through the same Preload→Boot→Init pipeline.

The takeaway: **`OnCoreDeployed` is your "the framework is live" signal.** Subscribe to it to start game logic that needs all subsystems present.

## E3. The Initialization Pipeline (Initium)

`Initium` (static, in `Threadlink.Core.NativeSubsystems.Initium`) runs any set of objects through three ordered phases. Each phase completes fully — across all participants — before the next begins; within a phase, work runs concurrently and **order is not deterministic**.

```
Phase 1  Preload     every IAddressablesPreloader.TryPreloadAssetsAsync(), awaited together
Phase 2  Boot        every IBootable.Boot(), each followed by a 1-frame yield
Phase 3  Initialize  every IInitializable.Initialize(), each followed by a 1-frame yield
```

Public entry points you can call yourself:

```csharp
// Run one object through Boot then Initialize:
await Initium.BootAndInitAsync(entity);          // entity : IBootable, IInitializable

// Boot / Initialize an ordered list (sequential, 1 frame between each):
await Initium.Boot(entities);
await Initium.Initialize(entities);

// Fire-and-forget single calls:
Initium.Boot(entity);
Initium.Initialize(entity);
```

A canonical subsystem implementing all three phases:

```csharp
public sealed class InventorySystem : ThreadlinkSubsystem<InventorySystem>,
    IAddressablesPreloader, IBootable, IInitializable, IDiscardable
{
    private InventoryConfig config;

    public async UniTask<bool> TryPreloadAssetsAsync()      // Phase 1
    {
        if (!Threadlink.TryGetSingleton(out var core)) return false;
        config = await core.LoadAssetAsync<InventoryConfig>(ThreadlinkIDs.Addressables.Assets.InventoryConfig);
        return config != null;
    }

    public override void Boot()                              // Phase 2 — self only
    {
        base.Boot();
        // allocate internal state here
    }

    public void Initialize()                                 // Phase 3 — cross-references
    {
        Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
    }

    public override void Discard()                           // teardown
    {
        Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
        base.Discard();
    }

    private void Tick() { /* … */ }
}
```

## E4. Iris — The Event System

`Iris` (static, `Threadlink.Core.NativeSubsystems.Iris`) is the framework's single event bus. **Every event in the entire framework is identified by one enum: `ThreadlinkIDs.Iris.Events`.** There are no per‑feature enums and no string topics.

### E4.1 The four signatures

A listener is a **delegate**, and you pass its delegate *type* as the generic argument. Iris supports exactly four shapes:

```csharp
// 1) No data:
Iris.Subscribe<Action>(eventID, Handler);                       void Handler()
Iris.Publish(eventID);

// 2) Input only:
Iris.Subscribe<Action<TIn>>(eventID, Handler);                  void Handler(TIn x)
Iris.Publish<TIn>(eventID, input);

// 3) Output only:
Iris.Subscribe<Func<TOut>>(eventID, Handler);                   TOut Handler()
TOut result = Iris.Publish<TOut>(eventID);

// 4) Input and output:
Iris.Subscribe<Func<TIn, TOut>>(eventID, Handler);              TOut Handler(TIn x)
TOut result = Iris.Publish<TIn, TOut>(eventID, input);
```

> **Signature discipline is on you.** Iris stores one delegate per event ID and casts on publish; a mismatch throws `InvalidCastException` at publish time. The generic type you subscribe with **must** match the type you publish with. The native events have fixed signatures (see Appendix A) that must be respected across the codebase.

Subscribe/unsubscribe are idempotent: subscribing the same listener twice is a no‑op, and an event with no listeners published as a `void` event simply does nothing.

### E4.2 Discipline

Always pair subscriptions with unsubscriptions in `Discard()`/`OnDestroy()`:

```csharp
public void Initialize() => Iris.Subscribe<Action<Dextra.InputDevice>>(
    ThreadlinkIDs.Iris.Events.OnInputDeviceChanged, OnDeviceChanged);

public override void Discard()
{
    Iris.Unsubscribe<Action<Dextra.InputDevice>>(
        ThreadlinkIDs.Iris.Events.OnInputDeviceChanged, OnDeviceChanged);
    base.Discard();
}
```

Utility members: `TryGetListenerCount(eventID, out int)`, `ContainsListener(eventID, listener)`, and `Discard(eventID)` (drops all listeners for an ID).

### E4.3 Defining your own events

Engineering owns the Iris events domain:

1. Add event names to `Threadlink User/Engineering/Codebase/Iris.Events.User.txt` (one per line).
2. Run **`Threadlink ▸ CodeGen ▸ Run Iris Events CodeGen`** (or *Run All CodeGens*).
3. Your names appear in the `User Events` region of `ThreadlinkIDs.Iris.Events`.
4. **You are responsible for documenting and consistently using each event's signature** — Iris won't enforce it for you.

## E5. Accessing Subsystems

Not every "subsystem" is a `ThreadlinkSubsystem` instance. Some are static utility classes. Know which is which:

| System | Kind | How you reach it |
|---|---|---|
| `Iris` | static class | `Iris.Subscribe(...)`, `Iris.Publish(...)` |
| `Scribe` | static class | `Scribe.Send<T>(...)` |
| `Initium` | static class | `Initium.BootAndInitAsync(...)` |
| `Nexus` | static class | `Nexus.LoadSceneAsync(...)` |
| `Chronos` | subsystem, **static API** | `Chronos.DeltaTime`, `Chronos.TimeScale` (static members) |
| `Sentinel` | subsystem instance | `Sentinel.TryGetSingleton(out var s)` |
| `Dextra` | subsystem instance | `Dextra.TryGetSingleton(out var d)` |
| `Aura` | subsystem instance | `Aura.TryGetSingleton(out var a)` |
| `Threadlink` (core) | subsystem instance | `Threadlink.TryGetSingleton(out var core)` |
| `ECSWorld` | subsystem instance | `ECSWorld.TryGetSingleton(out var world)` |

> Chronos is unusual: it is a subsystem (it preloads its config and ticks via Iris), but its entire public surface is exposed as **static** properties for ergonomic, allocation‑free access.

## E6. Scribe — Logging

`Scribe` builds log strings allocation‑free (via ZString) and routes them to the Unity console. The pattern is **build → emit**:

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

// Static form — prefixes "[TypeName] - ":
Scribe.Send<MySystem>("Player spawned at ", position).ToUnityConsole();                 // Info
Scribe.Send<MySystem>("Low health: ", hp).ToUnityConsole(DebugType.Warning);
Scribe.Send<MySystem>("Asset load failed: ", id).ToUnityConsole(DebugType.Error);

// Extension form on any object — prefixes "[GetType().Name] - ":
this.Send("Deployed.").ToUnityConsole();
```

- `Send<T>(params object[])` and `this.Send(params object[])` return a `Utf8ValueStringBuilder`. Nothing is logged until you call **`.ToUnityConsole(DebugType)`**.
- `DebugType` is `{ Info, Warning, Error }`, mapping to `Debug.Log` / `LogWarning` / `LogError`.

There is no separate `Warn`/`Error` method — the level is the argument to `ToUnityConsole`.

## E7. Chronos — Time

All time values are **static** members of `Chronos`. Read time through Chronos, not Unity's `Time`, so your code respects the framework's pause semantics.

```csharp
float dt        = Chronos.DeltaTime;            // scaled
float sdt        = Chronos.SmoothDeltaTime;       // scaled, smoothed
float fdt        = Chronos.FixedDeltaTime;        // physics step
float udt        = Chronos.UnscaledDeltaTime;     // ignores TimeScale (UI, pause menus)
double fps       = Chronos.CurrentFramerate;      // 1 / DeltaTime
float since      = Chronos.CurrentTimeSinceDeployment;
float playtime   = Chronos.TotalPlaytime;
```

### E7.1 Pausing — `TimeScale` accepts only 0 or 1

```csharp
Chronos.TimeScale = 0f;   // pause  → publishes OnGamePaused
Chronos.TimeScale = 1f;   // resume → publishes OnGameResumed
```

> **There is no slow‑motion via `TimeScale`. Manually manipulate Unity's Time.deltaTime for slow-motion mechanics. This is by design to clearly denote intent.** The setter only accepts `0` or `1`; any other value is ignored. `RawTimeScale` exists to change Unity's `Time.timeScale` (also only 0/1) **without** firing Iris events — use it when you need to suppress the pause/resume broadcast.

### E7.2 Playtime tracking

```csharp
Chronos.CountTotalPlaytime = true;                         // on by default
Chronos.PlaytimeCountingMode = Chronos.PlaytimeCountMode.Scaled;  // or Unscaled
Chronos.ClearTotalPlaytime();
```

While counting, Chronos publishes **`OnPlaytimeCountTick`** (`Action<float>`, the running total) each frame.

### E7.3 Manual physics

If the **Chronos Config**'s "Iris Physics Update" is enabled, Chronos sets `Physics.simulationMode = Script` and calls `Physics.Simulate()` itself each `OnFixedUpdate`. Disable this when an external framework (e.g. a deterministic simulation) must own the physics step instead.

## E8. Nexus — Scenes

`Nexus` (static) performs scene transitions with a built‑in audio‑fade / fader / loading‑screen pipeline. You drive it with an **`ISceneEntry`**, not a bare scene ID.

### E8.1 The `Nexus.ISceneEntry` contract

```csharp
public interface ISceneEntry
{
    ThreadlinkIDs.Addressables.Scenes ScenePointer { get; }
    LoadSceneMode LoadMode { get; }
    ThreadlinkIDs.Addressables.Assets MusicClipPointer { get; }
    ThreadlinkIDs.Addressables.Assets AtmosClipPointer { get; }
    float MusicVolume { get; }
    float AtmosVolume { get; }

    UniTask OnBeforeUnloadedAsync()  => UniTask.CompletedTask;  // virtual
    UniTask OnFinishedLoadingAsync() { /* default: transitions Aura to this scene's audio */ }
}
```

Implement it to bind a scene to its music/ambience. The default `OnFinishedLoadingAsync` loads the declared clips and transitions Aura to them; override it to add your own post‑load setup (spawning the player, positioning the camera, etc.).

### E8.2 Loading

```csharp
await Nexus.LoadSceneAsync(mySceneEntry);
```

The transition, in order:
1. Fade the audio listener to silence (if Aura is present) **and** show the fader (`OnDisplayFaderAsync`).
2. Show the loading screen (`OnDisplayLoadingScreenAsync`).
3. Hide the fader (`OnHideFaderAsync`).
4. Ask which scene is active (`OnActiveSceneRequested` → `ISceneEntry`); if one exists: `OnBeforeUnloadedAsync()`, publish `OnBeforeActiveSceneUnload`, unload it, publish `OnActiveSceneFinishedUnloading`.
5. Load the new scene; publish `OnNewSceneFinishedLoading` (payload: the entry).
6. `await entry.OnFinishedLoadingAsync()`; publish `OnNexusLoadingFinished`.
7. Re‑show the fader, hide the loading screen, fade audio back up, hide the fader.

> **You must service the presentation events.** Nexus publishes `OnDisplayFaderAsync` / `OnHideFaderAsync` / `OnDisplayLoadingScreenAsync` / `OnHideLoadingScreenAsync` as `Func<UniTask>` and `OnActiveSceneRequested` as `Func<ISceneEntry>`. Your UI layer must subscribe to these and return real tasks/values, or transitions will not display anything. Likewise something must answer `OnActiveSceneRequested` with the currently‑loaded entry so Nexus can unload it.

### E8.3 Spawn points

Spawn points are identified by the generated `ThreadlinkIDs.Nexus.SpawnPoints` enum (designers author the names; see Part II). The enum is consumed by your own spawn‑resolution logic — Nexus provides the IDs and the load pipeline; you decide how an entry maps an ID to a transform.

## E9. Dextra — Input & UI

`Dextra` (subsystem instance) unifies input and UI atop Unity's Input System and UGUI.

### E9.1 Devices

```csharp
public enum InputDevice : byte { MouseAndKeyboard, XBOXController, PSController, SwitchProController }
```

Dextra auto‑detects the active device from the current control scheme (distinguishing DualShock, Switch Pro, and generic/Xbox pads) and, on change, zeroes all gamepad rumble and publishes **`OnInputDeviceChanged`** (`Action<Dextra.InputDevice>`). Read the current device via `dextra.CurrentInputDevice`.

### E9.2 Input modes & maps

Modes are mapped to Input System action maps in the **Dextra Config**; retrieve a mode's map with:

```csharp
if (dextra.TryGetInputMap(ThreadlinkIDs.Dextra.InputModes.Gameplay, out InputActionMap map))
    map.Enable();
```

> Enabling/disabling the retrieved `InputActionMap` is done by your code.

### E9.3 The UI stack

UI screens are types deriving from `UserInterface`. They are preloaded from the **Dextra Config**'s interface list, instantiated once (`DontDestroyOnLoad`), and managed as a stack:

```csharp
dextra.Stack<PauseMenu>();             // push a screen
dextra.Stack<ConfirmDialog, string>("Quit?");  // push with stacking data
dextra.Cancel();                       // pop the top (if it's cancellable)
dextra.ClearStackedInterfaces();       // clear the whole stack
bool top = dextra.IsTopInterface(myUI);
dextra.SelectUIElement(go).Forget();   // drive EventSystem selection
```

`UserInterface` screens receive lifecycle callbacks as they move through the stack: `OnStacked`, `OnCovered`, `OnResurfaced`, `OnPopped`. Optional marker interfaces refine behavior:

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Screen can be cancelled (back‑navigation); receives `OnCancelled()` and triggers `OnUICancelled`. |
| `IPersistentInterface` | Stays visible when another screen stacks on top. |
| `IInteractableInterface` / `IInteractableInterface<T>` | Screen contains selectable elements (`T : DextraSelectable`). |
| `IStackingDataPreprocessor<T>` | Receives `Preprocess(T)` when stacked with data. |

### E9.4 Input icons & interactables

- **Input icons:** `dextra.TryGetInputIcon(device, controlPath, out Sprite)` resolves the right glyph; `DextraInputIcon` components in the scene are auto‑activated on `OnCoreDeployed` to listen for device changes. Control paths use the serializable `DextraInputControlPath` wrapper.
- **Interactables:** derive from `Interactable2D` or `Interactable3D` (which derive from `Interactable : LinkableBehaviour`). They carry an `InteractableConfig` (interaction prompt + options). On detection an interactable either fires immediately (`InteractOnContact`) or subscribes its `Interact()` to the **`OnInteract`** event (`Func<bool>`), so publishing `Iris.Publish<bool>(OnInteract)` triggers the in‑range interactable. Detection is handled by `InteractablesDetector2D`/`InteractablesDetector3D`/`EntityDetector`, which publish `OnInteractableDetected` / `OnInteractableOutOfRange`.

## E10. Aura — Audio (Runtime API)

`Aura` is a `Linker<Aura, AuraSpatialObject>` with three sources (Music, Atmos, SFX). Access via `Aura.TryGetSingleton(out var aura)`.

```csharp
// Global (max) music & ambience volumes — clamped 0..1:
aura.SetGlobalVolumes(musicVolume, atmosVolume);

// Fade the audio listener (used by Nexus transitions):
await aura.FadeAudioListenerVolumeAsync(0f);

// Swap the current music/ambience with a cross-fade:
await aura.TransitionToAudioScenarioAsync(musicClip, atmosClip, musicVolume, atmosVolume);

// One-shot UI SFX (clips come from the Aura Config):
aura.PlayUISFX(Aura.UISFX.Confirm);     // also: Cancel, Navigate
aura.PlayUISFX(Aura.UISFX.Cancel, volume: 0.8f);

// Parent the audio listener to a moving owner; auto-reparents on the owner's Discard:
aura.AttachAudioListenerTo(ownerBehaviour, ownerTransform);
```

Spatial ducking is automatic: every active `AuraSpatialObject`/`AuraZone` linked to Aura contributes an inverse‑distance influence that lowers Music/Atmos as the listener approaches. Zones are linked on `OnNexusLoadingFinished` and disconnected on `OnBeforeActiveSceneUnload`. Fades use `Chronos.UnscaledDeltaTime × VolumeFadeSpeed`.

## E11. Sentinel — Save & Load

`Sentinel` (subsystem instance) is an **environment‑aware, byte‑oriented** I/O layer. It does not serialize for you and it does not deal in typed records — it reads and writes **`byte[]`** addressed by a **folder ID** and a **file ID**. You serialize first (MessagePack via the core helpers), then write.

### E11.1 Environments

The save backend is a `Sentinel.Environment` chosen in the **Sentinel Config** via a `[SerializeReference]` field. Implementations:

| Environment | Status |
|---|---|
| Steam | Implemented |
| XBOX (incl. Microsoft Store / GDK) | Implemented (requires the GDK package → `THREADLINK_SENTINEL_XBOX`) |
| PlayStation | **Stub** — throws `NotImplementedException` |
| NintendoSwitch | **Stub** — throws `NotImplementedException` |

In the **Editor**, Sentinel short‑circuits to a "deployed" state and uses local paths, so you can develop save/load without a platform SDK.

### E11.2 The flow

```csharp
// 0) Deploy the environment once before any I/O (no-op/auto in editor):
await sentinel.DeployEnvironmentAsync();

// 1) Serialize your data to bytes (MessagePack, via the core):
[MessagePackObject]
public sealed class SaveData { [Key(0)] public int Level; [Key(1)] public float Playtime; }

Threadlink.TrySerialize(new SaveData { Level = 5, Playtime = 1234f }, out byte[] bytes);

// 2) Write:
bool ok = await sentinel.TryWriteToStorageAsync("profiles", "slot0", bytes);

// 3) Read & deserialize:
byte[] loaded = await sentinel.ReadFromStorageAsync("profiles", "slot0");
if (loaded != null && Threadlink.TryDeserialize(loaded, out SaveData data)) { /* … */ }

// 4) Delete:
sentinel.DeleteStoredData("profiles", "slot0");
```

`CurrentOperationState` (`Idle`/`Deploying`/`Reading`/`Writing`) guards against overlapping operations — a read/write is rejected unless the environment is deployed and currently `Idle`. Serialization uses `Threadlink.serializerOptions` (default `MessagePackSerializerOptions.Standard`); customize that field if needed.

## E12. Vault — Runtime API

A `Vault` (a `LinkableAsset`) holds a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`. Read and write fields by ID:

```csharp
// Strongly-typed get/set:
if (vault.TryGet<float>(ThreadlinkIDs.Vault.Fields.MoveSpeed, out float speed)) { … }
vault.TrySet<float>(ThreadlinkIDs.Vault.Fields.MoveSpeed, 7.5f);

// Existence / field-object access:
bool has = vault.Has(ThreadlinkIDs.Vault.Fields.MoveSpeed);
vault.TryGetDataField(id, out DataField field);
vault.TryGetGenericDataField<float>(id, out DataField<float> typed);   // exposes .Value + OnValueChanged
vault.TryGetConcreteDataField<DataFields.Float>(id, out var concrete);
```

Field backing types live in `Threadlink.Vault.DataFields` (`Integer`, `Float`, `Boolean`, `Double`, `Long`, `Integer2D`, `Float2D`, `Vector2D`, `Vector3D`, `Rotation`, `UnityGameObject`, and `LocalizedText` under `THREADLINK_LOCALIZATION`). Each `DataField<T>` exposes a `Value` property and an `OnValueChanged` event. To add a new field type, declare a `[Serializable] public sealed class MyField : DataField<MyType> { }`.

**Timeline integration** (under `THREADLINK_TIMELINE`): `VaultMarker` is a Timeline `Marker` that pushes a configured set of field values onto a `Vault` when the playhead reaches it (via `VaultTrack`/`VaultReceiver`). `VaultMarker` is *not* a scene component for linking GameObjects to Vaults.

## E13. Resource Loading (Addressables)

The core wraps Addressables behind the generated ID enums. All enum‑ID methods are instance methods on the core (`Threadlink.TryGetSingleton(out var core)`); `AssetReference` overloads are static.

```csharp
// Assets (sync + async), by generated ID:
T  a1 = core.LoadAsset<T>(ThreadlinkIDs.Addressables.Assets.SomeAsset);
T  a2 = await core.LoadAssetAsync<T>(ThreadlinkIDs.Addressables.Assets.SomeAsset);

// Prefabs (returns the requested component on the prefab):
T  p1 = core.LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs.SomePrefab);
T  p2 = await core.LoadPrefabAsync<T>(ThreadlinkIDs.Addressables.Prefabs.SomePrefab);

// Scenes:
SceneInstance s = await core.LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes.Level01, LoadSceneMode.Single);
await core.UnloadSceneAsync(ThreadlinkIDs.Addressables.Scenes.Level01);

// Release:
core.ReleaseAsset(assetID);
core.ReleasePrefab(prefabID);

// Static AssetReference overloads:
T a3 = await Threadlink.LoadAssetAsync<T>(someAssetReference);
```

The four ID groups — `ThreadlinkIDs.Addressables.Assets / Prefabs / Scenes / NativeResources` — are generated from the reference lists on the **User Config** (Assets/Prefabs/Scenes) and the **Native Config** map (NativeResources). Serialization helpers `Threadlink.TrySerialize/TryDeserialize` (MessagePack) round out the core's data API.

## E14. Writing a Custom Subsystem

Registering a subsystem is a **two‑file** operation that mirrors the native setup.

**Step 1 — Register a factory** in `Threadlink User/Engineering/Codebase/WeavingFactory.User.cs`:

```csharp
internal static class UserWeavingFactory
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        WeavingFactory.Register<InventorySystem>();   // requires a public parameterless ctor
        WeavingFactory.Register<QuestSystem>();
    }
}
```

**Step 2 — Weave it** in `Threadlink User/Engineering/Codebase/Subsystems.User.cs`:

```csharp
private static List<IThreadlinkSubsystem> WeaveSubsystems()
{
    var buffer = new List<IThreadlinkSubsystem>
    {
        Threadlink.Weave<InventorySystem>(),
        Threadlink.Weave<QuestSystem>(),
    };

    // To enable the netcode module, also call:
    // ThreadlinkNetcode.WeaveSubsystems(buffer);   // and ThreadlinkNetcode.RegisterSubsystems() in the factory above

    Iris.Unsubscribe<Func<List<IThreadlinkSubsystem>>>(REGISTRATION_EVENT, WeaveSubsystems);
    return buffer;
}
```

`Threadlink.Weave<T>()` calls the registered factory and links the instance into the core. Your subsystem then flows through the standard Preload→Boot→Init pipeline during deployment, and you reach it anywhere via `T.TryGetSingleton(out var x)`. Implement only the lifecycle interfaces you need (`IAddressablesPreloader` / `IBootable` / `IInitializable` / `IDiscardable`).

> `WeavingFactory.Register<T>()` and the equivalent `ThreadlinkSubsystems.Register<T>()` both register `() => new T()`. For non‑trivial construction, register a custom factory delegate via `WeavingFactory<T>.OnCreate`.

## E15. The Entity Component System (ECS)

Threadlink's ECS is an **unsafe, pointer‑based, zero‑GC** world for high‑throughput simulation. It is engineer‑only and entirely separate from `MonoBehaviour` gameplay. `ECSWorld` is a subsystem (`ThreadlinkSubsystem<ECSWorld>, IDisposable`) — register it like any user subsystem (the netcode module registers it for you).

### E15.1 Components

A component is an `unmanaged` struct implementing `IComponent` (which requires `Dispose()`), marked `[RuntimeComponent]` so the registry assigns it a deterministic bit index at boot:

```csharp
using Threadlink.ECS;
using Unity.Mathematics;

[RuntimeComponent]
public struct Position : IComponent { public float3 Value; public readonly void Dispose() { } }

[RuntimeComponent]
public struct Velocity : IComponent { public float3 Value; public readonly void Dispose() { } }
```

> For IL2CPP/managed‑stripping targets, also apply `[UnityEngine.Scripting.Preserve]` to each component — `ComponentRegistry.Hydrate()` warns at boot if a `[RuntimeComponent]` is missing `[Preserve]`. Bit indices are assigned by sorting component types by hash, so they are stable across runs and machines.

### E15.2 Entities & components

```csharp
ECSWorld.TryGetSingleton(out var world);

Entity e = world.CreateNewEntity();          // struct: { int ID; int Generation; }
bool valid = world.IsValid(e);               // generation check guards against stale handles

Position* p = world.Add<Position>(e);        // returns a pointer into dense storage
p->Value = float3.zero;

bool has = world.Has<Position>(e);
if (world.TryGetPointer<Velocity>(e, out Velocity* v)) v->Value = new float3(1,0,0);

world.Destroy(e);                            // recycles the ID with a bumped generation
```

`Entity` is a 8‑byte struct with an `ID` and a recycling `Generation`; destroyed IDs are reused with a new generation so stale handles fail `IsValid`.

### E15.3 Iteration with function pointers

Iteration uses **static function pointers** (no closures, no allocation). Provide a `static` method and pass its address:

```csharp
// Iterate all entities that have BOTH components:
world.ForEach<Position, Velocity>(&Integrate);

static void Integrate(in Entity e, Position* p, Velocity* v)
{
    p->Value += v->Value * Chronos.DeltaTime;   // note: capture-free; read globals as needed
}
```

`ForEach` is overloaded for 1–4 components. To restrict further, compose an `ECSFilter` (a `readonly struct`, immutable fluent builder) and pass it:

```csharp
var moving = new ECSFilter().With<Position>().With<Velocity>().Without<Frozen>();

world.ForEach(in moving, &TouchEntity);                 // entity only
world.ForEach<Position>(in moving, &TouchWithPosition);  // entity + Position*
```

`With<T>()`/`Without<T>()` return new filters; `Matches(in ComponentMask)` is the predicate the world applies. Filtered overloads also exist for 1–3 component payloads.

### E15.4 Systems pattern & memory

There is no base "system" class — a system is any object that subscribes to an Iris update event and calls `world.ForEach`:

```csharp
public sealed class MovementSystem : IBootable, IDiscardable
{
    private ECSFilter moving;
    public void Boot()
    {
        moving = new ECSFilter().With<Position>().With<Velocity>();
        Iris.Subscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
    }
    private void Tick() => ECSWorld.TryGetSingleton(out var w); // then w.ForEach<…>(&…)
    public void Discard() => Iris.Unsubscribe<Action>(ThreadlinkIDs.Iris.Events.OnUpdate, Tick);
}
```

The world stores generations, free IDs, masks, and per‑type `ComponentPool<T>` in `UnsafeList`/native memory with `Allocator.Persistent`. `ECSWorld.Discard()` (called by the framework on teardown) disposes everything; in editor, `PreventEditorMemoryLeaks()` guards against domain‑reload leaks. An `EntityCommandBuffer` exists for deferred structural changes. Supporting tests in `ECS/Tests/` are the canonical usage reference.

## E16. Deterministic Toolkit

For lockstep/replay/networked determinism, never use `float` or `UnityEngine.Random` in simulation code. Threadlink provides two pieces.

### E16.1 `DFP` — deterministic floating point

`DFP` (in `Threadlink.Deterministic`) is a software `binary32` float (ported from CodesInChaos/SoftFloat) that produces **bit‑identical results on every CPU**. It's a `readonly struct` with the full operator set (`+ - * / %`, comparisons), explicit conversions to/from `float` and `int`, and constants (`DFP.Zero`, `DFP.One`, etc.).

```csharp
using Threadlink.Deterministic;

DFP speed = (DFP)5f;
DFP dt     = (DFP)Chronos.FixedDeltaTime;
DFP dist   = speed * dt;
float view = (float)dist;   // convert back only for rendering/UI
```

Use `DFP` for simulation; convert to `float` only at the view boundary. Math helpers live alongside it (`Arithmetic`, `Transcendental`, `Trigonometry`).

### E16.2 `StatelessRNG` — reproducible randomness

`StatelessRNG` (static) is a hash‑based RNG: given the same global seed and the same inputs, it reconstructs the same stream — ideal for replays and netcode. You seed it once and sample through **scopes** keyed by a generated **domain** (engineers author domain names in `StatelessRNG.Domains.User.txt`).

```csharp
StatelessRNG.Boot(seed: 0xDEADBEEF);

var scope = StatelessRNG.CreateScope(ThreadlinkIDs.StatelessRNG.Domains.Loot);
int roll      = scope.Range(1, 100);
bool crit     = scope.Probability((DFP)0.05f);
DFP spread     = scope.Range((DFP)(-1f), (DFP)1f);
scope = scope.Advance();        // next deterministic state
int idx       = scope.Index(itemCount);
```

`Scope` sampling: `Range(int,int)`, `Index(int)`, `Boolean()`, `Probability(DFP)`, `Float01()`, `Range(DFP,DFP)`, and `Advance()`. An overload `CreateScope<C>(domain, in context)` mixes a context identity into the stream. **RNG domains share the "append, never reorder" rule** — reordering breaks determinism across versions.

## E17. Netcode (Overview)

> ATTENTION: The Threadlink Netcode is currently experimental and not fit for use in a production environment. While the foundation is functional, the module contains unpolished and even incorrect code for testing purposes. Do NOT use it unless it's explicitly exited its experimental phase in a future update.

The Netcode module is a **Steam P2P** networking layer built on the ECS, with deterministic transforms/animation, entity ownership, and a serialization pipeline. It is optional and engaged through the same two‑file registration as any user subsystem.

To enable it:

```csharp
// In UserWeavingFactory.Register():
ThreadlinkNetcode.RegisterSubsystems();

// In UserSubsystemsConfig.WeaveSubsystems(buffer):
ThreadlinkNetcode.WeaveSubsystems(buffer);
```

That registers and weaves the full stack: `ECSWorld`, `Networld`, `EntityOwnershipRegistry`, `NetworkSerializer`, `Netrunner` (connectivity/session/update loop), `Netflow`, `NetworkRouter`, `HandshakeSubsystem`, `NetworkSpawningSubsystem`, `NetworkTransformSubsystem`, `NetworkClipLibrary`, and `NetworkAnimationSubsystem`. (Ensure your assembly definition references the netcode assemblies.)

Convenience surface (`ThreadlinkNetcode` / `NetcodeUtils`):

```csharp
bool host = ThreadlinkNetcode.IsHost;
ThreadlinkNetcode.TryGetLocalSteamUID(out CSteamID id);
ThreadlinkNetcode.TryGetHostSteamUID(out ulong host);

// Entity extension methods:
NetworkEntity ne = entity.AsNetworkEntity();
bool mine   = entity.HasLocalAuthority();
bool hosts  = entity.IsOwnedByHost();
bool neutral= entity.IsNeutral();
```

Because netcode depends on the ECS and `DFP`/`StatelessRNG`, build networked simulation with the deterministic toolkit (§16). Steam App ID setup and redistributable copying are handled by the editor build processor in the Steamworks integration. The deep netcode internals (transport, payload headers, flow providers) live in `Threadlink/Netcode/` and are beyond this manual's scope.

## E18. ID Domains & Codegen (Engineer's View)

There are **two** code generators. Know both.

### E18.1 Native‑domain codegen (menu‑driven, template merge)

Each built‑in domain has a **native template** (a `.txt` C# template with a placeholder, in `Editor/CodeGen/CodeGen Templates/`) and a **user template** (the `.txt` you edit). `EnumCodeGen` reads the user entries, joins them into the placeholder, formats with CSharpier, and overwrites the target generated `.cs`. References (templates + target scripts) are wired on the **`ThreadlinkConfig.Editor.asset`**.

| Domain | User template | Owner | Menu item |
|---|---|---|---|
| `ThreadlinkIDs.Iris.Events` | `Iris.Events.User.txt` | Engineering | `Threadlink ▸ CodeGen ▸ Run Iris Events CodeGen` |
| `ThreadlinkIDs.StatelessRNG.Domains` | `StatelessRNG.Domains.User.txt` | Engineering | `Threadlink ▸ CodeGen ▸ Run RNG Domains CodeGen` |
| `ThreadlinkIDs.Dextra.InputModes` | `Dextra.InputModes.User.txt` | Design | `Threadlink ▸ CodeGen ▸ Run Dextra Input Modes CodeGen` |
| `ThreadlinkIDs.Nexus.SpawnPoints` | `Nexus.SpawnPoints.User.txt` | Design | `Threadlink ▸ CodeGen ▸ Run Nexus Spawn Points CodeGen` |
| `ThreadlinkIDs.Vault.Fields` | `Vault.DataFields.User.txt` | Design | `Threadlink ▸ CodeGen ▸ Run Vault Fields CodeGen` |
| `ThreadlinkIDs.Addressables.{Assets,Prefabs,Scenes}` | *(from User Config reference lists)* | Engineering | `Threadlink ▸ CodeGen ▸ Run Addressables CodeGen` |

`Threadlink ▸ Run All CodeGens` runs every one. Generated files carry an "AUTO‑GENERATED … DO NOT EDIT MANUALLY" banner — **never hand‑edit them.**

### E18.2 Custom user domains (automatic, file‑watched)

`ThreadlinkIDsImporter` is an `AssetPostprocessor`. Any `.txt` file you drop into the **User Domain Definitions Folder** (configured on the Editor Config) is automatically turned into an `enum` named after the file, in namespace `Threadlink.User`, written to the **User Domain Scripts Folder**. Each line becomes `Name = <xxHash32(name)>`, prefixed with `None = 0`. Spaces become nothing and dashes become underscores. No menu command needed — saving the file regenerates the enum. Use this for arbitrary game‑specific ID sets that don't belong to a built‑in domain.

## E19. Collections & Utilities

**Serializable maps** (`Threadlink.Collections`), both deriving from `ThreadlinkHashMap<TKey,TValue>`:

| Type | Backing | Use for |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` values | Concrete value types / Unity object refs (used by configs). |
| `RefHashMap<K,V>` | `[SerializeReference]` values | Polymorphic managed values (used by Vault for `DataField`). |

**Extension libraries** (`Threadlink.Utilities.*`) — representative members:

```csharp
using Threadlink.Utilities.Mathematics;   // float.IsSimilarTo(b), float.MoveTowards(target, maxDelta)
using Threadlink.Utilities.Vectors;       // Vector3.IsSimilarTo(b)
using Threadlink.Utilities.Strings;       // string.ToAbsolutePath()
using Threadlink.Utilities.UniTask;       // List<UniTask>.AwaitAllThenClear(trim)
using Threadlink.Utilities.Collections;   // IDisposable.PreventEditorMemoryLeaks()
```

Also under `Utilities`: `Objects`, `Flags` (bit ops like `HasFlagUnsafe`), `Attributes`, `Localization`, and `RNG`. Threadlink bundles **UniTask**, **ZString**, **MessagePack**, and the **SerializedReferenceInspector** in `Plugins/`.

## E20. Configuration & Project Setup

A new project needs these assets created and wired once.

### E20.1 The config assets

| Asset | Create via | Purpose |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` IDs → `AssetReference`. Must live at the Addressable address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update‑loop mode (`Native`/`Custom`); the Scene/Asset/Prefab reference lists; (editor) the binaries folder. |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Codegen template/script references and the user‑domain folders. Editor‑only. |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | "Iris Physics Update" toggle. |
| **Aura Config** | `… ▸ Aura Config` | Fade speed + UI SFX clip pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | UI screen list, input‑mode→action‑map map, input‑icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | The `[SerializeReference]` save environment. |

The `NativeResources` the Native Config must provide: `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraMixer`, `AuraComponentsPrefab`, `ChronosConfig`, `NetflowConfig`.

### E20.2 Making native assets Addressable

Run **`Threadlink ▸ Mark Native Assets as Addressable`**. It reads the Native Config, marks every referenced native asset (and the Native Config itself) Addressable in the "Threadlink Assets" group, using each asset's path as its address.

### E20.3 Update loop: Native vs Custom

On the **User Config**, `UpdateLoopBehaviour` is:
- **Native** (default) — Threadlink spawns the hidden `ThreadlinkLoop`, which publishes `OnUpdate`/`OnFixedUpdate`/`OnLateUpdate`.
- **Custom** — Threadlink spawns nothing. **You** must publish those three Iris events from your own driver (e.g. when Threadlink only renders the View for a Photon Quantum simulation). Subscribe to `OnCoreDeployed` to install your loop.

### E20.4 Scripting defines (auto‑activated by installed packages)

| Define | Activated by | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline ≥ 1.8.10` | Vault Timeline integration (`VaultMarker`/`VaultTrack`/`VaultReceiver`). |
| `THREADLINK_LOCALIZATION` | `com.unity.localization ≥ 1.5.9` | `LocalizedText` Vault field + localization utilities. |
| `THREADLINK_SENTINEL_XBOX` | `com.unity.microsoft.gdk ≥ 1.4.5` | The XBOX/GDK Sentinel environment & achievements. |
| `THREADLINK_MATHEMATICS` | *(manual)* | Routes select math through `Unity.Mathematics`. |

The runtime assembly (`Threadlink.Runtime.asmdef`) has `allowUnsafeCode: true` (required by the ECS) and these as `versionDefines`. Put your own code in `Threadlink User/Engineering/Codebase/` under `Threadlink.User.asmdef`.

### E20.5 Binary authoring & cleanup

Objects implementing `IBinaryAuthor` can serialize authoring data to `.bytes` files in the project (loaded later via Addressables; consumed at runtime through `IAsyncBinaryConsumer`). **`Threadlink ▸ Clear all Binaries`** empties the `.bytes` files in a chosen in‑project folder when you're iterating on their format.

## E21. Performance Guidelines

| Do | Why |
|---|---|
| Cache `Chronos.DeltaTime` once per tick into a local. | Avoids repeated static property reads in hot loops. |
| Build `ECSFilter`s once (in `Boot`) and reuse. | Construction is cheap but per‑frame allocation of anything is avoidable. |
| Use `static` methods for `ForEach` function pointers. | The ECS forbids closures by design — keep callbacks capture‑free. |
| Log via `Scribe`, not `Debug.Log`, in hot paths. | ZString builds messages without GC. |
| Use `UniTask`, never `System.Threading.Tasks` or coroutines, in framework code. | Mixing the two breaks the single‑threaded Unity model Threadlink assumes. |
| Unsubscribe every Iris listener in `Discard()`. | Dangling delegates keep dead objects alive and fire stale callbacks. |
| Keep all simulation in `DFP` + `StatelessRNG` for networked/replay code. | Hardware `float` and `UnityEngine.Random` are non‑deterministic. |

## E22. Engineer Checklists

**New subsystem**
- [ ] `class X : ThreadlinkSubsystem<X>` (+ the lifecycle interfaces you need).
- [ ] Public parameterless ctor (or a custom `WeavingFactory<X>.OnCreate`).
- [ ] `WeavingFactory.Register<X>()` in `UserWeavingFactory.Register()`.
- [ ] `Threadlink.Weave<X>()` in `UserSubsystemsConfig.WeaveSubsystems()`.
- [ ] Subscribe in `Initialize()`, unsubscribe in `Discard()`.

**New Iris event**
- [ ] Add to `Iris.Events.User.txt`; run the codegen.
- [ ] Document its delegate signature; use it consistently on subscribe and publish.

**New ECS component**
- [ ] `unmanaged struct : IComponent` with `Dispose()`.
- [ ] `[RuntimeComponent]` (+ `[Preserve]` for IL2CPP).

**New scene**
- [ ] Add the scene to the User Config's Scene References; run Addressables codegen.
- [ ] Implement an `ISceneEntry` binding it to its music/ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Ensure something services the Nexus fader/loading/`OnActiveSceneRequested` events.

---
---

# Appendices

## Appendix A — Built‑in Iris Events

All events are members of `ThreadlinkIDs.Iris.Events` (a `ushort` enum). Signatures verified from source where shown; honor them exactly.

| Event | Publisher | Delegate signature |
|---|---|---|
| `OnNativeSubsystemRegistration` | core | `Func<List<IThreadlinkSubsystem>>` |
| `OnUserSubsystemRegistration` | core | `Func<List<IThreadlinkSubsystem>>` |
| `OnCoreDeployed` | core | `Action<Threadlink>` |
| `OnUpdate` / `OnFixedUpdate` / `OnLateUpdate` | `ThreadlinkLoop` | `Action` |
| `OnPlaytimeCountTick` | Chronos | `Action<float>` (running total) |
| `OnGamePauseRequested` / `OnGameResumeRequested` | *(intended request events)* | `Action` |
| `OnGamePaused` / `OnGameResumed` | Chronos | `Action` |
| `OnInputDeviceChanged` | Dextra | `Action<Dextra.InputDevice>` |
| `OnUICancelled` | Dextra UI stack | `Action<UserInterface>` |
| `OnUIElementSelected` | Dextra | `Action<GameObject>` |
| `OnInteract` | gameplay/detectors | `Func<bool>` |
| `OnInteractableDetected` / `OnInteractableOutOfRange` | interactable detectors | interactable payload |
| `OnActiveSceneRequested` | Nexus | `Func<Nexus.ISceneEntry>` |
| `OnBeforeActiveSceneUnload` | Nexus | `Action` |
| `OnActiveSceneFinishedUnloading` | Nexus | `Action` |
| `OnNewSceneFinishedLoading` | Nexus | `Action<Nexus.ISceneEntry>` |
| `OnDisplayFaderAsync` / `OnHideFaderAsync` | Nexus (serviced by UI) | `Func<UniTask>` |
| `OnDisplayLoadingScreenAsync` / `OnHideLoadingScreenAsync` | Nexus (serviced by UI) | `Func<UniTask>` |
| `OnNexusLoadingFinished` | Nexus | `Action` |

## Appendix B — Systems & How to Reach Them

| System | Kind | Namespace | Accessor |
|---|---|---|---|
| `Threadlink` | Weaver subsystem | `Threadlink.Core` | `Threadlink.TryGetSingleton(out var core)` |
| `Iris` | static | `…NativeSubsystems.Iris` | direct |
| `Scribe` | static | `…NativeSubsystems.Scribe` | direct |
| `Initium` | static | `…NativeSubsystems.Initium` | direct |
| `Nexus` | static | `…NativeSubsystems.Nexus` | direct |
| `Chronos` | subsystem (static API) | `…NativeSubsystems.Chronos` | static members |
| `Sentinel` | subsystem | `…NativeSubsystems.Sentinel` | `Sentinel.TryGetSingleton(out …)` |
| `Dextra` | subsystem | `…NativeSubsystems.Dextra` | `Dextra.TryGetSingleton(out …)` |
| `Aura` | Linker subsystem | `…NativeSubsystems.Aura` | `Aura.TryGetSingleton(out …)` |
| `ECSWorld` | subsystem | `Threadlink.ECS` | `ECSWorld.TryGetSingleton(out …)` |

## Appendix C — ID Domains Map

| Generated enum | User source | Owner | How it regenerates |
|---|---|---|---|
| `ThreadlinkIDs.Iris.Events` | `Iris.Events.User.txt` | Engineer | Menu / Run All |
| `ThreadlinkIDs.StatelessRNG.Domains` | `StatelessRNG.Domains.User.txt` | Engineer | Menu / Run All |
| `ThreadlinkIDs.Dextra.InputModes` | `Dextra.InputModes.User.txt` | Designer | Menu / Run All |
| `ThreadlinkIDs.Nexus.SpawnPoints` | `Nexus.SpawnPoints.User.txt` | Designer | Menu / Run All |
| `ThreadlinkIDs.Vault.Fields` | `Vault.DataFields.User.txt` | Designer | Menu / Run All |
| `ThreadlinkIDs.Addressables.{Assets,Prefabs,Scenes}` | User Config reference lists | Engineer | Addressables codegen |
| `ThreadlinkIDs.Addressables.NativeResources` | *(fixed, framework)* | — | n/a |
| *Custom* `Threadlink.User.<File>` | any `.txt` in the user definitions folder | Either | Automatic on save |

## Appendix D — Menu & Asset‑Creation Reference

**`Threadlink ▸` menu items**
- `Run All CodeGens`
- `CodeGen ▸ Run Iris Events CodeGen`
- `CodeGen ▸ Run Addressables CodeGen`
- `CodeGen ▸ Run Nexus Spawn Points CodeGen`
- `CodeGen ▸ Run RNG Domains CodeGen`
- `CodeGen ▸ Run Vault Fields CodeGen`
- `CodeGen ▸ Run Dextra Input Modes CodeGen`
- `Mark Native Assets as Addressable`
- `Clear all Binaries`

**`Create ▸ Threadlink ▸` assets**
- `Vault`
- `User Config`, `Native Config`, `Editor Config`
- `Subsystem Dependencies ▸ Chronos Config | Aura Config | Dextra Config | Sentinel Config`

## Appendix E — Scripting Defines

| Define | Source |
|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline ≥ 1.8.10` |
| `THREADLINK_LOCALIZATION` | `com.unity.localization ≥ 1.5.9` |
| `THREADLINK_SENTINEL_XBOX` | `com.unity.microsoft.gdk ≥ 1.4.5` |
| `THREADLINK_MATHEMATICS` | manual project define |

## Appendix F — Glossary

| Term | Meaning |
|---|---|
| **Core** | `Threadlink`, the root `Weaver` that owns all subsystems. |
| **Subsystem** | A singleton managed by the core; reached via `TryGetSingleton`. |
| **Weaver / Linker / Register** | The three subsystem base classes (own / link / track objects). |
| **Weave** | Create a subsystem via its registered factory (`Threadlink.Weave<T>()`). |
| **Discoverable** | A scene `LinkableBehaviour` with `IDiscoverable`, auto‑run through the boot pipeline. |
| **Iris** | The single enum‑keyed event bus. |
| **Domain** | A named ID set generated from a `.txt` file into an enum. |
| **Vault** | A designer‑authored data asset of typed, ID‑keyed fields. |
| **DataField** | One typed entry in a Vault; `Serialized` (saved) or `Transient` (runtime). |
| **DFP** | Deterministic software `binary32` float for cross‑platform reproducibility. |
| **Scope / Domain (RNG)** | A `StatelessRNG` sampling context keyed by an RNG domain. |
| **Native vs User** | Framework‑provided vs project‑provided (subsystems, configs, ID entries). |
| **`ISceneEntry`** | An object binding a scene to its load mode and music/ambience for Nexus. |

---

*All APIs, enums, file paths, and menu items reflect the framework source.*

*Threadlink Framework — User Manual. Developed and maintained by Threadforge.*

*Lead Developer: George Rontoulis*