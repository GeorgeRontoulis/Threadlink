# Engineer Reference

This volume documents every package's API and the contracts behind it.

| Page | Covers |
|---|---|
| [Architecture](architecture.md) | The core, subsystems, the session, design and engineering, asynchronous work and failure reporting. |
| [Lifetimes](lifecycle.md) | Subscriptions and content bound to the game object that needs them. |
| [Iris](iris.md) | Synchronous and awaited events, requests, frame callbacks and externally driven frames. |
| [Framework Events](events.md) | What the framework announces on Iris, and when to subscribe. |
| [Chronos](chronos.md) | Pause claims, the time scale and playtime. |
| [Nexus](nexus.md) | Scene transitions, holds and transition steps. |
| [Dextra](dextra.md) | Players and input, rebinding, prompts and glyphs, screens and Back, interaction. |
| [Aura](aura.md) | Audio cues, voices, categories, fades, music, scene music and audio zones. |
| [Sentinel](sentinel/README.md) | Accounts, capabilities, providers, saving, cloud sync and invitations. |
| [Vault Runtime API](vault.md) | Typed definitions, generated mapping, variants, field sets and preparation. |
| [Serialization](serialization.md) | Snapshot envelopes, the MessagePack codec and versions. |
| [Resource Loading](resource-loading.md) | Typed content requests, the Content mapping, labels and finding assets by type. |
| [Deterministic Toolkit](deterministic/README.md) | Fixed point and StatelessRNG. |
| [Memory](memory.md) | Native allocation, hashing and inline collections. |
| [Diagnostics](diagnostics.md) | Subsystem reports and the operation log. |
| [Editor Tooling](editor-tooling.md) | The Workspace, project setup, the Scenes toolbar, the Runtime monitor, generated IDs, engineering settings, inspector helpers, validators and development tools. |
| [Performance](performance.md) | Measured costs and allocation guidance. |
| [Engineering Procedures](procedures.md) | Common tasks step by step. |
