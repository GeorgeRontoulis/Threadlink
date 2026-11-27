# Terminology

| Term | Definition |
|---|---|
| **Package** | One installable `com.threadforge.threadlink.*` unit with its declared dependencies. All Threadlink packages share one version. |
| **Integration** | An assembly inside a package that connects it to another package, such as Aura's scene music for Nexus, and compiles only when that package is installed. |
| **Core** | The `Threadlink` runtime that owns the session's subsystems (see [Architecture](../engineer/architecture.md)). |
| **Subsystem** | A `ThreadlinkSubsystem<T>` that lives for the whole session: registered automatically, started before the first scene, reached through `Instance` or `TryGetSingleton` (see [Subsystem Access](service-access.md)). |
| **Boot** | Startup phase one: each subsystem sets up only itself and subscribes to Iris (see [Architecture](../engineer/architecture.md#two-phases-instead-of-a-dependency-order)). |
| **Initialization** | Startup phase two, after every subsystem has booted: subsystems connect to each other, in any order. |
| **Session** | One deployment of the core, from before the first scene until Play mode ends, code reloads or the player quits. |
| **Engineering settings** | A `ThreadlinkSettings<T>` asset edited only in Project Settings ▸ Threadlink. |
| **Designer data** | Assets and scene components designers edit: cues, definitions, scene music, screen documents, tuning values. |
| **Event** | A typed payload delivered by Iris to its subscribers, synchronously or awaited (see [Iris](../engineer/iris.md)). |
| **Framework event** | An event the framework announces when its state changes, such as `PlatformReady` or `DeviceLost` (see [Framework Events](../engineer/events.md)). |
| **Request** | A `ResourceRequest<T>`: one acquisition of content, released by disposing it (see [Resource Loading](../engineer/resource-loading.md)). |
| **Content mapping** | Which Addressables entries game code loads by ID, generated as three read-only dictionaries; Addressables owns everything else. |
| **Identity** | A permanent GUID for content or a definition, independent of names and paths. |
| **ID domain** | A project-defined list of IDs that generates an enumeration or extends a native domain (see [ID domains](../engineer/editor-tooling.md#id-domains)). |
| **Generated ID** | A typed, stable reference generated from a project source, such as `GameScenes.Forest` or `GameInput.Gameplay.Jump` (see [Generated IDs](../engineer/editor-tooling.md#generated-ids)). |
| **Tombstone** | A removed identifier kept as an `[Obsolete]` member so existing data and code still resolve (see [Identifiers and Tombstones](../designer/identifiers.md)). |
| **Claim** | A held right: a scene hold, an input map claim, a screen's input block or a Chronos pause, released by disposing it. |
| **Transition** | A Nexus operation that prepares a scene before presenting it and keeps the previous scene if preparation fails. |
| **Definition** | An authored asset prepared into an immutable runtime value (see [Vault: Typed Definitions](../designer/vault.md)). |
| **Variant** | A definition that inherits from a parent and overrides selected values. |
| **Field set** | A definition made of named, typed values keyed by generated `VaultFields` IDs, authored without code (see [Field sets](../designer/vault.md#field-sets)). |
| **Glyph** | The image a prompt shows for a control on a family of devices, from an Input Glyphs asset (see [Input Prompts and Rebinding](../designer/input-prompts.md#glyphs)). |
| **Audio zone** | A place that lowers audio categories as the listener approaches (see [Audio Cues](../designer/audio.md#audio-zones)). |
| **Playtime** | Real time played while the game is not paused, counted by Chronos (see [Chronos](../engineer/chronos.md#playtime)). |
| **Save type** | A `[Save("name", version)]` type: a versioned identity for saved data, with explicit migration readers (see [Transactional Saving](../engineer/sentinel/saving.md)). |
| **Provider** | A Sentinel implementation of platform capabilities, such as `LocalProvider` or `SteamProvider`. |
| **Capability** | A typed platform contract, such as accounts, achievements or cloud storage, reported as supported and available separately. |
| **Join intent** | A captured request to join another player's session, held until the game is ready. |
| **FP** | Deterministic Q32.32 fixed-point number type (see [Fixed Point](../engineer/deterministic/fixed-point.md)). |
| **Stream** | A sequence of random numbers that is a pure function of a seed, a domain and identity parts (see [StatelessRNG](../engineer/deterministic/stateless-rng.md)). |
| **Inline collection** | A collection laid out inside another value's memory (see [Memory](../engineer/memory.md)). |
