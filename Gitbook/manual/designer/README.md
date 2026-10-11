# Designer Reference

> This volume covers data and identifier authoring. All operations are performed through text files, the Unity Inspector, and a small set of commands under the **Threadlink** menu. It requires no C#.

## Scope of the role

Designers produce four categories of artefact:

1. **Identifiers**: names for spawn points, input modes and Vault data fields, declared in text files and surfaced as Inspector dropdowns. See [The Identifier Workflow](identifiers.md).
2. **Vaults**: data assets holding tunable values keyed by those identifiers. See [The Vault](vault.md).
3. **Configuration values**: exposed fields on the framework's configuration assets. See [Configuration Assets](configuration-assets.md).
4. **Scene authoring**: placement of Threadlink components, including [audio zones](audio.md#audio-zones), interactables, and [input-prompt icons](input-prompts.md).

The governing principle: no name is typed twice, and nothing is referenced by raw string. An identifier is declared once and thereafter selected from a dropdown.

## In this volume

| Page | Covers |
|---|---|
| [The Identifier Workflow](identifiers.md) | Declaring, regenerating, removing and renaming identifiers. |
| [The Vault](vault.md) | Creating Vaults, field types, serialised and transient backings. |
| [Audio (Aura)](audio.md) | Aura configuration, audio zones, per-scene audio. |
| [Input Prompts](input-prompts.md) | Device-specific button glyphs. |
| [Configuration Assets](configuration-assets.md) | The designer-tunable fields of each configuration asset. |
| [Registering Scenes, Prefabs and Assets](registering-content.md) | The Addressables Mapping Window. |
| [Designer Procedures](procedures.md) | Step-by-step procedures and operating principles. |
