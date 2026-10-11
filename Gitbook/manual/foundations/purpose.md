# Purpose

Threadlink is a modular runtime framework layered over Unity, providing a unified backbone for games and interactive applications. It supplies:

- A self-deploying core that needs no scene-placed initialiser. Its one scene requirement is a bootstrap scene: the build's entry point, which stays loaded while Nexus loads every other scene through Addressables (see [Deployment Overview](deployment-overview.md)).
- Native subsystems covering event dispatch, time, scene management, input and UI, audio, and cross-platform platform services such as accounts, saves, achievements, and social/session affordances.
- A type-safe identifier system in which scenes, assets, prefabs, events, input modes, spawn points, RNG domains and data fields are C# enumerations generated from plain-text declarations, eliminating string-keyed lookup.
- The **Vault**, a polymorphic data container for designer-authored game data.
- A deterministic arithmetic and stateless RNG toolkit for reproducible simulation, replay-sensitive logic and procedural generation, and a native-memory toolkit for state that is copied, hashed and saved as raw bytes.

Threadlink does not supersede Unity. Scenes, prefabs and components are authored conventionally. Threadlink supersedes the intermediate infrastructure: manager singletons, event wiring, asset-reference bookkeeping, and persistence abstraction.

## Authored data, implemented behaviour

The framework maintains a single dividing line:

- **Designers author data.** They create Vault assets, populate configuration ScriptableObjects, place components in scenes, and declare identifiers in text files.
- **Engineers implement behaviour.** They author subsystems, implement scene logic, subscribe to events, and consume authored data.

Neither discipline encodes the other's concerns. Designers do not edit `.cs` files; engineers do not hard-code values belonging in a Vault.

## Content-addressed identity

Generated identifier values derive from a hash of the entry name rather than from its position in a declaration list. Consequently, removing an entry cannot alter the value of any other entry, and removed entries persist as obsolete tombstones so that previously serialised references remain resolvable.

This property underpins the safety of the identifier workflow. A single domain, Iris events, operates on positional allocation instead, and is identified as such at every point where the distinction is material. See [Identifier Domains and Code Generation](../engineer/code-generation.md).
