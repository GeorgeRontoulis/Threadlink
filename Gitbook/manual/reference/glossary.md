# Terminology

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
| **Bootstrap scene** | The one non-Addressable scene, first in Build Settings: the build's entry point, loaded for the whole run (see [Deployment Overview](../foundations/deployment-overview.md)). |
| **Discoverable** | A scene component Initium boots automatically when its scene loads and discards before it unloads. |
| **Distribution** | The storefront or ecosystem Sentinel deploys on a platform, such as Steam or Local. |
| **Module** | An implementation of Sentinel for exact platform/distribution pairs. |
| **FP** | Deterministic Q32.32 fixed-point number type (see [Fixed Point](../engineer/deterministic/fixed-point.md)). |
| **Stream** | A sequence of random numbers that is a pure function of a seed, a domain and identity parts (see [StatelessRNG](../engineer/deterministic/stateless-rng.md)). |
| **Inline collection** | A collection laid out inside another value's memory (see [Memory](../engineer/memory.md)). |
