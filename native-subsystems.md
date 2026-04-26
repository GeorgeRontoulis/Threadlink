# Native Subsystems

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
