<!-- GENERATED FILE. Do not edit: the source is the GitBook manual in Gitbook/manual of the Threadlink repository. Regenerate with: python Gitbook/tools/build_single_file.py -->

# Threadlink Framework — Reference Manual

> This is the offline, single-file edition of the Threadlink reference manual, generated from the GitBook space in the repository's `Gitbook/manual` folder. The manual is online at [threadforge.gitbook.io/threadlink-documentation](https://threadforge.gitbook.io/threadlink-documentation/).


![Powered by Threadlink. Weaving worlds together.](Threadlink_Banner.png)

**Threadlink** is a modular runtime framework layered over Unity: a self-deploying core, native subsystems for events, time, scenes, input and UI, audio and platform services, a type-safe identifier system, a designer-facing data container, and a deterministic toolkit for reproducible simulation.

This is the complete reference for the Threadlink runtime, its editor tooling and its authoring workflow, as of **Threadlink 6.6**.

## Organisation

Threadlink enforces a strict separation between authored data and implemented behaviour. The manual mirrors that separation across two self-contained volumes that share a common foundation.

| Part | Audience | Contents |
|---|---|---|
| [Getting Started](#requirements) | All | Requirements, installation and first-time project setup. |
| [Foundations](#purpose) | All | Framework purpose, project layout, assembly topology, deployment overview. |
| [Designer Reference](#designer-reference) | Content authors | Identifier declaration, Vault authoring, spatial audio, input prompts, configuration tuning, content registration. Requires no C#. |
| [Engineer Reference](#engineer-reference) | Programmers | Architecture, deployment pipeline, subsystem APIs, PlayerLoop integration, Sentinel platform services, deterministic and memory tooling, code generation internals, custom subsystems, and performance. |
| [Reference](#native-iris-events) | All | Native Iris events, service access, identifier domains, menus and asset-creation entries, terminology. |

Both volumes describe the same systems from opposing ends. Where the designer volume specifies *"declare the identifier in `Vault.Fields.User.txt`"*, the engineer volume specifies the domain pipeline that emits `ThreadlinkIDs.Vault.Fields`, the manifest that stabilises its value, and the runtime API that consumes it.

> All API names, identifiers, menu paths, file paths and asset-creation entries in this manual are drawn from framework source. Systems that are stubbed or under development are identified as such.

## Where to start

- **New to Threadlink:** [Requirements](#requirements), then [Installation and Project Setup](#installation-and-project-setup), then [Purpose](#purpose).
- **Designers:** [The Identifier Workflow](#the-identifier-workflow) and [The Vault](#the-vault).
- **Engineers:** [Architecture](#architecture), [Deployment and Initialisation](#deployment-and-initialisation) and [Iris](#iris-event-dispatch).

---

*Threadlink Framework — Reference Manual. Developed and maintained by Threadforge.*

*Lead Developer: George Rontoulis*

---

# Getting Started

## Requirements

### Unity

Threadlink 6.6 is developed and validated against **Unity 6000.6.4f1**. It relies on recent editor APIs, among them the `Unity.Scripting.LifecycleManagement` attributes (`[OnEnteringPlayMode]`, `[OnExitingPlayMode]`, `[AutoStaticsCleanup]`) and the main-toolbar extension API; earlier editor versions are untested.

### Required packages

| Package | Version in the reference project | Used by |
|---|---|---|
| Addressables (`com.unity.addressables`) | 2.11.2 | Every runtime-loaded scene, prefab and asset, and the configuration assets themselves. |
| Input System (`com.unity.inputsystem`) | 1.20.0 | Dextra: devices, action maps, UI input module. |
| uGUI (`com.unity.ugui`) | 2.6.0 | Dextra's interfaces and selectables. |
| Collections (`com.unity.collections`) | 6.6.0 | Unsafe collections and native memory; brings Mathematics (`int2`, `float2`, `quaternion`). |
| Burst (`com.unity.burst`) | 2.0.0 | Dependency of Collections. |

### Optional packages

| Package | Effect |
|---|---|
| Timeline (`com.unity.timeline`) ≥ 1.8.10 | Defines `THREADLINK_TIMELINE`, enabling the Vault Timeline integration. The reference project uses 6.6.0. |
| Localization (`com.unity.localization`) ≥ 1.5.9 | Defines `THREADLINK_LOCALIZATION`, enabling the `LocalizedText` Vault field, localised interaction prompts and the localisation utilities. The reference project uses 1.5.13. |
| Odin Inspector | Defines `ODIN_INSPECTOR`; hash maps, Vault fields and several inspectors draw through Odin. |
| Steamworks.NET (`com.rlabrecque.steamworks.net`) | Required by the Steam Sentinel module, which also requires the `STEAMWORKS_NET` scripting define. Validated against 2025.165.0. |

See [Scripting Defines](#scripting-defines) for the full list.

### Bundled dependencies

Threadlink ships the following under `Threadlink/Plugins/` and `Threadlink/Editor/`. They need no separate installation.

| Dependency | Purpose |
|---|---|
| UniTask | Every asynchronous API in the framework. `System.Threading.Tasks` is not used. |
| ZString | Allocation-free string building for Scribe and code generation. |
| MessagePack 3.1.4 (with its Unity integration and analyser) | `Threadlink.TrySerialize` / `TryDeserialize`. |
| SerializeReference UI (`Textus.SerializeReferenceUI`) | The managed-reference type pickers used by Vault fields and Sentinel Config when Odin is absent. |
| CSharpier (editor only) | The [Code Formatter](#code-formatter) window. |

## Installation and Project Setup

Threadlink is distributed as a folder, not as a UPM package. The repository's `Threadforge/` directory is the framework together with a ready-made project territory and default configuration assets.

### Add the framework

1. Install the [required packages](#required-packages).
2. Copy the repository's `Threadforge/` folder into the project as **`Assets/Threadforge/`**, `.meta` files included.

The location is not a convention but a requirement: the core loads the Native Config from the fixed Addressables address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset` (`NativeConstants.Addressables.NATIVE_CONFIG`). The asset references between the shipped configuration assets also rely on the `.meta` GUIDs being preserved.

### Shipped configuration

The framework arrives configured. Nothing below needs to be created; it needs to be reviewed.

| Asset | Location |
|---|---|
| Native Config | `Threadlink/ThreadlinkConfig.Native.asset`, already mapping every native resource. |
| Editor Config | `Threadlink/ThreadlinkConfig.Editor.asset`, already declaring every native identifier domain. |
| User Config | `Threadlink User/Engineering/Configs/ThreadlinkConfig.User.asset` |
| Chronos Config, Sentinel Config | `Threadlink User/Engineering/Configs/` |
| Aura Config, `AuraComponents.prefab`, `AuraMixer.mixer` | `Threadlink User/Engineering/Configs/Aura/` |
| Dextra Config, `DextraComponents.prefab`, `ThreadlinkInputActions.inputactions`, `UIInputActions.inputactions`, `InputSystem.inputsettings.asset` | `Threadlink User/Engineering/Configs/Dextra/` |

Every configuration asset is described in [Configuration and Project Setup](#configuration-and-project-setup).

### Make the native assets Addressable

1. Create the project's Addressables settings if it has none, and an Addressable group named **`Threadlink Assets`**.
2. Select **`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`**.

The command reads the Native Config and marks the Native Config itself and every native resource it references as Addressable in the `Threadlink Assets` group, each addressed by its asset path. If that group does not exist, the default group is used and a warning is logged.

### Create the bootstrap scene

Every project needs exactly one **bootstrap scene**, the build's entry point:

1. Create a scene that is **not** Addressable.
2. Make it the first, and ideally the only, scene in Build Settings.
3. Keep it minimal. It stays loaded for the whole run; gameplay content belongs in Addressable scenes loaded through Nexus.
4. Make Play Mode start from it, so the Editor boots as a build does.

Play Mode otherwise starts from whichever scene is open, and that scene takes the bootstrap scene's place. If it is an Addressable gameplay scene, Nexus later loads a second copy of it beside the first. Unity exposes the Play Mode start scene through `EditorSceneManager.playModeStartScene`, for example:

```csharp
#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
internal static class BootstrapPlayModeStart
{
    static BootstrapPlayModeStart()
    {
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
            UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Scenes/Bootstrap.unity");
    }
}
#endif
```

The reasons for this arrangement are set out in [Deployment Overview](#deployment-overview).

### Choose the platform distribution

Open the **Sentinel Config** and confirm the distribution for platforms that offer a choice, such as Windows (Local, Steam, Microsoft Store, Epic or GOG). The shipped default is **Local**, which needs no platform SDK. See [Sentinel](#sentinel-platform-services).

### Verify the deployment

Enter Play Mode from the bootstrap scene. The console lists the woven native subsystems (Sentinel, Chronos, Dextra and Aura), then the user subsystems (or reports that there are none), then `Core successfully deployed.` Sentinel separately reports the platform and distribution it deployed, or why it failed; a failed platform does not stop the deployment.

Deployment aborts with an error if either the Native Config or the User Config cannot be loaded, most often because the native assets were never made Addressable.

> **Previewing a scene without the core.** Setting `ThreadlinkEditorDeployment.Suppressed = true` before `AfterSceneLoad` (for example from a `BeforeSceneLoad` hook) skips Addressables initialisation and core deployment for that Play session. The flag is Editor-only and resets on entering and exiting Play Mode.

### Next steps

- Designers: [The Identifier Workflow](#the-identifier-workflow).
- Engineers: [Architecture](#architecture) and [Implementing a Subsystem](#implementing-a-subsystem).

---

# Foundations

## Purpose

Threadlink is a modular runtime framework layered over Unity, providing a unified backbone for games and interactive applications. It supplies:

- A self-deploying core that needs no scene-placed initialiser. Its one scene requirement is a bootstrap scene: the build's entry point, which stays loaded while Nexus loads every other scene through Addressables (see [Deployment Overview](#deployment-overview)).
- Native subsystems covering event dispatch, time, scene management, input and UI, audio, and cross-platform platform services such as accounts, saves, achievements, and social/session affordances.
- A type-safe identifier system in which scenes, assets, prefabs, events, input modes, spawn points, RNG domains and data fields are C# enumerations generated from plain-text declarations, eliminating string-keyed lookup.
- The **Vault**, a polymorphic data container for designer-authored game data.
- A deterministic arithmetic and stateless RNG toolkit for reproducible simulation, replay-sensitive logic and procedural generation, and a native-memory toolkit for state that is copied, hashed and saved as raw bytes.

Threadlink does not supersede Unity. Scenes, prefabs and components are authored conventionally. Threadlink supersedes the intermediate infrastructure: manager singletons, event wiring, asset-reference bookkeeping, and persistence abstraction.

### Authored data, implemented behaviour

The framework maintains a single dividing line:

- **Designers author data.** They create Vault assets, populate configuration ScriptableObjects, place components in scenes, and declare identifiers in text files.
- **Engineers implement behaviour.** They author subsystems, implement scene logic, subscribe to events, and consume authored data.

Neither discipline encodes the other's concerns. Designers do not edit `.cs` files; engineers do not hard-code values belonging in a Vault.

### Content-addressed identity

Generated identifier values derive from a hash of the entry name rather than from its position in a declaration list. Consequently, removing an entry cannot alter the value of any other entry, and removed entries persist as obsolete tombstones so that previously serialised references remain resolvable.

This property underpins the safety of the identifier workflow. A single domain, Iris events, operates on positional allocation instead, and is identified as such at every point where the distinction is material. See [Identifier Domains and Code Generation](#identifier-domains-and-code-generation).

## Project Layout

Threadlink occupies one folder, `Assets/Threadforge/`, divided into two territories.

```
Threadforge/
├── Threadlink/                        ← Framework. Not user-editable. Updated as a unit.
│   ├── Core/
│   │   ├── Native Subsystems/         ← Aura, Chronos, Dextra, Iris, Nexus, Sentinel, Initium
│   │   │   └── Sentinel/
│   │   │       ├── Contracts/         ← Multiplayer and social service contracts
│   │   │       └── Modules/           ← The Local and Steam platform modules
│   │   └── Objects/                   ← LinkableBehaviour, LinkableAsset, singletons, native weaving factory
│   ├── Shared/                        ← Contracts, Scribe, hashing, Addressables helpers, WeavingFactory
│   ├── Collections/                   ← Serialisable hash maps, arrays and lists
│   ├── Utilities/                     ← Extension-method libraries, FixedPointConversions
│   ├── Vault/                         ← Data container and Timeline integration
│   ├── Deterministic/                 ← Fixed point (FP, its vectors and rotations) and StatelessRNG
│   ├── Memory/                        ← Native allocation, raw-memory hashing, inline collections
│   ├── Editor/                        ← Domain code generation, Addressables tooling, Sentinel build pipeline,
│   │                                    formatter, inspectors
│   ├── Generated/                     ← Generated output: ThreadlinkIDs enumerations and manifests
│   ├── Plugins/                       ← UniTask, ZString, MessagePack, SerializeReference UI
│   ├── Constants.Native.cs
│   ├── ThreadlinkConfig.Native.asset
│   └── ThreadlinkConfig.Editor.asset
│
└── Threadlink User/                   ← Project territory.
    ├── Design/
    │   └── Custom Domain Definitions/ ← Project-defined identifier domains
    ├── Generated Addressables Injectors/ ← Mapping Window output; not hand-edited
    ├── Native Domain Injectors/       ← Plain-text identifier declarations
    │   ├── Dextra.InputModes.User.txt
    │   ├── Iris.Events.User.txt
    │   ├── Nexus.SpawnPoints.User.txt
    │   ├── StatelessRNG.Domains.User.txt
    │   └── Vault.Fields.User.txt
    └── Engineering/
        ├── Codebase/                  ← Project code
        │   ├── Generated/             ← Generated output: project-defined domain enumerations
        │   ├── Constants.User.cs
        │   ├── Subsystems.User.cs
        │   ├── WeavingFactory.User.cs
        │   └── Threadlink.User.asmdef
        └── Configs/                   ← User, Chronos and Sentinel configs; Aura/ and Dextra/ configs,
                                         prefabs, mixer and input actions
```

The `Threadlink/` directory constitutes the framework and is not hand-edited, `Threadlink/Generated/` included: that directory is owned exclusively by the code generator. All project authoring occurs under `Threadlink User/`. Binary data authored through `IBinaryAuthor` lives in a folder of the project's choosing, assigned on the User Config (see [Binary Authoring](#binary-authoring)).

### Assembly topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | No references; `noEngineReferences`. Referenceable from any assembly, including a deterministic simulation assembly. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers, `WeavingFactory`, `AnimatorHash` | — |
| `Threadlink.Deterministic` | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, `StatelessRNG`, `Bits` | `noEngineReferences`; `allowUnsafeCode`. References `Threadlink.Shared` and `Threadlink.Generated`. |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections | `allowUnsafeCode`. No assembly references; no other Threadlink assembly references it. |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities (including `FixedPointConversions`, between Unity's float types and the deterministic ones) | `allowUnsafeCode`. Defines `THREADLINK_TIMELINE` and `THREADLINK_LOCALIZATION` from package versions. |
| `Threadlink.Sentinel.Multiplayer.Contracts` | `IMultiplayerService` and the social/session types | `noEngineReferences` |
| `Threadlink.Sentinel.Local` | The Local Sentinel module | — |
| `Threadlink.Sentinel.Steam` | The Steam Sentinel module | Compiled only with the `STEAMWORKS_NET` define, on standalone platforms and in the Editor. |
| `Threadlink.Sentinel.Steam.Shared` | `SteamSentinelSettings`, module identity | No references; `noEngineReferences` |
| `Threadlink.Sentinel.Local.Editor`, `Threadlink.Sentinel.Steam.Editor` | Sentinel build modules and the Steam App ID build processor | Editor-only |
| `Threadlink.Editor` | Code generation, editor tooling, the Sentinel build pipeline | Editor-only |
| `Threadlink.User` | Project code | `allowUnsafeCode`. References `Threadlink.Runtime`, `Threadlink.Shared`, `Threadlink.Deterministic`, `Threadlink.Generated`, `Threadlink.User.Generated` and UniTask. |
| `Threadlink.User.Generated` | Project-defined domain enumerations | No references; `noEngineReferences` |

Generated identifiers reside in namespace `Threadlink.Generated`; consuming code requires `using Threadlink.Generated;`. Project-defined domains are emitted into namespace `Threadlink.User`. Isolating generated output in dedicated assemblies guarantees that neither project authoring nor third-party module installation produces a write within framework source.

`Threadlink.User` does not reference `Threadlink.Memory`, the Sentinel contracts or the Sentinel modules. Project code that uses them adds the reference to `Threadlink.User.asmdef`, or to its own assembly definition.

## Deployment Overview

The core deploys itself through Unity's runtime-initialisation hooks, so no scene-placed initialiser is required.

### The bootstrap scene

Every project needs one **bootstrap scene**, for three reasons:

- **Unity needs a scene as the entry point.** A player starts from the first scene in Build Settings.
- **An Addressable scene can't be that scene.** Unity keeps Addressable scenes out of the build's scene list, and Addressables only initialise once the first scene has loaded.
- **So every project needs one non-Addressable scene**, first and ideally only in Build Settings. That is the bootstrap scene. It stays loaded for the whole run, as the persistent scene every other scene loads beside (see [Nexus](#nexus-scene-management)), and every other scene is Addressable and loaded through Nexus.

Setting it up is covered in [Installation and Project Setup](#create-the-bootstrap-scene).

### The deployment

1. Assemblies load. Native and project subsystem factories register, and both tiers subscribe to their respective registration events. Sentinel platform modules register with the module registry.
2. Once the bootstrap scene has loaded, the core initialises Addressables, loads the **Native Config**, and loads the **User Config** referenced by it.
3. The core constructs itself, then registers, preloads, boots and initialises native subsystems, followed by project subsystems.
4. The core publishes `OnCoreDeployed`.
5. Discoverable objects present in the bootstrap scene are booted.

Deployment requires two assets addressable through the Addressables system:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

The precise sequence is specified in [Deployment and Initialisation](#deployment-and-initialisation); configuration is detailed in [Configuration and Project Setup](#configuration-and-project-setup).

---

# Designer Reference


> This volume covers data and identifier authoring. All operations are performed through text files, the Unity Inspector, and a small set of commands under the **Threadlink** menu. It requires no C#.

### Scope of the role

Designers produce four categories of artefact:

1. **Identifiers**: names for spawn points, input modes and Vault data fields, declared in text files and surfaced as Inspector dropdowns. See [The Identifier Workflow](#the-identifier-workflow).
2. **Vaults**: data assets holding tunable values keyed by those identifiers. See [The Vault](#the-vault).
3. **Configuration values**: exposed fields on the framework's configuration assets. See [Configuration Assets](#configuration-assets).
4. **Scene authoring**: placement of Threadlink components, including [audio zones](#audio-zones), interactables, and [input-prompt icons](#input-prompts).

The governing principle: no name is typed twice, and nothing is referenced by raw string. An identifier is declared once and thereafter selected from a dropdown.

### In this volume

| Page | Covers |
|---|---|
| [The Identifier Workflow](#the-identifier-workflow) | Declaring, regenerating, removing and renaming identifiers. |
| [The Vault](#the-vault) | Creating Vaults, field types, serialised and transient backings. |
| [Audio (Aura)](#audio-aura) | Aura configuration, audio zones, per-scene audio. |
| [Input Prompts](#input-prompts) | Device-specific button glyphs. |
| [Configuration Assets](#configuration-assets) | The designer-tunable fields of each configuration asset. |
| [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets) | The Addressables Mapping Window. |
| [Designer Procedures](#designer-procedures) | Step-by-step procedures and operating principles. |

## The Identifier Workflow

Threadlink compiles plain-text declaration lists into C# enumerations. The declaration files reside in **`Threadlink User/Native Domain Injectors/`**. Three are designer-owned:

| File | Resulting dropdown | Purpose |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Control contexts (`Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Locations at which entities may be placed. |
| `Vault.Fields.User.txt` | Vault Fields | Data fields available to Vault assets. |

These files are termed **injectors**: each injects entries into an enumeration the framework declares. Two further injectors in the same directory are engineer-owned (`Iris.Events.User.txt`, `StatelessRNG.Domains.User.txt`).

### Declaration syntax

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

- One identifier per line. Leading and trailing whitespace is ignored.
- Identifiers begin with a letter or underscore and comprise letters, digits and underscores. Any other character is substituted with an underscore during generation; declaring `Checkpoint_Alpha` directly is preferred to relying on substitution.
- Lines prefixed `//`, including `///`, are treated as comments.
- Blank lines are ignored.
- Identifier comparison is case-insensitive: `Alpha` and `alpha` denote the same entry, and only the first is kept.

### Regeneration

Saving the file is sufficient. The injector directory is monitored, generation runs automatically, and Unity recompiles. New identifiers appear in the corresponding dropdowns.

**`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a generation pass on demand.

### Removal semantics

Entries may be removed by deleting the corresponding line. Values are content-addressed, with the following consequences:

- Removing an entry does not alter the value of any other entry.
- The removed identifier is retained in the generated enumeration as an obsolete tombstone, preserving resolution for assets that still reference it. Code referencing it produces a compiler warning.
- Declaring a removed identifier again revives it with its original value.
- Reordering declarations has no effect; declaration order is not significant.

Iris events constitute the sole exception and fall under engineering ownership. Their values are positional, so removal produces compilation failures at every subscription site rather than a tombstone. This behaviour is intentional.

### Rename semantics

A rename is equivalent to a removal followed by an addition: the former identifier becomes a tombstone and the new identifier receives a distinct value. References to the former identifier require reassignment. Engineering must be notified of any rename affecting an identifier referenced in code.

Each generation pass that changes a domain emits a console summary enumerating additions, removals, renames, rescopes and collisions. Consulting this summary is the most direct means of detecting an unintended rename.

## The Vault

A **Vault** is a polymorphic data asset holding a set of named, typed **fields**: the authoring unit for any game entity, such as an enemy, a weapon, a level or an item.

### Creation

1. In the Project window, select **Create ▸ Threadlink ▸ Vault**.
2. Assign a descriptive name (`Vault_Enemy_Goblin`, `Vault_Weapon_Longsword`).
3. Select the asset to expose its data-field map in the Inspector.

### Field composition

Each field comprises two elements:

1. A **field identifier**, selected from the Vault Fields dropdown (declared in `Vault.Fields.User.txt`, see [The Identifier Workflow](#the-identifier-workflow)).
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

Additional field types are introduced through a minor engineering task (see [Vault Runtime API](#custom-field-types)).

### Serialised and transient backings

Each field is assigned one of two value backings:

- **Serialised**: persisted with the asset. Appropriate for authored values such as base health or weapon damage. This is the backing for design data.
- **Transient**: runtime-only, never persisted, reset each session. Appropriate for scratch values populated during play.

Authored values are serialised; runtime scratch values are transient.

> Unity [Asset Presets](https://docs.unity3d.com/Manual/Presets.html) may be applied to guarantee that every Vault of a given class is instantiated with its complete field set.

### Runtime consumption

Engineering reads and writes fields by identifier at runtime. Renaming a field identifier invalidates those references; engineering must be notified.

A Timeline can carry Vault values on markers (see [Vault Runtime API](#timeline-integration)), and an animation event can pass a Vault to code through an `AnimationEventsReceiver` (see [Collections and Utilities](#animation-helpers)).

## Audio (Aura)

**Aura** is the audio subsystem, managing the Music, Atmos (ambience) and SFX channels and supporting spatial audio zones.

### Aura configuration

| Field | Function |
|---|---|
| Master Audio Mixer | The mixer whose exposed parameters engineering reads and writes. **Engineering-owned.** |
| Volume Fade Speed | Rate at which music, ambience and listener volumes transition. Higher values produce faster transitions. |
| Navigation Clip | Effect played on UI element traversal. |
| Confirm Clip | Effect played on UI confirmation. |
| Cancel Clip | Effect played on UI cancellation. |

Clips are selected from the Assets dropdown; [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets) covers the procedure for populating that dropdown.

### Audio zones

An **AuraZone** is a scene component producing a localised sound source that attenuates global music and ambience by inverse-distance influence as the listener approaches.

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same GameObject and assign its clip. The zone picks the AudioSource up into its **Source** field, and configures it for looping playback on awake, at full volume. An AudioSource elsewhere can be assigned to **Source** instead.
3. Configure two parameters:
   - **Radius Coefficient** (0–1): scales the influence radius relative to the AudioSource's maximum distance.
   - **Influence** (0–1): attenuation applied to the global channels when the listener is fully within the zone.

Zones are linked automatically when their scene becomes the presented scene and disconnected when it is replaced or unloaded; placement is the only required action. Only the presented scene is audible, so zones in other loaded scenes (for example, scenes kept loaded for other players in a multiplayer session) have no effect.

Zone names must be unique within a scene, ignoring case: a zone sharing another zone's name is not linked, and a warning is logged.

### Per-scene audio

Each scene declares music and ambience tracks with target volumes through a scene entry implemented by engineering (see [Nexus](#the-isceneentry-contract)). Track selection and volume levels are design decisions and should be communicated as part of the scene specification.

## Input Prompts

Threadlink resolves the correct button glyph for the active input device and substitutes it automatically on device change. Four device families are distinguished: mouse and keyboard, Xbox controller, PlayStation controller, and Switch Pro controller.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Configure the control it represents (its input control path).

Inside one of Dextra's interfaces, that is all. Anywhere else, an engineer's component must boot and discard it (see [Dextra](#device-resolution)).

The control-and-device to sprite mapping resides on the Dextra Config asset. Populating it is shared work: designers supply and assign sprites, engineers configure control paths. Sprites are Addressable assets; see [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets).

If no sprite is assigned for the active device, the image is hidden and an error is logged.

## Configuration Assets

| Asset | Designer-tunable fields |
|---|---|
| **Aura Config** | Volume fade speed; navigation, confirm and cancel clips. |
| **Dextra Config** | Interface fade-in and fade-out durations (seconds, default 0.25); input-icon sprite assignments; interface list, in conjunction with engineering. |
| **Chronos Config** | Iris Physics Update. **Engineering-owned; do not modify.** |
| **Sentinel Config** | Target distribution where a platform permits multiple storefront/ecosystem choices. **Engineering-owned; do not modify without coordination.** |

The physics toggle alters the simulation model for the entire application. Values of uncertain ownership should be confirmed with engineering before modification.

The shipped assets reside in `Threadlink User/Engineering/Configs/`. The full description of every configuration asset, including the engineering-owned ones, is in [Configuration and Project Setup](#configuration-and-project-setup).

## Registering Scenes, Prefabs and Assets

Runtime-loaded content (scenes, prefabs, audio clips, sprites) is referenced through Addressables and surfaced as a dropdown identifier. Registration is performed through a dedicated tool.

Every scene is Addressable except the bootstrap scene, which is the only one in Build Settings (see [Deployment Overview](#deployment-overview)). A new scene therefore never goes into Build Settings.

1. Ensure the asset belongs to an Addressable group.
2. Open **`Threadlink ▸ Addressables ▸ Mapping Window`**.
3. Locate the asset within its group and enable its checkbox. The toolbar's search field filters entries by name; **All** and **None** toggle a whole group; **Ping** locates the asset in the Project window.
4. Select **Apply**.

The window classifies assets by type automatically (scenes yield Scene identifiers, prefabs yield Prefab identifiers, all others yield Asset identifiers) and writes the corresponding reference into the User Config. The reference maps on the User Config are read-only in the Inspector and are owned exclusively by this window. Read-only Addressable groups are not listed.

Two properties govern the resulting identifier:

- **The identifier derives from the asset name.** An audio clip named `Music_BossTheme` yields the identifier `Music_BossTheme`. Assets should carry stable, code-safe names prior to mapping.
- **The Addressable group participates in identity.** Two assets named `Splash` in distinct groups coexist; the second is qualified as `GroupName_Splash`. Relocating a mapped asset to a different group alters its identifier. Group assignment should be settled before mapping.

Disabling an asset's checkbox unmaps it. The identifier is retained as a tombstone.

Renaming a mapped asset, or moving it to another group, gives it a new identifier and makes the old one a tombstone. Applying the mappings again records the old identifier as an alias of the new one, so engineering can resolve data saved under the old identifier (see [Resource Loading](#renamed-assets)). References in assets and code still require reassignment.

Group names become part of the identity too: two groups whose names reduce to the same code-safe form (for example `Test Assets` and `Test_Assets`) are reported, and Apply refuses to run until one is renamed.

The **Scenes** dropdown in the Editor's main toolbar lists every scene that ships, both those in Build Settings and those in Addressable groups, and opens the selected one.

## Designer Procedures

### Declaring a spawn point

1. Add the identifier to `Threadlink User/Native Domain Injectors/Nexus.SpawnPoints.User.txt`.
2. Save. Generation runs automatically.
3. The spawn point becomes selectable wherever spawn points are configured.

### Adding a tunable value to an entity

1. Add the field identifier to `Vault.Fields.User.txt` and save.
2. Open the entity's Vault, add a field with that identifier, select its type, and enter the value.
3. Select the **Serialised** or **Transient** backing.

### Declaring an input context

1. Add the mode identifier to `Dextra.InputModes.User.txt` and save.
2. Provide the identifier to engineering for binding to an input action map.

### Making an asset loadable

1. Assign the asset to an Addressable group under a stable name.
2. Map it through **`Threadlink ▸ Addressables ▸ Mapping Window`** and select **Apply**.

### Placing an audio zone

1. Add an **AuraZone** and an **AudioSource** to a GameObject in the scene, and assign the source's clip.
2. Give the zone a name unique within the scene.
3. Set **Radius Coefficient** and **Influence**.

### Operating principles

- Nothing is referenced by raw string. Declare an identifier, save, and select from the dropdown.
- Removal is safe; renaming orphans existing references and requires coordination.
- Consult the console summary emitted by each generation pass.
- Assign stable names and final group membership to Addressable assets before mapping them.
- Notify engineering of any identifier rename that may be referenced in code.

---

# Engineer Reference


> This volume assumes C# proficiency and familiarity with Unity, Addressables and asynchronous programming. Threadlink uses **UniTask** exclusively in place of `System.Threading.Tasks`, and relies on runtime-initialisation hooks (`[RuntimeInitializeOnLoadMethod]` and Unity's lifecycle-management attributes), generic constraints, generated identifier domains, and direct Unity PlayerLoop integration.

### In this volume

| Page | Covers |
|---|---|
| [Architecture](#architecture) | Subsystems and static services, the register hierarchy, lifecycle contracts, base object types, ownership, subsystem access. |
| [Deployment and Initialisation](#deployment-and-initialisation) | The deployment sequence, the Initium pipeline, shutdown. |
| [Iris](#iris-event-dispatch) | Event dispatch and the update loop. |
| [Scribe](#scribe-logging) | Logging. |
| [Chronos](#chronos-time) | Time, timescale, playtime, manual physics. |
| [Nexus](#nexus-scene-management) | Scene residency, presentation and transitions. |
| [Dextra](#dextra-input-and-ui) | Input devices, input modes, the UI stack, interactables. |
| [Aura](#aura-audio) | Audio mixing, the listener, spatial objects. |
| [Sentinel](#sentinel-platform-services) | Platform services: accounts, saves, achievements, social affordances, modules. |
| [Vault Runtime API](#vault-runtime-api) | Reading and writing Vault fields; Timeline integration. |
| [Resource Loading](#resource-loading) | Loading by identifier or by reference; queries; renamed assets. |
| [Implementing a Subsystem](#implementing-a-subsystem) | Registering and weaving project subsystems. |
| [Deterministic Toolkit](#deterministic-toolkit) | `FP`, its vectors and rotations, `StatelessRNG`. |
| [Memory](#memory) | Native allocation, raw-memory hashing, inline collections. |
| [Identifier Domains and Code Generation](#identifier-domains-and-code-generation) | Domain kinds, sources, manifests, the Mapping Window. |
| [Collections and Utilities](#collections-and-utilities) | Serialisable maps and collections, extension libraries, attributes. |
| [Configuration and Project Setup](#configuration-and-project-setup) | Configuration assets, update loop modes, scripting defines. |
| [Editor Tooling](#editor-tooling) | Menu commands, the scene dropdown, the code formatter, diagnostics. |
| [Performance Constraints](#performance-constraints) | Rules for hot paths. |
| [Engineering Procedures](#engineering-procedures) | Checklists for common engineering tasks. |

## Architecture

### Subsystems and static services

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

Additional project systems are registered as woven user subsystems through `WeavingFactory.User.cs` and `Subsystems.User.cs` (see [Implementing a Subsystem](#implementing-a-subsystem)).

### Register hierarchy

`ThreadlinkSubsystem<T>` employs the curiously recurring generic pattern to expose a type-safe static singleton per subsystem. A subsystem's `ID` is `ThreadlinkSubsystem<T>.TypeHash`, the xxHash32 of its type's full name. Three specialisations extend it:

| Base | Additions | Application |
|---|---|---|
| `Register<S, O>` | A `Dictionary<int, O>` keyed by `IIdentifiable.ID`; `HasLinked(id)`, `TryGetLinkedObject<T>(id, out T)` | Lookup tables |
| `Linker<S, O>` | `TryLink`, `TryDisconnect<T>(id, out T)`, `DisconnectAll(trimRegistry)` | Tracking externally-created objects |
| `Weaver<S, O>` | `TryWeave<T>(out T)`, `TryWeave<T>(original, out T)`, `TrySever(id)`, `SeverAll()` | Owning object lifecycles |

A `Weaver` creates objects through `WeavingFactory<T>`: `TryWeave<T>(out T)` through its `OnCreate` factory, and `TryWeave<T>(original, out T)` through its `OnCreateFrom` clone factory. Severing discards the object.

`Threadlink` is itself a `Weaver<Threadlink, IThreadlinkSubsystem>`. `Aura` is a `Linker<Aura, AuraSpatialObject>`.

### Lifecycle contracts

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | Executes first. Dependency acquisition. |
| `IBootable` | `void Boot()` | Awake equivalent. Execution order within the phase is non-deterministic; implementations must be self-contained. |
| `IInitializable` | `void Initialize()` | Start equivalent. Cross-object references are valid. |
| `IDiscardable` | `void Discard()` | Teardown. Unsubscription occurs here, followed by `base.Discard()`. |
| `IDiscoverable` | Marker | Scene-placed `LinkableBehaviour` implementations are booted automatically on scene load, and discarded before the scene unloads, wherever they sit in it. Inactive objects are not booted. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Dependency injection point. |
| `IAsyncBinaryConsumer` | `UniTask ConsumeBinariesAsync()` | Consumes binary data authored through `IBinaryAuthor` (see [Binary Authoring](#binary-authoring)). Not part of the Initium pipeline: the consumer's owner awaits it. |

Boot and initialise execute as batches separated by a single-frame yield. Ordering within a batch is not guaranteed. See [Deployment and Initialisation](#initialisation-pipeline).

### Base object types

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
| `UserInterface<S>` | `UserInterface` (see [Dextra](#the-ui-stack)) |

Their static instances are cleared when Play Mode starts or ends, so no Play session sees the previous one's instance when Domain Reload is disabled.

### Ownership and discarding

`Discard()` is called by whoever owns the object, never by the object itself, and never from an Iris event:

- **Nexus**, through `Initium.DiscardSceneObjects`, for the discoverables placed in a scene it unloads (see [Deployment and Initialisation](#deployment-and-initialisation) and [Nexus](#nexus-scene-management)).
- **The subsystem that created an object**, in its own `Discard()`. Dextra discards its interfaces this way.
- **An interface**, for the selectables and input icons in it.

The framework boots, initialises and discards `IDiscoverable`s: every one it finds, and nothing else. How objects nest is the developer's concern. Either don't nest discoverables, or know how they nest and manage what that implies. Whatever isn't discoverable, the developer boots and discards.

The framework's own object types (`AuraSpatialObject`, `Interactable`, `EntityDetector`, `DextraInputIcon`, the selectables) are never discoverable. Gameplay decides how they are mixed and controlled, so the developer manages them: from the object that owns them, or through a discoverable type of their own.

A subsystem that links objects (a `Linker`) does not tear its links down on a scene event. Each linked object unlinks itself in `Discard()`, as `AuraSpatialObject` does, and its owner calls that in time.

### Subsystem access

```csharp
if (Dextra.TryGetSingleton(out var dextra))
    dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
```

`TryGetSingleton` verifies both instance existence and continued linkage to the core, returning `false` cleanly during teardown. Subsystem references must not be cached across a scene transition without revalidation. Subsystem singletons are also cleared when Play Mode starts or ends.

The core is reached the same way, `Threadlink.TryGetSingleton(out var core)`. `Threadlink.Terminate()` discards it, which uninstalls the update loop and quits the application (exits Play Mode in the Editor).

## Deployment and Initialisation

### Deployment sequence

| Phase | Hook | Action |
|---|---|---|
| 1 | `[OnEnteringPlayMode]` | `NativeWeavingFactory.Register()` registers factories for Sentinel, Chronos, Dextra and Aura. Nexus subscribes to Unity's scene events. |
| 1 | `SubsystemRegistration` | `UserWeavingFactory.Register()` registers project factories. The Sentinel module registry is cleared. |
| 2 | `AfterAssembliesLoaded` | `UserSubsystemsConfig` subscribes to `OnUserSubsystemRegistration`. Sentinel platform modules register with `SentinelModuleRegistry`. |
| 3 | `BeforeSceneLoad` | `NativeSubsystemsConfig` subscribes to `OnNativeSubsystemRegistration`. |
| 4 | `AfterSceneLoad` | `Threadlink.DeployCoreAsync()` executes. |

`DeployCoreAsync` performs:

1. In the Editor, an early return if `ThreadlinkEditorDeployment.Suppressed` is set (see [Installation and Project Setup](#verify-the-deployment)).
2. `await Addressables.InitializeAsync()`.
3. Loading of `ThreadlinkNativeConfig` from the address in `NativeConstants.Addressables.NATIVE_CONFIG`.
4. Loading of `ThreadlinkUserConfig` through the Native Config's `UserConfig` native resource.
5. Core construction and `DeployAsync()`:
   - `Boot()` installs `ThreadlinkPlayerLoop` into Unity's current `PlayerLoop` when the update loop is configured as `Native`; no scene object or `MonoBehaviour` is created. It also subscribes the core's shutdown to `Application.quitting`.
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` publishes the `Func<List<IThreadlinkSubsystem>>` event and passes the collected subsystems through the [initialisation pipeline](#initialisation-pipeline), then logs the woven subsystems.
   - The sequence repeats for `OnUserSubsystemRegistration`. An empty list is reported and skipped.
   - `OnCoreDeployed` is published with the core as payload.
6. The initialisation pipeline is dispatched as fire-and-forget for the discoverable objects of every scene already loaded.

Failure to load either configuration asset aborts deployment with an error. A preloader returning `false` throws, as described below; Sentinel is the exception, since a failed platform does not fail its preloader (see [Sentinel](#deployment)).

### Initialisation pipeline

Initium's pipeline is the one path through which the core prepares subsystems and discoverable objects. It partitions its input by interface and executes three phases, awaiting completion of each before proceeding:

1. `IAddressablesPreloader.TryPreloadAssetsAsync()`
2. `IBootable.Boot()`
3. `IInitializable.Initialize()`

The input is materialised once, so every phase operates on the same objects: an object created during `Boot` is not swept into the `Initialize` phase without having been booted. All preloaders in the first phase are awaited together. If any preloader returns `false`, the pipeline throws `InvalidOperationException` naming the failed preloader and does not proceed to Boot/Initialize. Boot and Initialize are batch-oriented: every object in the phase runs, then a single frame passes; intra-phase ordering is not guaranteed.

At deployment, scene objects are discovered across every loaded scene via `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`.

The pipeline entry point itself is internal. Individual scenes are handled by a public pair:

| Member | Semantics |
|---|---|
| `Initium.BootAndInitSceneObjectsAsync(Scene)` | Preloads, boots and initialises the active `IDiscoverable` objects placed in the scene. |
| `Initium.DiscardSceneObjects(Scene)` | Discards every `IDiscoverable` placed in the scene, active or not, children before parents. An exception thrown by one object is logged and the rest are still discarded. Must run before the scene unloads. |

Scenes held and presented through [Nexus](#nexus-scene-management) are processed automatically, whether one scene is resident or many. Scenes loaded by other means must call both members themselves.

Unity destroys scene objects without invoking `Discard()`, so a scene must never unload any other way: every Nexus unload discards its discoverables first. Scene objects release in `Discard` what they registered in `Boot`, and never discard themselves. Nor does any subsystem tear them down on a scene event: an object registered with a subsystem unregisters itself when Nexus discards it (see [Ownership and discarding](#ownership-and-discarding)).

#### Booting objects directly

Objects the developer owns are booted and initialised through Initium's direct members. None of them runs preloaders.

| Member | Semantics |
|---|---|
| `Boot(IBootable)`, `Initialize(IInitializable)` | One phase, synchronously. A null object is ignored. |
| `BootAndInit<T>(T)` | Both phases, synchronously. |
| `BootAsync(IBootable)`, `InitializeAsync(IInitializable)` | One phase, then a single frame. |
| `BootAndInitAsync<T>(T)` | Boot, a frame, initialise, a frame. |
| `Boot<T>(IReadOnlyList<T>)`, `Initialize<T>(IReadOnlyList<T>)` | Sequential: one object, then a frame, for each object in order. |
| `BootAndInit<T>(IReadOnlyList<T>)` | Sequential boot of the list, then sequential initialisation. |

### Shutdown

Shutdown runs when the application quits, which in the Editor is when Play Mode exits:

1. **Scenes.** Nexus releases every hold. Nothing is presented any more, and each held scene has its objects discarded while the subsystems they registered with still exist, and its unload started. Unity finishes the unloads that quitting cuts short.
2. **Subsystems.** Every woven subsystem is discarded in reverse weave order: user subsystems in reverse of the order they are listed, then the native ones (Aura, Dextra, Chronos, Sentinel). A subsystem is therefore discarded before those it depends on, provided its dependencies are woven first. A subsystem whose `Discard` throws is reported, and the others are still discarded.

`Discard()` is therefore the one place a subsystem releases what it holds: sockets, native memory, platform sessions.

The update loop is uninstalled when the core is discarded or the application quits. When Play Mode ends, Iris clears every listener, Nexus forgets its scenes, and framework statics are reset, so a following Play session starts clean even with Domain Reload disabled.

## Iris: Event Dispatch

`Iris` is a static class backed by `object[] EventRegistry`, dimensioned at type load to span the highest value of `ThreadlinkIDs.Iris.Events` (values can have gaps, see [Domain kinds](#domain-kinds)). Each slot holds a `DelegateList<T>` allocated on first subscription.

### Dispatch signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        (single listener)
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> (single listener)
```

Subscription and unsubscription state the delegate type explicitly:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
```

Subscribing a delegate that is already subscribed has no effect.

Diagnostic members: `TryGetListenerCount(eventID, out int)`, `ContainsListener<T>(eventID, listener)`, `Clear(eventID)`.

### Constraints

- **The delegate type constitutes the contract and is not enforced across subscribers at compile time.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type-mismatch error and discards the subscription. Publishing under a mismatched type is a no-op. Signature agreement remains a project discipline; the native signatures are listed in [Native Iris Events](#native-iris-events).
- **`Func` events throw `InvalidOperationException` beyond a single listener.** They model a single provider rather than a broadcast. With no listener, a `Func` publication returns `default`.
- **Action dispatch iterates in reverse registration order with a fixed publication bound.** Removal or clearing immediately skips affected listeners; additions wait until the next publication (a nested publication counts as new). Slots are compacted after the outermost dispatch, including when a listener throws. Dispatch allocates no snapshot array.
- **Unsubscription belongs in `Discard()`.** A retained delegate keeps a destroyed object reachable and dispatches to stale state.
- Dispatch to an event without listeners is a no-op.
- Every listener is cleared when Play Mode exits.

### Update events

When the update loop is configured as `Native`, `ThreadlinkPlayerLoop` injects three marker systems into Unity's **current** `PlayerLoop` and publishes the Iris events directly; no `GameObject` or `MonoBehaviour` exists.

| Iris event | Native PlayerLoop position |
|---|---|
| `OnUpdate` | Immediately **before** `Update.ScriptRunBehaviourUpdate` |
| `OnFixedUpdate` | Immediately **after** `FixedUpdate.ScriptRunBehaviourFixedUpdate` |
| `OnLateUpdate` | Immediately **after** `PreLateUpdate.ScriptRunBehaviourLateUpdate` |

Installation removes stale Threadlink markers first, then fails fast if any expected Unity anchor cannot be found. The loop is uninstalled when the core is discarded or the application quits, and an entering-play-mode reset protects against stale PlayerLoop state when Domain Reload is disabled. `Custom` mode installs nothing; the project is responsible for publishing the three Iris update events (see [Update loop modes](#update-loop-modes)).

### Declaring events

Declare the identifier in `Threadlink User/Native Domain Injectors/Iris.Events.User.txt` and save.

Iris events are the framework's sole **ordinal** domain: values are array indices allocated in source order, native entries preceding injector entries, and kept by the domain's manifest. Values are never serialised, so removal produces compilation failure at every subscription site. Tombstoning is disabled for this domain accordingly.

## Scribe: Logging

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

this.Send("Loaded ", count, " entries.").ToUnityConsole();
Scribe.Send<MySystem>("Static context message.").ToUnityConsole(DebugType.Error);
```

`Send` accepts `params object[]` and appends each element to a ZString UTF-8 builder, so multi-argument invocation avoids materialising an interpolated string. It returns the builder; `ToUnityConsole(DebugType)` writes it to the Unity console. `DebugType` enumerates `Info` (the default), `Warning` and `Error`.

The message prefix is a type name, in rich-text colour: the runtime type of the receiver for the extension form, or the supplied type argument for the static form. Each severity is also tinted (white, yellow, red).

Scribe is the logging path throughout the framework's runtime and most of its editor tooling. A few build-time tools (the Sentinel build pipeline, the Steam App ID processor, the code formatter, and **Match Addressables to Paths**) log through `Debug` directly.

## Chronos: Time

Chronos's time members are static; `Start()` and `Stop()` belong to the instance.

| Member | Semantics |
|---|---|
| `TimeScale` | Accepts only 0 or 1; other values, and the current value, are ignored. Assignment publishes `OnGamePaused` or `OnGameResumed`. |
| `RawTimeScale` | Identical restriction; publishes nothing. |
| `DeltaTime`, `SmoothDeltaTime`, `UnscaledDeltaTime` | Cached once per `OnUpdate`. |
| `FixedDeltaTime` | Cached once per `OnFixedUpdate`. |
| `CurrentFramerate` | `1 / DeltaTime`. |
| `CurrentTimeSinceDeployment` | Unity's `Time.time`, cached each `OnUpdate`. |
| `TotalPlaytime`, `CountTotalPlaytime`, `PlaytimeCountingMode`, `ClearTotalPlaytime()` | `PlaytimeCountMode` enumerates `Scaled` (the default) and `Unscaled`. |
| `Start()`, `Stop()` | Subscribe and unsubscribe the internal tick handlers. |

`Boot()` resets `TotalPlaytime` to zero, enables `CountTotalPlaytime`, and starts the tick handlers. Playtime accumulation publishes `OnPlaytimeCountTick` with the running total each frame while `CountTotalPlaytime` is set.

Chronos drives its caches from the Iris update events, so in `Custom` update-loop mode its values are only as current as the project's own publication of `OnUpdate` and `OnFixedUpdate`.

### Manual physics simulation

When `ChronosConfig.IrisPhysicsUpdate` is enabled, `Chronos.Boot()` assigns `Physics.simulationMode = SimulationMode.Script` and steps `Physics.Simulate(Time.fixedDeltaTime)` on each `OnFixedUpdate`.

The setting must remain disabled where another framework owns the simulation. The simulation mode is global state: any additional code assigning `SimulationMode.Script` and stepping independently produces double stepping. No runtime guard enforces exclusivity.

## Nexus: Scene Management

`Nexus` is a static class with one scene workflow, for a game with one scene at a time or many at once:

- **Residency:** a scene is loaded while anything holds it.
- **Presentation:** the resident scene the local player sees and hears.
- **Transitions:** presentation behind the fader and loading screen.

Every scene loads additively beside the bootstrap scene (see [Deployment Overview](#deployment-overview)). The bootstrap scene stays loaded throughout, and Nexus never loads or unloads it.

### The ISceneEntry contract

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

The interface is nested in `Nexus` (`Nexus.ISceneEntry`) and may be implemented on any type: a ScriptableObject, a struct, or an asset type belonging to another framework. Every member after the volumes has a default implementation.

- **`PhysicsMode`:** `LocalPhysicsMode.None` shares the default physics scene. A scene resident alongside others should use its own (`LocalPhysicsMode.Physics2D` or `Physics3D`). Query it through `scene.GetPhysicsScene2D()` / `GetPhysicsScene()` and step it explicitly, since Unity does not simulate local physics scenes.
- **`UnloadDelay`:** how long the scene stays resident once nothing holds it. A hold within that time keeps it without reloading.
- **Audio:** a music or ambience pointer that is not mapped (for example `None`) plays nothing on that channel. If neither is mapped, presenting the scene leaves the audio as it is.

### Residency: holds

A scene is resident while anything holds it:

```csharp
Nexus.SceneHold farm = await Nexus.HoldAsync(farmEntry);   // resident and booted; null if it failed to load
Scene scene = farm.Scene;

farm.Release();                                            // unloads once nothing holds it, after entry.UnloadDelay
farm.Release(delaySeconds: 10f);                           // or after this long, unless it is held again meanwhile
```

A `SceneHold` exposes its `Entry`, its `Scene`, and `IsHeld`, which is `false` once released.

- **Loading.** A scene loads once, additively and with the entry's `PhysicsMode`, boots its discoverables through Initium, and awaits `OnFinishedLoadingAsync`. Only then does any holder get it, and `OnSceneResident` is published. Holders arriving during the load share it.
- **Failed loads.** If the scene cannot be loaded or its objects fail to boot, every holder gets `null` and an error is logged. A scene that loaded but failed to boot is discarded and unloaded again; a later hold tries afresh.
- **Unloading**, whatever the reason, follows one path. It awaits `OnBeforeUnloadedAsync`, publishes `OnSceneUnloading`, and discards every `IDiscoverable` in the scene through `Initium.DiscardSceneObjects`, children before parents. Only then does it unload the scene. Scene objects therefore unregister in `Discard` whatever they registered in `Boot`, and never discard themselves. An unload that fails leaves the scene owned by Nexus, and the next hold, release or shutdown retries it.
- **Ordering.** Operations on one scene never overlap. A hold during an unload waits for it, then loads the scene again. A hold during a delayed unload cancels the unload. A scene whose last hold is released while it is still loading unloads once the load completes.
- **Queries.** `Nexus.IsResident(entry)`, `Nexus.TryGetResidentScene(entry, out scene)` and `Nexus.TryGetResidentScene(scenePointer, out scene)` answer only for a scene that is loaded, booted and not unloading.
- **Robustness.** An exception thrown by `OnSceneResident` or `OnSceneUnloading` listeners is logged and cannot stop a load or unload halfway.

Release each hold exactly once, when its holder no longer needs the scene. Releasing it again does nothing. Residency changes nothing the player sees or hears.

### Presentation and transitions

```csharp
bool presented = await Nexus.PresentAsync(entry);      // hold, activate, audio, OnScenePresented, then release the previous scene
bool transitioned = await Nexus.TransitionAsync(entry); // the same behind the fader and loading screen: the single-scene workflow
await Nexus.StopPresentingAsync();                     // present nothing, releasing the presented scene

Nexus.ISceneEntry current = Nexus.Presented;
Nexus.TryGetPresentedScene(out Scene scene);
```

- **Presenting holds the presented scene,** loading it if needed. It makes it the active scene, transitions to its audio scenario, and publishes `OnScenePresented`. Then it releases its hold on the scene presented before. So a scene held only by presentation unloads when something else is presented, while a scene another system holds stays resident.
- **The result** is `false` if the scene could not be loaded; what was presented stays presented. Presenting the scene already presented does nothing and returns `true`.
- **Presentations run one at a time,** in call order. `StopPresentingAsync` takes its turn with them.
- **`TransitionAsync`** wraps `PresentAsync` in `FadeToLoadingScreenAsync` and `FadeToGameplayAsync`, which drive four `Func<UniTask>` events, `OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync` and `OnHideLoadingScreenAsync`, plus Aura's listener volume fade. Each event requires exactly one provider.

The fade steps are public for custom sequences: `FadeToLoadingScreenAsync()`, `FadeToGameplayAsync()`, `FadeFaderAsync(bool visible)`, `FadeLoadingScreenAsync(bool visible)` and `FadeAudioAsync(float targetVolume)`.

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

### Events

| Event | Delegate | Raised |
|---|---|---|
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` (`Scene`, `Mode`) | Synchronously as Unity activates a scene: before it renders, and before its discoverables boot. |
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Once a scene has loaded, booted and run `OnFinishedLoadingAsync`, before any holder gets it. |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Once the presented scene is active and its audio transition has run. |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Before any unload, while the scene's objects are still there, undiscarded. For reading them, such as saving their state; their own `Discard` is their teardown. |
| `OnSceneUnloaded` | `Action<Scene>` | Once a scene has unloaded. The `Scene` is only a key by then. |

`OnSceneLoaded` and `OnSceneUnloaded` republish Unity's own, however a scene was loaded, so project code never subscribes to `SceneManager`.

### Shutdown

When the application quits, which in the Editor is when Play Mode exits, Threadlink's shutdown first releases every hold. Nothing is presented any more, and each held scene has its objects discarded while the subsystems they registered with still exist. Then the subsystems are discarded (see [Shutdown](#shutdown)). Unity finishes the unloads that quitting cuts short.

## Dextra: Input and UI

Dextra is built on Unity's Input System and uGUI. Its preloader loads the Dextra Config, every interface prefab it lists, and the **Dextra Components** prefab, whose `EventSystem` (with an `InputSystemUIInputModule`) Dextra instantiates as a `DontDestroyOnLoad` object, hidden in the Hierarchy when the config's hide flag is set.

### Device resolution

`Dextra.InputDevice` enumerates `MouseAndKeyboard`, `XBOXController`, `PSController` and `SwitchProController`. `CurrentInputDevice` updates automatically from the first control that changes on any device: DualShock and DualSense gamepads resolve to `PSController`, Switch Pro controllers to `SwitchProController`, any other gamepad to `XBOXController`, and keyboard or mouse to `MouseAndKeyboard`. On a change, every gamepad's rumble is stopped and `OnInputDeviceChanged` is published.

`TryGetInputIcon(device, controlPath, out Sprite)` resolves the corresponding glyph from the Dextra Config, where a `DextraInputControlPath` is an Input System control path.

A `DextraInputIcon` is a bootable `LinkableBehaviour`, not a discoverable one. It shows the glyph of its control from `Boot` until it is discarded, and hides its image when no glyph exists for the active device:

- In one of Dextra's interfaces, the interface boots it and discards it.
- Anywhere else, the developer's code that owns it does.

### Input modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The mode-to-`InputActionReference` mapping resides on the Dextra Config as a `FieldHashMap`. A mode resolves to the action map that its referenced action belongs to. `InputModes.Unresponsive` (zero) is the reserved sentinel.

### Pointer and selection

| Member | Semantics |
|---|---|
| `CurrentPointedObject`, `OnPointerEnter`, `OnPointerExit` | The `Selectable` under the mouse, from the UI input module's last raycast, polled once per frame after late update. A change must persist for three polls before it is reported, which absorbs the module's double-firing. |
| `SelectUIElement(GameObject)` | Selects an element through the EventSystem and publishes `OnUIElementSelected`. Returns a `UniTaskVoid`; call `.Forget()`. Passing `null` clears the selection. |
| `ClearEventSystemSelection()` | Clears the selection and publishes `OnUIElementSelected` with `null`. |
| `SetEventSystemActive(bool)`, `SetUIInputModuleActive(bool)` | Enable or disable the EventSystem, or activate or deactivate its UI input module. |
| `TryGetEventSystem(out EventSystem)`, `TryGetUIInputModule(out InputSystemUIInputModule)` | Access to the instantiated objects. |

A selection made natively by the EventSystem is accepted only while the top interface is interactable (`IInteractableInterface`); otherwise it is cleared on the next frame.

### The UI stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and require a `CanvasGroup`. They are not discovered by Initium: Dextra instantiates them from `DextraConfig.interfacePointers` (Addressable prefab identifiers), marks them `DontDestroyOnLoad`, forces their alpha to zero, makes them non-interactable, and boots and initialises them. One instance exists per concrete interface type. Dextra discards them in its own `Discard`. Each interface discards its selectables, and boots and discards its input icons.

Introducing a stacked interface therefore requires constructing the prefab, mapping it through the Addressables Mapping Window, and adding its identifier to `interfacePointers`.

```csharp
dextra.Stack<PauseMenuUI>();
dextra.Stack<ShopUI, ShopData>(data);   // ShopUI implements IStackingDataPreprocessor<ShopData>
dextra.PopTopInterface();
dextra.Cancel();
dextra.ClearStackedInterfaces();
Dextra.IsTopInterface<PauseMenuUI>();
Dextra.IsStacked<PauseMenuUI>();
bool interactable = dextra.TopInterfaceIsInteractable;
```

- **Stacking** an interface that is already on top does nothing. Otherwise the current top is covered, and the new interface is pushed and notified. `Stack<U, D>` hands the payload to `IStackingDataPreprocessor<D>.Preprocess` before the interface is shown, and logs a warning if the interface is not a preprocessor of that type.
- **Popping** removes the top interface and resurfaces the one beneath it.
- **Cancelling** acts only on an `ICancellableInterface` on top. If it reports `IsInSubPanel`, it receives `OnSubPanelCancelled()` and stays. Otherwise it is popped, the interface beneath resurfaces, it receives `OnCancelled()`, and `OnUICancelled` is published with it as payload. Either way Aura's cancel sound plays.
- **Clearing** pops every stacked interface.

#### Interface lifecycle

`UserInterface` reacts to the stack through protected virtual hooks:

| Hook | Default behaviour |
|---|---|
| `OnStacked()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnCovered()` | Fades out unless it is an `IPersistentInterface`; becomes non-interactable. |
| `OnResurfaced()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnPopped()` | Fades out; becomes non-interactable. |

Fades run on `OnUpdate` at unscaled time, over the Dextra Config's fade-in and fade-out durations. `Display()` and `Hide()` start them from derived code; `ForceCanvasGroupAlphaTo(float)` sets alpha immediately, and `SetInteractableState(bool)` toggles interactivity and raycast blocking. `IsVisible` and `IsHidden` report a fully shown or fully hidden interface. An interface with a `Canvas` disables it while fully transparent.

#### Marker interfaces

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Receives `OnCancelled()` and `OnSubPanelCancelled()`; reports `IsInSubPanel` |
| `IPersistentInterface` | Exempt from concealment when covered |
| `IInteractableInterface`, `IInteractableInterface<T>` | Declares selectable content; the generic form exposes `Selectables` and `LastSelectable` |
| `IStackingDataPreprocessor<T>` | Preprocesses the stacking payload |

#### Selectables

`DextraSelectable` wraps a uGUI `Selectable`, raising `OnSelected` and `OnDeselected`, and turns mouse hover into selection so gamepad and mouse navigation behave alike. `DextraSelectable<T>` exposes the wrapped component as `GenericUnitySelectable`; `DextraButton` wraps a `Button` and exposes its `OnClick`. `ClearSelectionListeners()` detaches selection listeners without a full discard, for pooled selectables.

`InteractableUserInterface<Singleton, Selectable>` is the base for an interactable singleton interface. It registers its `Selectables` at boot, remembers the last one selected, reselects it when stacked or resurfaced, and discards its selectables in its own `Discard`.

`NonDrawingGraphic` is a raycast target that draws nothing.

Scene-placed interfaces outside the stack are not managed by it. `ClearStackedInterfaces()` does not affect them, and `DontDestroyOnLoad` instances persist across transitions. Concealment must be invoked explicitly.

### Interactables

`Interactable` derives from `LinkableBehaviour` and carries an `InteractableConfig`; `Interactable2D` and `Interactable3D` bind it to a 2D or 3D collider on the same GameObject, which acts as its active area. Like every framework type it isn't discoverable: the developer's code discards it, which disables its collider and stops it answering `OnInteract`.

`InteractablesDetector2D` (a `CapsuleCollider2D`) and `InteractablesDetector3D` (a `CapsuleCollider`) are the trigger components that find them, publishing `OnInteractableDetected` and `OnInteractableOutOfRange` with the interactable as payload. Both derive from the generic `EntityDetector2D<Area, Entity>` and `EntityDetector3D<Area, Entity>`, which project code can extend for other entity types.

On detection, an interactable subscribes its `Interact` method as `Func<bool>` to `OnInteract`, and unsubscribes when out of range. Project code publishes `OnInteract`, typically from an input action, to interact with whatever is in range. An `InteractableConfig` with `InteractionOptions.InteractOnContact` interacts immediately on detection instead. `InteractionPrompt` exposes the config's prompt text, localised when `THREADLINK_LOCALIZATION` is defined.

`OnInteract` is a `Func` event, so at most one interactable may be in range at any time. Overlapping active areas violate this constraint and throw.

## Aura: Audio

Aura's preloader loads the Aura Config and the **Aura Components** prefab, which it instantiates as a `DontDestroyOnLoad` object. The prefab must contain three children named `Music`, `Atmos` and `SFX`, each carrying an `AudioSource`; without them, deployment fails during preloading. At boot Aura silences the music and ambience channels and creates its own hidden `AudioListener`.

```csharp
aura.DriveAudioListener(position, rotation);     // position-only and rotation-only overloads exist
aura.PlayUISFX(Aura.UISFX.Confirm);              // Cancel, Navigate or Confirm; optional oneShot and volume
await aura.FadeAudioListenerVolumeAsync(0f);
await aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
aura.SetGlobalVolumesMax(musicVolume, atmosVolume);
aura.TryGetMixerValue(name, out float value);
aura.TrySetMixerValue(name, value);
await aura.FadeAudiosourceVolumeAsync(source, target);
aura.MoveTowardsVolume(source, target);
```

- **The listener.** The AudioListener transform is driven rather than parented: some component must invoke `DriveAudioListener` each frame, conventionally a camera controller on `OnLateUpdate`. Absent a driver, the listener retains its last assigned transform. Scene transitions require particular attention, as the previous driver is destroyed before its successor exists.
- **Fades** advance at the config's Volume Fade Speed on unscaled time, one step per frame, and clamp their targets to 0–1.
- **Audio scenarios.** `TransitionToAudioScenarioAsync` fades both channels out, swaps their clips, and fades them in to the requested volumes, capped by the maxima set through `SetGlobalVolumesMax`. Nexus calls both when a scene is presented (see [Nexus](#the-isceneentry-contract)).
- **UI sounds.** `PlayUISFX` loads the config's navigation, confirm or cancel clip and plays it on the SFX channel. With `oneShot: false`, the channel is stopped first.
- **Mixer parameters.** `TryGetMixerValue` and `TrySetMixerValue` read and write exposed parameters of the config's master mixer.

### Spatial objects

Aura links the `AuraSpatialObject`s (including `AuraZone`s) of the **presented** scene only: once the core has deployed, and on every `OnScenePresented`, it disconnects all spatial objects and links the active ones in the active scene. Spatial objects of other resident scenes are never linked, so a scene kept loaded for another player is inaudible.

While anything is linked, Aura sums the linked objects' influence at the listener's position on each `OnUpdate` and moves the music and ambience volumes toward their maxima minus that influence. An object's influence falls linearly from its `Influence` at its source to zero at its radius, which is the AudioSource's maximum distance scaled by the Radius Coefficient, read at runtime from the object's `Source` field or, if that is unassigned, the AudioSource on its GameObject. Custom spatial objects derive from `AuraSpatialObject`, supply `SourcePosition`, and may assign `Radius` themselves or override `GetSpatialInfluence`.

Aura never tears its links down on a scene event. A spatial object unlinks itself when discarded, stopping its source. It isn't discoverable, so the developer's code that owns it discards it before its scene unloads; otherwise Aura keeps evaluating a destroyed object until the next presentation relinks. Aura's own `Discard` disconnects whatever is left, such as the persistent scene's.

The registry is keyed by `LinkableBehaviour.ID`, a hash of the object's name that ignores case. Spatial object names must therefore be unique within a scene: a colliding object is not linked, and a warning names both objects.

## Sentinel: Platform Services

Sentinel is Threadlink's cross-platform platform-service abstraction. Unity determines the hardware/platform; `SentinelConfig` only selects a **distribution** where that platform is ambiguous (for example Windows: Local, Steam, Microsoft Store, Epic or GOG). Runtime modules register exact `SentinelPlatformMarker` / `SentinelDistribution` pairs and expose a capability-driven service graph.

Sentinel provides platform-native affordances: accounts, saves, achievements, presence, friends and invites. It is not a transport or game-state networking layer.

| Page | Covers |
|---|---|
| This page | Deployment, distributions, capabilities, services, accounts, results. |
| [Transactional Saving](#transactional-saving) | `ISaveService` and save transactions. |
| [Social and Invitations](#social-and-invitations) | `IMultiplayerService`: friends, presence, invites, join requests. |
| [Modules](#modules) | The shipped Local and Steam modules, writing a module, the build pipeline. |

### Deployment

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
    string module = sentinel.ActiveModuleDisplayName;   // and ActiveModuleID
    SentinelResult result = sentinel.InitializationResult;
}
```

1. **Platform.** `SentinelRuntimePlatformResolver` maps Unity's runtime platform to a `SentinelPlatformMarker`. In Play Mode, the Editor host's platform is used (Windows, macOS or Linux).
2. **Distribution.** `SentinelConfig.GetDistribution(platform)` picks the distribution, after any [override](#distribution-override).
3. **Module.** `SentinelModuleRegistry` requires an exact platform/distribution match. If no installed module implements the resolved key, deployment fails with `SentinelError.Unsupported`.
4. **Platform initialisation.** The module creates its `ISentinelPlatform`, and Sentinel awaits its `InitializeAsync()`.

**A failed platform does not abort Threadlink.** Only a missing `SentinelConfig` fails Sentinel's preloader. Every platform-level failure leaves Sentinel in `Failed`, logs `InitializationResult` as an error during `Boot`, and lets deployment continue with no platform services: for example, Steam not running, a missing module, or a native initialisation error. `TryGetService` and `HasCapability` return `false` in that state. Whether a failed platform is acceptable (degraded play, an error screen, quitting) is the project's decision, made by checking `State` once deployment completes.

**Retrying, losing the platform, and state changes.**

- `RetryAsync()` deploys the platform again while Sentinel is `Failed`, for example after the player has started Steam, and returns whether it is `Ready`. It returns `true` at once if Sentinel is already `Ready`, and `false` while another retry is running.
- A platform that goes away after initialising reports it: a module calls the protected `SentinelPlatform.ReportLost(result)`, as the Steam module does on `SteamShutdown_t`.
  - Sentinel then moves to `Failed` with `SentinelError.PlatformLost`.
  - It publishes the state change at once, so code using the platform can release it synchronously.
  - It discards the platform one frame later, after the reporting callback has returned.
- Every time a deployment settles (`Ready` or `Failed`), Sentinel publishes the native Iris event `OnSentinelStateChanged` (`Action<Sentinel>`): after the first deployment, after a retry, and on a loss.
- Sentinel deploys before any subsystem initialises, so a subscriber reads `State` once when it subscribes, then follows the event.
- In the Editor and development builds, `SimulatePlatformLoss()` runs the loss path, for tests. It does nothing in release builds.

### Distributions

`SentinelDistributionPolicy` defines which distributions each platform allows. Where only one is allowed, the platform resolves automatically and the Sentinel Config is not consulted; where several are, the config's choice is used, and an invalid choice resolves to `None`, which fails deployment.

| Platform | Allowed distributions |
|---|---|
| Windows | Local, Steam, MicrosoftStore, Epic, GOG |
| MacOS | Local, Steam, MacAppStore |
| Linux | Local, Steam, GOG |
| Android | Local, GooglePlay, AmazonAppstore |
| IOS, TvOS, VisionOS | Local, AppleAppStore |
| Xbox, PlayStation, Nintendo | Native |
| WindowsStore | MicrosoftStore |
| WebGL | Local |

The policy declares the ecosystems a project may target, but a target is usable only when a matching runtime module is installed and registered (see [Modules](#modules)).

#### Distribution override

`SentinelDistributionOverride` replaces the configured distribution at runtime. It is intended for instances that must not deploy the configured ecosystem, such as several development instances on one machine sharing a single Steam account. Sources, in order of precedence:

| Source | Availability |
|---|---|
| `SentinelDistributionOverride.Requested`, assigned by project code before deployment (for example at `BeforeSceneLoad`) | Always |
| Command-line argument `-threadlink-sentinel-distribution <Distribution>` (`COMMAND_LINE_ARGUMENT`) | Editor and development builds |
| Environment variable `THREADLINK_SENTINEL_DISTRIBUTION` (`ENVIRONMENT_VARIABLE`) | Editor and development builds |

Values are case-insensitive `SentinelDistribution` names (for example `Local`). An override must still be allowed for the active platform by `SentinelDistributionPolicy`; a disallowed or unknown value is ignored with a warning. An applied override is logged. `Requested` is reset on entering Play Mode.

### Capabilities

`SentinelCapability` is a flags enumeration. A platform module advertises only what it actually implements; `sentinel.HasCapability(flag)` is `true` only while Sentinel is `Ready`.

| Group | Flags |
|---|---|
| Accounts | `Accounts`, `AccountPicker`, `MultipleLocalAccounts` |
| Saving | `LocalSaves`, `CloudSaves`, `SaveTransactions` |
| Achievements | `Achievements`, `AchievementProgress` |
| Input | `InputOwnership` |
| Online | `Stats`, `Leaderboards`, `Presence`, `Friends`, `Entitlements`, `Commerce`, `Invites` |

`capabilities.Has(flag)` tests a set of flags directly.

### Services

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

| Service | Scope | Contract |
|---|---|---|
| `IAccountService` | Platform | Account list, primary account, refresh and picker operations, account lifecycle events. |
| `ISaveService` | Account | [Transactional Saving](#transactional-saving) |
| `IAchievementService` | Account | `GetStateAsync(id)`, `SetProgressAsync(id, progress)`, `UnlockAsync(id)`. Progress is 0–1; `SentinelAchievementState.Unlocked` is progress at 1. Achievement ID 0 is reserved. |
| `IMultiplayerService` | Account | [Social and Invitations](#social-and-invitations) |

### Accounts

`IAccountService` exposes `Accounts`, `PrimaryAccount`, `RefreshAsync()`, `GetPrimaryAccountAsync(allowUI)` and `PickAccountAsync()`, and the events `AccountAdded`, `AccountRemoved`, `PrimaryAccountChanged` and `AccountStateChanged`. `AccountStateChanged` reports an account's `State` changing, for example between `SignedIn` and `Suspended` as a platform loses and regains its servers.

`ISentinelAccount` carries `ID`, `DisplayName`, `State` (`Unknown`, `SignedOut`, `SigningIn`, `SignedIn`, `Suspended`) and `IsPrimary`, and is itself a service provider, allowing platform-specific account-scoped services such as saves, achievements and multiplayer/social affordances.

### Results

`SentinelResult` / `SentinelResult<T>` carry `Succeeded`, `Error`, `Message`, and an optional `NativeCode`, rather than relying on exceptions for normal service failures. The typed result adds `Value`, and `Untyped()` drops it.

`SentinelError` enumerates `None`, `NotInitialized`, `Unsupported`, `InvalidArgument`, `NotFound`, `Busy`, `Cancelled`, `PermissionDenied`, `NetworkUnavailable`, `QuotaExceeded`, `Conflict`, `CorruptData`, `AccountUnavailable`, `NativeFailure`, `Unknown` and `PlatformLost`. A failure created with `None` is reported as `Unknown`.

## Transactional Saving

`ISaveService` operates exclusively on byte arrays. A logical save is identified by a `saveID` and holds any number of files, each identified by a `fileID`.

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

| Member | Semantics |
|---|---|
| `ISaveService.ReadAsync(saveID, fileID)` | Reads one file of the published save. A missing save or file fails with `NotFound`. |
| `ISaveService.BeginTransactionAsync(saveID)` | Starts a transaction based on the published save. |
| `ISaveService.DeleteSaveAsync(saveID)` | Deletes the save logically, with the same atomic publication guarantee. |
| `ISaveTransaction.ReadAsync`, `WriteAsync`, `DeleteAsync` | Read, write or delete one file within the transaction. |
| `ISaveTransaction.CommitAsync()` | Publishes the transaction's generation; `IsCommitted` reports it. |
| `ISaveTransaction.Discard()` | Releases the transaction, committed or not. |

A transaction prepares a complete logical save while leaving the currently published generation untouched. `CommitAsync()` atomically publishes the new generation; failed or abandoned transactions leave the previous published state intact. Save ID 0 and file ID 0 are reserved.

Serialisation remains the caller's responsibility. `Threadlink.TrySerialize<T>` and `Threadlink.TryDeserialize<T>` provide MessagePack wrappers (with optional `MessagePackSerializerOptions`, and a `ReadOnlyMemory<byte>` overload for deserialisation), but Sentinel itself stores opaque bytes.

### Storage by module

| Module | Storage |
|---|---|
| **Local** | Generation directories under `Application.persistentDataPath/Sentinel/<saveID>/`. A transaction is staged under `.staging`, flushed, and moved into the immutable `generations` directory; publication is the atomic replacement of the save's `current.ptr`. Older generations stay valid for readers that already observed them, and deletion is a pointer tombstone. |
| **Steam** | Steam Remote Storage, one file per published generation, keeping the newest `SteamSentinelSettings.RetainedSaveGenerations` (default 3). A generation is limited to Steam's 100 MB file-write limit and checked against the available quota. Steam Cloud synchronises it when enabled for the app and the account. |

## Social and Invitations

The account-scoped `IMultiplayerService` exposes platform-native social and session affordances only. Networking owns the creation, serialisation and interpretation of join payloads; Sentinel carries them as opaque strings. The contract lives in the `Threadlink.Sentinel.Multiplayer.Contracts` assembly, which project code references explicitly.

| Member | Semantics |
|---|---|
| `GetFriendsAsync()` | The friend list as `SentinelFriend` values: `ID`, `DisplayName`, `Presence`, `IsPlayingThisGame`, `IsJoinable`, and the friend's public `JoinPayload`. |
| `SetSessionPresenceAsync(SentinelSessionPresence)` | Publishes the local player's session: a public `JoinPayload`, plus optional `Status`, `GroupID` and `GroupSize`. |
| `ClearSessionPresenceAsync()` | Clears the published session. |
| `InviteFriendAsync(friendID)` | Invites a friend to the published session. |
| `InviteFriendAsync(friendID, joinPayload)` | Sends a private opaque payload to one friend without publishing it. |
| `JoinRequested`, `TryDequeueJoinRequest(out SentinelJoinRequest)` | Join requests: `SourceUserID`, `SourceDisplayName` and the `JoinPayload` to join with. |

`SentinelFriendPresence` enumerates `Unknown`, `Offline`, `Online`, `Busy`, `Away`, `Snooze`, `LookingToTrade` and `LookingToPlay`.

### Private invitations

`InviteFriendAsync(friendID)` uses the published session presence; `InviteFriendAsync(friendID, joinPayload)` sends a private payload instead. The caller owns token creation, authorisation, expiration and revocation. Both use the same `SentinelJoinRequest` delivery, including cold launches.

A `SentinelSessionPresence` with a null or empty `JoinPayload` retains status and group information but clears the joinable connect key, making the session non-joinable.

### Join requests

A join request is delivered to `JoinRequested` if it has a subscriber, and queued otherwise, so a request that arrives before the game is ready to handle it is not lost: the game drains the queue with `TryDequeueJoinRequest` once it is. A game launched by accepting an invitation (a cold launch) receives the request the same way; its source user is unknown.

### Steam

Steam remains the only current provider. The join payload is published as the `connect` rich-presence value, encoded as `+threadlink_join <payload>` with the payload in URL-safe Base64, and must fit within Steam's rich-presence value limit. Cold-launch requests are recovered from Steam's launch command line. `status`, `steam_player_group` and `steam_player_group_size` carry the optional presence fields. Sentinel continues to own the Steam lifecycle and its callbacks.

## Modules

A **module** implements Sentinel for exact platform/distribution pairs. It has a runtime half, which registers a platform factory with `SentinelModuleRegistry`, and an editor half, an `ISentinelBuildModule` that configures and validates builds for the same pairs.

### Shipped modules

| Module | ID | Keys | Capabilities |
|---|---|---|---|
| **Threadlink Local** | `threadlink.local` | Windows, macOS, Linux / `Local` | `Accounts`, `LocalSaves`, `SaveTransactions`, `Achievements`, `AchievementProgress` |
| **Steam** | `threadlink.steam` | Windows, macOS, Linux / `Steam` | `Accounts`, `LocalSaves`, `SaveTransactions`, `Achievements`, `AchievementProgress`, `Presence`, `Friends`, `Invites`; `CloudSaves` when Steam Cloud is enabled for the app and the account |

#### Local

The Local module needs no platform SDK or storefront configuration.

- **One implicit account**, `local-user` ("Local User"), always `SignedIn` and primary. `PickAccountAsync` fails with `Unsupported`, and the account events never fire.
- **Saves** are generation-based files under `Application.persistentDataPath/Sentinel` (see [Transactional Saving](#storage-by-module)).
- **Achievements** are held in memory for the session and are not persisted. Progress only increases.

#### Steam

The Steam module requires Steamworks.NET (`com.rlabrecque.steamworks.net`, validated against 2025.165.0) and the `STEAMWORKS_NET` scripting define; its assembly compiles only for standalone platforms and the Editor.

- **Sole lifecycle owner.** The module initialises and shuts down the Steam API itself, and pumps Steam callbacks every frame from a hidden persistent object. If Steamworks.NET was initialised by other code first, deployment fails with `Conflict`.
- **The account follows the client's connection to Steam's servers.** It is `SignedIn` when connected and `Suspended` in offline mode or without a connection, and `AccountStateChanged` reports each change.
- **The Steam client shutting down** reports the platform lost.
- **App ID:** the project-root `steam_appid.txt` is the one App ID source.
  - At startup the expected App ID is resolved from the `SteamAppId` environment variable (which Steam sets when it launches the game), then `steam_appid.txt` in the working directory, then beside the executable. If one resolves and Steam initialises under a different App ID, deployment fails.
  - Development builds get a copy of `steam_appid.txt` beside the executable.
  - Release builds ship without it, as Valve requires, and get the App ID in `<Data>/threadlink_steam_appid.txt` instead: a file the Steam client never reads.
  - With it, a release build started outside Steam calls `SteamAPI.RestartAppIfNecessary` and Steam relaunches it.
- **Saves** use Steam Remote Storage (see [Transactional Saving](#storage-by-module)).
- **Achievements.** A Threadlink achievement ID maps to the Steam API name `TL_ACH_<ID as 8 hex digits>` unless registered otherwise. Partial progress is persisted only through a registered progress stat; without one, `SetProgressAsync` below 1 fails with `Unsupported`. Progress only increases.

`SteamSentinelSettings` (in `Threadlink.Sentinel.Steam.Shared`) configures the module in code, before Sentinel deploys:

| Member | Default | Semantics |
|---|---|---|
| `RestartAppIfNecessary` | `true` | Whether a player build started outside Steam asks Steam to relaunch it. |
| `RetainedSaveGenerations` | 3 | Published save generations kept per save; at least 1. |
| `RegisterAchievement(id, apiName, progressStatAPIName, progressMaximum)` | — | Maps an achievement ID to an explicit Steam API name and, optionally, an integer progress stat with its maximum. |
| `RemoveAchievement(id)`, `ClearAchievementOverrides()` | — | Remove explicit mappings. |
| `ResolveAchievement(id)`, `BuildDefaultAchievementAPIName(id)` | — | The mapping in effect, and the default name. |

### Writing a runtime module

1. Derive a platform from `SentinelPlatform` (or implement `ISentinelPlatform`) for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair. Implement `Capabilities` and `InitializeAsync()`, which returns a `SentinelResult`.
2. In `InitializeAsync`, register platform-level services with the protected `RegisterService<T>`, and account-scoped services on `SentinelAccount` implementations the same way. `ReplaceService<T>` swaps a service, discarding the previous one; `UnregisterService<T>` removes one.
3. Advertise only capabilities actually implemented.
4. If the platform can go away after initialising, call `ReportLost(result)`; it is safe to call from an SDK callback.
5. Register a `SentinelModuleDescriptor` (platform, distribution, module ID, display name, platform factory) through `SentinelModuleRegistry.Register` from an `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]` hook. The registry is cleared at `SubsystemRegistration`, and Sentinel resolves it during preloading.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
private static void Register()
{
    SentinelModuleRegistry.Register(new SentinelModuleDescriptor
    (
        SentinelPlatformMarker.Windows,
        SentinelDistribution.Epic,
        "studio.epic",
        "Epic Games",
        static () => new EpicSentinelPlatform()
    ));
}
```

`Register` returns a failed `SentinelResult` for an unknown platform or `None` distribution, a pair the distribution policy does not allow, an empty module ID or display name, or a key another module has already registered (`Conflict`).

### The build pipeline

Before every build, Sentinel resolves the target the same way the runtime does: Unity build target to `SentinelPlatformMarker`, `SentinelConfig` to `SentinelDistribution`, and the exact key to exactly one installed `ISentinelBuildModule`. The build fails if:

- the build target is not supported;
- there is not exactly one `SentinelConfig` asset, or the Native Config's `SentinelConfig` resource does not reference it;
- the config holds no valid distribution for the platform;
- no build module, or more than one, implements the key;
- the module throws, or reports an error diagnostic.

An `ISentinelBuildModule` exposes `ModuleID`, `DisplayName`, `Implements(in SentinelModuleKey)`, `ConfigureBuild` and `ValidateBuild`. The last two receive a `SentinelBuildContext` (the `BuildReport`, the key, the output path, and `GetSubtarget<T>()`) and append `SentinelBuildDiagnostic.Info`, `Warning` or `Error` entries, which are logged. Build modules are found through the `TypeCache` and must expose a public parameterless constructor.

The Steam build module checks that Steamworks.NET is installed and that `steam_appid.txt` holds a valid App ID, and warns if it is Valve's test App ID 480.

The Sentinel Config's Inspector shows the current build target, its Sentinel platform, and the distribution choice the target allows.

## Vault Runtime API

`Vault` (namespace `Threadlink.Vault`) is a `LinkableAsset`. Its fields are read and written by identifier:

```csharp
vault.Has(fieldID);
vault.TryGetDataField(fieldID, out DataField field);
vault.TryGetGenericDataField<float>(fieldID, out DataField<float> field);
vault.TryGetConcreteDataField<Float>(fieldID, out Float field);
vault.TryGet<float>(fieldID, out float value);
vault.TrySet<float>(fieldID, value);
```

`TryGet<T>` fails when the field is absent, is not a `DataField<T>`, or holds `null`. `TrySet<T>` fails when the field is absent or is not a `DataField<T>`. Every member is virtual, so a derived Vault can intercept access.

Fields are stored in a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`, backed by `[SerializeReference]` to permit polymorphic field types. The shipped field types reside in `Threadlink.Vault.DataFields` and are listed in [The Vault](#field-composition).

`DataField<T>` exposes `Value` and an `OnValueChanged` event, raised on every assignment. The backing is either `SerializedValue<T>` or `TransientValue<T>`; the transient variant is `[NonSerialized]` and resets per session. As a Vault is a ScriptableObject, transient state persists across editor play-mode sessions unless explicitly reset. Discarding a field clears its backing and raises `OnValueChanged` with `default` before removing its listeners. `TryApplyValueTo(vault, fieldID)` copies a field's value into another Vault's field of the same type.

### Custom field types

A new field type is a serialisable subclass of `DataField<T>` in a runtime assembly:

```csharp
[Serializable] public sealed class Colour : DataField<Color> { }
```

It then appears in the Inspector's field-type dropdown.

### Timeline integration

Under `THREADLINK_TIMELINE`, a Timeline can carry Vault values:

- **`VaultMarker`** is a Timeline marker holding a list of field identifiers with typed values, and notification flags.
- **`VaultTrack`** is a marker track bound to a `VaultReceiver`.
- **`VaultReceiver`** is a component that raises `OnMarkerReceived(PlayableDirector, VaultMarker)` when a marker fires. `ClearCallback()` removes its listeners.

Applying the values is the listener's decision: `marker.TryApplyConfigTo(vault)` writes every value of the marker into the given Vault, logging each field it could not set.

## Resource Loading

Two parallel APIs exist: one keyed by generated identifier on the core instance, one accepting an Addressables reference directly as static members.

```csharp
core.LoadAsset<T>(ThreadlinkIDs.Addressables.Assets id);
Threadlink.LoadAsset<T>(AssetReference reference);
await core.LoadAssetAsync<T>(id);
await Threadlink.LoadAssetAsync<T>(reference);

core.LoadPrefab<T>(ThreadlinkIDs.Addressables.Prefabs id);       // T : Component
Threadlink.LoadPrefab<T>(AssetReferenceGameObject reference);
await core.LoadPrefabAsync<T>(id);
await Threadlink.LoadPrefabAsync<T>(reference);

await core.LoadSceneAsync(ThreadlinkIDs.Addressables.Scenes id, LoadSceneMode mode);
await core.LoadSceneAsync(id, new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D));
await core.UnloadSceneAsync(id);

core.ReleaseAsset(id);
core.ReleasePrefab(id);
```

Asset and prefab loads return `null` when the identifier is unmapped or its reference invalid; a prefab load also returns `null` when the prefab carries no `T`. Synchronous loads block until the Addressables operation completes. A failed load is logged and its handle released. Loading a reference whose asset is already loaded, or loading, reuses that operation.

Scenes are loaded through [Nexus](#nexus-scene-management) in normal use. The scene members here are the layer beneath it, for code that manages a scene by other means and therefore also boots and discards its objects (see [Deployment and Initialisation](#initialisation-pipeline)).

### Queries and validation

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes id, out Scene scene);
core.CheckIDValidity(id);                                                  // scenes, assets or prefabs
int found = core.CollectAssetIDs<T>(List<ThreadlinkIDs.Addressables.Assets> result);   // mapped assets of type T, none loaded
```

All lookups resolve through the User Config's keyed maps and validate `RuntimeKeyIsValid()`, reporting failure through Scribe.

`CollectAssetIDs<T>` appends every mapped asset whose main type is `T` or derives from it, without loading any, and returns how many it appended. When the Mapping Window applies, it records each asset's main type in the User Config (`assetTypes`, as `"Namespace.Type, Assembly"`) beside its reference. A consumer that needs every asset of a kind (a game's simulation content, say) asks for their IDs and loads them, and keeps no catalogue of its own. Assets mapped before types were recorded are found once the mappings are applied again.

### Renamed assets

An asset ID is a hash of the group and the asset's name, so renaming an asset or moving it to another group gives it a new ID, and the old one becomes a tombstone. The manifest records each mapped asset's GUID, so applying the mappings finds the asset again under its new name and records the old ID as an alias of the new one in the User Config (`assetAliases`).

```csharp
core.TryResolveRenamedAsset(oldID, out ThreadlinkIDs.Addressables.Assets currentID);
core.CollectRenamedAssets(List<(ThreadlinkIDs.Addressables.Assets OldID, ThreadlinkIDs.Addressables.Assets CurrentID)> result);
```

Data that stored the old ID (a save) can resolve it or rewrite it. Aliases exist only for renames made after GUIDs were recorded: entries mapped earlier get theirs the next time the mappings are applied.

### Scene references

Scene loads are "load or get": loading a scene that is already loaded (or loading) returns that instance, whatever parameters are passed. `UnloadSceneAsync` completes once the scene has actually unloaded. A failed scene load or unload is reported through Scribe and returns a default `SceneInstance` instead of throwing.

`SceneAssetReference` tracks its load through `SceneOperation` rather than `AssetReference.OperationHandle`, which Addressables only populates for `LoadSceneMode` loads. `LoadSceneAsync(LoadSceneParameters)`, `UnloadSceneAsync(bool autoReleaseHandle)` and `TryGetLoadedScene(out Scene)` extend the base API. The base `LoadSceneAsync(LoadSceneMode)`, `UnLoadScene()` and `ReleaseAsset()` are routed through the same operation.

The reference overloads exist for portable modules: a module may hold its own references without depending on the consuming project's generated `Assets` enumeration. The underlying extension methods (`LoadSynchronously<T>`, `LoadAsync<T>`, and the scene `LoadAsync` / `UnloadAsync`) are public in `Threadlink.Shared.AssetReferenceExtensions`.

### Native resource batches

For framework-owned resource batches, `ThreadlinkNativeConfig.LoadNativeResourcesAsync` accepts an `AddressablesRequest<NativeResources>` and fills a caller-provided dictionary. The request uses a fixed-capacity `UnsafeList<T>` (`new AddressablesRequest<T>(count, allocator)`, `Add`, `Dispose`), and the loader batches Addressables operations through pooled task storage before collecting results. Single native resources load through `LoadNativeResourceAsync<T>(id)`.

## Implementing a Subsystem

A project subsystem derives from `ThreadlinkSubsystem<T>` (or `Register`, `Linker` or `Weaver`, see [Architecture](#register-hierarchy)) and implements whichever further lifecycle interfaces it needs. Every subsystem is bootable and discardable; overrides of `Boot()` and `Discard()` call the base implementation, which publishes and clears the singleton.

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

The order of the list is the weave order. Shutdown discards subsystems in reverse weave order, so a subsystem should be woven after the subsystems it depends on (see [Shutdown](#shutdown)).

> Non-trivial construction is accommodated by assigning a factory delegate to `WeavingFactory<T>.OnCreate` in place of `Register<T>()`. `WeavingFactory<T>.OnCreateFrom` supplies the clone factory used by `TryWeave<T>(original, out T)`. Each holds a single delegate: adding one replaces the previous, and removing clears it. `ThreadlinkSubsystems.Register<T>()` (namespace `Threadlink.Utilities.Subsystems`) is equivalent to `WeavingFactory.Register<T>()`.

## Deterministic Toolkit

Everything a simulation needs to give the same result on every machine, runtime and compiler: numbers, vectors and rotations in fixed point, and the framework's one source of randomness. Networked simulation, lockstep, replays and procedural generation build on it.

The toolkit lives in the `Threadlink.Deterministic` assembly (namespace `Threadlink.Deterministic`), which references no engine type, so a deterministic simulation assembly can depend on it without depending on Unity. Its only Threadlink dependencies are `Threadlink.Shared` (hashing) and `Threadlink.Generated` (RNG domains).

| Page | Covers |
|---|---|
| [Fixed Point](#fixed-point) | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, and conversion to and from Unity types. |
| [StatelessRNG](#statelessrng) | Streams, sources, identity, samplers, domains. |

`Bits` provides bit counts and rotations of 64-bit words that are the same on every runtime (Mono, IL2CPP and CoreCLR): `TrailingZeroCount`, `LeadingZeroCount`, `PopCount` and `RotateLeft`.

State that is copied, hashed and saved as raw bytes, such as a simulation's frames, is the domain of the [Memory](#memory) toolkit.

## Fixed Point

### FP

A Q32.32 fixed-point number: 32 integer and 32 fractional bits in a `long`, for a range of about ±2.1 billion at a uniform resolution of 2^-32 (about 2.3e-10). Every operation is integer arithmetic.

- **Exact arithmetic.** `+`, `-`, `*`, `/` and `%` give the true result, rounded to nearest with ties away from zero, so results are symmetric in sign: `(-a) * b == -(a * b)`. `%` is the remainder of truncated division, with the dividend's sign. `MultiplyTowardZero(a, b)` truncates the exact product instead, for conservative geometry that must never overshoot.
- **Saturation.** Results beyond the range stop at `MaxValue` or `MinValue` instead of wrapping to the opposite sign.
- **Nothing throws.** Dividing by zero saturates toward the dividend's sign (zero over zero is zero). Each function states its value outside its domain: `Sqrt` of a negative value is zero, `Log` and `Log2` of zero or less are `MinValue`, `Asin` and `Acos` clamp their argument to [−1, 1], and `Pow` of a negative base with a fractional exponent is zero.
- **Functions.** `Sqrt` is exact: the true root, rounded to nearest. `Sin`, `Cos`, `SinCos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `Exp`, `Log`, `Log2` and `Pow` are evaluated in Q3.61 and rounded once, to within about one raw unit of the true value. Trigonometric arguments are reduced exactly, so `Sin(1e6)` is as accurate as `Sin(1)`. `Pow` with a whole exponent multiplies by repeated squaring.
- **Rounding.** `Floor`, `Ceiling`, `Round` (halves away from zero), `Truncate`, `Frac`, and `FloorToInt`, `CeilToInt` and `RoundToInt`.
- **Helpers.** `Abs`, `Sign`, `Min`, `Max`, `Clamp`, `Clamp01`, `Lerp`, `LerpUnclamped`, `InverseLerp` and `MoveTowards`.
- **Constants.** `Zero`, `One`, `MinusOne`, `Half`, `Two`, `Epsilon` (2^-32), `MaxValue`, `MinValue`, `Pi`, `TwoPi`, `PiOver2`, `PiOver4`, `E`, `Ln2`, `Deg2Rad` and `Rad2Deg`.
- **Construction and conversion.** `FromInt`, `FromFraction(numerator, denominator)` and `FromRaw(raw)`; `int` converts implicitly and exactly; `long`, `float` and `double` convert explicitly, saturating (NaN is zero). Converting to `int` rounds toward negative infinity.

```csharp
FP speed = 3;                           // integers convert implicitly and exactly
FP chance = FP.FromFraction(3, 20);     // exactly the nearest value to 0.15
FP friction = (FP)0.92;                 // a literal, converted once: deterministic
FP angle = FP.Atan2(dy, dx) * FP.Rad2Deg;
```

Converting a literal or an authored value (a serialized field) is deterministic, since its bits are the same everywhere. A float computed at runtime is not, so it never flows into simulated state. Serialize and hash `RawValue`, never a converted float. `(float)value` is for presentation. `GetHashCode` is defined explicitly from `RawValue`, so no runtime can change it, and `ToString` formats with the invariant culture.

### Vectors and rotations

`FPVector2`, `FPVector3` and `FPQuaternion` are mutable structs of `FP` components, with the usual operators.

- **Exact lengths.** `Magnitude` is the true length of the raw components (a 128-bit sum of squares and an exact root), rounded once. `SqrMagnitude` is rounded once and saturated. `Normalized` scales the components exactly before measuring, so a vector one raw unit long normalizes as well as one a million units long; the zero vector normalizes to zero.
- **`FPVector2`.** `Zero`, `One`, `Up`, `Down`, `Left`, `Right`; `Dot`, `Cross` (the z of the 3D cross product), `Distance`, `SqrDistance`, `Lerp`, `LerpUnclamped`, `MoveTowards`, `ClampMagnitude`, `Perpendicular` (a quarter turn counterclockwise), `Rotate`, `FromAngle`, `Angle` and `SignedAngle`.
- **`FPVector3`.** `Zero`, `One`, `Up`, `Down`, `Left`, `Right`, `Forward`, `Back`; `Dot`, `Cross`, `Distance`, `SqrDistance`, `Lerp`, `LerpUnclamped`, `MoveTowards`, `ClampMagnitude`, `Angle`, `Project` and `ProjectOnPlane`. Axes follow Unity: y up, z forward.
- **`FPQuaternion`.** Unit rotations: `Identity`, `FromAxisAngle` (a zero axis is no turn), composition with `*` (`a * b` rotates by `b`, then by `a`), `Rotate` (or `rotation * vector`), `Conjugate`, `Inverse`, `Dot`, `Magnitude`, `SqrMagnitude`, `Normalized`, `Lerp` (normalized), `Slerp` and `Angle`. Renormalize a rotation composed many times over, since each product rounds.

### Conversions

`FixedPointConversions`, in `Threadlink.Runtime` (namespace `Threadlink.Deterministic`), converts between these and Unity's types:

| Direction | Members | Use |
|---|---|---|
| To fixed point | `ToFP()` on `float`, `Vector2`, `Vector3`, `Vector2Int`, `Vector3Int` and `Quaternion` (renormalized after conversion) | Authored data and setup |
| To Unity types | `ToFloat()`, `ToVector2()`, `ToVector3()` (from `FPVector3`, or from `FPVector2` in the xy plane) and `ToQuaternion()` | Presentation only |

## StatelessRNG

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

### Sources and seeds

`StatelessRNG.Seed` is the process-wide seed, set through `SetSeed`. Every machine simulating a world must use the same one, and the world's save keeps it. The static `CreateStream` overloads draw from `StatelessRNG.Global`, the source of that seed.

A world that can share its process with another (a host's authoritative simulation and its prediction, two worlds in a test, a world and a preview of another) keeps its own **`StatelessRNG.Source`** instead, so the worlds never share randomness:

```csharp
var rng = new StatelessRNG.Source(worldSeed);
var loot = rng.CreateStream(ThreadlinkIDs.StatelessRNG.Domains.Loot, chestID, day);
```

For the same seed, a source gives exactly the streams the static methods give for `StatelessRNG.Seed`. A `default` source is the source of seed 0.

### Streams and identity

- **Streams.** Each draw advances the stream copy it is made on; `Position` counts the draws. Create streams where you roll, one per purpose, entity and tick, so a result never depends on how many draws came before it. Two streams created from the same identity give the same numbers. `Fork(part)` derives a finer stream from a stream's identity, independent of the draws made on it.
- **Identity.** Parts fold in order through SplitMix64's finalizer, with each part mixed on its own first. Order matters, equal parts don't cancel, a zero seed with zero parts isn't degenerate, and a domain's whole 32-bit ID counts. `CreateStream` takes a domain alone, up to three `ulong` parts, a `ReadOnlySpan<ulong>` of any number (`stackalloc` allocates nothing), or an `IContext`.
- **Contexts.** An `IContext` is a readonly struct that adds parts that belong together, always in the same order, through `Compose(ref StatelessRNG.Identity identity)`. `Identity.Add` accepts `ulong`, `long`, `uint`, `int`, `FP` (its raw value) and `string`.
- **Strings.** `StatelessRNG.Part(text)` turns a string into a part, hashed as Threadlink hashes names (trimmed, ignoring case). Hash fixed strings once and keep the part.

```csharp
public readonly struct AttackRoll : StatelessRNG.IContext
{
    public readonly uint Attacker, Target, Tick;

    public void Compose(ref StatelessRNG.Identity identity)
    {
        identity.Add(Attacker);
        identity.Add(Target);
        identity.Add(Tick);
    }
}
```

### Samplers

| Member | Result |
|---|---|
| `NextUInt64()`, `NextUInt32()` | Raw bits. |
| `Range(int min, int max)`, `Index(count)` | An integer in [min, max), or [0, count). |
| `Range(FP min, FP max)`, `Value01()` | An `FP` in [min, max), or [0, 1). |
| `Bool()`, `Chance(FP probability)`, `Chance(numerator, denominator)` | A coin flip, or an outcome at the given odds. |
| `Angle()` | An angle in [0, 2π). |
| `OnUnitCircle()`, `InsideUnitCircle()`, `OnUnitSphere()`, `InsideUnitSphere()` | Points on or within the unit circle or sphere. |
| `Rotation()` | A uniformly random rotation. |
| `Pick(items)` | An element of a span, array or read-only list. |
| `Shuffle(items)` | Fisher–Yates over a span, array or list. |
| `WeightedIndex(weights)` | An index in proportion to `int` or `FP` weights. |

Every sampler is unbiased (Lemire's method, redrawing the rare biased case), and makes the same draws whatever its arguments' values: a probability of zero advances the stream as much as any other.

**Not for secrets or uniqueness.** Session tokens and nonces come from `System.Security.Cryptography.RandomNumberGenerator`, and unique IDs from `Guid`.

### Domains

Domains partition streams: systems drawing from one seed under distinct domains do not interfere. New domains are declared in `StatelessRNG.Domains.User.txt` and surface as `ThreadlinkIDs.StatelessRNG.Domains`.

> Domain values are name hashes. Renaming a domain alters its stream, causing divergence in anything reproducing a prior sequence from a stored seed or replay. Domain names constitute part of the save format, as does the stream format itself.

## Memory

`Threadlink.Memory` (namespace `Threadlink.Memory`) is the toolkit for long-lived state that is copied, hashed and saved as raw bytes, such as a simulation's frames. It allocates native memory, hashes it identically on every machine, and lays collections out inside it so that a collection is copied, hashed and saved with the bytes that hold it.

The assembly has no Threadlink dependencies and nothing in the framework references it. Project code that uses it adds a reference to `Threadlink.Memory` in its assembly definition and enables unsafe code.

### NativeAllocator

| Member | Semantics |
|---|---|
| `AllocateZeroed(bytes, alignment = DEFAULT_ALIGNMENT)` | A block of `bytes` zeroed bytes, aligned to `alignment` (a power of two; `DEFAULT_ALIGNMENT` is 64, a cache line). Zeroing is not optional: a block hashed whole must not contain whatever the allocator left there. |
| `Free(block)` | Frees a block from `AllocateZeroed`. A null block is ignored. |
| `Copy(destination, source, bytes)` | Copies between non-overlapping ranges. |
| `Clear(block, bytes)` | Zeroes a range. |
| `Equal(a, b, bytes)` | Whether two ranges hold the same bytes. |

Blocks come from Unity's tracked persistent allocator, so the Editor's leak detection reports a block that is never freed. Invalid sizes and alignments throw `ArgumentOutOfRangeException`; a failed allocation throws `OutOfMemoryException`.

### MemoryHash

Hashes of raw memory: the same bytes give the same hash on every machine and runtime, which a type's own `GetHashCode` does not promise.

| Member | Use |
|---|---|
| `XxHash64(byte* data, long length, long seed = 0)` | Fingerprints whole blocks, for example a simulation frame, to detect divergence. Gives exactly the value of `System.IO.Hashing.XxHash64`, computed directly over raw memory, which is many times faster than the library in the Editor's Mono runtime. Big-endian runtimes fall back to the library. |
| `XxHash32(byte* data, int length)`, `XxHash32<T>(in T value)` | Places keys in hash tables. A value's padding must be zeroed. |

### Inline collections

The inline collections are views over a fixed number of slots inside another value's memory, such as a simulation component. Each is a `ref struct` over a pointer, because it points into memory that can be copied or moved (a frame rolled back): it cannot be stored, boxed or captured, so no pointer outlives the call that made it. Elements are `unmanaged`.

| Type | Behaviour |
|---|---|
| `InlineArray<T>(memory, length)` | `Length` slots, always present. Indexer by reference, `Clear()`, enumeration. |
| `InlineList<T>(memory, capacity)` | `Count`, indexer, `Add`, `RemoveAt` (moves the last element into the gap), `Clear()`, enumeration. |
| `InlineQueue<T>(memory, capacity)` | A first-in, first-out ring: `Enqueue`, `Dequeue`, `Peek`, `Clear()`. |
| `InlineStack<T>(memory, capacity)` | Last in, first out: `Push`, `Pop`, `Peek`, `Clear()`. |
| `InlineHashSet<T>(memory, capacity)` | `Contains`, `Add` and `Remove` (each reporting whether it changed the set), `Clear()`, enumeration. |
| `InlineDictionary<TKey, TValue>(memory, capacity)` | `ContainsKey`, `TryGetValue`, indexer by reference (throws if absent), `TryAdd`, `Remove`, `Clear()`, enumeration of `(Key, Value)`. |

Adding past capacity throws. Element order depends only on the operations made, as everything hashed or compared byte for byte must.

Sets and dictionaries use open addressing with linear probing over a fixed slot table. Keys are hashed and compared as raw bytes, never through `GetHashCode` or `Equals`, so a table holds the same bytes on every machine; a key's padding must therefore be zeroed. Removal leaves a marker that a later insertion reuses, so churn never exhausts the table. The capacity is the table's size: declare headroom above the most entries expected, since a nearly full table probes further.

#### Layout

`InlineLayout` is the reference for the byte layout, and a layout generator must produce exactly its sizes:

- Every header, slot and value starts on an 8-byte boundary (`InlineLayout.Align(size)` rounds up to a multiple of 8).
- A list, queue, stack, set or dictionary starts with an 8-byte header (`HEADER_BYTES`) holding its count; a queue's header also holds its head. An array has no header.
- A set or dictionary slot is an 8-byte state (empty, occupied or removed), then the key, then the value.

| Member | Bytes of |
|---|---|
| `ArrayBytes(capacity, elementSize)` | An array. |
| `SequenceBytes(capacity, elementSize)` | A list, queue or stack. |
| `SetSlotBytes(elementSize)`, `SetBytes(capacity, elementSize)` | One hash-set slot; a hash set. |
| `DictionarySlotBytes(keySize, valueSize)`, `DictionaryBytes(capacity, keySize, valueSize)` | One dictionary slot; a dictionary. |

```csharp
int bytes = InlineLayout.SequenceBytes(capacity: 16, sizeof(int));
byte* block = NativeAllocator.AllocateZeroed(bytes);

var list = new InlineList<int>(block, capacity: 16);
list.Add(42);
ulong fingerprint = MemoryHash.XxHash64(block, bytes);

NativeAllocator.Free(block);
```

## Identifier Domains and Code Generation

A single pipeline produces every enumeration under `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

### Domain kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | xxHash32 of the scope-qualified key (`scope/name`, trimmed, ignoring case) | Retained as `[Obsolete]` tombstone with value preserved | All domains except Iris |
| **Ordinal** | The lowest free index, allocated in source order and kept by the manifest; removing an entry leaves a gap that a later entry may fill | Removed outright | `Iris.Events` |

Identity is the default kind, guaranteeing that removal cannot shift another entry's value. Iris is ordinal because dispatch indexes `EventRegistry` with the value directly; the registry spans the highest value, so the gaps removals leave are harmless. As Iris values are never serialised, outright removal producing compilation failure is the correct failure mode.

### Sources

Domain entries originate from up to three sources, merged in order:

1. **Native entries**: a framework-owned `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`), with an empty scope.
2. **Injectors**: files named `{DomainName}.{Injector}.txt` within the injector directory, its subfolders included. The injector name becomes the **scope**, folded into each entry's hash key. `Iris.Events.User.txt` is the injector named `User`. Injectors are read in ordinal file-path order.
3. **Domain definitions**: a `.txt` at the top level of the definitions directory declares an entirely new enumeration named after the file, emitted through the shared `CustomDomain.Shell.txt` into namespace `Threadlink.User` and assembly `Threadlink.User.Generated`.

In every source, blank lines and lines starting with `//` are ignored, and an entry repeated with the same scope (ignoring case) is read once.

Injectors may extend domain definitions on identical terms to native domains, permitting a third-party module to supply content for a project-defined domain.

Modules use this mechanism. A module requiring its own Iris events ships `Iris.Events.MyModule.txt`; placing it in the injector directory constitutes the entire installation procedure. A module's entries cannot displace existing ones: values and member names already recorded in the manifest never change, and new entries only receive values and names that no existing entry holds.

> A module's injector filename forms part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` alters the scope and therefore every identity-domain value that injector contributes.

### Manifests

Each domain maintains a `{DomainName}.manifest.json` in a `Manifests` folder beside its generated script, recording every entry's key, member name, scope, value, seed offset and tombstone state (and, for mapped assets, the asset's GUID). Manifests are version-controlled artefacts and establish identity stability:

- An assigned member name survives regeneration unchanged.
- Name collisions resolve deterministically: a second entry claiming `Splash` is qualified with its scope, as `GroupName_Splash`, then numbered (`Splash_2`, `Splash_3`, …) if that is taken too. The incumbent is never displaced.
- Member names are sanitised: a first character that is not a letter or underscore, and any later character that is not a letter, digit or underscore, become underscores.
- Removed entries are flagged as tombstoned rather than discarded. An entry declared again is revived from its tombstone.
- Hash collisions, and collisions with reserved values, are detected and rehashed under a recorded seed offset, rendering the resolution reproducible.

Each pass that changes a domain emits a summary of additions, removals, renames, rescopes and collisions through Scribe. A collision that cannot be resolved is reported as an error, and that domain is left untouched.

### Shells

A **shell** supplies the C# scaffolding for a generated file: namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` substitution token. `CustomDomain.Shell.txt` carries an additional `{DOMAIN_NAME}` token, permitting one template to serve every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator is configured to reserve those values. Shells do not declare generated entries. The Iris shell declares no sentinel, so Iris values start at zero.

### Generation triggers and guards

An `AssetPostprocessor` monitors every native-entries file and the injector, Addressables-injector and definition directories, regenerating when a `.txt` there is imported, deleted or moved. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** forces a pass.

The pipeline enforces:

- Output confinement to the configured generated directories, preventing a misconfigured domain from overwriting hand-authored source.
- Abort on two domains resolving to the same output path.
- Diagnostic reporting, by filename, of any injector whose prefix matches no declared domain.
- Rejection of injectors carrying more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the sole accepted form.
- Emission of a sentinel-only enumeration (plus any surviving tombstones), with warning, for a domain yielding zero entries.
- A domain that fails is left untouched; the others are still generated.

### Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** enumerates every writable Addressable group with its member assets and a per-asset selection toggle. **Apply** performs:

1. Emission of one injector per group per reference kind (`Addressables.Assets.{Group}.txt` and equivalents) into the Addressables injector directory.
2. Regeneration of the three Addressables domains.
3. Reconstruction of the User Config reference maps from the resulting manifests, including each asset's main type and the aliases of renamed assets (see [Resource Loading](#resource-loading)).

Group names are sanitised into scopes: `Test Assets` yields `Test_Assets`. Two groups sanitising to an identical scope are shown with a warning, and Apply refuses to run until one is renamed.

Apply writes changed files in an import batch, preserves existing file GUIDs, and removes obsolete injectors after their replacements exist. Identical files are not rewritten. Deselection still unmaps. Injector files in that directory are generated output and are not hand-edited.

**Reset Addressables manifests and apply** is the secondary button. After confirmation it clears the history (including tombstones) of the Scenes, Prefabs and Assets manifests and rebuilds them from the selected mappings. Other domains are untouched. Collision-resolved IDs can change and removed IDs may be reused, so use it only when the team intends a compatibility reset; normal Apply retains manifest history. Each file replacement is atomic, but the complete multi-file operation is not crash-atomic.

Editor tools map through the same logic with `ThreadlinkAddressablesMapping` (`Threadlink.Editor.CodeGen`). A tool that creates Addressable content maps it without a manual step:

```csharp
// Exactly what ticking the rows and selecting Apply does; existing mappings are kept.
if (ThreadlinkAddressablesMapping.Map("Assets/Content/Scenes/Farm/Farm.unity") is false)
    return; // Not an entry of an editable Addressable group; the reason is logged and nothing is applied.

bool mapped = ThreadlinkAddressablesMapping.IsMapped("Assets/Content/Scenes/Farm/Farm.unity");
List<string> unmapped = ThreadlinkAddressablesMapping.FindUnmapped(assetPaths);
```

`Map` accepts one or more paths, and applies nothing when every entry is mapped already. `FindUnmapped` returns the paths among those given that are not mapped. `ThreadlinkAddressablesMapping.Apply()` explicitly reapplies the current mappings without resetting manifest history. Applying regenerates the domains and recompiles scripts when needed, so code that needs new identifiers runs after the domain reload, for example from an `[InitializeOnLoad]` reconciler. An open Mapping Window refreshes itself after `Map`.

### Editor Config composition

Each entry in the Editor Config's `nativeDomains` array declares a domain name, an output filename (the domain name when empty), a shell, optional native entries, and three flags:

| Flag | Semantics |
|---|---|
| `ordinalValues` | Dense positional allocation. Applies to `Iris.Events` exclusively. |
| `reserveZero` | The shell declares a sentinel at zero that the allocator must not issue. On by default. |
| `sourcedFromAddressables` | Also draws injectors from the Addressables injector directory. |

`domainName` is the prefix injector filenames must match. No validation constrains it, so a mistyped name renders the corresponding injector unread. The orphaned-injector diagnostic addresses this case.

The Editor Config also assigns the generated scripts folder, the injector and Addressables-injector folders, and the domain-definition folder, scripts folder and shell. The shipped Editor Config declares every native domain listed in [Identifier Domains](#identifier-domains).

## Collections and Utilities

### Serialisable maps

Serialisable maps reside in `Threadlink.Collections`. Both derive from `ThreadlinkHashMap<TKey, TValue>`: bucket-indexed, allocation-free on lookup, and rebuilt by `ISerializationCallbackReceiver` after deserialisation.

| Type | Value backing | Application |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` | Value types and Unity object references |
| `RefHashMap<K,V>` | `[SerializeReference]` | Polymorphic managed values, including Vault's `DataField` |

Runtime access is read-only: `Count`, `ContainsKey`, `TryGetValue`, the indexer's getter (a missing key logs a `KeyNotFoundException` and returns `default` rather than throwing), `Keys` and `Values` as spans, `KeyMemory` and `ValueMemory` as memory, `For(action)` to visit every entry by reference, and `Clear()`.

Editor-only mutation is exposed through `EditorOnly_TryAdd`, `EditorOnly_Add`, `EditorOnly_Remove`, and the indexer's setter. `OnAfterDeserialize` clamps a serialised entry count exceeding the backing arrays and reports the discrepancy rather than throwing from a deserialisation callback.

### Serialisable arrays and lists

`Threadlink.Collections` also provides indexable wrappers for serialised collections, each exposing `Collection` as an `IList<T>` and an indexer:

| Type | Backing |
|---|---|
| `FieldArray<T>`, `FieldList<T>` | `[SerializeField]` array or list |
| `RefArray<T>`, `RefList<T>` | `[SerializeReference]` array or list, with a type picker for polymorphic elements |

Arrays expose `Length`; lists expose `Count`.

### Extension libraries

Under `Threadlink.Utilities`:

```csharp
using Threadlink.Utilities.Mathematics;   // float.IsSimilarTo(b), MoveTowards(target, maxDelta), NormalizeBetween,
                                          // ToUVCoordinates, ToScreenPosition, CubicInterpolation
using Threadlink.Utilities.Vectors;       // Vector3.IsSimilarTo(b), Clamp, ClampWithFallback, VectorTo; Vectors.Half, Vectors.XZ
using Threadlink.Utilities.Strings;       // string.ToAbsolutePath(), ToProjectRelativePath(), WithUpperCaseSpacing(),
                                          // ToNonAllocText(), ReadLines, DeserializeIntoDictionaryAsync
using Threadlink.Utilities.UniTask;       // List<UniTask>.AwaitAllThenClear(trim)
using Threadlink.Utilities.Collections;   // IDisposable.PreventEditorMemoryLeaks(), IsWithinBoundsOf, DisposeSafely,
                                          // EnsureSize, SerializeIntoBinary (Editor)
using Threadlink.Utilities.Flags;         // HasFlagUnsafe, AddFlag, RemoveFlag, SetFlag, ClearFlag; EnumUtilities
using Threadlink.Utilities.Objects;       // As<T>, Set<T>, Clone<T>, IsPrefabAssetInProject
using Threadlink.Utilities.Localization;  // GetSafeLocalizedString, TryGetLocalizedString, GetLocalizedStringAsync, GetSafeComment
using Threadlink.Utilities.RNG;           // System.Random.NextNonZeroUInt(), NextUInt64()
using Threadlink.Utilities.Attributes;    // [MinMaxRange], [ReadOnly], [HashMapDrawer]
```

`Threadlink.Utilities.RNG` extends `System.Random` for non-deterministic needs only; anything reproducible draws from [StatelessRNG](#statelessrng).

### Attributes

- **`[MinMaxRange(min, max)]`** draws a range slider for a `Vector2`, `Vector2Int`, `float2` or `int2` field. The float form takes an optional number of displayed decimals (0–3, default 1).
- **`[ReadOnly]`** is a marker attribute without an associated drawer; supplying one would displace the hash-map drawer, as attribute drawers take precedence over type drawers. `ThreadlinkHashMapDrawer` reads the attribute from `fieldInfo` and renders the map without addition, removal or reordering controls. The Odin drawer honours both this attribute and Odin's own `ReadOnly`.
- **`[HashMapDrawer(mode)]`** selects which inspector draws a hash map: `Automatic` (Odin's when Odin is installed, otherwise Threadlink's), `Native` (always Threadlink's), or `Odin` (Odin's, falling back to Threadlink's when Odin is absent).

### Shared types and helpers

In `Threadlink.Shared`:

| Type | Purpose |
|---|---|
| `HashFunctions` | `ToXxHash32` and `ToXxHash64` of a string (trimmed and lower-cased first, optionally seeded), and `ToSplitMix64`. The framework's name hashing, used for object, subsystem and identifier IDs. |
| `AssetReferenceExtensions` | `LoadSynchronously<T>`, `LoadAsync<T>`, and scene `LoadAsync` / `UnloadAsync` for Addressables references (see [Resource Loading](#resource-loading)). |
| `AddressablesRequest<T>` | A fixed-capacity, disposable batch of resource identifiers. |
| `byte2`, `ushort2` | Compact two-component integer structs; `ushort2` converts to and from Mathematics' `int2` and `uint2`. |
| `ThreadlinkConfigFinder` (Editor) | `TryGetConfig<T>(out T)` finds and caches the project's single asset of a configuration type. |

### Animation helpers

| Type | Purpose |
|---|---|
| `AnimatorHash` (`Create ▸ Threadlink ▸ Animation ▸ Animator Hash`) | An asset holding a precomputed `Animator.StringToHash` value, exposed as `Value`. Enter the parameter or state name and generate the hash from the asset's context menu (or button, under Odin). |
| `AnimationEventsReceiver` | A `LinkableBehaviour` beside an `Animator` whose `Receive…` methods are animation-event targets. It re-raises them as C# events with the animator: `OnVoidReceived`, `OnFloatReceived`, `OnIntegerReceived`, `OnStringReceived`, `OnUnityObjectReceived`, `OnVaultReceived` (when the object passed is a Vault) and `OnEventReceived`. |

Both reside in namespace `Threadlink.Animation`.

## Configuration and Project Setup

### Bootstrap scene

Every project needs a bootstrap scene, the build's entry point (see [Deployment Overview](#deployment-overview)):

- **Create one scene that is not Addressable**, and make it the first, and ideally the only, scene in Build Settings. Every other scene is Addressable and mapped (see [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets)).
- **Keep it minimal.** It stays loaded for the whole run, so anything placed in it lives for the whole run. Its discoverables boot at deployment, and nothing ever unloads it. Gameplay content belongs in the scenes Nexus loads.
- **Start from it in the Editor too.** Play Mode starts from whichever scene is open, and that scene then takes the bootstrap scene's place: it stays loaded, and if it is an Addressable gameplay scene, Nexus loads a second copy of it beside the first. Set `EditorSceneManager.playModeStartScene` to the bootstrap scene, so Play boots as a build does (see [Installation and Project Setup](#create-the-bootstrap-scene)).

### Configuration assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`. Must reside at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset and prefab reference maps, asset types and asset aliases; binaries folder (Editor). |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector and definition directories (see [Editor Config composition](#editor-config-composition)). |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Master mixer, volume fade speed, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface fade durations, interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | Distribution choice for platforms where Unity's target does not uniquely identify the storefront/ecosystem. Exactly one may exist. |

Additional creation paths: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply the following native resources:

| Resource | Asset |
|---|---|
| `UserConfig` | The User Config. |
| `SentinelConfig` | The Sentinel Config. The Sentinel build pipeline verifies that this references the project's one Sentinel Config. |
| `ChronosConfig` | The Chronos Config. |
| `DextraConfig` | The Dextra Config. |
| `DextraComponentsPrefab` | A prefab with an `EventSystem` and an `InputSystemUIInputModule`. |
| `AuraConfig` | The Aura Config. |
| `AuraComponentsPrefab` | A prefab with three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`. |

The reference maps on the User Config (scenes, assets, asset types, asset aliases, prefabs) are read-only in the Inspector and are owned by the [Addressables Mapping Window](#addressables-mapping-window). The framework ships a configured set of these assets; see [Installation and Project Setup](#shipped-configuration).

### Addressable registration of native assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** reads the Native Config and marks every referenced native asset, together with the Native Config itself, as Addressable within the `Threadlink Assets` group (the default group, with a warning, if that group does not exist), assigning each asset's path as its address.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** realigns addresses that have diverged from their asset paths, across every writable group.

### Update loop modes

Configured on the User Config:

- **Native**: Threadlink injects `ThreadlinkPlayerLoop` marker systems into Unity's current PlayerLoop and publishes `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` directly.
- **Custom**: Threadlink installs no update callbacks. The project publishes those events from its own driver, commonly configured in response to `OnCoreDeployed`.

Custom mode applies where Threadlink renders the view for a simulation owned by another framework, for example Photon Quantum owning a deterministic multiplayer simulation while Threadlink manages the view.

### Scripting defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline` ≥ 1.8.10 (version define on `Threadlink.Runtime` and `Threadlink.Editor`) | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization` ≥ 1.5.9 (version define on `Threadlink.Runtime`) | `LocalizedText` Vault field, localised interaction prompts, localisation utilities |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps, Vault fields and inspectors |
| `STEAMWORKS_NET` | Defined by the project (Player Settings) | Compiles the Steam Sentinel module (`Threadlink.Sentinel.Steam`) |
| `THREADLINK_STEAMWORKS_NET_165` | `com.rlabrecque.steamworks.net` ≥ 2025.165.0 (version define on `Threadlink.Sentinel.Steam`) | Reserved for version-specific code; no code consumes it yet |

### Binary authoring

Types implementing `IBinaryAuthor` serialise authoring data to `.bytes` files within the project, from an Inspector button or context menu that calls `SerializeAuthoringDataIntoBinary()`. `ICollection<T>.SerializeIntoBinary(binariesFolder, filename)` (Editor, `Threadlink.Utilities.Collections`) writes a runtime collection with MessagePack into a project folder, conventionally the User Config's binaries folder (`TryGetBinariesFolder`). The files are then loaded through Addressables and consumed via `IAsyncBinaryConsumer`.

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file within a selected in-project directory during format iteration.

## Editor Tooling

Every command lives under the **Threadlink** menu. The complete list is in [Menus and Asset Creation](#menus-and-asset-creation).

### Code generation and Addressables

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Forces a generation pass across every domain (see [Identifier Domains and Code Generation](#identifier-domains-and-code-generation)). |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers (see [Addressables Mapping Window](#addressables-mapping-window)). |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Registers framework assets with the Addressables system (see [Configuration](#addressable-registration-of-native-assets)). |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Realigns addresses to asset paths. |

### Scene dropdown

A **Scenes** dropdown in the Editor's main toolbar lists every scene that ships: the enabled scenes in Build Settings, then, after a separator, the scenes that are entries of Addressable groups, marked `(Addressable)`. Selecting one offers to save modified scenes and opens it. The dropdown's label shows the active scene, and its context menu offers **Refresh** and **Open Build Settings...**. Scenes made Addressable only through a folder entry are not listed.

### Code formatter

**`Threadlink ▸ Code Formatter`** opens an editor window backed by CSharpier. Assign a project folder and select **Format C# Files** to recursively format every `.cs` file beneath it. Files with CSharpier compilation errors are skipped and reported; changed files are written in place, Unity's AssetDatabase is refreshed once at the end, and the window reports changed/failed/scanned counts.

### Diagnostics

**`Threadlink ▸ Registers Tracker`** inspects live register contents at runtime, reporting the objects each `Register`-derived subsystem currently holds: each entry's ID, name, type and reference. It requires Play Mode and a deployed core.

### Binaries

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file within a selected directory, which must be inside the project (see [Binary authoring](#binary-authoring)).

### Inspectors

- **Hash maps** draw through `ThreadlinkHashMapDrawer`, or through Odin when installed; see the `[HashMapDrawer]` and `[ReadOnly]` attributes in [Collections and Utilities](#attributes).
- **`[MinMaxRange]`** fields draw as range sliders.
- **Sentinel Config** shows the current build target, its Sentinel platform, and the distribution choice the target allows.
- **`PopupInspector.TryInspect(asset)`** opens any asset's Inspector in a separate window, for editor tools.

### Build processors

| Processor | Stage | Function |
|---|---|---|
| `SentinelBuildProcessor` | Pre-build, first (`callbackOrder` −10000) | Resolves and validates the build's Sentinel platform, distribution and build module; fails the build on any error (see [Sentinel Modules](#the-build-pipeline)). |
| Steam `SteamAppIdBuildProcessor` | Post-build, standalone targets | Stages `steam_appid.txt` for development Steam builds and `threadlink_steam_appid.txt` for release Steam builds, and removes them from other builds (see [Sentinel Modules](#steam-1)). |

## Performance Constraints

| Practice | Rationale |
|---|---|
| Cache `Chronos.DeltaTime` into a local once per tick. | Eliminates repeated static property access in hot loops. |
| Log through `Scribe` rather than `Debug.Log`. | ZString composition avoids interpolated strings, and the prefix identifies the source. |
| Use `UniTask` exclusively; avoid `System.Threading.Tasks` and coroutines in framework code. | Mixing violates the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | Retained delegates keep destroyed objects reachable and dispatch to stale state. |
| Confine replay-sensitive and cross-machine deterministic logic to `FP` and `StatelessRNG`. | Hardware floating point and `UnityEngine.Random` are not suitable for Threadlink's deterministic contract. |
| Keep state that is copied, hashed or saved byte for byte in [Memory](#memory) blocks and inline collections. | Raw-byte hashing and comparison are only stable when layout and padding are. |
| Prefer `OnLateUpdate` for camera-relative computation. | Dispatch occurs after camera transformation. |
| Hash fixed strings once (`StatelessRNG.Part`, `HashFunctions`) and keep the result. | Hashing a string trims and lower-cases it first, which allocates; a kept hash costs nothing. |

## Engineering Procedures

### Introducing a subsystem

- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`, after the subsystems it depends on.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`, then call `base.Discard()`.

See [Implementing a Subsystem](#implementing-a-subsystem).

### Introducing an Iris event

- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document the delegate signature and apply it consistently; mismatched subscriptions log an error, while mismatched publications are a no-op.

### Introducing a scene

- [ ] Map the scene through the Addressables Mapping Window, and keep it out of Build Settings: only the bootstrap scene belongs there (see [Configuration](#bootstrap-scene)).
- [ ] Implement `Nexus.ISceneEntry` binding it to its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Choose its `PhysicsMode`: a scene resident alongside others needs its own physics scene.
- [ ] Provide a listener for the four fader and loading-screen `Func` events.
- [ ] Reach it through Nexus only: `TransitionAsync` / `PresentAsync` to show it, `HoldAsync` to keep it resident. Nexus discards its objects whenever it unloads (see [Nexus](#residency-holds)); release every hold.

### Introducing a stacked interface

- [ ] Construct the prefab with a `CanvasGroup`, deriving its component from `UserInterface` or `UserInterface<S>`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

### Using a custom update loop

- [ ] Set the User Config update-loop mode to `Custom`.
- [ ] Do not install `ThreadlinkPlayerLoop` yourself; it is internal, and Native mode owns it.
- [ ] Publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` from the external driver at the intended phases.
- [ ] Preserve the same event contract expected by Chronos, Aura, Dextra and project listeners.

### Introducing a Sentinel runtime module

- [ ] Implement an `ISentinelPlatform` / `SentinelPlatform` for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair.
- [ ] Register a `SentinelModuleDescriptor` through `SentinelModuleRegistry.Register` at `AfterAssembliesLoaded`.
- [ ] Advertise only capabilities actually implemented.
- [ ] Register platform-level services on the platform and account-scoped services on `ISentinelAccount` implementations.
- [ ] Provide an `ISentinelBuildModule` for the same pair; without one, builds for that pair fail.

See [Sentinel Modules](#modules).

---

# Reference

## Native Iris Events

Values are ordinals allocated in the order listed, from `Iris.Events.Native.txt`; project and module events follow. The delegate is the type that subscribers state and publishers use (see [Iris](#iris-event-dispatch)). For `Func` events, the "provider" column names who must subscribe the single listener.

| Value | Event | Delegate | Publisher | Provider / typical listener |
|---|---|---|---|---|
| 0 | `OnNativeSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment | `NativeSubsystemsConfig` |
| 1 | `OnUserSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment | `UserSubsystemsConfig` |
| 2 | `OnCoreDeployed` | `Action<Threadlink>` | Core deployment | Aura; project code |
| 3 | `OnUpdate` | `Action` | `ThreadlinkPlayerLoop` (Native mode) or the project (Custom mode) | Chronos, Aura, interfaces |
| 4 | `OnFixedUpdate` | `Action` | As `OnUpdate` | Chronos |
| 5 | `OnLateUpdate` | `Action` | As `OnUpdate` | Interfaces |
| 6 | `OnPlaytimeCountTick` | `Action<float>` | Chronos | Project code |
| 7 | `OnGamePauseRequested` | `Action` | Project code | Project code; no framework use |
| 8 | `OnGameResumeRequested` | `Action` | Project code | Project code; no framework use |
| 9 | `OnGamePaused` | `Action` | Chronos | Project code |
| 10 | `OnGameResumed` | `Action` | Chronos | Project code |
| 11 | `OnInputDeviceChanged` | `Action<Dextra.InputDevice>` | Dextra | `DextraInputIcon` |
| 12 | `OnUICancelled` | `Action<UserInterface>` | Dextra's UI stack | Project code |
| 13 | `OnUIElementSelected` | `Action<GameObject>` | Dextra | Project code |
| 14 | `OnInteract` | `Func<bool>` | Project code, typically from an input action | The interactable in range |
| 15 | `OnInteractableDetected` | `Action<Interactable2D>` or `Action<Interactable3D>` | `InteractablesDetector2D` / `InteractablesDetector3D` | Project code |
| 16 | `OnInteractableOutOfRange` | `Action<Interactable2D>` or `Action<Interactable3D>` | `InteractablesDetector2D` / `InteractablesDetector3D` | Project code |
| 17 | `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Nexus | Project code |
| 18 | `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Nexus | Project code |
| 19 | `OnDisplayFaderAsync` | `Func<UniTask>` | Nexus | The project's fader |
| 20 | `OnHideFaderAsync` | `Func<UniTask>` | Nexus | The project's fader |
| 21 | `OnDisplayLoadingScreenAsync` | `Func<UniTask>` | Nexus | The project's loading screen |
| 22 | `OnHideLoadingScreenAsync` | `Func<UniTask>` | Nexus | The project's loading screen |
| 23 | `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Nexus | Aura; project code |
| 24 | `OnSceneLoaded` | `Action<Nexus.SceneLoad>` | Nexus | Project code |
| 25 | `OnSceneUnloaded` | `Action<Scene>` | Nexus | Project code |
| 26 | `OnSentinelStateChanged` | `Action<Sentinel>` | Sentinel | Project code |

The interactable detection events carry the detector's concrete interactable type: a 2D detector publishes `Interactable2D`, a 3D detector `Interactable3D`. A subscriber states the one it expects.

## Service Access

| Service | Category | Access |
|---|---|---|
| `Threadlink` | Core (Weaver) | `Threadlink.TryGetSingleton(out var core)` |
| `Iris` | Static | `Iris.Publish(...)`, `Iris.Subscribe<T>(...)` |
| `Nexus` | Static | `Nexus.TransitionAsync(...)`, `Nexus.HoldAsync(...)` |
| `Initium` | Static | `Initium.BootAndInitSceneObjectsAsync(...)`, `Initium.BootAndInitAsync(...)` |
| `Scribe` | Static | `this.Send(...)`, `Scribe.Send<T>(...)` |
| `Sentinel` | Native subsystem | `Sentinel.TryGetSingleton(out var sentinel)` |
| `Chronos` | Native subsystem (static time members) | `Chronos.TimeScale`, `Chronos.DeltaTime`; `Chronos.TryGetSingleton(out var chronos)` for `Start` / `Stop` |
| `Dextra` | Native subsystem | `Dextra.TryGetSingleton(out var dextra)`; `Dextra.IsTopInterface<T>()` |
| `Aura` | Native subsystem (Linker) | `Aura.TryGetSingleton(out var aura)` |
| `StatelessRNG` | Static | `StatelessRNG.CreateStream(...)`, or a `StatelessRNG.Source` per world |
| `Vault` | Asset | A `LinkableAsset` instance |

Namespaces:

| Namespace | Contents |
|---|---|
| `Threadlink.Core` | `Threadlink`, `ThreadlinkSubsystem<T>` and the register hierarchy, `LinkableBehaviour`, `LinkableAsset` and their singletons, configs |
| `Threadlink.Core.NativeSubsystems.Iris` / `.Nexus` / `.Initium` / `.Scribe` | The static services |
| `Threadlink.Core.NativeSubsystems.Sentinel` / `.Chronos` / `.Dextra` / `.Aura` | The native subsystems and their types |
| `Threadlink.Shared` | Lifecycle contracts, `WeavingFactory`, hashing, Addressables helpers |
| `Threadlink.Generated` | `ThreadlinkIDs` |
| `Threadlink.Vault`, `Threadlink.Vault.DataFields` | The Vault and its field types |
| `Threadlink.Collections` | Serialisable maps and collections |
| `Threadlink.Deterministic` | `FP`, vectors, `FPQuaternion`, `StatelessRNG`, `Bits`, `FixedPointConversions` |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections |
| `Threadlink.Animation` | `AnimatorHash`, `AnimationEventsReceiver` |
| `Threadlink.Utilities.*` | Extension libraries and attributes |
| `Threadlink.SentinelModules.Local`, `Threadlink.SentinelModules.Steam` | The shipped Sentinel modules |
| `Threadlink.User` | Project code and project-defined domains |

## Identifier Domains

| Enumeration | Kind | Reserved | Native entries | Injector | Ownership |
|---|---|---|---|---|---|
| `ThreadlinkIDs.Iris.Events` | Ordinal | — | `Iris.Events.Native.txt` | `Iris.Events.User.txt` | Engineering |
| `ThreadlinkIDs.StatelessRNG.Domains` | Identity | `None = 0` | — | `StatelessRNG.Domains.User.txt` | Engineering |
| `ThreadlinkIDs.Dextra.InputModes` | Identity | `Unresponsive = 0` | — | `Dextra.InputModes.User.txt` | Design |
| `ThreadlinkIDs.Nexus.SpawnPoints` | Identity | `None = 0` | — | `Nexus.SpawnPoints.User.txt` | Design |
| `ThreadlinkIDs.Vault.Fields` | Identity | `None = 0` | — | `Vault.Fields.User.txt` | Design |
| `ThreadlinkIDs.Addressables.Scenes` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Assets` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Prefabs` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.NativeResources` | Identity | `None = 0` | `Addressables.NativeResources.Native.txt` | — | Framework |

All reside in namespace `Threadlink.Generated`, assembly `Threadlink.Generated`, under `Threadlink/Generated/`, with their manifests in `Threadlink/Generated/Manifests/`.

Project-defined domains (one `.txt` per domain in `Threadlink User/Design/Custom Domain Definitions/`) are Identity domains with `None = 0` reserved, emitted into namespace `Threadlink.User`, assembly `Threadlink.User.Generated`, under `Threadlink User/Engineering/Codebase/Generated/`.

The native resources are `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraComponentsPrefab` and `ChronosConfig`.

## Menus and Asset Creation

### Threadlink menu

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Forces a generation pass across every domain. |
| `Threadlink ▸ Code Formatter` | Recursively formats C# files in a selected project folder using CSharpier. |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers. |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Registers framework assets with the Addressables system. |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Realigns addresses to asset paths. |
| `Threadlink ▸ Clear all Binaries` | Empties `.bytes` files within a selected directory. |
| `Threadlink ▸ Registers Tracker` | Inspects live register contents. |

The **Scenes** dropdown in the main toolbar opens any scene that ships (see [Editor Tooling](#scene-dropdown)).

### Asset creation

| Entry | Creates |
|---|---|
| `Create ▸ Threadlink ▸ Native Config` | `ThreadlinkConfig.Native.asset` |
| `Create ▸ Threadlink ▸ User Config` | `ThreadlinkConfig.User.asset` |
| `Create ▸ Threadlink ▸ Editor Config` | `ThreadlinkConfig.Editor.asset` |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | A Chronos Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Aura Config` | An Aura Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Dextra Config` | A Dextra Config |
| `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Sentinel Config` | `SentinelConfig.asset` |
| `Create ▸ Threadlink ▸ Vault` | A Vault |
| `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config` | An Interactable Config |
| `Create ▸ Threadlink ▸ Animation ▸ Animator Hash` | An Animator Hash |

## Terminology

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
| **Hold** | A claim on a scene that keeps it resident until released. |
| **Presented scene** | The resident scene the local player sees and hears. |
| **Bootstrap scene** | The one non-Addressable scene, first in Build Settings: the build's entry point, loaded for the whole run (see [Deployment Overview](#deployment-overview)). |
| **Discoverable** | A scene component Initium boots automatically when its scene loads and discards before it unloads. |
| **Distribution** | The storefront or ecosystem Sentinel deploys on a platform, such as Steam or Local. |
| **Module** | An implementation of Sentinel for exact platform/distribution pairs. |
| **FP** | Deterministic Q32.32 fixed-point number type (see [Fixed Point](#fixed-point)). |
| **Stream** | A sequence of random numbers that is a pure function of a seed, a domain and identity parts (see [StatelessRNG](#statelessrng)). |
| **Inline collection** | A collection laid out inside another value's memory (see [Memory](#memory)). |
