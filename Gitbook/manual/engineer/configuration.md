# Configuration and Project Setup

## Bootstrap scene

Every project needs a bootstrap scene, the build's entry point (see [Deployment Overview](../foundations/deployment-overview.md)):

- **Make one scene that is not Addressable**, the first and ideally only scene in Build Settings. Every other scene is Addressable and mapped (see [Registering Scenes, Prefabs and Assets](../designer/registering-content.md)).
- **Keep it minimal.** It is never unloaded, so whatever is in it lives for the whole run. Its discoverables boot at deployment. Gameplay belongs in the scenes Nexus loads.
- **Start from it in the Editor too.** Otherwise Play Mode starts from the open scene, which then stands in for the bootstrap scene: it stays loaded, and if it is an Addressable gameplay scene, Nexus loads a second copy beside it. Set `EditorSceneManager.playModeStartScene` to the bootstrap scene so Play boots as a build does (see [Installation and Project Setup](../getting-started/installation.md#create-the-bootstrap-scene)).

## Configuration assets

| Asset | Creation path | Function |
|---|---|---|
| **Native Config** | `Create ▸ Threadlink ▸ Native Config` | Maps `NativeResources` identifiers to `AssetReference`s. Must be at `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`. |
| **User Config** | `Create ▸ Threadlink ▸ User Config` | Update-loop mode; scene, asset and prefab reference maps, asset types and asset aliases; binaries folder (Editor). |
| **Editor Config** | `Create ▸ Threadlink ▸ Editor Config` | Domain declarations, shells, and the generated, injector and definition folders (see [Editor Config composition](code-generation.md#editor-config-composition)). |
| **Chronos Config** | `Create ▸ Threadlink ▸ Subsystem Dependencies ▸ Chronos Config` | Iris physics toggle. |
| **Aura Config** | `… ▸ Aura Config` | Master mixer, volume fade speed, interface SFX pointers. |
| **Dextra Config** | `… ▸ Dextra Config` | Interface fade durations, interface prefab pointers, input-mode map, input-icon map, EventSystem hide flag. |
| **Sentinel Config** | `… ▸ Sentinel Config` | The distribution (store) for platforms where Unity's build target doesn't decide it. Only one may exist. |

Other entries: `Create ▸ Threadlink ▸ Vault`, `Create ▸ Threadlink ▸ Dextra ▸ Interactable Config`, `Create ▸ Threadlink ▸ Animation ▸ Animator Hash`.

The Native Config must supply these native resources:

| Resource | Asset |
|---|---|
| `UserConfig` | The User Config. |
| `SentinelConfig` | The Sentinel Config. The Sentinel build pipeline checks it is the project's one Sentinel Config. |
| `ChronosConfig` | The Chronos Config. |
| `DextraConfig` | The Dextra Config. |
| `DextraComponentsPrefab` | A prefab with an `EventSystem` and an `InputSystemUIInputModule`. |
| `AuraConfig` | The Aura Config. |
| `AuraComponentsPrefab` | A prefab with three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`. |

Only the [Addressables Mapping Window](code-generation.md#addressables-mapping-window) writes the User Config's reference maps (scenes, assets, asset types, asset aliases, prefabs); they are read-only in the Inspector. The framework ships these assets configured; see [Installation and Project Setup](../getting-started/installation.md#shipped-configuration).

## Addressable registration of native assets

**`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`** makes the Native Config and every native asset it references Addressable in the `Threadlink Assets` group (or the default group, with a warning, if that group is missing), addressed by asset path.

**`Threadlink ▸ Addressables ▸ Match Addressables to Paths`** resets every address in the writable groups to its asset path.

## Update loop modes

Set on the User Config:

- **Native**: Threadlink inserts `ThreadlinkPlayerLoop` systems into Unity's current PlayerLoop, which publish `OnUpdate`, `OnFixedUpdate` and `OnLateUpdate`.
- **Custom**: Threadlink installs nothing. The project publishes those events from its own driver, usually set up on `OnCoreDeployed`.

Use Custom when another framework owns the simulation and Threadlink only runs the view, for example with Photon Quantum running a deterministic multiplayer simulation.

## Scripting defines

| Define | Activation | Enables |
|---|---|---|
| `THREADLINK_TIMELINE` | `com.unity.timeline` ≥ 1.8.10 (version define on `Threadlink.Runtime` and `Threadlink.Editor`) | Vault Timeline integration |
| `THREADLINK_LOCALIZATION` | `com.unity.localization` ≥ 1.5.9 (version define on `Threadlink.Runtime`) | `LocalizedText` Vault field, localised interaction prompts, localisation utilities |
| `ODIN_INSPECTOR` | Odin installation | Odin-drawn hash maps, Vault fields and inspectors |
| `STEAMWORKS_NET` | Defined by the project (Player Settings) | Compiles the Steam Sentinel module (`Threadlink.Sentinel.Steam`) |
| `THREADLINK_STEAMWORKS_NET_165` | `com.rlabrecque.steamworks.net` ≥ 2025.165.0 (version define on `Threadlink.Sentinel.Steam`) | Reserved for version-specific code; no code consumes it yet |

## Binary authoring

Types implementing `IBinaryAuthor` write authoring data to `.bytes` files in the project, from an Inspector button or context menu that calls `SerializeAuthoringDataIntoBinary()`. `ICollection<T>.SerializeIntoBinary(binariesFolder, filename)` (Editor, `Threadlink.Utilities.Collections`) writes a collection with MessagePack into a project folder, usually the User Config's binaries folder (`TryGetBinariesFolder`). The files are then loaded through Addressables and read through `IAsyncBinaryConsumer`.

**`Threadlink ▸ Clear all Binaries`** empties every `.bytes` file in a chosen project folder, which is useful while iterating on a format.
