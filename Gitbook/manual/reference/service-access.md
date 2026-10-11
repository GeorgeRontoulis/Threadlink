# Service Access

| Service | Category | Access |
|---|---|---|
| `Threadlink` | Core (Weaver) | `Threadlink.TryGetSingleton(out var core)` |
| `Iris` | Static | `Iris.Publish(...)`, `Iris.Subscribe<T>(...)` |
| `Nexus` | Static | `Nexus.TransitionAsync(...)`, `Nexus.HoldAsync(...)` |
| `Initium` | Static | `Initium.BootAndInitSceneObjectsAsync(...)`, `Initium.BootAndInitAsync(...)` |
| `Scribe` | Static | `this.Send(...)`, `Scribe.Send<T>(...)` |
| `Sentinel` | Native subsystem | `Sentinel.TryGetSingleton(out var sentinel)` |
| `Chronos` | Native subsystem (static time members) | `Chronos.TimeScale`, `Chronos.DeltaTime`; `Chronos.TryGetSingleton(out var chronos)` for `Start` / `Stop` |
| `Dextra` | Native subsystem | `Dextra.TryGetSingleton(out var dextra)`; `Dextra.IsTopInterface<T>()` |
| `Aura` | Native subsystem (Linker) | `Aura.TryGetSingleton(out var aura)` |
| `StatelessRNG` | Static | `StatelessRNG.CreateStream(...)`, or a `StatelessRNG.Source` per world |
| `Vault` | Asset | A `LinkableAsset` instance |

Namespaces:

| Namespace | Contents |
|---|---|
| `Threadlink.Core` | `Threadlink`, `ThreadlinkSubsystem<T>` and the register hierarchy, `LinkableBehaviour`, `LinkableAsset` and their singletons, configs |
| `Threadlink.Core.NativeSubsystems.Iris` / `.Nexus` / `.Initium` / `.Scribe` | The static services |
| `Threadlink.Core.NativeSubsystems.Sentinel` / `.Chronos` / `.Dextra` / `.Aura` | The native subsystems and their types |
| `Threadlink.Shared` | Lifecycle contracts, `WeavingFactory`, hashing, Addressables helpers, monitoring (`IMonitorable`, `MonitorReport`, `[Monitored]`, `MonitorRegistry`) |
| `Threadlink.Generated` | `ThreadlinkIDs` |
| `Threadlink.Vault`, `Threadlink.Vault.DataFields` | The Vault and its field types |
| `Threadlink.Collections` | Serialisable maps and collections |
| `Threadlink.Deterministic` | `FP`, vectors, `FPQuaternion`, `StatelessRNG`, `Bits`, `FixedPointConversions` |
| `Threadlink.Memory` | `NativeAllocator`, `MemoryHash`, inline collections |
| `Threadlink.Animation` | `AnimatorHash`, `AnimationEventsReceiver` |
| `Threadlink.Utilities.*` | Extension libraries and attributes |
| `Threadlink.SentinelModules.Local`, `Threadlink.SentinelModules.Steam` | The shipped Sentinel modules |
| `Threadlink.User` | Project code and project-defined domains |
