# Package Layout

Threadlink is nine packages. Every package is named `com.threadforge.threadlink[.feature]`; every public type is in the `Threadforge.Threadlink` namespace, so one `using Threadforge.Threadlink;` reaches the whole framework. Packages follow the Unity package layout: `Runtime/`, optional `Editor/` and integration folders, `Tests/`, `Samples~/`, a changelog and third-party notices.

## Packages

| Package | Responsibilities |
|---|---|
| `com.threadforge.threadlink` | The core: subsystems and the session, Iris events, content loading, Vault definitions, engineering settings, diagnostics, memory toolkits, the source generator, and the Editor workspace with the Hub and Runtime monitor. |
| `.nexus` | Scene transitions. |
| `.aura` | Audio; scene music, with a Nexus integration. |
| `.dextra` | Players and input, screens, interaction; screen sounds (with Aura) and uGUI screens (with uGUI). |
| `.sentinel` | Accounts, platform capabilities, saves. |
| `.sentinel.steam` | Steam. |
| `.deterministic` | Fixed point and StatelessRNG. |
| `.starter`, `.showcase` | Samples: a small complete game and a gallery of demos. |

All Threadlink packages share one version. MessagePack comes as `com.threadforge.dependencies.messagepack`.

## Dependency rules

- Feature packages depend on the core; the core depends on no feature package.
- Integrations between features (Aura with Nexus, Dextra with Aura, Dextra with uGUI) are assemblies inside the package that compile only when the other package is installed.
- Runtime packages never reference project code. Generated project code (content IDs, subsystem registration) references packages, never the reverse.
- Diagnostics and the Workspace observe; removing them never changes behavior.

## Design and engineering in the project

| Folder | Contents |
| --- | --- |
| `Threadlink User/Engineering/Codebase` | Project subsystems and code |
| `Threadlink User/Engineering/Configs` | Engineering configuration: settings assets (edited in Project Settings ▸ Threadlink), Addressables, input, Localization settings and render pipelines |
| `Threadlink User/Design/Configs` | Designer configuration: locales and string tables |
| `Threadlink User/Design` | Designer data: cues, definitions (new ones from the Vault page go to `Design/Definitions`), documents, ID domains |

The configuration folders are defaults: choose others, per asset kind too, in **Threadlink ▸ Workspace ▸ Configuration** ([Configuration assets and folders](../engineer/editor-tooling.md#configuration-assets-and-folders)).

The Hub's **Set Up Project** creates these folders and two assembly definitions: `Threadlink.User` for `Engineering/Codebase` and `Threadlink.User.Generated` for its `Generated` folder ([Set up the project](../getting-started/installation.md#set-up-the-project)).

Generated outputs, such as the content ID classes with their `.ids.json` ledgers, are written to `Threadlink User/Engineering/Codebase/Generated` ([Content mapping](../engineer/resource-loading.md#content-mapping)). Preserve `.meta` files and identity ledgers with the assets they describe.
