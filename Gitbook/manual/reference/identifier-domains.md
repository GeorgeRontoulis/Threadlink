# Identifier Domains

| Enumeration | Kind | Reserved | Native entries | Injector | Ownership |
|---|---|---|---|---|---|
| `ThreadlinkIDs.Iris.Events` | Ordinal | — | `Iris.Events.Native.txt` | `Iris.Events.User.txt` | Engineering |
| `ThreadlinkIDs.StatelessRNG.Domains` | Identity | `None = 0` | — | `StatelessRNG.Domains.User.txt` | Engineering |
| `ThreadlinkIDs.Dextra.InputModes` | Identity | `Unresponsive = 0` | — | `Dextra.InputModes.User.txt` | Design |
| `ThreadlinkIDs.Nexus.SpawnPoints` | Identity | `None = 0` | — | `Nexus.SpawnPoints.User.txt` | Design |
| `ThreadlinkIDs.Vault.Fields` | Identity | `None = 0` | — | `Vault.Fields.User.txt` | Design |
| `ThreadlinkIDs.Addressables.Scenes` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Assets` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.Prefabs` | Identity | `None = 0` | — | Mapping Window | Shared |
| `ThreadlinkIDs.Addressables.NativeResources` | Identity | `None = 0` | `Addressables.NativeResources.Native.txt` | — | Framework |

All reside in namespace `Threadlink.Generated`, assembly `Threadlink.Generated`, under `Threadlink/Generated/`, with their manifests in `Threadlink/Generated/Manifests/`.

Project-defined domains (one `.txt` per domain in `Threadlink User/Design/Custom Domain Definitions/`) are Identity domains with `None = 0` reserved, emitted into namespace `Threadlink.User`, assembly `Threadlink.User.Generated`, under `Threadlink User/Engineering/Codebase/Generated/`.

The native resources are `UserConfig`, `SentinelConfig`, `DextraConfig`, `DextraComponentsPrefab`, `AuraConfig`, `AuraComponentsPrefab` and `ChronosConfig`.
