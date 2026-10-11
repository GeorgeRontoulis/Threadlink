# Designer Reference

> This volume covers authoring data and identifiers. Everything is done through text files, the Inspector and a few commands under the **Threadlink** menu. No C# is needed.

## Scope of the role

Designers produce four things:

1. **Identifiers**: names for spawn points, input modes and Vault fields, declared in text files and picked from Inspector dropdowns. See [The Identifier Workflow](identifiers.md).
2. **Vaults**: data assets of tunable values, keyed by those identifiers. See [The Vault](vault.md).
3. **Configuration values**: the exposed fields of the framework's configuration assets. See [Configuration Assets](configuration-assets.md).
4. **Scene content**: Threadlink components placed in scenes, such as [audio zones](audio.md#audio-zones), interactables and [input-prompt icons](input-prompts.md).

The rule: declare a name once, then pick it from a dropdown. Nothing is referenced by a typed string.

## In this volume

| Page | Covers |
|---|---|
| [The Identifier Workflow](identifiers.md) | Declaring, regenerating, removing and renaming identifiers. |
| [The Vault](vault.md) | Creating and browsing Vaults, field types, serialised and transient backings. |
| [Audio (Aura)](audio.md) | Aura configuration, audio zones, per-scene audio. |
| [Input Prompts](input-prompts.md) | Device-specific button glyphs. |
| [Configuration Assets](configuration-assets.md) | The designer-tunable fields of each configuration asset. |
| [Registering Scenes, Prefabs and Assets](registering-content.md) | The Addressables Mapping Window. |
| [Designer Procedures](procedures.md) | Step-by-step procedures and working rules. |
