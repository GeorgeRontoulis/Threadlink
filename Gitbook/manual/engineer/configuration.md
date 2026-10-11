# Configuration and Project Setup

## Bootstrap scene

Every project needs a bootstrap scene, the build's entry point (see [Deployment Overview](../foundations/deployment-overview.md)):

- **Create one scene that is not Addressable**, and make it the first, and ideally the only, scene in Build Settings. Every other scene is Addressable and mapped (see [Registering Scenes, Prefabs and Assets](../designer/registering-content.md)).
- **Keep it minimal.** It stays loaded for the whole run, so anything placed in it lives for the whole run. Its discoverables boot at deployment, and nothing ever unloads it. Gameplay content belongs in the scenes Nexus loads.
- **Start from it in the Editor too.** Play Mode starts from whichever scene is open, and that scene then takes the bootstrap scene's place: it stays loaded, and if it is an Addressable gameplay scene, Nexus loads a second copy of it beside the first. Set `EditorSceneManager.playModeStartScene` to the bootstrap scene, so Play boots as a build does (see [Installation and Project Setup](../getting-started/installation.md#create-the-bootstrap-scene)).

## Configuration assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`. Must reside at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset and prefab reference maps, asset types and asset aliases; binaries folder (Editor). |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector and definition directories (see [Editor Config composition](code-generation.md#editor-config-composition)). |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Master mixer, volume fade speed, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface fade durations, interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | Distribution choice for platforms where Unity's target does not uniquely identify the storefront/ecosystem. Exactly one may exist. |

Additional creation paths: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply the following native resources:

| Resource | Asset |
|---|---|
| `UserConfig` | The User Config. |
| `SentinelConfig` | The Sentinel Config. The Sentinel build pipeline verifies that this references the project's one Sentinel Config. |
| `ChronosConfig` | The Chronos Config. |
| `DextraConfig` | The Dextra Config. |
| `DextraComponentsPrefab` | A prefab with an `EventSystem` and an `InputSystemUIInputModule`. |
| `AuraConfig` | The Aura Config. |
| `AuraComponentsPrefab` | A prefab with three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`. |

The reference maps on the User Config (scenes, assets, asset types, asset aliases, prefabs) are read-only in the Inspector and are owned by the [Addressables Mapping Window](code-generation.md#addressables-mapping-window). The framework ships a configured set of these assets; see [Installation and Project Setup](../getting-started/installation.md#shipped-configuration).

## Addressable registration of native assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** reads the Native Config and marks every referenced native asset, together with the Native Config itself, as Addressable within the `Threadlink Assets` group (the default group, with a warning, if that group does not exist), assigning each asset's path as its address.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** realigns addresses that have diverged from their asset paths, across every writable group.

## Update loop modes

Configured on the User Config:

- **Native**: Threadlink injects `ThreadlinkPlayerLoop` marker systems into Unity's current PlayerLoop and publishes `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate` directly.
- **Custom**: Threadlink installs no update callbacks. The project publishes those events from its own driver, commonly configured in response to `OnCoreDeployed`.

Custom mode applies where Threadlink renders the view for a simulation owned by another framework, for example Photon Quantum owning a deterministic multiplayer simulation while Threadlink manages the view.

## Scripting defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline` ≥ 1.8.10 (version define on `Threadlink.Runtime` and `Threadlink.Editor`) | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization` ≥ 1.5.9 (version define on `Threadlink.Runtime`) | `LocalizedText` Vault field, localised interaction prompts, localisation utilities |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps, Vault fields and inspectors |
| `STEAMWORKS_NET` | Defined by the project (Player Settings) | Compiles the Steam Sentinel module (`Threadlink.Sentinel.Steam`) |
| `THREADLINK_STEAMWORKS_NET_165` | `com.rlabrecque.steamworks.net` ≥ 2025.165.0 (version define on `Threadlink.Sentinel.Steam`) | Reserved for version-specific code; no code consumes it yet |

## Binary authoring

Types implementing `IBinaryAuthor` serialise authoring data to `.bytes` files within the project, from an Inspector button or context menu that calls `SerializeAuthoringDataIntoBinary()`. `ICollection<T>.SerializeIntoBinary(binariesFolder, filename)` (Editor, `Threadlink.Utilities.Collections`) writes a runtime collection with MessagePack into a project folder, conventionally the User Config's binaries folder (`TryGetBinariesFolder`). The files are then loaded through Addressables and consumed via `IAsyncBinaryConsumer`.

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file within a selected in-project directory during format iteration.
