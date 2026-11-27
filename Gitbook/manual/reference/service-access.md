# Subsystem Access

Every subsystem is reached the same way, from any script, from Awake onwards.

| Need | Call |
| --- | --- |
| Use a subsystem's API | Its static methods: `Iris.Publish(…)`, `Chronos.Pause()`, `Aura.Play(…)`, `Wallet.Add(…)` |
| The instance | `Wallet.Instance` (throws `SubsystemUnavailableException` with the reason when it is not part of the session or failed) |
| Check existence and linkage to the core | `Wallet.TryGetSingleton(out var wallet)`; it never starts a subsystem |
| The core | `Threadlink.TryGetSingleton(out var core)`, `Threadlink.Instance`, `Threadlink.IsDeployed` |
| Another subsystem from a subsystem | `T.Instance` in `OnSubsystemInit`, in any order (recorded for the Runtime monitor). Only Iris is available in `OnSubsystemBoot`. |
| Everything in the session | `Threadlink.Instance.Subsystems` |
| Work bound to the session | `Threadlink.Lifetime` (cancelled at shutdown) |

`Instance` and `TryGetSingleton` fail outside Play mode and players unless a core was deployed explicitly with `Threadlink.DeployIsolated()`. A subsystem that failed to boot or initialize stays visible in the Runtime monitor with its error, `Instance` reports that error, and `SubsystemFailed` announces it on Iris.

## Registration

Subsystems register through generated code at `SubsystemRegistration`; no assemblies are scanned at runtime. A subsystem the generator cannot register (private, generic, or without a public parameterless constructor) produces warning TL0001. `[ManualRegistration]` keeps a subsystem out of the session; register it yourself with `Threadlink.Register<T>()` or add it to a core with `core.Add<T>()`. A subsystem registered after deployment joins the running session immediately.

## Domain reload and code reload

Sessions do not rely on domain reload: the session shuts down when the Editor returns to Edit mode and before code reloads, registration repeats at every `SubsystemRegistration`, and statics owned by the core are reset with it. Projects can enter Play mode with domain reload disabled.
