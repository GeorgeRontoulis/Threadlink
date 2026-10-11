# Project Layout

Threadlink lives in one folder, `Assets/Threadforge/`, split in two: the framework and the project's own area.

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
│   ├── Editor/                        ← Code generation, Addressables tooling, Sentinel build pipeline,
│   │                                    Vault Browser, Runtime Monitor, formatter, inspectors
│   ├── Generated/                     ← Generated output: ThreadlinkIDs enumerations and manifests
│   ├── Plugins/                       ← UniTask, ZString, MessagePack, SerializeReference UI
│   ├── Constants.Native.cs
│   ├── ThreadlinkConfig.Native.asset
│   └── ThreadlinkConfig.Editor.asset
│
└── Threadlink User/                   ← The project's area.
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

Don't edit `Threadlink/` by hand. That includes `Threadlink/Generated/`, which only the code generator writes. Project work goes under `Threadlink User/`. Binary data authored through `IBinaryAuthor` goes in a folder the project chooses on the User Config (see [Binary Authoring](../engineer/configuration.md#binary-authoring)).

## Assembly topology

| Assembly | Contents | Constraints |
|---|---|---|
| `Threadlink.Generated` | `ThreadlinkIDs` enumerations | No references; `noEngineReferences`. Any assembly can reference it, including a deterministic simulation. |
| `Threadlink.Shared` | Contracts, Scribe, hashing, Addressables helpers, `WeavingFactory`, `AnimatorHash` | — |
| `Threadlink.Deterministic` | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, `StatelessRNG`, `Bits` | `noEngineReferences`; `allowUnsafeCode`. References `Threadlink.Shared` and `Threadlink.Generated`. |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections | `allowUnsafeCode`. No assembly references; no other Threadlink assembly references it. |
| `Threadlink.Runtime` | Core, native subsystems, Vault, collections, utilities (including `FixedPointConversions`, between Unity's float types and the deterministic ones) | `allowUnsafeCode`. Defines `THREADLINK_TIMELINE` and `THREADLINK_LOCALIZATION` from package versions. |
| `Threadlink.Sentinel.Multiplayer.Contracts` | `IMultiplayerService` and the social/session types | `noEngineReferences` |
| `Threadlink.Sentinel.Local` | The Local Sentinel module | — |
| `Threadlink.Sentinel.Steam` | The Steam Sentinel module | Compiled only with the `STEAMWORKS_NET` define, on standalone platforms and in the Editor. |
| `Threadlink.Sentinel.Steam.Shared` | `SteamSentinelSettings`, module identity | No references; `noEngineReferences` |
| `Threadlink.Sentinel.Local.Editor`, `Threadlink.Sentinel.Steam.Editor` | Sentinel build modules and the Steam App ID build processor | Editor-only |
| `Threadlink.Editor` | Code generation, editor tools, the Sentinel build pipeline | Editor-only. Also references UniTask, Input System and uGUI. |
| `Threadlink.User` | Project code | `allowUnsafeCode`. References `Threadlink.Runtime`, `Threadlink.Shared`, `Threadlink.Deterministic`, `Threadlink.Generated`, `Threadlink.User.Generated` and UniTask. |
| `Threadlink.User.Generated` | Project-defined domain enumerations | No references; `noEngineReferences` |

Generated identifiers are in the `Threadlink.Generated` namespace, so code that uses them needs `using Threadlink.Generated;`. Project-defined domains go in the `Threadlink.User` namespace. Because generated output has its own assemblies, neither project work nor installing a module ever writes into framework source.

`Threadlink.User` does not reference `Threadlink.Memory`, the Sentinel contracts or the Sentinel modules. To use them, add the reference to `Threadlink.User.asmdef` or to your own assembly definition.
