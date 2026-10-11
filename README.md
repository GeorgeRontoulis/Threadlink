<p align="center">
  <img src="Threadforge/Threadlink_Banner.png" alt="Powered by Threadlink. Weaving worlds together." width="720">
</p>

# Threadlink Framework

**Threadlink** is an opinionated, modular runtime framework for Unity. It replaces the infrastructure every project rebuilds (manager singletons, event wiring, asset-reference bookkeeping, persistence abstraction) with one backbone, while scenes, prefabs and components stay ordinary Unity content.

- **A self-deploying core.** No scene-placed initialiser: the core deploys from runtime-initialisation hooks and installs its update loop directly into Unity's PlayerLoop.
- **Native subsystems.** Iris (events), Chronos (time), Nexus (scene residency and presentation), Dextra (input and UI), Aura (audio) and Sentinel (accounts, transactional saves, achievements and social affordances across platforms and storefronts).
- **Type-safe identifiers.** Scenes, assets, prefabs, events, input modes, spawn points, RNG domains and data fields are C# enumerations generated from plain-text declarations, with content-addressed values that survive removals.
- **The Vault.** A polymorphic, designer-authored data container.
- **Deterministic and memory toolkits.** Q32.32 fixed point, deterministic vectors and rotations, a stateless RNG, native memory with stable hashing, and inline collections for simulation state.

## Requirements

Unity **6000.6** (Threadlink 6.6 is validated against 6000.6.4f1) with Addressables, Input System, uGUI and Collections. Timeline, Localization, Odin Inspector and Steamworks.NET are optional. See [Requirements](Gitbook/manual/getting-started/requirements.md).

## Installation

Copy the [`Threadforge`](Threadforge) folder into your project as `Assets/Threadforge/`, keeping its `.meta` files, then follow [Installation and Project Setup](Gitbook/manual/getting-started/installation.md).

## Documentation

The reference manual is a GitBook space in [`Gitbook/manual`](Gitbook/manual/README.md), readable directly on GitHub:

| Start here | |
|---|---|
| [Introduction](Gitbook/manual/README.md) | What the manual covers and how it is organised. |
| [Designer Reference](Gitbook/manual/designer/README.md) | Identifiers, Vaults, audio zones, input prompts, content registration. No C#. |
| [Engineer Reference](Gitbook/manual/engineer/README.md) | Architecture, subsystem APIs, Sentinel, code generation, deterministic tooling. |
| [Reference](Gitbook/manual/reference/iris-events.md) | Native Iris events, service access, identifier domains, menus, terminology. |

An offline, single-file edition ships inside the framework as [`Threadforge/MarkdownDocumentation.md`](Threadforge/MarkdownDocumentation.md).

### Maintaining the documentation

- The pages in `Gitbook/manual` are the source of truth. Edit them in the repository, or in GitBook, whose Git Sync commits changes back.
- A new page must be listed in [`Gitbook/manual/SUMMARY.md`](Gitbook/manual/SUMMARY.md), which defines the navigation.
- After changing the manual, regenerate the single-file edition with `python Gitbook/tools/build_single_file.py`. `--check` reports whether it is out of date.
- **GitBook setup.** For site-level Git Sync, set the **Project directory** to `Gitbook`. [`Gitbook/gitbook-docs.yaml`](Gitbook/gitbook-docs.yaml) maps the `threadlink-manual` space to `Gitbook/manual`. For space-level Git Sync without a project directory, the root [`.gitbook.yaml`](.gitbook.yaml) points GitBook at the same folder. Choose GitHub → GitBook as the direction of the first sync.

## License

[MIT](LICENSE). Developed and maintained by Threadforge. Lead Developer: George Rontoulis.
