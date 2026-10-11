<!-- GENERATED FILE. Do not edit: the source is the GitBook manual in Gitbook/manual of the Threadlink repository. Regenerate with: python Gitbook/tools/build_single_file.py -->

# Threadlink Framework — Reference Manual

> This is the offline, single-file edition of the Threadlink reference manual, generated from the GitBook space in the repository's `Gitbook/manual` folder. The manual is online at [threadforge.gitbook.io/threadlink-documentation](https://threadforge.gitbook.io/threadlink-documentation/).


![Powered by Threadlink. Weaving worlds together.](Threadlink_Banner.png)

**Threadlink** is a modular runtime framework for Unity. It provides a core that deploys itself; subsystems for events, time, scenes, input and UI, audio and platform services; type-safe identifiers; a data container for designers; and deterministic maths for reproducible simulation.

This manual covers the runtime, the editor tools and the authoring workflow of **Threadlink 6.6**.

## Organisation

Threadlink separates authored data from code. The manual follows suit: a designer volume and an engineer volume, built on shared foundations.

| Part | Audience | Contents |
|---|---|---|
| [Getting Started](#requirements) | All | Requirements, installation and first-time project setup. |
| [Foundations](#purpose) | All | Framework purpose, project layout, assembly topology, deployment overview. |
| [Designer Reference](#designer-reference) | Content authors | Identifiers, Vaults, spatial audio, input prompts, tuning and content registration. No C# needed. |
| [Engineer Reference](#engineer-reference) | Programmers | Architecture, deployment, subsystem APIs, the PlayerLoop, Sentinel, deterministic and memory tools, code generation, custom subsystems, editor tools and performance. |
| [Reference](#native-iris-events) | All | Native Iris events, service access, identifier domains, menus and asset-creation entries, terminology. |

Both volumes describe the same systems from opposite ends. Where the designer volume says *"declare the identifier in `Vault.Fields.User.txt`"*, the engineer volume explains the pipeline that generates `ThreadlinkIDs.Vault.Fields`, the manifest that keeps its values stable, and the API that reads it.

> API names, identifiers, menu paths, file paths and asset-creation entries are taken from the framework source. Systems that are stubbed or in development are marked as such.

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

Threadlink 6.6 is developed and tested on **Unity 6000.6.4f1**. It uses recent editor APIs, such as the `Unity.Scripting.LifecycleManagement` attributes (`[OnEnteringPlayMode]`, `[OnExitingPlayMode]`, `[AutoStaticsCleanup]`) and the main-toolbar extension API. Earlier versions are untested.

### Required packages

| Package | Version in the reference project | Used by |
|---|---|---|
| Addressables (`com.unity.addressables`) | 2.11.2 | Every scene, prefab and asset loaded at runtime, including the configuration assets. |
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

These ship in `Threadlink/Plugins/` and `Threadlink/Editor/`; there is nothing to install.

| Dependency | Purpose |
|---|---|
| UniTask | Every asynchronous API in the framework. `System.Threading.Tasks` is not used. |
| ZString | Allocation-free string building for Scribe and code generation. |
| MessagePack 3.1.4 (with its Unity integration and analyser) | `Threadlink.TrySerialize` / `TryDeserialize`. |
| SerializeReference UI (`Textus.SerializeReferenceUI`) | The managed-reference type pickers used by Vault fields and Sentinel Config when Odin is absent. |
| CSharpier (editor only) | The [Code Formatter](#code-formatter) window. |

## Installation and Project Setup

Threadlink ships as a folder, not a UPM package. The repository's `Threadforge/` folder holds the framework, a ready-made project area and default configuration assets.

### Add the framework

1. Install the [required packages](#required-packages).
2. Copy the repository's `Threadforge/` folder into the project as **`Assets/Threadforge/`**, `.meta` files included.

This location is required. The core loads the Native Config from the fixed address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset` (`NativeConstants.Addressables.NATIVE_CONFIG`), and the shipped assets reference each other by the GUIDs in their `.meta` files.

### Shipped configuration

The framework comes configured. Review these assets; there is nothing to create.

| Asset | Location |
|---|---|
| Native Config | `Threadlink/ThreadlinkConfig.Native.asset`, already mapping every native resource. |
| Editor Config | `Threadlink/ThreadlinkConfig.Editor.asset`, already declaring every native identifier domain. |
| User Config | `Threadlink User/Engineering/Configs/ThreadlinkConfig.User.asset` |
| Chronos Config, Sentinel Config | `Threadlink User/Engineering/Configs/` |
| Aura Config, `AuraComponents.prefab`, `AuraMixer.mixer` | `Threadlink User/Engineering/Configs/Aura/` |
| Dextra Config, `DextraComponents.prefab`, `ThreadlinkInputActions.inputactions`, `UIInputActions.inputactions`, `InputSystem.inputsettings.asset` | `Threadlink User/Engineering/Configs/Dextra/` |

[Configuration and Project Setup](#configuration-and-project-setup) describes each one.

### Make the native assets Addressable

1. Create the project's Addressables settings if it has none, and an Addressable group named **`Threadlink Assets`**.
2. Select **`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`**.

This makes the Native Config, and every native resource it references, Addressable in the `Threadlink Assets` group, each addressed by its asset path. Without that group, it uses the default group and logs a warning.

### Create the bootstrap scene

Every project needs one **bootstrap scene**: the build's entry point.

1. Create a scene that is **not** Addressable.
2. Make it the first, and ideally the only, scene in Build Settings.
3. Keep it minimal. It stays loaded for the whole run; gameplay belongs in Addressable scenes loaded through Nexus.
4. Make Play Mode start from it, so the Editor boots as a build does.

Otherwise Play Mode starts from whichever scene is open, which then stands in for the bootstrap scene. If that is an Addressable gameplay scene, Nexus later loads a second copy of it. Set the start scene with `EditorSceneManager.playModeStartScene`:

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

[Deployment Overview](#deployment-overview) explains why.

### Choose the platform distribution

Open the **Sentinel Config** and check the distribution for each platform that offers a choice, such as Windows (Local, Steam, Microsoft Store, Epic or GOG). The default, **Local**, needs no platform SDK. See [Sentinel](#sentinel-platform-services).

### Verify the deployment

Enter Play Mode from the bootstrap scene. The console lists the native subsystems (Sentinel, Chronos, Dextra and Aura), then any user subsystems, then `Core successfully deployed.` Sentinel also reports its platform and distribution, or why it failed; a failed platform does not stop deployment. **`Threadlink ▸ Runtime Monitor`** shows the result live.

If the Native Config or User Config cannot be loaded, deployment stops with an error. The usual cause is that the native assets were never made Addressable.

> **Previewing a scene without the core.** Set `ThreadlinkEditorDeployment.Suppressed = true` before `AfterSceneLoad` (for example from a `BeforeSceneLoad` hook) to skip Addressables initialisation and deployment for that Play session. The flag is Editor-only and resets on entering and exiting Play Mode.

### Next steps

- Designers: [The Identifier Workflow](#the-identifier-workflow).
- Engineers: [Architecture](#architecture) and [Implementing a Subsystem](#implementing-a-subsystem).

---

# Foundations

## Purpose

Threadlink is a modular runtime framework for Unity games and interactive applications. It provides:

- A core that deploys itself. Its one requirement is a bootstrap scene: the build's entry point, which stays loaded while Nexus loads every other scene through Addressables (see [Deployment Overview](#deployment-overview)).
- Native subsystems for events, time, scenes, input and UI, audio, and platform services such as accounts, saves, achievements and invitations.
- Type-safe identifiers. Scenes, assets, prefabs, events, input modes, spawn points, RNG domains and data fields are C# enumerations generated from plain-text lists, so nothing is looked up by string.
- The **Vault**, a container for game data authored by designers.
- Deterministic maths and a stateless RNG for reproducible simulation, replays and procedural generation, and native-memory tools for state that is copied, hashed and saved as raw bytes.

Threadlink does not replace Unity: scenes, prefabs and components are authored as usual. It replaces the plumbing in between: manager singletons, event wiring, asset-reference bookkeeping and persistence code.

### Authored data, implemented behaviour

The framework draws one line:

- **Designers author data.** They create Vaults, fill in configuration assets, place components in scenes and declare identifiers in text files.
- **Engineers implement behaviour.** They write subsystems and scene logic, subscribe to events, and read the authored data.

Neither does the other's job. Designers don't edit `.cs` files; engineers don't hard-code values that belong in a Vault.

### Content-addressed identity

A generated identifier's value is a hash of its name, not its position in the list. Removing an entry never changes another entry's value, and removed entries stay behind as obsolete tombstones, so existing serialised references still resolve.

That is what makes the identifier workflow safe. One domain, Iris events, uses positions instead; the manual flags this wherever it matters. See [Identifier Domains and Code Generation](#identifier-domains-and-code-generation).

## Project Layout

Threadlink lives in one folder, `Assets/Threadforge/`, split in two: the framework and the project's own area.

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
│   ├── Editor/                        ← Code generation, Addressables tooling, Sentinel build pipeline,
│   │                                    Vault Browser, Runtime Monitor, formatter, inspectors
│   ├── Generated/                     ← Generated output: ThreadlinkIDs enumerations and manifests
│   ├── Plugins/                       ← UniTask, ZString, MessagePack, SerializeReference UI
│   ├── Constants.Native.cs
│   ├── ThreadlinkConfig.Native.asset
│   └── ThreadlinkConfig.Editor.asset
│
└── Threadlink User/                   ← The project's area.
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

Don't edit `Threadlink/` by hand. That includes `Threadlink/Generated/`, which only the code generator writes. Project work goes under `Threadlink User/`. Binary data authored through `IBinaryAuthor` goes in a folder the project chooses on the User Config (see [Binary Authoring](#binary-authoring)).

### Assembly topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | No references; `noEngineReferences`. Any assembly can reference it, including a deterministic simulation. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers, `WeavingFactory`, `AnimatorHash` | — |
| `Threadlink.Deterministic` | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, `StatelessRNG`, `Bits` | `noEngineReferences`; `allowUnsafeCode`. References `Threadlink.Shared` and `Threadlink.Generated`. |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections | `allowUnsafeCode`. No assembly references; no other Threadlink assembly references it. |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities (including `FixedPointConversions`, between Unity's float types and the deterministic ones) | `allowUnsafeCode`. Defines `THREADLINK_TIMELINE` and `THREADLINK_LOCALIZATION` from package versions. |
| `Threadlink.Sentinel.Multiplayer.Contracts` | `IMultiplayerService` and the social/session types | `noEngineReferences` |
| `Threadlink.Sentinel.Local` | The Local Sentinel module | — |
| `Threadlink.Sentinel.Steam` | The Steam Sentinel module | Compiled only with the `STEAMWORKS_NET` define, on standalone platforms and in the Editor. |
| `Threadlink.Sentinel.Steam.Shared` | `SteamSentinelSettings`, module identity | No references; `noEngineReferences` |
| `Threadlink.Sentinel.Local.Editor`, `Threadlink.Sentinel.Steam.Editor` | Sentinel build modules and the Steam App ID build processor | Editor-only |
| `Threadlink.Editor` | Code generation, editor tools, the Sentinel build pipeline | Editor-only. Also references UniTask, Input System and uGUI. |
| `Threadlink.User` | Project code | `allowUnsafeCode`. References `Threadlink.Runtime`, `Threadlink.Shared`, `Threadlink.Deterministic`, `Threadlink.Generated`, `Threadlink.User.Generated` and UniTask. |
| `Threadlink.User.Generated` | Project-defined domain enumerations | No references; `noEngineReferences` |

Generated identifiers are in the `Threadlink.Generated` namespace, so code that uses them needs `using Threadlink.Generated;`. Project-defined domains go in the `Threadlink.User` namespace. Because generated output has its own assemblies, neither project work nor installing a module ever writes into framework source.

`Threadlink.User` does not reference `Threadlink.Memory`, the Sentinel contracts or the Sentinel modules. To use them, add the reference to `Threadlink.User.asmdef` or to your own assembly definition.

## Deployment Overview

The core deploys itself through Unity's runtime-initialisation hooks. No initialiser needs placing in a scene.

### The bootstrap scene

Every project needs one **bootstrap scene**, for three reasons:

- **Unity needs a scene as the entry point.** A player starts from the first scene in Build Settings.
- **An Addressable scene can't be that scene.** Addressable scenes are not in the build's scene list, and Addressables only initialise after the first scene loads.
- **So one scene must be non-Addressable**: the first, and ideally only, scene in Build Settings. That is the bootstrap scene. It stays loaded for the whole run, and every other scene is Addressable and loads beside it through [Nexus](#nexus-scene-management).

[Installation and Project Setup](#create-the-bootstrap-scene) shows how to set it up.

### The deployment

1. Assemblies load. Weaving factories register, native and project code subscribe to their registration events, and Sentinel platform modules register.
2. Once the bootstrap scene has loaded, the core initialises Addressables and loads the **Native Config**, then the **User Config** it references.
3. The core creates itself, then weaves, preloads, boots and initialises the native subsystems, then the project's.
4. The core sets `IsDeployed` and publishes `OnCoreDeployed`.
5. The discoverable objects in the bootstrap scene boot.

Deployment needs two Addressable assets:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

[Deployment and Initialisation](#deployment-and-initialisation) gives the exact sequence; [Configuration and Project Setup](#configuration-and-project-setup) covers the assets.

---

# Designer Reference


> This volume covers authoring data and identifiers. Everything is done through text files, the Inspector and a few commands under the **Threadlink** menu. No C# is needed.

### Scope of the role

Designers produce four things:

1. **Identifiers**: names for spawn points, input modes and Vault fields, declared in text files and picked from Inspector dropdowns. See [The Identifier Workflow](#the-identifier-workflow).
2. **Vaults**: data assets of tunable values, keyed by those identifiers. See [The Vault](#the-vault).
3. **Configuration values**: the exposed fields of the framework's configuration assets. See [Configuration Assets](#configuration-assets).
4. **Scene content**: Threadlink components placed in scenes, such as [audio zones](#audio-zones), interactables and [input-prompt icons](#input-prompts).

The rule: declare a name once, then pick it from a dropdown. Nothing is referenced by a typed string.

### In this volume

| Page | Covers |
|---|---|
| [The Identifier Workflow](#the-identifier-workflow) | Declaring, regenerating, removing and renaming identifiers. |
| [The Vault](#the-vault) | Creating and browsing Vaults, field types, serialised and transient backings. |
| [Audio (Aura)](#audio-aura) | Aura configuration, audio zones, per-scene audio. |
| [Input Prompts](#input-prompts) | Device-specific button glyphs. |
| [Configuration Assets](#configuration-assets) | The designer-tunable fields of each configuration asset. |
| [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets) | The Addressables Mapping Window. |
| [Designer Procedures](#designer-procedures) | Step-by-step procedures and working rules. |

## The Identifier Workflow

Threadlink turns plain-text lists into C# enumerations. The lists are in **`Threadlink User/Native Domain Injectors/`**. Designers own three of them:

| File | Resulting dropdown | Purpose |
|---|---|---|
| `Dextra.InputModes.User.txt` | Input Modes | Control contexts (`Gameplay`, `Menu`, `Cutscene`). |
| `Nexus.SpawnPoints.User.txt` | Spawn Points | Places where entities can be put. |
| `Vault.Fields.User.txt` | Vault Fields | The fields Vaults can hold. |

These files are called **injectors**, because each adds entries to an enumeration the framework declares. Engineers own the other two in the folder (`Iris.Events.User.txt`, `StatelessRNG.Domains.User.txt`).

### Declaration syntax

Each file starts with comment lines beginning `///`, followed by one identifier per line:

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

Rules:

- One identifier per line. Surrounding whitespace is ignored.
- Start with a letter or underscore; use only letters, digits and underscores. Other characters become underscores, but write `Checkpoint_Alpha` yourself rather than relying on that.
- Lines starting `//` (including `///`) are comments. Blank lines are ignored.
- Case doesn't matter: `Alpha` and `alpha` are the same entry, and only the first is kept.

### Regeneration

Save the file. Generation runs automatically, Unity recompiles, and the new identifiers appear in their dropdowns.

**`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** runs generation on demand.

### Removal semantics

To remove an entry, delete its line. Because each value is a hash of its name:

- Removing an entry never changes another entry's value.
- The removed identifier stays in the enumeration as an obsolete tombstone, so assets that still use it keep working. Code that uses it gets a compiler warning.
- Declaring it again brings it back with its original value.
- Order doesn't matter; reordering changes nothing.

The one exception is Iris events, which engineers own. Their values are positional, so removing one breaks compilation wherever it is used, instead of leaving a tombstone. This is deliberate.

### Rename semantics

A rename is a removal plus an addition: the old name becomes a tombstone and the new name gets a new value. Anything that used the old name must be reassigned, so tell engineering about any rename of an identifier used in code.

Each generation pass that changes a domain logs a summary of additions, removals, renames, rescopes and collisions. Check it to catch an accidental rename.

## The Vault

A **Vault** is a data asset holding named, typed **fields**. Use one per game entity, such as an enemy, a weapon, a level or an item.

### Creation

1. In the Project window, select **Create ▸ Threadlink ▸ Vault**, or select **+** in the Vault Browser.
2. Give it a descriptive name (`Vault_Enemy_Goblin`, `Vault_Weapon_Longsword`).
3. Select it to edit its fields in the Inspector.

### The Vault Browser

**`Threadlink ▸ Vault Browser`** lists every Vault in the project, with its folder and field count. Select one to edit it in place, with undo. Search filters by name or folder, and right-clicking a Vault offers ping, duplicate and delete. To jump to a Vault in the browser, right-click it in the Project window and choose **Threadlink ▸ Open in Vault Browser**.

Edits made in Play Mode change the asset itself and are kept after Play Mode ends.

### Field composition

Each field has:

1. A **field identifier**, picked from the Vault Fields dropdown (declared in `Vault.Fields.User.txt`; see [The Identifier Workflow](#the-identifier-workflow)).
2. A **value**, whose type you pick from a dropdown before entering it.

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

Engineers can add more field types (see [Vault Runtime API](#custom-field-types)).

### Serialised and transient backings

Each field stores its value in one of two ways:

- **Serialised**: saved with the asset. Use it for authored values such as base health or weapon damage.
- **Transient**: runtime-only, never saved, reset each session. Use it for scratch values set during play.

> Unity [Presets](https://docs.unity3d.com/Manual/Presets.html) can give every new Vault of a kind its full set of fields.

### Runtime consumption

Engineers read and write fields by identifier at runtime. Renaming a field identifier breaks those references, so tell engineering first.

A Timeline can carry Vault values on markers (see [Vault Runtime API](#timeline-integration)), and an animation event can pass a Vault to code through an `AnimationEventsReceiver` (see [Collections and Utilities](#animation-helpers)).

## Audio (Aura)

**Aura** is the audio subsystem. It manages the Music, Atmos (ambience) and SFX channels, and spatial audio zones.

### Aura configuration

| Field | Function |
|---|---|
| Master Audio Mixer | The mixer whose exposed parameters code reads and writes. **Engineering-owned.** |
| Volume Fade Speed | How fast music, ambience and listener volumes change. Higher is faster. |
| Navigation Clip | Played when moving between UI elements. |
| Confirm Clip | Played on UI confirmation. |
| Cancel Clip | Played on UI cancellation. |

Pick clips from the Assets dropdown. [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets) shows how to add them to it.

### Audio zones

An **AuraZone** is a local sound source in a scene. As the listener gets closer, it turns global music and ambience down.

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same GameObject and assign its clip. The zone puts it in its **Source** field and sets it to loop from the start at full volume. You can assign an AudioSource on another object to **Source** instead.
3. Set two values:
   - **Radius Coefficient** (0–1): the zone's radius, as a fraction of the AudioSource's maximum distance.
   - **Influence** (0–1): how far the global channels are turned down when the listener is fully inside the zone.

Placing the zone is all it takes. Zones start working when their scene becomes the presented scene, and stop when it is replaced or unloaded. Only the presented scene is heard, so zones in other loaded scenes (such as scenes kept loaded for other players) do nothing.

Zone names must be unique within a scene, ignoring case. A zone with the same name as another is ignored, and a warning is logged.

### Per-scene audio

Each scene has music and ambience tracks with target volumes, set through a scene entry that engineering writes (see [Nexus](#the-isceneentry-contract)). Choose the tracks and volumes, and include them in the scene's specification.

## Input Prompts

Threadlink shows the right button glyph for the active input device, and swaps it when the device changes. It recognises four device families: mouse and keyboard, Xbox, PlayStation, and Switch Pro controllers.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Set the control it shows (its input control path).

Inside one of Dextra's interfaces, that's all. Elsewhere, an engineer's component must boot and discard it (see [Dextra](#device-resolution)).

The Dextra Config maps each control and device to a sprite. Designers supply and assign the sprites; engineers set the control paths. Sprites are Addressable assets (see [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets)).

If the active device has no sprite, the image is hidden and an error is logged.

## Configuration Assets

| Asset | Designer-tunable fields |
|---|---|
| **Aura Config** | Volume fade speed; navigation, confirm and cancel clips. |
| **Dextra Config** | Interface fade-in and fade-out times (seconds, default 0.25); input-icon sprites; the interface list, with engineering. |
| **Chronos Config** | Iris Physics Update. **Engineering-owned; do not change.** |
| **Sentinel Config** | The distribution (store) for platforms that offer several. **Engineering-owned; change only with engineering.** |

The physics toggle changes how the whole game simulates physics. If you're unsure who owns a value, ask engineering before changing it.

The shipped assets are in `Threadlink User/Engineering/Configs/`. [Configuration and Project Setup](#configuration-and-project-setup) describes every one, including the engineering-owned ones.

## Registering Scenes, Prefabs and Assets

Content loaded at runtime (scenes, prefabs, audio clips, sprites) is referenced through Addressables and picked from a dropdown. The Mapping Window adds it to the dropdowns.

Every scene is Addressable except the bootstrap scene, which is the only one in Build Settings (see [Deployment Overview](#deployment-overview)). So never add a new scene to Build Settings.

1. Put the asset in an Addressable group.
2. Open **`Threadlink ▸ Addressables ▸ Mapping Window`**.
3. Find the asset in its group and tick it. The search field filters by name; **All** and **None** tick or clear a whole group; **Ping** shows the asset in the Project window.
4. Select **Apply**.

The window sorts assets by type (scenes become Scene identifiers, prefabs Prefab identifiers, everything else Asset identifiers) and writes the references into the User Config. Only this window edits those references; they are read-only in the Inspector. Read-only Addressable groups are not listed.

Two things decide the identifier:

- **The asset's name.** An audio clip named `Music_BossTheme` becomes `Music_BossTheme`. Give assets stable, code-safe names before mapping them.
- **The asset's group.** Two assets named `Splash` in different groups can coexist; the second becomes `GroupName_Splash`. Moving a mapped asset to another group changes its identifier, so settle groups before mapping.

Clearing an asset's tick unmaps it; its identifier stays as a tombstone.

Renaming a mapped asset, or moving it to another group, gives it a new identifier and leaves the old one as a tombstone. The next Apply records the old identifier as an alias of the new one, so data saved under the old identifier still resolves (see [Resource Loading](#renamed-assets)). References in assets and code still need reassigning.

Two groups whose names reduce to the same code-safe form (for example `Test Assets` and `Test_Assets`) are reported, and Apply won't run until one is renamed.

The **Scenes** dropdown in the main toolbar lists every scene that ships, from Build Settings and from Addressable groups, and opens the one you pick.

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

### Browsing and editing Vaults

1. Open **`Threadlink ▸ Vault Browser`**.
2. Search or scroll to the Vault, select it, and edit its fields on the right.

### Making an asset loadable

1. Assign the asset to an Addressable group under a stable name.
2. Map it through **`Threadlink ▸ Addressables ▸ Mapping Window`** and select **Apply**.

### Placing an audio zone

1. Add an **AuraZone** and an **AudioSource** to a GameObject in the scene, and assign the source's clip.
2. Give the zone a name unique within the scene.
3. Set **Radius Coefficient** and **Influence**.

### Working rules

- Never type a name as a string. Declare an identifier, save, and pick it from the dropdown.
- Removing is safe. Renaming breaks existing references, so coordinate it.
- Read the console summary after each generation pass.
- Give Addressable assets stable names and final groups before mapping them.
- Tell engineering about any rename of an identifier that code may use.

---

# Engineer Reference


> This volume assumes you know C#, Unity, Addressables and async programming. Threadlink uses **UniTask** throughout, never `System.Threading.Tasks`. It relies on runtime-initialisation hooks (`[RuntimeInitializeOnLoadMethod]` and Unity's lifecycle-management attributes), generic constraints, generated identifiers and the Unity PlayerLoop.

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
| [Editor Tooling](#editor-tooling) | Menu commands, the Vault Browser, the Runtime Monitor, the scene dropdown, the code formatter. |
| [Performance Constraints](#performance-constraints) | Rules for hot paths. |
| [Engineering Procedures](#engineering-procedures) | Checklists for common engineering tasks. |

## Architecture

### Subsystems and static services

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

Project systems become woven user subsystems through `WeavingFactory.User.cs` and `Subsystems.User.cs` (see [Implementing a Subsystem](#implementing-a-subsystem)).

### Register hierarchy

`ThreadlinkSubsystem<T>` uses the curiously recurring generic pattern to give each subsystem a type-safe static singleton. A subsystem's `ID` is `ThreadlinkSubsystem<T>.TypeHash`, the xxHash32 of its type's full name. Three bases build on it:

| Base | Additions | Application |
|---|---|---|
| `Register<S, O>` | A `Dictionary<int, O>` keyed by `IIdentifiable.ID`; `HasLinked(id)`, `TryGetLinkedObject<T>(id, out T)` | Lookup tables |
| `Linker<S, O>` | `TryLink`, `TryDisconnect<T>(id, out T)`, `DisconnectAll(trimRegistry)` | Tracking objects created elsewhere |
| `Weaver<S, O>` | `TryWeave<T>(out T)`, `TryWeave<T>(original, out T)`, `TrySever(id)`, `SeverAll()` | Owning object lifecycles |

A `Weaver` creates objects through `WeavingFactory<T>`: `TryWeave<T>(out T)` through its `OnCreate` factory, and `TryWeave<T>(original, out T)` through its `OnCreateFrom` clone factory. Severing discards the object.

`Threadlink` is itself a `Weaver<Threadlink, IThreadlinkSubsystem>`. `Aura` is a `Linker<Aura, AuraSpatialObject>`.

In the Editor, every register also implements `IEditorOnlyRegister`, which exposes its contents (`EditorOnly_Count`, `EditorOnly_ObjectType`, `EditorOnly_Registry`) to tools such as the [Runtime Monitor](#runtime-monitor). It is not compiled into builds.

### Lifecycle contracts

| Interface | Member | Semantics |
|---|---|---|
| `IAddressablesPreloader` | `UniTask<bool> TryPreloadAssetsAsync()` | Runs first. Loads dependencies. |
| `IBootable` | `void Boot()` | Like `Awake`. Order within the phase is not defined, so don't rely on other objects. |
| `IInitializable` | `void Initialize()` | Like `Start`. Other objects are ready to reference. |
| `IDiscardable` | `void Discard()` | Teardown. Unsubscribe here, then call `base.Discard()`. |
| `IDiscoverable` | Marker | A `LinkableBehaviour` placed in a scene boots when the scene loads and is discarded before it unloads, wherever it sits. Inactive objects don't boot. |
| `IDependencyConsumer<T>` | `bool TryConsumeDependency(T)` | Receives a dependency. |
| `IAsyncBinaryConsumer` | `UniTask ConsumeBinariesAsync()` | Reads binary data authored through `IBinaryAuthor` (see [Binary Authoring](#binary-authoring)). Not part of the Initium pipeline: the consumer's owner awaits it. |

Boot and initialise run as batches, one frame apart. Order within a batch is not guaranteed. See [Deployment and Initialisation](#initialisation-pipeline).

### Base object types

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
| `UserInterface<S>` | `UserInterface` (see [Dextra](#the-ui-stack)) |

Their instances are cleared when Play Mode starts or ends, so with Domain Reload disabled no Play session sees the previous one's.

### Ownership and discarding

`Discard()` is called by whoever owns the object, never by the object itself, and never from an Iris event:

- **Nexus**, through `Initium.DiscardSceneObjects`, for the discoverables placed in a scene it unloads (see [Deployment and Initialisation](#deployment-and-initialisation) and [Nexus](#nexus-scene-management)).
- **The subsystem that created an object**, in its own `Discard()`. Dextra discards its interfaces this way.
- **An interface**, for the selectables and input icons in it.

The framework boots, initialises and discards every `IDiscoverable` it finds, and nothing else. Nesting is up to you: avoid nesting discoverables, or know how they nest and handle the consequences. You boot and discard everything that isn't discoverable.

The framework's own object types (`AuraSpatialObject`, `Interactable`, `EntityDetector`, `DextraInputIcon`, the selectables) are never discoverable, because gameplay decides how they are combined and controlled. Manage them from the object that owns them, or through a discoverable type of your own.

A `Linker` does not drop its links on scene events. Each linked object unlinks itself in `Discard()`, as `AuraSpatialObject` does, and its owner calls that in time.

### Subsystem access

```csharp
if (Dextra.TryGetSingleton(out var dextra))
    dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
```

`TryGetSingleton` checks that the instance exists and is still linked to the core, so it returns `false` during teardown. Don't keep a subsystem reference across a scene transition without checking it again. Subsystem singletons are also cleared when Play Mode starts or ends.

Reach the core the same way, with `Threadlink.TryGetSingleton(out var core)`. `core.IsDeployed` becomes `true` once every subsystem is woven and initialised, just before `OnCoreDeployed`. `Threadlink.Terminate()` discards the core, which uninstalls the update loop and quits the application (exits Play Mode in the Editor).

## Deployment and Initialisation

### Deployment sequence

| Phase | Hook | Action |
|---|---|---|
| 1 | `[OnEnteringPlayMode]` | `NativeWeavingFactory.Register()` registers factories for Sentinel, Chronos, Dextra and Aura. Nexus subscribes to Unity's scene events. |
| 1 | `SubsystemRegistration` | `UserWeavingFactory.Register()` registers project factories. The Sentinel module registry is cleared. |
| 2 | `AfterAssembliesLoaded` | `UserSubsystemsConfig` subscribes to `OnUserSubsystemRegistration`. Sentinel platform modules register with `SentinelModuleRegistry`. |
| 3 | `BeforeSceneLoad` | `NativeSubsystemsConfig` subscribes to `OnNativeSubsystemRegistration`. |
| 4 | `AfterSceneLoad` | `Threadlink.DeployCoreAsync()` runs. |

`DeployCoreAsync`:

1. In the Editor, returns at once if `ThreadlinkEditorDeployment.Suppressed` is set (see [Installation and Project Setup](#verify-the-deployment)).
2. Awaits `Addressables.InitializeAsync()`.
3. Loads `ThreadlinkNativeConfig` from the address in `NativeConstants.Addressables.NATIVE_CONFIG`.
4. Loads `ThreadlinkUserConfig` through the Native Config's `UserConfig` native resource.
5. Creates the core and runs `DeployAsync()`:
   - `Boot()` installs `ThreadlinkPlayerLoop` into Unity's `PlayerLoop` when the update loop is `Native`. No scene object or `MonoBehaviour` is created. It also hooks the core's shutdown to `Application.quitting`.
   - `RegisterSubsystemsAsync(OnNativeSubsystemRegistration)` publishes the `Func<List<IThreadlinkSubsystem>>` event, runs the returned subsystems through the [initialisation pipeline](#initialisation-pipeline), and logs them.
   - The same happens for `OnUserSubsystemRegistration`. An empty list is reported and skipped.
   - `IsDeployed` becomes `true`, and `OnCoreDeployed` is published with the core as payload.
6. Starts the initialisation pipeline, without awaiting it, for the discoverable objects of every scene already loaded.

If either configuration asset fails to load, deployment stops with an error. A preloader that returns `false` throws, as described below. Sentinel is the exception: a failed platform does not fail its preloader (see [Sentinel](#deployment)).

### Initialisation pipeline

Initium's pipeline is how the core prepares both subsystems and discoverable objects. It sorts its input by interface and runs three phases, each finishing before the next starts:

1. `IAddressablesPreloader.TryPreloadAssetsAsync()`
2. `IBootable.Boot()`
3. `IInitializable.Initialize()`

The input is collected once, so every phase sees the same objects: an object created during `Boot` is not initialised without having been booted. Preloaders all run together. If any returns `false`, the pipeline throws an `InvalidOperationException` naming it and stops before Boot. Boot and Initialize run in batches: every object in the phase, then one frame. Order within a phase is not guaranteed.

At deployment, scene objects are found across every loaded scene with `Object.FindObjectsByType<LinkableBehaviour>(FindObjectsInactive.Exclude).OfType<IDiscoverable>()`.

The pipeline itself is internal. Two public members handle individual scenes:

| Member | Semantics |
|---|---|
| `Initium.BootAndInitSceneObjectsAsync(Scene)` | Preloads, boots and initialises the active `IDiscoverable` objects in the scene. |
| `Initium.DiscardSceneObjects(Scene)` | Discards every `IDiscoverable` in the scene, active or not, children first. If one throws, the error is logged and the rest are still discarded. Call it before the scene unloads. |

Nexus does both automatically for the scenes it holds, however many are resident. Code that loads scenes some other way must call both itself.

Unity destroys scene objects without calling `Discard()`, so never unload a scene behind Nexus's back: every Nexus unload discards its discoverables first. Scene objects release in `Discard` what they registered in `Boot`, and never discard themselves. Subsystems don't tear them down on scene events either: an object registered with a subsystem unregisters itself when Nexus discards it (see [Ownership and discarding](#ownership-and-discarding)).

#### Booting objects directly

Boot and initialise objects you own with Initium's direct members. None of them runs preloaders.

| Member | Semantics |
|---|---|
| `Boot(IBootable)`, `Initialize(IInitializable)` | One phase, synchronously. A null object is ignored. |
| `BootAndInit<T>(T)` | Both phases, synchronously. |
| `BootAsync(IBootable)`, `InitializeAsync(IInitializable)` | One phase, then a single frame. |
| `BootAndInitAsync<T>(T)` | Boot, a frame, initialise, a frame. |
| `Boot<T>(IReadOnlyList<T>)`, `Initialize<T>(IReadOnlyList<T>)` | Each object in order, with a frame after each. |
| `BootAndInit<T>(IReadOnlyList<T>)` | Boots the list in order, then initialises it in order. |

### Shutdown

Shutdown runs when the application quits, which in the Editor is when Play Mode exits:

1. **Scenes.** Nexus releases every hold, so nothing is presented. Each held scene's objects are discarded while the subsystems they registered with still exist, and its unload starts. Unity finishes any unload that quitting cuts short.
2. **Subsystems.** Woven subsystems are discarded in reverse weave order: user subsystems in reverse of their listed order, then Aura, Dextra, Chronos and Sentinel. So a subsystem is discarded before those it depends on, as long as its dependencies are woven first. If one subsystem's `Discard` throws, it is reported and the rest are still discarded.

So `Discard()` is the one place a subsystem releases what it holds: sockets, native memory, platform sessions.

The update loop is uninstalled when the core is discarded or the application quits. When Play Mode ends, Iris clears every listener, Nexus forgets its scenes and framework statics reset, so the next Play session starts clean even with Domain Reload disabled.

## Iris: Event Dispatch

`Iris` is a static class. Its `object[] EventRegistry` has a slot for every value up to the highest `ThreadlinkIDs.Iris.Events` value (values can have gaps; see [Domain kinds](#domain-kinds)). Each slot gets a `DelegateList<T>` on its first subscription.

### Dispatch signatures

```csharp
Iris.Publish(eventID);                          // Action
Iris.Publish<Input>(eventID, input);            // Action<Input>
Iris.Publish<Output>(eventID);                  // Func<Output>        (single listener)
Iris.Publish<Input, Output>(eventID, input);    // Func<Input, Output> (single listener)
```

Subscribe and unsubscribe with an explicit delegate type:

```csharp
Iris.Subscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
Iris.Unsubscribe<Action<Nexus.ISceneEntry>>(ThreadlinkIDs.Iris.Events.OnScenePresented, OnScenePresented);
```

Subscribing a delegate that is already subscribed has no effect.

Diagnostics: `TryGetListenerCount(eventID, out int)`, `ContainsListener<T>(eventID, listener)`, `Clear(eventID)`. The [Runtime Monitor](#runtime-monitor) shows listener counts per event.

### Constraints

- **The delegate type is the contract, and the compiler doesn't check it across subscribers.** Subscribing `Action<Foo>` to a slot holding `Action<Bar>` logs a type-mismatch error and drops the subscription. Publishing with the wrong type does nothing. Keep signatures consistent; the native ones are in [Native Iris Events](#native-iris-events).
- **A `Func` event allows one listener**, and throws `InvalidOperationException` beyond that: it is a single provider, not a broadcast. With no listener, publishing returns `default`.
- **`Action` listeners run newest first, over the listeners present when publishing began.** A listener removed or cleared during dispatch is skipped at once; one added waits for the next publication (a nested publication counts as new). Slots are compacted after the outermost dispatch, even if a listener throws. Dispatch allocates no snapshot.
- **Unsubscribe in `Discard()`.** A delegate left subscribed keeps a destroyed object alive and runs on stale state.
- Publishing an event with no listeners does nothing.
- Every listener is cleared when Play Mode exits.

### Update events

With the `Native` update loop, `ThreadlinkPlayerLoop` inserts three systems into Unity's **current** `PlayerLoop` that publish the Iris update events. No `GameObject` or `MonoBehaviour` is involved.

| Iris event | Native PlayerLoop position |
|---|---|
| `OnUpdate` | Immediately **before** `Update.ScriptRunBehaviourUpdate` |
| `OnFixedUpdate` | Immediately **after** `FixedUpdate.ScriptRunBehaviourFixedUpdate` |
| `OnLateUpdate` | Immediately **after** `PreLateUpdate.ScriptRunBehaviourLateUpdate` |

Installing first removes any old Threadlink systems, then fails at once if a Unity anchor is missing. The loop is uninstalled when the core is discarded or the application quits, and reset on entering Play Mode in case Domain Reload is disabled. `Custom` mode installs nothing; the project publishes the three update events itself (see [Update loop modes](#update-loop-modes)).

### Declaring events

Declare the identifier in `Threadlink User/Native Domain Injectors/Iris.Events.User.txt` and save.

Iris events are the only **ordinal** domain: values are array indices, assigned in source order (native entries first, then the injector's) and kept stable by the domain's manifest. They are never serialised, so there are no tombstones: removing an event breaks compilation wherever it is used.

## Scribe: Logging

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

this.Send("Loaded ", count, " entries.").ToUnityConsole();
Scribe.Send<MySystem>("Static context message.").ToUnityConsole(DebugType.Error);
```

`Send` takes `params object[]` and appends each argument to a ZString UTF-8 builder, so passing several arguments avoids building an interpolated string. It returns the builder; `ToUnityConsole(DebugType)` writes it to the console. `DebugType` is `Info` (the default), `Warning` or `Error`.

Each message starts with a type name in colour: the receiver's runtime type for the extension form, or the type argument for the static form. Severities are tinted white, yellow and red.

The runtime and most editor tools log through Scribe. A few build-time tools (the Sentinel build pipeline, the Steam App ID processor, the code formatter and **Match Addressables to Paths**) use `Debug` directly. The [Runtime Monitor](#runtime-monitor) counts the session's messages and shows recent Threadlink ones.

## Chronos: Time

Chronos's time members are static; `Start()` and `Stop()` are instance methods.

| Member | Semantics |
|---|---|
| `TimeScale` | Accepts only 0 or 1; other values, and the current value, are ignored. Setting it publishes `OnGamePaused` or `OnGameResumed`. |
| `RawTimeScale` | Same restriction; publishes nothing. |
| `DeltaTime`, `SmoothDeltaTime`, `UnscaledDeltaTime` | Cached once per `OnUpdate`. |
| `FixedDeltaTime` | Cached once per `OnFixedUpdate`. |
| `CurrentFramerate` | `1 / DeltaTime`. |
| `CurrentTimeSinceDeployment` | Unity's `Time.time`, cached each `OnUpdate`. |
| `TotalPlaytime`, `CountTotalPlaytime`, `PlaytimeCountingMode`, `ClearTotalPlaytime()` | `PlaytimeCountMode` is `Scaled` (the default) or `Unscaled`. |
| `Start()`, `Stop()` | Subscribe and unsubscribe the tick handlers. |

`Boot()` resets `TotalPlaytime` to zero, turns on `CountTotalPlaytime` and starts the tick handlers. While `CountTotalPlaytime` is on, each frame publishes `OnPlaytimeCountTick` with the running total.

Chronos updates its values from the Iris update events. In `Custom` update-loop mode, they are only as current as the project's own `OnUpdate` and `OnFixedUpdate` publications.

### Manual physics simulation

With `ChronosConfig.IrisPhysicsUpdate` on, `Chronos.Boot()` sets `Physics.simulationMode = SimulationMode.Script` and calls `Physics.Simulate(Time.fixedDeltaTime)` on each `OnFixedUpdate`.

Leave it off if another framework owns the simulation. The simulation mode is global: if other code also steps physics in script mode, physics steps twice. Nothing guards against this.

## Nexus: Scene Management

`Nexus` is a static class with one scene workflow, whether a game has one scene at a time or many at once:

- **Residency:** a scene is loaded while anything holds it.
- **Presentation:** the resident scene the local player sees and hears.
- **Transitions:** presentation behind the fader and loading screen.

Every scene loads additively beside the bootstrap scene (see [Deployment Overview](#deployment-overview)), which stays loaded throughout. Nexus never loads or unloads the bootstrap scene.

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

The interface is nested in `Nexus` (`Nexus.ISceneEntry`). Any type can implement it: a ScriptableObject, a struct, or another framework's asset type. Every member after the volumes has a default.

- **`PhysicsMode`:** `LocalPhysicsMode.None` shares the default physics scene. A scene resident alongside others should have its own (`LocalPhysicsMode.Physics2D` or `Physics3D`). Get it with `scene.GetPhysicsScene2D()` or `GetPhysicsScene()` and step it yourself: Unity does not simulate local physics scenes.
- **`UnloadDelay`:** how long the scene stays resident once nothing holds it. A hold within that time keeps it without reloading.
- **Audio:** an unmapped music or ambience pointer (such as `None`) plays nothing on that channel. If neither is mapped, presenting the scene leaves the audio as it is.

### Residency: holds

A scene is resident while anything holds it:

```csharp
Nexus.SceneHold farm = await Nexus.HoldAsync(farmEntry);   // resident and booted; null if it failed to load
Scene scene = farm.Scene;

farm.Release();                                            // unloads once nothing holds it, after entry.UnloadDelay
farm.Release(delaySeconds: 10f);                           // or after this long, unless it is held again meanwhile
```

A `SceneHold` exposes its `Entry`, its `Scene`, and `IsHeld`, which is `false` once released.

- **Loading.** A scene loads once, additively with the entry's `PhysicsMode`. Its discoverables boot through Initium and `OnFinishedLoadingAsync` is awaited; only then do holders get it, and `OnSceneResident` is published. Holders that arrive during the load share it.
- **Failed loads.** If the scene fails to load or its objects fail to boot, every holder gets `null` and an error is logged. A scene that loaded but failed to boot is discarded and unloaded; a later hold tries again.
- **Unloading** always takes the same path: await `OnBeforeUnloadedAsync`, publish `OnSceneUnloading`, discard every `IDiscoverable` in the scene through `Initium.DiscardSceneObjects` (children first), then unload. So scene objects unregister in `Discard` whatever they registered in `Boot`, and never discard themselves. If an unload fails, Nexus keeps the scene and retries on the next hold, release or shutdown.
- **Ordering.** Operations on one scene never overlap. A hold during an unload waits for it, then loads the scene again. A hold during a delayed unload cancels it. A scene whose last hold is released while it is still loading unloads once the load finishes.
- **Queries.** `Nexus.IsResident(entry)`, `Nexus.TryGetResidentScene(entry, out scene)` and `Nexus.TryGetResidentScene(scenePointer, out scene)` only succeed for a scene that is loaded, booted and not unloading.
- **Robustness.** An exception in an `OnSceneResident` or `OnSceneUnloading` listener is logged and can't stop a load or unload halfway.

Release each hold once, when its holder no longer needs the scene; releasing it again does nothing. Residency alone changes nothing the player sees or hears. The [Runtime Monitor](#runtime-monitor) lists held scenes with their state and hold count.

### Presentation and transitions

```csharp
bool presented = await Nexus.PresentAsync(entry);      // hold, activate, audio, OnScenePresented, then release the previous scene
bool transitioned = await Nexus.TransitionAsync(entry); // the same behind the fader and loading screen: the single-scene workflow
await Nexus.StopPresentingAsync();                     // present nothing, releasing the presented scene

Nexus.ISceneEntry current = Nexus.Presented;
Nexus.TryGetPresentedScene(out Scene scene);
```

- **Presenting holds the scene,** loading it if needed, makes it the active scene, switches to its audio, and publishes `OnScenePresented`. Then it releases its hold on the previously presented scene. So a scene held only by presentation unloads when something else is presented, while a scene something else holds stays resident.
- **The result** is `false` if the scene could not be loaded, and the current scene stays presented. Presenting the scene already presented does nothing and returns `true`.
- **Presentations run one at a time,** in call order, and `StopPresentingAsync` queues with them.
- **`TransitionAsync`** wraps `PresentAsync` in `FadeToLoadingScreenAsync` and `FadeToGameplayAsync`. These drive four `Func<UniTask>` events (`OnDisplayFaderAsync`, `OnHideFaderAsync`, `OnDisplayLoadingScreenAsync`, `OnHideLoadingScreenAsync`) and Aura's listener volume fade. Each event needs exactly one provider.

The fade steps are public for custom sequences: `FadeToLoadingScreenAsync()`, `FadeToGameplayAsync()`, `FadeFaderAsync(bool visible)`, `FadeLoadingScreenAsync(bool visible)` and `FadeAudioAsync(float targetVolume)`.

A single-scene game needs nothing else:

```csharp
await Nexus.TransitionAsync(townEntry);   // the previous scene unloads, its objects discarded
```

A game with several resident scenes, such as a multiplayer host that simulates every occupied area but shows only its own player's, holds the areas it simulates and presents one:

```csharp
var area = await Nexus.HoldAsync(entry);   // simulated: no visible or audible change
await Nexus.TransitionAsync(entry);        // the local view moves to it; it stays held by both
area.Release(gracePeriod);                 // later, once no one needs it
```

Objects booting in a resident scene must not assume it is the active scene, and objects instantiated at runtime must be moved to their scene explicitly. Hiding resident scenes that aren't presented (renderers, lights, canvases) is up to the project; `OnSceneLoaded` fires just before a scene's first frame.

### Events

| Event | Delegate | Raised |
|---|---|---|
| `OnSceneLoaded` | `Action<Nexus.SceneLoad>` (`Scene`, `Mode`) | As Unity activates a scene: before it renders and before its discoverables boot. |
| `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Once a scene has loaded, booted and run `OnFinishedLoadingAsync`, before any holder gets it. |
| `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Once the presented scene is active and its audio transition has run. |
| `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Before any unload, while the scene's objects still exist. Use it to read them, for example to save their state; their own `Discard` handles teardown. |
| `OnSceneUnloaded` | `Action<Scene>` | Once a scene has unloaded. The `Scene` is only useful as a key by then. |

`OnSceneLoaded` and `OnSceneUnloaded` republish Unity's events for every scene, however it was loaded, so project code never needs to subscribe to `SceneManager`.

### Shutdown

When the application quits (in the Editor, when Play Mode exits), shutdown first releases every hold, so nothing is presented and each held scene's objects are discarded while their subsystems still exist. Then the subsystems are discarded (see [Shutdown](#shutdown)). Unity finishes any unload that quitting cuts short.

## Dextra: Input and UI

Dextra is built on Unity's Input System and uGUI. Its preloader loads the Dextra Config, every interface prefab the config lists, and the **Dextra Components** prefab. Dextra instantiates that prefab's `EventSystem` (with an `InputSystemUIInputModule`) as a `DontDestroyOnLoad` object, hidden in the Hierarchy if the config says so.

### Device resolution

`Dextra.InputDevice` is `MouseAndKeyboard`, `XBOXController`, `PSController` or `SwitchProController`. `CurrentInputDevice` follows whichever device last had a control change: DualShock and DualSense gamepads map to `PSController`, Switch Pro controllers to `SwitchProController`, other gamepads to `XBOXController`, and keyboard or mouse to `MouseAndKeyboard`. On a change, every gamepad's rumble stops and `OnInputDeviceChanged` is published.

`TryGetInputIcon(device, controlPath, out Sprite)` gets the glyph from the Dextra Config. A `DextraInputControlPath` is an Input System control path.

A `DextraInputIcon` is a bootable `LinkableBehaviour`, but not discoverable. From `Boot` until it is discarded it shows its control's glyph, and it hides its image when the active device has none:

- In one of Dextra's interfaces, the interface boots and discards it.
- Anywhere else, the code that owns it must.

### Input modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The Dextra Config maps each mode to an `InputActionReference` in a `FieldHashMap`. A mode resolves to the action map that action belongs to. `InputModes.Unresponsive` (zero) is reserved.

### Pointer and selection

| Member | Semantics |
|---|---|
| `CurrentPointedObject`, `OnPointerEnter`, `OnPointerExit` | The `Selectable` under the mouse, from the UI input module's last raycast, checked once per frame after late update. A change is reported only after it holds for three checks, which filters out the module's double-firing. |
| `SelectUIElement(GameObject)` | Selects an element through the EventSystem and publishes `OnUIElementSelected`. Returns a `UniTaskVoid`; call `.Forget()`. `null` clears the selection. |
| `ClearEventSystemSelection()` | Clears the selection and publishes `OnUIElementSelected` with `null`. |
| `SetEventSystemActive(bool)`, `SetUIInputModuleActive(bool)` | Turn the EventSystem, or its UI input module, on or off. |
| `TryGetEventSystem(out EventSystem)`, `TryGetUIInputModule(out InputSystemUIInputModule)` | The instantiated objects. |

A selection the EventSystem makes by itself is kept only while the top interface is interactable (`IInteractableInterface`); otherwise it is cleared on the next frame.

### The UI stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and need a `CanvasGroup`. Initium doesn't discover them. Dextra instantiates them from `DextraConfig.interfacePointers` (Addressable prefab identifiers), marks them `DontDestroyOnLoad`, sets their alpha to zero, makes them non-interactable, and boots and initialises them. There is one instance per concrete interface type, and Dextra discards them in its own `Discard`. Each interface discards its selectables, and boots and discards its input icons.

So to add an interface: build the prefab, map it in the Addressables Mapping Window, and add its identifier to `interfacePointers`.

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

- **Stacking** an interface already on top does nothing. Otherwise the current top is covered, and the new interface is pushed and notified. `Stack<U, D>` passes the payload to `IStackingDataPreprocessor<D>.Preprocess` before showing the interface, and warns if the interface doesn't implement it for that type.
- **Popping** removes the top interface and resurfaces the one beneath.
- **Cancelling** only affects an `ICancellableInterface` on top. If it reports `IsInSubPanel`, it gets `OnSubPanelCancelled()` and stays. Otherwise it is popped, the one beneath resurfaces, it gets `OnCancelled()`, and `OnUICancelled` is published with it as payload. Either way, Aura's cancel sound plays.
- **Clearing** pops every stacked interface.

The [Runtime Monitor](#runtime-monitor) shows the stack, top first.

#### Interface lifecycle

`UserInterface` reacts to the stack through protected virtual hooks:

| Hook | Default behaviour |
|---|---|
| `OnStacked()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnCovered()` | Fades out unless it is an `IPersistentInterface`; becomes non-interactable. |
| `OnResurfaced()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnPopped()` | Fades out; becomes non-interactable. |

Fades run on `OnUpdate` in unscaled time, over the Dextra Config's fade durations. Derived code starts them with `Display()` and `Hide()`. `ForceCanvasGroupAlphaTo(float)` sets alpha at once, and `SetInteractableState(bool)` toggles interactivity and raycast blocking. `IsVisible` and `IsHidden` are true when fully shown or fully hidden. An interface with a `Canvas` disables it while fully transparent.

#### Marker interfaces

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Receives `OnCancelled()` and `OnSubPanelCancelled()`; reports `IsInSubPanel` |
| `IPersistentInterface` | Stays visible when covered |
| `IInteractableInterface`, `IInteractableInterface<T>` | Has selectable content; the generic form exposes `Selectables` and `LastSelectable` |
| `IStackingDataPreprocessor<T>` | Preprocesses the stacking payload |

#### Selectables

`DextraSelectable` wraps a uGUI `Selectable`, raises `OnSelected` and `OnDeselected`, and turns mouse hover into selection so gamepad and mouse navigation behave the same. `DextraSelectable<T>` exposes the wrapped component as `GenericUnitySelectable`; `DextraButton` wraps a `Button` and exposes its `OnClick`. `ClearSelectionListeners()` removes selection listeners without a full discard, for pooled selectables.

`InteractableUserInterface<Singleton, Selectable>` is the base for an interactable singleton interface. It registers its `Selectables` at boot, remembers the last one selected, reselects it when stacked or resurfaced, and discards its selectables in its own `Discard`.

`NonDrawingGraphic` is a raycast target that draws nothing.

The stack doesn't manage interfaces placed in scenes. `ClearStackedInterfaces()` doesn't affect them, and `DontDestroyOnLoad` instances survive transitions, so hide them yourself.

### Interactables

`Interactable` is a `LinkableBehaviour` with an `InteractableConfig`. `Interactable2D` and `Interactable3D` use a 2D or 3D collider on the same GameObject as the active area. Like the other framework types it isn't discoverable: your code discards it, which disables its collider and stops it answering `OnInteract`.

`InteractablesDetector2D` (a `CapsuleCollider2D`) and `InteractablesDetector3D` (a `CapsuleCollider`) are the triggers that find interactables. They publish `OnInteractableDetected` and `OnInteractableOutOfRange` with the interactable as payload. Both derive from the generic `EntityDetector2D<Area, Entity>` and `EntityDetector3D<Area, Entity>`, which project code can extend for other entity types.

When detected, an interactable subscribes its `Interact` method to `OnInteract` as a `Func<bool>`, and unsubscribes when out of range. Project code publishes `OnInteract`, typically from an input action, to interact with whatever is in range. With `InteractionOptions.InteractOnContact` in its config, an interactable interacts as soon as it is detected instead. `InteractionPrompt` is the config's prompt text, localised when `THREADLINK_LOCALIZATION` is defined.

Because `OnInteract` is a `Func` event, only one interactable can be in range at a time. Overlapping active areas throw.

## Aura: Audio

Aura's preloader loads the Aura Config and the **Aura Components** prefab, which it instantiates as a `DontDestroyOnLoad` object. The prefab needs three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`; without them, preloading fails. At boot Aura mutes the music and ambience channels and creates its own hidden `AudioListener`.

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

- **The listener** is moved, not parented. Something must call `DriveAudioListener` every frame, usually a camera controller on `OnLateUpdate`; otherwise the listener stays where it was last put. Take care during scene transitions, when the old driver is destroyed before the new one exists.
- **Fades** step once per frame at the config's Volume Fade Speed, in unscaled time, and clamp their targets to 0–1.
- **Audio scenarios.** `TransitionToAudioScenarioAsync` fades both channels out, swaps their clips and fades them in to the requested volumes, capped by the maxima from `SetGlobalVolumesMax`. Nexus calls both when it presents a scene (see [Nexus](#the-isceneentry-contract)).
- **UI sounds.** `PlayUISFX` loads the config's navigation, confirm or cancel clip and plays it on the SFX channel. With `oneShot: false`, it stops the channel first.
- **Mixer parameters.** `TryGetMixerValue` and `TrySetMixerValue` read and write the master mixer's exposed parameters.

### Spatial objects

Aura links only the **presented** scene's `AuraSpatialObject`s (including `AuraZone`s). Once the core has deployed, and on every `OnScenePresented`, it disconnects every spatial object and links the active ones in the active scene. Other resident scenes are never linked, so a scene kept loaded for another player is silent.

While anything is linked, each `OnUpdate` Aura adds up the objects' influence at the listener and moves music and ambience towards their maxima minus that total. An object's influence falls linearly from its `Influence` at the source to zero at its radius. The radius is the AudioSource's maximum distance times the Radius Coefficient, read at runtime from the `Source` field or, if that is empty, the AudioSource on the same GameObject. Custom spatial objects derive from `AuraSpatialObject` and supply `SourcePosition`; they can set `Radius` themselves or override `GetSpatialInfluence`.

Aura never drops its links on scene events. A spatial object unlinks itself, and stops its source, when discarded. It isn't discoverable, so the code that owns it must discard it before its scene unloads; otherwise Aura keeps evaluating a destroyed object until the next presentation. Aura's own `Discard` disconnects whatever is left, such as the persistent scene's.

Links are keyed by `LinkableBehaviour.ID`, a case-insensitive hash of the object's name, so spatial object names must be unique within a scene. A duplicate is not linked, and a warning names both objects. The [Runtime Monitor](#runtime-monitor) lists what is linked.

## Sentinel: Platform Services

Sentinel gives one API for platform services on every platform. Unity decides the platform; `SentinelConfig` only picks the **distribution** (store) where a platform has several, such as Windows: Local, Steam, Microsoft Store, Epic or GOG. Runtime modules each handle one exact `SentinelPlatformMarker` / `SentinelDistribution` pair, and expose services according to their capabilities.

Sentinel covers what platforms provide: accounts, saves, achievements, presence, friends and invites. It is not a networking or game-state transport.

| Page | Covers |
|---|---|
| This page | Deployment, distributions, capabilities, services, accounts, results. |
| [Transactional Saving](#transactional-saving) | `ISaveService` and save transactions. |
| [Social and Invitations](#social-and-invitations) | `IMultiplayerService`: friends, presence, invites, join requests. |
| [Modules](#modules) | The shipped Local and Steam modules, writing a module, the build pipeline. |

### Deployment

Sentinel deploys while preloading, moving through:

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

1. **Platform.** `SentinelRuntimePlatformResolver` maps Unity's runtime platform to a `SentinelPlatformMarker`. In Play Mode it uses the Editor's host platform (Windows, macOS or Linux).
2. **Distribution.** `SentinelConfig.GetDistribution(platform)` picks the distribution, unless [overridden](#distribution-override).
3. **Module.** `SentinelModuleRegistry` needs a module for that exact platform and distribution. If none is installed, deployment fails with `SentinelError.Unsupported`.
4. **Platform initialisation.** The module creates its `ISentinelPlatform`, and Sentinel awaits its `InitializeAsync()`.

**A failed platform does not stop Threadlink.** Only a missing `SentinelConfig` fails Sentinel's preloader. Any platform failure (Steam not running, a missing module, a native error) leaves Sentinel `Failed`, logs `InitializationResult` as an error during `Boot`, and lets deployment continue without platform services. `TryGetService` and `HasCapability` then return `false`. The project decides what a failed platform means (degraded play, an error screen, quitting) by checking `State` after deployment. The [Runtime Monitor](#runtime-monitor) shows the state, result and capabilities live.

**Retrying, losing the platform, and state changes.**

- `RetryAsync()` deploys the platform again while Sentinel is `Failed` (for example, after the player starts Steam) and returns whether it is now `Ready`. It returns `true` at once if already `Ready`, and `false` while another retry is running.
- A module reports a platform that goes away after initialising by calling the protected `SentinelPlatform.ReportLost(result)`, as the Steam module does on `SteamShutdown_t`.
  - Sentinel then moves to `Failed` with `SentinelError.PlatformLost`.
  - It publishes the state change at once, so code using the platform can release it synchronously.
  - It discards the platform one frame later, after the reporting callback has returned.
- Whenever deployment settles (`Ready` or `Failed`), Sentinel publishes the native Iris event `OnSentinelStateChanged` (`Action<Sentinel>`): after the first deployment, after a retry, and on a loss.
- Sentinel deploys before any subsystem initialises, so a subscriber reads `State` once when subscribing, then follows the event.
- In the Editor and development builds, `SimulatePlatformLoss()` runs the loss path, for tests. It does nothing in release builds.

### Distributions

`SentinelDistributionPolicy` defines which distributions each platform allows. If only one is allowed, it is used and the Sentinel Config is ignored. If several are, the config's choice is used; an invalid choice resolves to `None`, which fails deployment.

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

The policy lists what a project may target, but a target only works with a matching runtime module installed (see [Modules](#modules)).

#### Distribution override

`SentinelDistributionOverride` replaces the configured distribution at runtime. Use it for instances that must not use the configured store, such as several development instances on one machine sharing one Steam account. Sources, highest priority first:

| Source | Availability |
|---|---|
| `SentinelDistributionOverride.Requested`, assigned by project code before deployment (for example at `BeforeSceneLoad`) | Always |
| Command-line argument `-threadlink-sentinel-distribution <Distribution>` (`COMMAND_LINE_ARGUMENT`) | Editor and development builds |
| Environment variable `THREADLINK_SENTINEL_DISTRIBUTION` (`ENVIRONMENT_VARIABLE`) | Editor and development builds |

Values are `SentinelDistribution` names, in any case (for example `Local`). The active platform must allow the override; a disallowed or unknown value is ignored with a warning. An applied override is logged. `Requested` resets on entering Play Mode.

### Capabilities

`SentinelCapability` is a flags enumeration. A module advertises only what it implements; `sentinel.HasCapability(flag)` is only `true` while Sentinel is `Ready`.

| Group | Flags |
|---|---|
| Accounts | `Accounts`, `AccountPicker`, `MultipleLocalAccounts` |
| Saving | `LocalSaves`, `CloudSaves`, `SaveTransactions` |
| Achievements | `Achievements`, `AchievementProgress` |
| Input | `InputOwnership` |
| Online | `Stats`, `Leaderboards`, `Presence`, `Friends`, `Entitlements`, `Commerce`, `Invites` |

`capabilities.Has(flag)` tests a set of flags directly.

### Services

Get platform services from `Sentinel`, and account services from the account:

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

`ISentinelAccount` has `ID`, `DisplayName`, `State` (`Unknown`, `SignedOut`, `SigningIn`, `SignedIn`, `Suspended`) and `IsPrimary`. It is also a service provider, for account services such as saves, achievements and social features.

### Results

Services report ordinary failures through `SentinelResult` / `SentinelResult<T>`, not exceptions. A result has `Succeeded`, `Error`, `Message` and an optional `NativeCode`. The typed result adds `Value`, and `Untyped()` drops it.

`SentinelError` values are `None`, `NotInitialized`, `Unsupported`, `InvalidArgument`, `NotFound`, `Busy`, `Cancelled`, `PermissionDenied`, `NetworkUnavailable`, `QuotaExceeded`, `Conflict`, `CorruptData`, `AccountUnavailable`, `NativeFailure`, `Unknown` and `PlatformLost`. A failure created with `None` is reported as `Unknown`.

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

`SentinelFriendPresence` values are `Unknown`, `Offline`, `Online`, `Busy`, `Away`, `Snooze`, `LookingToTrade` and `LookingToPlay`.

### Private invitations

`InviteFriendAsync(friendID)` uses the published session presence; `InviteFriendAsync(friendID, joinPayload)` sends a private payload instead. The caller owns token creation, authorisation, expiration and revocation. Both use the same `SentinelJoinRequest` delivery, including cold launches.

A `SentinelSessionPresence` with a null or empty `JoinPayload` retains status and group information but clears the joinable connect key, making the session non-joinable.

### Join requests

A join request goes to `JoinRequested` if it has a subscriber, and is queued otherwise, so a request that arrives before the game is ready isn't lost. Once ready, the game drains the queue with `TryDequeueJoinRequest`. A game launched by accepting an invitation (a cold launch) receives the request the same way; its source user is unknown.

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

`TryGet<T>` fails if the field is missing, isn't a `DataField<T>`, or holds `null`. `TrySet<T>` fails if the field is missing or isn't a `DataField<T>`. Every member is virtual, so a derived Vault can intercept access.

Fields live in a `RefHashMap<ThreadlinkIDs.Vault.Fields, DataField>`, which uses `[SerializeReference]` so fields can be of any type. The shipped field types are in `Threadlink.Vault.DataFields` and listed in [The Vault](#field-composition).

`DataField<T>` has a `Value` and an `OnValueChanged` event, raised on every assignment. Its backing is `SerializedValue<T>` or `TransientValue<T>`. The transient one is `[NonSerialized]` and resets each session, but because a Vault is a ScriptableObject, it survives between Play sessions in the Editor unless you reset it. Discarding a field clears its backing and raises `OnValueChanged` with `default`, then removes its listeners. `TryApplyValueTo(vault, fieldID)` copies a field's value into another Vault's field of the same type.

The [Vault Browser](#vault-browser) lists and edits every Vault in the project.

### Custom field types

To add a field type, write a serialisable subclass of `DataField<T>` in a runtime assembly:

```csharp
[Serializable] public sealed class Colour : DataField<Color> { }
```

It appears in the field-type dropdown.

### Timeline integration

Under `THREADLINK_TIMELINE`, a Timeline can carry Vault values:

- **`VaultMarker`** is a Timeline marker holding field identifiers with typed values, and notification flags.
- **`VaultTrack`** is a marker track bound to a `VaultReceiver`.
- **`VaultReceiver`** is a component that raises `OnMarkerReceived(PlayableDirector, VaultMarker)` when a marker fires. `ClearCallback()` removes its listeners.

The listener decides what to do with the values. `marker.TryApplyConfigTo(vault)` writes them all into a Vault, logging any field it couldn't set.

## Resource Loading

There are two matching APIs: instance members on the core that take a generated identifier, and static members that take an Addressables reference.

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

Asset and prefab loads return `null` if the identifier is unmapped or its reference is invalid; a prefab load also returns `null` if the prefab has no `T`. Synchronous loads block until Addressables finishes. A failed load is logged and its handle released. Loading an asset that is already loaded, or loading, reuses that operation.

Normally, load scenes through [Nexus](#nexus-scene-management). The scene members here are the layer beneath it, for code that manages a scene itself and so must also boot and discard its objects (see [Deployment and Initialisation](#initialisation-pipeline)).

### Queries and validation

```csharp
core.TryGetAssetReference(id, out AssetReference reference);
core.TryGetPrefabReference(id, out AssetReferenceGameObject reference);
core.TryGetSceneReference(id, out SceneAssetReference reference);
core.TryGetLoadedScene(ThreadlinkIDs.Addressables.Scenes id, out Scene scene);
core.CheckIDValidity(id);                                                  // scenes, assets or prefabs
int found = core.CollectAssetIDs<T>(List<ThreadlinkIDs.Addressables.Assets> result);   // mapped assets of type T, none loaded
```

Every lookup goes through the User Config's maps and checks `RuntimeKeyIsValid()`, reporting failures through Scribe.

`CollectAssetIDs<T>` adds every mapped asset whose main type is `T` or derives from it, without loading any, and returns how many it added. The Mapping Window records each asset's main type in the User Config (`assetTypes`, as `"Namespace.Type, Assembly"`) when it applies. So code that needs every asset of a kind (a game's simulation content, say) asks for their IDs and loads them, without keeping its own catalogue. Assets mapped before types were recorded are found once the mappings are applied again.

### Renamed assets

An asset ID is a hash of its group and name, so renaming an asset or moving it to another group gives it a new ID and leaves the old one as a tombstone. The manifest records each mapped asset's GUID, so the next Apply finds the asset under its new name and records the old ID as an alias of the new one in the User Config (`assetAliases`).

```csharp
core.TryResolveRenamedAsset(oldID, out ThreadlinkIDs.Addressables.Assets currentID);
core.CollectRenamedAssets(List<(ThreadlinkIDs.Addressables.Assets OldID, ThreadlinkIDs.Addressables.Assets CurrentID)> result);
```

Data that stored the old ID, such as a save, can resolve or rewrite it. Aliases only exist for renames made after GUIDs were recorded; older entries get their GUIDs the next time the mappings are applied.

### Scene references

Scene loads are "load or get": loading a scene that is already loaded, or loading, returns that instance, whatever parameters are passed. `UnloadSceneAsync` completes once the scene has actually unloaded. A failed scene load or unload is reported through Scribe and returns a default `SceneInstance` rather than throwing.

`SceneAssetReference` tracks its load through `SceneOperation`, not `AssetReference.OperationHandle`, which Addressables only fills for `LoadSceneMode` loads. It adds `LoadSceneAsync(LoadSceneParameters)`, `UnloadSceneAsync(bool autoReleaseHandle)` and `TryGetLoadedScene(out Scene)`, and routes the base `LoadSceneAsync(LoadSceneMode)`, `UnLoadScene()` and `ReleaseAsset()` through the same operation.

The reference overloads let a portable module hold its own references without depending on a project's generated `Assets` enumeration. The extension methods underneath (`LoadSynchronously<T>`, `LoadAsync<T>`, and the scene `LoadAsync` / `UnloadAsync`) are public in `Threadlink.Shared.AssetReferenceExtensions`.

### Native resource batches

To load several framework resources at once, `ThreadlinkNativeConfig.LoadNativeResourcesAsync` takes an `AddressablesRequest<NativeResources>` and fills a dictionary you supply. The request is a fixed-capacity `UnsafeList<T>` (`new AddressablesRequest<T>(count, allocator)`, `Add`, `Dispose`); the loader runs the Addressables operations together using pooled task storage, then collects the results. Load a single native resource with `LoadNativeResourceAsync<T>(id)`.

## Implementing a Subsystem

A project subsystem derives from `ThreadlinkSubsystem<T>` (or `Register`, `Linker` or `Weaver`; see [Architecture](#register-hierarchy)) and implements whichever other lifecycle interfaces it needs. Every subsystem can boot and be discarded. Overrides of `Boot()` and `Discard()` must call the base method, which sets and clears the singleton.

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

Register it in two files.

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

During deployment the subsystem then goes through preload, boot and initialise, and `InventorySystem.TryGetSingleton(out var instance)` returns it. Implement only the lifecycle interfaces you need.

The list order is the weave order. Shutdown discards in reverse, so weave a subsystem after the ones it depends on (see [Shutdown](#shutdown)). The [Runtime Monitor](#runtime-monitor) shows the weave order, and lists a register's linked objects.

> For custom construction, assign a factory delegate to `WeavingFactory<T>.OnCreate` instead of calling `Register<T>()`. `WeavingFactory<T>.OnCreateFrom` is the clone factory used by `TryWeave<T>(original, out T)`. Each holds one delegate: adding one replaces the previous, and removing clears it. `ThreadlinkSubsystems.Register<T>()` (namespace `Threadlink.Utilities.Subsystems`) is the same as `WeavingFactory.Register<T>()`.

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

When two worlds can run in one process (a host's authoritative simulation and its prediction, two worlds in a test, a world and a preview), give each its own **`StatelessRNG.Source`** so they never share randomness:

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

> Domain values are name hashes. Renaming a domain changes its stream, so anything that replays an earlier sequence from a stored seed or replay will diverge. Domain names are part of the save format, as is the stream format itself.

## Memory

`Threadlink.Memory` (namespace `Threadlink.Memory`) is for long-lived state that is copied, hashed and saved as raw bytes, such as a simulation's frames. It allocates native memory, hashes it the same way on every machine, and lays collections out inside it, so a collection is copied, hashed and saved along with the bytes that hold it.

The assembly depends on nothing in Threadlink, and nothing in the framework references it. To use it, reference `Threadlink.Memory` from your assembly definition and enable unsafe code.

### NativeAllocator

| Member | Semantics |
|---|---|
| `AllocateZeroed(bytes, alignment = DEFAULT_ALIGNMENT)` | A zeroed block of `bytes` bytes, aligned to `alignment` (a power of two; `DEFAULT_ALIGNMENT` is 64, a cache line). Zeroing is always done, because a block hashed whole must not contain leftover bytes. |
| `Free(block)` | Frees a block from `AllocateZeroed`. A null block is ignored. |
| `Copy(destination, source, bytes)` | Copies between non-overlapping ranges. |
| `Clear(block, bytes)` | Zeroes a range. |
| `Equal(a, b, bytes)` | Whether two ranges hold the same bytes. |

Blocks come from Unity's tracked persistent allocator, so the Editor's leak detection reports any block never freed. Invalid sizes and alignments throw `ArgumentOutOfRangeException`; a failed allocation throws `OutOfMemoryException`.

### MemoryHash

Hashes of raw memory. The same bytes give the same hash on every machine and runtime, which a type's own `GetHashCode` does not promise.

| Member | Use |
|---|---|
| `XxHash64(byte* data, long length, long seed = 0)` | Fingerprints whole blocks, such as a simulation frame, to detect divergence. Returns exactly what `System.IO.Hashing.XxHash64` would, but works directly on raw memory and is many times faster in the Editor's Mono runtime. Big-endian runtimes fall back to the library. |
| `XxHash32(byte* data, int length)`, `XxHash32<T>(in T value)` | Places keys in hash tables. A value's padding must be zeroed. |

### Inline collections

Inline collections are views over a fixed number of slots inside another value's memory, such as a simulation component. Each is a `ref struct` over a pointer, because that memory can be copied or moved (a frame rolled back). It can't be stored, boxed or captured, so no pointer outlives the call that made it. Elements are `unmanaged`.

| Type | Behaviour |
|---|---|
| `InlineArray<T>(memory, length)` | `Length` slots, always present. Indexer by reference, `Clear()`, enumeration. |
| `InlineList<T>(memory, capacity)` | `Count`, indexer, `Add`, `RemoveAt` (moves the last element into the gap), `Clear()`, enumeration. |
| `InlineQueue<T>(memory, capacity)` | A first-in, first-out ring: `Enqueue`, `Dequeue`, `Peek`, `Clear()`. |
| `InlineStack<T>(memory, capacity)` | Last in, first out: `Push`, `Pop`, `Peek`, `Clear()`. |
| `InlineHashSet<T>(memory, capacity)` | `Contains`, `Add` and `Remove` (each reporting whether it changed the set), `Clear()`, enumeration. |
| `InlineDictionary<TKey, TValue>(memory, capacity)` | `ContainsKey`, `TryGetValue`, indexer by reference (throws if absent), `TryAdd`, `Remove`, `Clear()`, enumeration of `(Key, Value)`. |

Adding past capacity throws. Element order depends only on the operations performed, as it must for anything hashed or compared byte for byte.

Sets and dictionaries use open addressing with linear probing over a fixed table. Keys are hashed and compared as raw bytes, never through `GetHashCode` or `Equals`, so a table holds the same bytes on every machine; zero a key's padding for this reason. Removal leaves a marker that a later insertion reuses, so churn never fills the table. Capacity is the table size: leave headroom above the most entries you expect, because a nearly full table probes further.

#### Layout

`InlineLayout` defines the byte layout; a layout generator must produce exactly these sizes:

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

One pipeline generates every enumeration in `Threadlink/Generated/` and `Threadlink User/Engineering/Codebase/Generated/`.

### Domain kinds

| Kind | Value derivation | Removal semantics | Applies to |
|---|---|---|---|
| **Identity** | xxHash32 of the scope-qualified key (`scope/name`, trimmed, ignoring case) | Kept as an `[Obsolete]` tombstone, value unchanged | Every domain except Iris |
| **Ordinal** | The lowest free index, assigned in source order and kept by the manifest. A removal leaves a gap that a later entry may fill | Removed entirely | `Iris.Events` |

Identity is the default, so a removal can never shift another entry's value. Iris is ordinal because dispatch uses the value as an index into `EventRegistry`; the registry spans the highest value, so gaps are harmless. Iris values are never serialised, so a removal that breaks compilation is the right outcome.

### Sources

A domain's entries come from up to three sources, merged in this order:

1. **Native entries**: a framework `.txt` (`Iris.Events.Native.txt`, `Addressables.NativeResources.Native.txt`), with an empty scope.
2. **Injectors**: files named `{DomainName}.{Injector}.txt` anywhere in the injector folder. The injector name becomes the **scope**, which is part of each entry's hash key; `Iris.Events.User.txt` is the injector named `User`. Injectors are read in file-path order.
3. **Domain definitions**: a `.txt` at the top of the definitions folder declares a new enumeration named after the file, generated through the shared `CustomDomain.Shell.txt` into the `Threadlink.User` namespace and the `Threadlink.User.Generated` assembly.

In every source, blank lines and lines starting `//` are ignored, and an entry repeated in the same scope (ignoring case) counts once.

Injectors can extend project-defined domains exactly as they extend native ones, so a third-party module can add entries to a project's domain.

This is how modules install. A module that needs its own Iris events ships `Iris.Events.MyModule.txt`; dropping it into the injector folder is the whole installation. A module's entries can't displace existing ones: values and member names already in the manifest never change, and new entries only get values and names nobody holds.

> A module's injector filename is part of its data contract. Renaming `Iris.Events.Photon.txt` to `Iris.Events.PhotonQuantum.txt` changes the scope, and so every identity-domain value that injector contributes.

### Manifests

Each domain has a `{DomainName}.manifest.json` in a `Manifests` folder beside its generated script. It records every entry's key, member name, scope, value, seed offset and tombstone state (and, for mapped assets, the asset's GUID). Commit manifests to version control; they are what keeps identifiers stable:

- A member name, once assigned, never changes.
- Name collisions resolve the same way every time: a second `Splash` is qualified with its scope, as `GroupName_Splash`, then numbered (`Splash_2`, `Splash_3`, …) if that is taken too. The existing entry keeps its name.
- Member names are sanitised: a first character that isn't a letter or underscore, and any later character that isn't a letter, digit or underscore, becomes an underscore.
- Removed entries are marked as tombstones, not deleted. Declaring one again revives it.
- Hash collisions, including with reserved values, are detected and rehashed with a recorded seed offset, so the result is reproducible.

Each pass that changes a domain logs a summary of additions, removals, renames, rescopes and collisions through Scribe. A collision that can't be resolved is reported as an error, and that domain is left as it was.

### Shells

A **shell** is the C# template for a generated file: namespace, documentation comment, enumeration declaration, and a `{DOMAIN_ENTRIES}` token where entries go. `CustomDomain.Shell.txt` also has a `{DOMAIN_NAME}` token, so one template serves every project-defined domain.

Shells declare sentinels (`None = 0`, `Unresponsive = 0`) as literal members, and the allocator reserves those values. Shells don't declare generated entries. The Iris shell has no sentinel, so Iris values start at zero.

### Generation triggers and guards

An `AssetPostprocessor` watches the native-entries files and the injector, Addressables-injector and definition folders, and regenerates when a `.txt` there is imported, deleted or moved. **`Threadlink ▸ CodeGen ▸ Run Domain CodeGen`** runs a pass on demand.

The pipeline:

- Only writes inside the configured generated folders, so a misconfigured domain can't overwrite hand-written code.
- Stops if two domains would write to the same file.
- Reports, by filename, any injector whose prefix matches no domain.
- Rejects injectors with more than one segment after the domain name; `{DomainName}.{Injector}.txt` is the only accepted form.
- Generates just the sentinel (plus any tombstones), with a warning, for a domain with no entries.
- Leaves a failed domain as it was, and still generates the others.

### Addressables Mapping Window

**`Threadlink ▸ Addressables ▸ Mapping Window`** lists every writable Addressable group, its assets, and a tick box per asset. **Apply**:

1. Writes one injector per group and reference kind (`Addressables.Assets.{Group}.txt` and so on) into the Addressables injector folder.
2. Regenerates the three Addressables domains.
3. Rebuilds the User Config's reference maps from the manifests, including each asset's main type and the aliases of renamed assets (see [Resource Loading](#resource-loading)).

Group names become scopes: `Test Assets` becomes `Test_Assets`. Two groups that reduce to the same scope are flagged, and Apply won't run until one is renamed.

Apply writes changed files in one import batch, keeps existing file GUIDs, and removes old injectors only once their replacements exist. Unchanged files are not rewritten. Clearing a tick unmaps the asset. The injectors in that folder are generated; don't edit them.

The secondary button, **Reset Addressables manifests and apply**, asks for confirmation, then clears the history (tombstones included) of the Scenes, Prefabs and Assets manifests and rebuilds them from the current ticks. Other domains are untouched. IDs resolved after a collision can change and removed IDs can be reused, so use it only for a deliberate compatibility reset; a normal Apply keeps history. Each file is replaced atomically, but the operation as a whole is not crash-safe.

Editor tools can map through the same logic with `ThreadlinkAddressablesMapping` (`Threadlink.Editor.CodeGen`), so a tool that creates Addressable content needs no manual step:

```csharp
// Exactly what ticking the rows and selecting Apply does; existing mappings are kept.
if (ThreadlinkAddressablesMapping.Map("Assets/Content/Scenes/Farm/Farm.unity") is false)
    return; // Not an entry of an editable Addressable group; the reason is logged and nothing is applied.

bool mapped = ThreadlinkAddressablesMapping.IsMapped("Assets/Content/Scenes/Farm/Farm.unity");
List<string> unmapped = ThreadlinkAddressablesMapping.FindUnmapped(assetPaths);
```

`Map` takes one or more paths, and does nothing if they are all mapped already. `FindUnmapped` returns those of the given paths that aren't mapped. `ThreadlinkAddressablesMapping.Apply()` reapplies the current mappings without resetting history. Applying regenerates domains and recompiles when needed, so code that needs the new identifiers must run after the domain reload, for example from an `[InitializeOnLoad]` reconciler. An open Mapping Window refreshes itself after `Map`.

### Editor Config composition

Each entry in the Editor Config's `nativeDomains` array has a domain name, an output filename (the domain name if empty), a shell, optional native entries, and three flags:

| Flag | Meaning |
|---|---|
| `ordinalValues` | Positional values. Only for `Iris.Events`. |
| `reserveZero` | The shell declares a sentinel at zero, so the allocator never uses it. On by default. |
| `sourcedFromAddressables` | Also reads injectors from the Addressables injector folder. |

`domainName` is the prefix injector filenames must match. Nothing validates it, so a typo means its injectors are never read; the orphaned-injector report catches this.

The Editor Config also sets the generated scripts folder, the injector and Addressables-injector folders, and the domain-definition folder, scripts folder and shell. The shipped Editor Config declares every native domain in [Identifier Domains](#identifier-domains).

## Collections and Utilities

### Serialisable maps

Serialisable maps are in `Threadlink.Collections`. Both derive from `ThreadlinkHashMap<TKey, TValue>`: bucket-indexed, allocation-free on lookup, and rebuilt by `ISerializationCallbackReceiver` after deserialisation.

| Type | Value backing | Use for |
|---|---|---|
| `FieldHashMap<K,V>` | `[SerializeField]` | Value types and Unity object references |
| `RefHashMap<K,V>` | `[SerializeReference]` | Managed values of varying types, such as Vault's `DataField` |

At runtime they are read-only: `Count`, `ContainsKey`, `TryGetValue`, the indexer's getter (a missing key logs a `KeyNotFoundException` and returns `default` instead of throwing), `Keys` and `Values` as spans, `KeyMemory` and `ValueMemory` as memory, `For(action)` to visit each entry by reference, and `Clear()`.

In the Editor, `EditorOnly_TryAdd`, `EditorOnly_Add`, `EditorOnly_Remove` and the indexer's setter modify them. If a serialised entry count exceeds the backing arrays, `OnAfterDeserialize` clamps it and reports the problem instead of throwing.

### Serialisable arrays and lists

`Threadlink.Collections` also wraps serialised collections. Each wrapper exposes `Collection` as an `IList<T>`, and an indexer:

| Type | Backing |
|---|---|
| `FieldArray<T>`, `FieldList<T>` | `[SerializeField]` array or list |
| `RefArray<T>`, `RefList<T>` | `[SerializeReference]` array or list, with a type picker for each element |

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

`Threadlink.Utilities.RNG` extends `System.Random` for non-deterministic uses only; anything reproducible uses [StatelessRNG](#statelessrng).

### Attributes

- **`[MinMaxRange(min, max)]`** draws a range slider for a `Vector2`, `Vector2Int`, `float2` or `int2` field. The float form takes an optional number of displayed decimals (0–3, default 1).
- **`[ReadOnly]`** is a marker with no drawer of its own, since an attribute drawer would replace the hash-map drawer. `ThreadlinkHashMapDrawer` reads it from `fieldInfo` and draws the map without add, remove or reorder controls. The Odin drawer respects both this attribute and Odin's own `ReadOnly`.
- **`[HashMapDrawer(mode)]`** chooses which inspector draws a hash map: `Automatic` (Odin's if installed, otherwise Threadlink's), `Native` (always Threadlink's), or `Odin` (Odin's, or Threadlink's if Odin is missing).

### Shared types and helpers

In `Threadlink.Shared`:

| Type | Purpose |
|---|---|
| `HashFunctions` | `ToXxHash32` and `ToXxHash64` of a string (trimmed and lower-cased first, optionally seeded), and `ToSplitMix64`. The framework hashes names this way for object, subsystem and identifier IDs. |
| `AssetReferenceExtensions` | `LoadSynchronously<T>`, `LoadAsync<T>`, and scene `LoadAsync` / `UnloadAsync` for Addressables references (see [Resource Loading](#resource-loading)). |
| `AddressablesRequest<T>` | A fixed-capacity, disposable batch of resource identifiers. |
| `byte2`, `ushort2` | Compact two-component integer structs; `ushort2` converts to and from Mathematics' `int2` and `uint2`. |
| `ThreadlinkConfigFinder` (Editor) | `TryGetConfig<T>(out T)` finds and caches the project's single asset of a configuration type. |

### Animation helpers

| Type | Purpose |
|---|---|
| `AnimatorHash` (`Create ▸ Threadlink ▸ Animation ▸ Animator Hash`) | An asset holding a precomputed `Animator.StringToHash` value as `Value`. Enter the parameter or state name, then generate the hash from the asset's context menu (or a button, with Odin). |
| `AnimationEventsReceiver` | A `LinkableBehaviour`, placed beside an `Animator`, whose `Receive…` methods are animation-event targets. It re-raises them as C# events with the animator: `OnVoidReceived`, `OnFloatReceived`, `OnIntegerReceived`, `OnStringReceived`, `OnUnityObjectReceived`, `OnVaultReceived` (when the object is a Vault) and `OnEventReceived`. |

Both are in the `Threadlink.Animation` namespace.

## Configuration and Project Setup

### Bootstrap scene

Every project needs a bootstrap scene, the build's entry point (see [Deployment Overview](#deployment-overview)):

- **Make one scene that is not Addressable**, the first and ideally only scene in Build Settings. Every other scene is Addressable and mapped (see [Registering Scenes, Prefabs and Assets](#registering-scenes-prefabs-and-assets)).
- **Keep it minimal.** It is never unloaded, so whatever is in it lives for the whole run. Its discoverables boot at deployment. Gameplay belongs in the scenes Nexus loads.
- **Start from it in the Editor too.** Otherwise Play Mode starts from the open scene, which then stands in for the bootstrap scene: it stays loaded, and if it is an Addressable gameplay scene, Nexus loads a second copy beside it. Set `EditorSceneManager.playModeStartScene` to the bootstrap scene so Play boots as a build does (see [Installation and Project Setup](#create-the-bootstrap-scene)).

### Configuration assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`s. Must be at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset and prefab reference maps, asset types and asset aliases; binaries folder (Editor). |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector and definition folders (see [Editor Config composition](#editor-config-composition)). |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Master mixer, volume fade speed, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface fade durations, interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | The distribution (store) for platforms where Unity's build target doesn't decide it. Only one may exist. |

Other entries: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply these native resources:

| Resource | Asset |
|---|---|
| `UserConfig` | The User Config. |
| `SentinelConfig` | The Sentinel Config. The Sentinel build pipeline checks it is the project's one Sentinel Config. |
| `ChronosConfig` | The Chronos Config. |
| `DextraConfig` | The Dextra Config. |
| `DextraComponentsPrefab` | A prefab with an `EventSystem` and an `InputSystemUIInputModule`. |
| `AuraConfig` | The Aura Config. |
| `AuraComponentsPrefab` | A prefab with three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`. |

Only the [Addressables Mapping Window](#addressables-mapping-window) writes the User Config's reference maps (scenes, assets, asset types, asset aliases, prefabs); they are read-only in the Inspector. The framework ships these assets configured; see [Installation and Project Setup](#shipped-configuration).

### Addressable registration of native assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** makes the Native Config and every native asset it references Addressable in the `Threadlink Assets` group (or the default group, with a warning, if that group is missing), addressed by asset path.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** resets every address in the writable groups to its asset path.

### Update loop modes

Set on the User Config:

- **Native**: Threadlink inserts `ThreadlinkPlayerLoop` systems into Unity's current PlayerLoop, which publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate`.
- **Custom**: Threadlink installs nothing. The project publishes those events from its own driver, usually set up on `OnCoreDeployed`.

Use Custom when another framework owns the simulation and Threadlink only runs the view, for example with Photon Quantum running a deterministic multiplayer simulation.

### Scripting defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline` ≥ 1.8.10 (version define on `Threadlink.Runtime` and `Threadlink.Editor`) | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization` ≥ 1.5.9 (version define on `Threadlink.Runtime`) | `LocalizedText` Vault field, localised interaction prompts, localisation utilities |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps, Vault fields and inspectors |
| `STEAMWORKS_NET` | Defined by the project (Player Settings) | Compiles the Steam Sentinel module (`Threadlink.Sentinel.Steam`) |
| `THREADLINK_STEAMWORKS_NET_165` | `com.rlabrecque.steamworks.net` ≥ 2025.165.0 (version define on `Threadlink.Sentinel.Steam`) | Reserved for version-specific code; no code consumes it yet |

### Binary authoring

Types implementing `IBinaryAuthor` write authoring data to `.bytes` files in the project, from an Inspector button or context menu that calls `SerializeAuthoringDataIntoBinary()`. `ICollection<T>.SerializeIntoBinary(binariesFolder, filename)` (Editor, `Threadlink.Utilities.Collections`) writes a collection with MessagePack into a project folder, usually the User Config's binaries folder (`TryGetBinariesFolder`). The files are then loaded through Addressables and read through `IAsyncBinaryConsumer`.

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file in a chosen project folder, which is useful while iterating on a format.

## Editor Tooling

Every command is under the **Threadlink** menu. [Menus and Asset Creation](#menus-and-asset-creation) lists them all.

### Code generation and Addressables

| Command | Function |
|---|---|
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Runs a generation pass for every domain (see [Identifier Domains and Code Generation](#identifier-domains-and-code-generation)). |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers (see [Addressables Mapping Window](#addressables-mapping-window)). |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Makes the framework's own assets Addressable (see [Configuration](#addressable-registration-of-native-assets)). |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Sets each address to its asset path. |

### Vault Browser

**`Threadlink ▸ Vault Browser`** lists every Vault in the project on the left. Select one to inspect and edit it on the right, with undo, through the same drawers as the Inspector.

- Search filters by name or folder. The arrow keys move the selection.
- The **+** button creates a Vault. Each row shows the Vault's field count.
- Right-click a row, or use **⋯**, to ping, select, reveal, copy the path of, duplicate or delete a Vault.
- To open a Vault in the browser, right-click it and choose **Threadlink ▸ Open in Vault Browser**, or use **Open in Vault Browser** in its Inspector context menu.
- Edits made in Play Mode change the asset and persist, as they do in the Inspector. The header says so while playing.

### Runtime Monitor

**`Threadlink ▸ Runtime Monitor`** shows the runtime as a graph: the core in the centre, with every woven subsystem and static service (Iris, Nexus, Initium, Scribe) around it. It replaces the Registers Tracker.

- **Nodes.** Filled nodes are woven subsystems; outlined nodes are static services; dark nodes are not running. Each caption gives a live summary, such as linked objects, listeners or Sentinel's state. A red dot marks a failed Sentinel or errors in the console.
- **Details.** Click a node to inspect it in the side panel:

  | Node | Shows |
  |---|---|
  | Threadlink | Deployment state, weave order, configs, and the frame, memory and rendering figures below. |
  | Any register (Aura, user Linkers and Weavers) | Every linked object: name, ID, and a ping link for Unity objects. |
  | Sentinel | State, platform, distribution, module, initialisation result, capabilities. |
  | Chronos | Delta times, time scale, playtime. |
  | Dextra | Input device, pointed object, UI stack. |
  | Iris | Listener counts per event. |
  | Nexus | Presented scene, held scenes with state and holds, loaded Unity scenes. |
  | Initium | Discoverable objects per loaded scene. |
  | Scribe | Messages, warnings and errors this session, and recent Threadlink messages. |

- **Metrics.** The bottom strip shows FPS, main-thread time against the frame budget, GC allocation per frame, managed heap, memory and batches. The budget comes from a target frame rate (**Auto** uses `Application.targetFrameRate`, then the display's refresh rate); change it in the core's panel, which also plots main-thread time over the last 12 seconds.

Outside Play Mode the graph shows the native subsystems and the user subsystems declared in the project, without live data. Editor figures include the Editor's own work and memory, so budget against a development build.

The monitor reads the runtime through editor-only members, which are not compiled into builds: `Register.EditorOnly_Registry` (through `IEditorOnlyRegister`), `Threadlink.EditorOnly_WeaveOrder`, `Nexus.EditorOnly_Scenes` and `Dextra.EditorOnly_StackedInterfaces`. `Threadlink.IsDeployed` is public in every build.

### Scene dropdown

A **Scenes** dropdown in the main toolbar lists every scene that ships: the enabled scenes in Build Settings, then the scenes in Addressable groups, marked `(Addressable)`. Choosing one offers to save modified scenes, then opens it. The label shows the active scene; the context menu offers **Refresh** and **Open Build Settings...**. Scenes that are Addressable only through a folder entry are not listed.

### Code formatter

**`Threadlink ▸ Code Formatter`** formats C# with CSharpier. Assign a project folder and select **Format C# Files** to format every `.cs` file beneath it. Files CSharpier cannot parse are skipped and reported. Changed files are written in place, the AssetDatabase refreshes once at the end, and the window reports how many files changed, failed and were scanned.

### Binaries

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file in a chosen folder inside the project (see [Binary authoring](#binary-authoring)).

### Inspectors

- **Hash maps** draw through `ThreadlinkHashMapDrawer`, or Odin when installed. See the `[HashMapDrawer]` and `[ReadOnly]` attributes in [Collections and Utilities](#attributes).
- **`[MinMaxRange]`** fields draw as range sliders.
- **Sentinel Config** shows the current build target, its Sentinel platform, and the distributions that target allows.
- **`PopupInspector.TryInspect(asset)`** opens an asset's Inspector in its own window, for use by editor tools.

### Build processors

| Processor | Stage | Function |
|---|---|---|
| `SentinelBuildProcessor` | Pre-build, first (`callbackOrder` −10000) | Resolves and validates the build's Sentinel platform, distribution and build module, and fails the build on any error (see [Sentinel Modules](#the-build-pipeline)). |
| Steam `SteamAppIdBuildProcessor` | Post-build, standalone targets | Copies `steam_appid.txt` into development Steam builds and `threadlink_steam_appid.txt` into release Steam builds, and removes both from other builds (see [Sentinel Modules](#steam-1)). |

## Performance Constraints

| Practice | Why |
|---|---|
| Copy `Chronos.DeltaTime` into a local once per tick. | Avoids repeated static property access in hot loops. |
| Log through `Scribe`, not `Debug.Log`. | ZString avoids interpolated strings, and the prefix names the source. |
| Use `UniTask` only; no `System.Threading.Tasks` or coroutines in framework code. | Mixing them breaks the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | A delegate left subscribed keeps a destroyed object alive and runs on stale state. |
| Keep replay-sensitive and cross-machine deterministic logic to `FP` and `StatelessRNG`. | Hardware floats and `UnityEngine.Random` don't give the same results everywhere. |
| Keep state that is copied, hashed or saved byte for byte in [Memory](#memory) blocks and inline collections. | Raw-byte hashing and comparison are only stable when layout and padding are. |
| Do camera-relative work on `OnLateUpdate`. | It runs after the camera has moved. |
| Hash fixed strings once (`StatelessRNG.Part`, `HashFunctions`) and keep the result. | Hashing a string trims and lower-cases it, which allocates; a kept hash costs nothing. |

### Measuring

The [Runtime Monitor](#runtime-monitor) shows FPS, main-thread time against a frame budget, GC allocation per frame, managed heap and memory while in Play Mode. Use it to spot regressions while iterating. Editor figures include the Editor's own work, so use the Unity Profiler on a development build for numbers to budget against.

## Engineering Procedures

### Introducing a subsystem

- [ ] Declare `class X : ThreadlinkSubsystem<X>` with the required lifecycle interfaces.
- [ ] Provide a public parameterless constructor or a `WeavingFactory<X>.OnCreate` delegate.
- [ ] Register the factory in `UserWeavingFactory.Register()`.
- [ ] Weave the subsystem in `UserSubsystemsConfig.WeaveSubsystems()`, after the subsystems it depends on.
- [ ] Subscribe in `Initialize()`; unsubscribe in `Discard()`, then call `base.Discard()`.

See [Implementing a Subsystem](#implementing-a-subsystem). Check it in the [Runtime Monitor](#runtime-monitor): it should appear in the graph, linked to the core.

### Introducing an Iris event

- [ ] Declare it in `Iris.Events.User.txt` and save.
- [ ] Document its delegate signature and use it everywhere. A mismatched subscription logs an error; a mismatched publication does nothing.

### Introducing a scene

- [ ] Map the scene through the Addressables Mapping Window, and keep it out of Build Settings: only the bootstrap scene belongs there (see [Configuration](#bootstrap-scene)).
- [ ] Implement `Nexus.ISceneEntry` with its music and ambience; override `OnFinishedLoadingAsync` for setup.
- [ ] Choose its `PhysicsMode`: a scene resident alongside others needs its own physics scene.
- [ ] Provide a listener for the four fader and loading-screen `Func` events.
- [ ] Load it only through Nexus: `TransitionAsync` or `PresentAsync` to show it, `HoldAsync` to keep it resident. Nexus discards its objects whenever it unloads (see [Nexus](#residency-holds)). Release every hold.

### Introducing a stacked interface

- [ ] Build the prefab with a `CanvasGroup` and a component derived from `UserInterface` or `UserInterface<S>`.
- [ ] Map it through the Addressables Mapping Window.
- [ ] Add its prefab identifier to `DextraConfig.interfacePointers`.

### Using a custom update loop

- [ ] Set the User Config update-loop mode to `Custom`.
- [ ] Do not install `ThreadlinkPlayerLoop` yourself; it is internal, and Native mode owns it.
- [ ] Publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` from your driver at the right points in the frame.
- [ ] Keep the event contract Chronos, Aura, Dextra and project listeners expect.

### Introducing a Sentinel runtime module

- [ ] Implement an `ISentinelPlatform` / `SentinelPlatform` for one exact `SentinelPlatformMarker` + `SentinelDistribution` pair.
- [ ] Register a `SentinelModuleDescriptor` through `SentinelModuleRegistry.Register` at `AfterAssembliesLoaded`.
- [ ] Advertise only the capabilities it implements.
- [ ] Register platform-level services on the platform and account-scoped services on `ISentinelAccount` implementations.
- [ ] Provide an `ISentinelBuildModule` for the same pair; without it, builds for that pair fail.

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

All are in namespace `Threadlink.Generated`, assembly `Threadlink.Generated`, under `Threadlink/Generated/`, with their manifests in `Threadlink/Generated/Manifests/`.

Project-defined domains (one `.txt` per domain in `Threadlink User/Design/Custom Domain Definitions/`) are Identity domains with `None = 0` reserved, emitted into namespace `Threadlink.User`, assembly `Threadlink.User.Generated`, under `Threadlink User/Engineering/Codebase/Generated/`.

The native resources are `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraComponentsPrefab` and `ChronosConfig`.

## Menus and Asset Creation

### Threadlink menu

| Command | Function |
|---|---|
| `Threadlink ▸ Vault Browser` | Lists every Vault, and inspects and edits the selected one (see [Editor Tooling](#vault-browser)). |
| `Threadlink ▸ Runtime Monitor` | Shows the live runtime as a graph, with per-subsystem state and frame, memory and rendering metrics (see [Editor Tooling](#runtime-monitor)). |
| `Threadlink ▸ CodeGen ▸ Run Domain CodeGen` | Runs a generation pass for every domain. |
| `Threadlink ▸ Code Formatter` | Formats the C# files in a chosen folder with CSharpier. |
| `Threadlink ▸ Addressables ▸ Mapping Window` | Maps Addressable assets to generated identifiers. |
| `Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable` | Makes the framework's own assets Addressable. |
| `Threadlink ▸ Addressables ▸ Match Addressables to Paths` | Sets each address to its asset path. |
| `Threadlink ▸ Clear all Binaries` | Empties the `.bytes` files in a chosen folder. |

A Vault's context menus also offer **Open in Vault Browser**: `Assets ▸ Threadlink` in the Project window, and the Inspector's context menu.

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
| **Subsystem** | A service the core owns and runs through a lifecycle. |
| **Weaving** | Creating an object and owning its lifecycle. |
| **Linking** | Tracking an object created elsewhere. |
| **Domain** | A generated enumeration of identifiers. |
| **Injector** | A text file that adds entries to an existing domain; its name is the entries' scope. |
| **Domain definition** | A text file that declares a new enumeration. |
| **Shell** | The C# template a generated enumeration is written into. |
| **Manifest** | The JSON record of a domain's assigned member names and values. |
| **Tombstone** | A removed entry kept as `[Obsolete]` so existing references still resolve. |
| **Scope** | The qualifier in an entry's hash key: an injector name or an Addressable group. |
| **Scene entry** | An `ISceneEntry` implementation: a scene with its audio, physics mode and unload delay. |
| **Hold** | A claim on a scene that keeps it resident until released. |
| **Presented scene** | The resident scene the local player sees and hears. |
| **Bootstrap scene** | The one non-Addressable scene, first in Build Settings: the build's entry point, loaded for the whole run (see [Deployment Overview](#deployment-overview)). |
| **Discoverable** | A scene component Initium boots automatically when its scene loads and discards before it unloads. |
| **Distribution** | The store Sentinel uses on a platform, such as Steam or Local. |
| **Module** | A Sentinel implementation for exact platform/distribution pairs. |
| **FP** | Deterministic Q32.32 fixed-point number type (see [Fixed Point](#fixed-point)). |
| **Stream** | A sequence of random numbers that is a pure function of a seed, a domain and identity parts (see [StatelessRNG](#statelessrng)). |
| **Inline collection** | A collection laid out inside another value's memory (see [Memory](#memory)). |
