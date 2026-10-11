# Engineer Reference

> This volume assumes you know C#, Unity, Addressables and async programming. Threadlink uses **UniTask** throughout, never `System.Threading.Tasks`. It relies on runtime-initialisation hooks (`[RuntimeInitializeOnLoadMethod]` and Unity's lifecycle-management attributes), generic constraints, generated identifiers and the Unity PlayerLoop.

## In this volume

| Page | Covers |
|---|---|
| [Architecture](architecture.md) | Subsystems and static services, the register hierarchy, lifecycle contracts, base object types, ownership, subsystem access. |
| [Deployment and Initialisation](deployment.md) | The deployment sequence, the Initium pipeline, shutdown. |
| [Iris](iris.md) | Event dispatch and the update loop. |
| [Scribe](scribe.md) | Logging. |
| [Chronos](chronos.md) | Time, timescale, playtime, manual physics. |
| [Nexus](nexus.md) | Scene residency, presentation and transitions. |
| [Dextra](dextra.md) | Input devices, input modes, the UI stack, interactables. |
| [Aura](aura.md) | Audio mixing, the listener, spatial objects. |
| [Sentinel](sentinel/README.md) | Platform services: accounts, saves, achievements, social affordances, modules. |
| [Vault Runtime API](vault.md) | Reading and writing Vault fields; Timeline integration. |
| [Resource Loading](resource-loading.md) | Loading by identifier or by reference; queries; renamed assets. |
| [Implementing a Subsystem](implementing-a-subsystem.md) | Registering and weaving project subsystems. |
| [Monitoring](monitoring.md) | Reporting a subsystem's or service's state to the Runtime Monitor. |
| [Deterministic Toolkit](deterministic/README.md) | `FP`, its vectors and rotations, `StatelessRNG`. |
| [Memory](memory.md) | Native allocation, raw-memory hashing, inline collections. |
| [Identifier Domains and Code Generation](code-generation.md) | Domain kinds, sources, manifests, the Mapping Window. |
| [Collections and Utilities](collections-and-utilities.md) | Serialisable maps and collections, extension libraries, attributes. |
| [Configuration and Project Setup](configuration.md) | Configuration assets, update loop modes, scripting defines. |
| [Editor Tooling](editor-tooling.md) | Menu commands, the Vault Browser, the Runtime Monitor, the scene dropdown, the code formatter. |
| [Performance Constraints](performance.md) | Rules for hot paths. |
| [Engineering Procedures](procedures.md) | Checklists for common engineering tasks. |
