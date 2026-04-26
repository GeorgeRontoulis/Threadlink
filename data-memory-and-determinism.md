# Data, Memory, and Determinism

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
