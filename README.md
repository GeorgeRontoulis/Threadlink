# User Manual

## Welcome

Welcome to the Threadlink Framework documentation. [Threadlink](https://github.com/GeorgeRontoulis/Threadlink) is a highly decoupled, modular architectural foundation built by Threadforge. Tailored specifically for Unity, it leverages Addressables, asynchronous programming, and domain-driven design to ensure your project remains scalable, performant, and clean across large studio teams.

***

## Core Architecture & Object Lifecycle

Threadlink avoids monolithic managers by separating logic into strictly defined Native Subsystems and Registries.

#### Object Linking & Management

Threadlink offers distinct patterns for handling entities and objects in the game world, prioritizing explicit intent regarding memory and lifecycle authority:

* Linkers (`Linker<Singleton, Object>`): A specialized registry that links _existing_ scene objects to a singleton manager. Linkers do not manage object lifecycles. They strictly handle the connection (`TryLink`) and disconnection (`TryDisconnect`) of objects.
* Weavers (`Weaver<Singleton, Object>`): Unlike Linkers, Weavers have full authority to instantiate, track, and destroy objects. Use this when the framework needs to manage the lifecycle of prefabs or entities dynamically.
* Linkable Types: Scene elements and assets integrate into this architecture by inheriting from foundational base classes: `LinkableAsset`, `LinkableBehaviour`, or `LinkableSingletons`.

***

## Native Subsystems

The core of Threadlink is divided into dedicated Native Subsystems, found under `Threadlink/Core/Native Subsystems/`.

#### Initium & Chronos

* Initium: The primary bootstrapper invoked by the core. It handles the deployment and strict initialization order of all native and user-defined subsystems.
* Chronos: The centralized update loop and time management system. It prevents the need for scattered `Update()` loops across MonoBehaviours.

#### Nexus (Scene & Spawning)

Nexus acts as the overarching environment and world manager.

* SceneEntry: Handles scene transitions, bootstrapping, and binding local scene data.
* Spawn Points: Centralized management of predefined entity spawn locations, backed by code-generated IDs (`Nexus.SpawnPoints.Native.txt`).

#### Dextra (Input, UI & Interactables)

Dextra is the bridge between the player and the game world.

* Interactables: A comprehensive 2D and 3D physics detection solution (`InteractablesDetector2D`, `InteractablesDetector3D`, `EntityDetector`). It uses an `Interactable` component paired with an `InteractableConfig` for additional customization.
* User Interface: An encapsulated, stack-based UI architecture (`UIStack`, `InteractableUserInterface`) that integrates cleanly with Threadlink's `DextraButton` and `DextraSelectable` .
* Input: Routes Unity's Input System via `DextraInputControlPath` to framework-level actions.

#### Aura (Audio)

Aura manages audio, including use cases with spatial context. By utilizing `AuraZone` and `AuraSpatialObject`, it allows you to control audio in a centralized manner and handle audio transitions as well as playback of small clips in events.

#### Sentinel (Platform Integrations)

Sentinel provides a unified API for interacting with external services and hardware.

* Out-of-the-box integrations for Steam (`Steam.cs`) and XBOX / GDK (`GDKRuntime`, `GDKSaveProvider`, `XBLAchievement`). Playstation and Ninentdo Switch are coming soon.
* Environment Configuration: Manages local execution contexts and environment states via `SentinelConfig`.

#### Iris (Event Bus)

Iris is a high-performance, GC-free global event dispatcher. It relies on code-generated event definitions (`Iris.Events.Native.txt`) to ensure type safety and eliminate string-based event lookups.

***

## Data, Memory, and Determinism

Threadlink is built with strict memory guidelines and determinism in mind.

#### The Vault System

The Vault is Threadlink's persistent data and state container.

* State fields are strictly typed and accessed via code-generated IDs (`VaultField.cs`, `NativeVaultFields.cs`).
* Designed to securely hold mutable player state, configurations, and other data separate from logic execution. Designers are recommended to use this solution to create Vault `ScriptableObjects` for all game data, utilizing Vault's powerful polymorphic serialization support. Unity Presets may then be utilized to enforce data archetypes (e.g. `WeaponData`, `StatData` etc.)

#### Determinism Utilities

For projects requiring high synchronization or predictable generation, Threadlink provides:

* StatelessRNG: A context-aware, stateless random number generator. It utilizes a local set of abstract identity components (`StatelessRNG.Context.cs`) that are hashed and XOR-ed to produce a highly predictable, scope-based RNG sample. Supported inputs for hashing include `longs`, `integers`, and `strings`.
* DeterministicFloat (DFP): A custom deterministic floating-point math library including Transcendentals and Trigonometry, preventing desyncs across varying hardware architectures.

#### Optimized Collections (`ThreadlinkHashMap`)

Standard dictionaries are innately unwieldy in Unity's ecosystem. Threadlink provides a custom `ThreadlinkHashMap` optimized both for the Unity Editor and read-only runtime access:

* `FieldHashMap<TKey, TValue>`: For standard struct/value serialization.
* `RefHashMap<TKey, TValue>`: Leverages Unity's `[SerializeReference]` for robust polymorphic serialization in the inspector.

***

## Code Generation & External Plugins

#### Code Generation (CodeGen)

To maintain performance and type-safety, Threadlink utilizes aggressive edit-time code generation (`Threadlink/Editor/CodeGen/`).

* Generates static lookup IDs for Addressables, Iris Events, Nexus Spawn Points, RNG Domains, and Vault Fields.
* This prevents runtime string hashing and ensures broken references result in compile-time errors rather than runtime exceptions.

#### Integrated Third-Party Tooling

Threadlink natively wraps and extends industry-standard Unity packages:

* UniTask: Replaces Coroutines with zero-allocation `async/await` state machines. The framework includes deep Addressables and DOTween async extensions.
* MessagePack: Secure and highly optimized binary serializer used for data persistence, networking or other operations.
* ZString: A zero-allocation string builder to format text for UI and logs without triggering the Garbage Collector.
