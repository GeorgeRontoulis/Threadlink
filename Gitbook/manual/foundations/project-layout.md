# Project Layout

Threadlink occupies one folder, `Assets/Threadforge/`, divided into two territories.

```
Threadforge/
├── Threadlink/                        ← Framework. Not user-editable. Updated as a unit.
│   ├── Core/
│   │   ├── Native Subsystems/         ← Aura, Chronos, Dextra, Iris, Nexus, Sentinel, Initium
│   │   │   └── Sentinel/
│   │   │       ├── Contracts/         ← Multiplayer and social service contracts
│   │   │       └── Modules/           ← The Local and Steam platform modules
│   │   └── Objects/                   ← LinkableBehaviour, LinkableAsset, singletons, native weaving factory
│   ├── Shared/                        ← Contracts, Scribe, hashing, Addressables helpers, WeavingFactory
│   ├── Collections/                   ← Serialisable hash maps, arrays and lists
│   ├── Utilities/                     ← Extension-method libraries, FixedPointConversions
│   ├── Vault/                         ← Data container and Timeline integration
│   ├── Deterministic/                 ← Fixed point (FP, its vectors and rotations) and StatelessRNG
│   ├── Memory/                        ← Native allocation, raw-memory hashing, inline collections
│   ├── Editor/                        ← Domain code generation, Addressables tooling, Sentinel build pipeline,
│   │                                    formatter, inspectors
│   ├── Generated/                     ← Generated output: ThreadlinkIDs enumerations and manifests
│   ├── Plugins/                       ← UniTask, ZString, MessagePack, SerializeReference UI
│   ├── Constants.Native.cs
│   ├── ThreadlinkConfig.Native.asset
│   └── ThreadlinkConfig.Editor.asset
│
└── Threadlink User/                   ← Project territory.
    ├── Design/
    │   └── Custom Domain Definitions/ ← Project-defined identifier domains
    ├── Generated Addressables Injectors/ ← Mapping Window output; not hand-edited
    ├── Native Domain Injectors/       ← Plain-text identifier declarations
    │   ├── Dextra.InputModes.User.txt
    │   ├── Iris.Events.User.txt
    │   ├── Nexus.SpawnPoints.User.txt
    │   ├── StatelessRNG.Domains.User.txt
    │   └── Vault.Fields.User.txt
    └── Engineering/
        ├── Codebase/                  ← Project code
        │   ├── Generated/             ← Generated output: project-defined domain enumerations
        │   ├── Constants.User.cs
        │   ├── Subsystems.User.cs
        │   ├── WeavingFactory.User.cs
        │   └── Threadlink.User.asmdef
        └── Configs/                   ← User, Chronos and Sentinel configs; Aura/ and Dextra/ configs,
                                         prefabs, mixer and input actions
```

The `Threadlink/` directory constitutes the framework and is not hand-edited, `Threadlink/Generated/` included: that directory is owned exclusively by the code generator. All project authoring occurs under `Threadlink User/`. Binary data authored through `IBinaryAuthor` lives in a folder of the project's choosing, assigned on the User Config (see [Binary Authoring](../engineer/configuration.md#binary-authoring)).

## Assembly topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | No references; `noEngineReferences`. Referenceable from any assembly, including a deterministic simulation assembly. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers, `WeavingFactory`, `AnimatorHash` | — |
| `Threadlink.Deterministic` | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, `StatelessRNG`, `Bits` | `noEngineReferences`; `allowUnsafeCode`. References `Threadlink.Shared` and `Threadlink.Generated`. |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections | `allowUnsafeCode`. No assembly references; no other Threadlink assembly references it. |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities (including `FixedPointConversions`, between Unity's float types and the deterministic ones) | `allowUnsafeCode`. Defines `THREADLINK_TIMELINE` and `THREADLINK_LOCALIZATION` from package versions. |
| `Threadlink.Sentinel.Multiplayer.Contracts` | `IMultiplayerService` and the social/session types | `noEngineReferences` |
| `Threadlink.Sentinel.Local` | The Local Sentinel module | — |
| `Threadlink.Sentinel.Steam` | The Steam Sentinel module | Compiled only with the `STEAMWORKS_NET` define, on standalone platforms and in the Editor. |
| `Threadlink.Sentinel.Steam.Shared` | `SteamSentinelSettings`, module identity | No references; `noEngineReferences` |
| `Threadlink.Sentinel.Local.Editor`, `Threadlink.Sentinel.Steam.Editor` | Sentinel build modules and the Steam App ID build processor | Editor-only |
| `Threadlink.Editor` | Code generation, editor tooling, the Sentinel build pipeline | Editor-only |
| `Threadlink.User` | Project code | `allowUnsafeCode`. References `Threadlink.Runtime`, `Threadlink.Shared`, `Threadlink.Deterministic`, `Threadlink.Generated`, `Threadlink.User.Generated` and UniTask. |
| `Threadlink.User.Generated` | Project-defined domain enumerations | No references; `noEngineReferences` |

Generated identifiers reside in namespace `Threadlink.Generated`; consuming code requires `using Threadlink.Generated;`. Project-defined domains are emitted into namespace `Threadlink.User`. Isolating generated output in dedicated assemblies guarantees that neither project authoring nor third-party module installation produces a write within framework source.

`Threadlink.User` does not reference `Threadlink.Memory`, the Sentinel contracts or the Sentinel modules. Project code that uses them adds the reference to `Threadlink.User.asmdef`, or to its own assembly definition.
