# Terminology

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
| **Bootstrap scene** | The one non-Addressable scene, first in Build Settings: the build's entry point, loaded for the whole run (see [Deployment Overview](../foundations/deployment-overview.md)). |
| **Discoverable** | A scene component Initium boots automatically when its scene loads and discards before it unloads. |
| **Distribution** | The store Sentinel uses on a platform, such as Steam or Local. |
| **Module** | A Sentinel implementation for exact platform/distribution pairs. |
| **FP** | Deterministic Q32.32 fixed-point number type (see [Fixed Point](../engineer/deterministic/fixed-point.md)). |
| **Stream** | A sequence of random numbers that is a pure function of a seed, a domain and identity parts (see [StatelessRNG](../engineer/deterministic/stateless-rng.md)). |
| **Report** | What a monitored subsystem or service says about its state: a summary and, on request, details (see [Monitoring](../engineer/monitoring.md)). |
| **Inline collection** | A collection laid out inside another value's memory (see [Memory](../engineer/memory.md)). |
