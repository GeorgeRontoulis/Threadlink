# Core Architecture & Object Lifecycle

Threadlink avoids monolithic managers by separating logic into strictly defined Native Subsystems and Registries.

#### Object Linking & Management

Threadlink offers distinct patterns for handling entities and objects in the game world, prioritizing explicit intent regarding memory and lifecycle authority:

* Linkers (`Linker<Singleton, Object>`): A specialized registry that links _existing_ scene objects to a singleton manager. Linkers do not manage object lifecycles. They strictly handle the connection (`TryLink`) and disconnection (`TryDisconnect`) of objects.
* Weavers (`Weaver<Singleton, Object>`): Unlike Linkers, Weavers have full authority to instantiate, track, and destroy objects. Use this when the framework needs to manage the lifecycle of prefabs or entities dynamically.
* Linkable Types: Scene elements and assets integrate into this architecture by inheriting from foundational base classes: `LinkableAsset`, `LinkableBehaviour`, or `LinkableSingletons`.
