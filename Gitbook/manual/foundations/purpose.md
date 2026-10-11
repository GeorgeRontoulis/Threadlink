# Purpose

Threadlink is a modular runtime framework for Unity games and interactive applications. It provides:

- A core that deploys itself. Its one requirement is a bootstrap scene: the build's entry point, which stays loaded while Nexus loads every other scene through Addressables (see [Deployment Overview](deployment-overview.md)).
- Native subsystems for events, time, scenes, input and UI, audio, and platform services such as accounts, saves, achievements and invitations.
- Type-safe identifiers. Scenes, assets, prefabs, events, input modes, spawn points, RNG domains and data fields are C# enumerations generated from plain-text lists, so nothing is looked up by string.
- The **Vault**, a container for game data authored by designers.
- Deterministic maths and a stateless RNG for reproducible simulation, replays and procedural generation, and native-memory tools for state that is copied, hashed and saved as raw bytes.

Threadlink does not replace Unity: scenes, prefabs and components are authored as usual. It replaces the plumbing in between: manager singletons, event wiring, asset-reference bookkeeping and persistence code.

## Authored data, implemented behaviour

The framework draws one line:

- **Designers author data.** They create Vaults, fill in configuration assets, place components in scenes and declare identifiers in text files.
- **Engineers implement behaviour.** They write subsystems and scene logic, subscribe to events, and read the authored data.

Neither does the other's job. Designers don't edit `.cs` files; engineers don't hard-code values that belong in a Vault.

## Content-addressed identity

A generated identifier's value is a hash of its name, not its position in the list. Removing an entry never changes another entry's value, and removed entries stay behind as obsolete tombstones, so existing serialised references still resolve.

That is what makes the identifier workflow safe. One domain, Iris events, uses positions instead; the manual flags this wherever it matters. See [Identifier Domains and Code Generation](../engineer/code-generation.md).
