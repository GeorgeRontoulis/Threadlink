# Chronos: Time

Chronos's time members are static; `Start()` and `Stop()` belong to the instance.

| Member | Semantics |
|---|---|
| `TimeScale` | Accepts only 0 or 1; other values, and the current value, are ignored. Assignment publishes `OnGamePaused` or `OnGameResumed`. |
| `RawTimeScale` | Identical restriction; publishes nothing. |
| `DeltaTime`, `SmoothDeltaTime`, `UnscaledDeltaTime` | Cached once per `OnUpdate`. |
| `FixedDeltaTime` | Cached once per `OnFixedUpdate`. |
| `CurrentFramerate` | `1 / DeltaTime`. |
| `CurrentTimeSinceDeployment` | Unity's `Time.time`, cached each `OnUpdate`. |
| `TotalPlaytime`, `CountTotalPlaytime`, `PlaytimeCountingMode`, `ClearTotalPlaytime()` | `PlaytimeCountMode` enumerates `Scaled` (the default) and `Unscaled`. |
| `Start()`, `Stop()` | Subscribe and unsubscribe the internal tick handlers. |

`Boot()` resets `TotalPlaytime` to zero, enables `CountTotalPlaytime`, and starts the tick handlers. Playtime accumulation publishes `OnPlaytimeCountTick` with the running total each frame while `CountTotalPlaytime` is set.

Chronos drives its caches from the Iris update events, so in `Custom` update-loop mode its values are only as current as the project's own publication of `OnUpdate` and `OnFixedUpdate`.

## Manual physics simulation

When `ChronosConfig.IrisPhysicsUpdate` is enabled, `Chronos.Boot()` assigns `Physics.simulationMode = SimulationMode.Script` and steps `Physics.Simulate(Time.fixedDeltaTime)` on each `OnFixedUpdate`.

The setting must remain disabled where another framework owns the simulation. The simulation mode is global state: any additional code assigning `SimulationMode.Script` and stepping independently produces double stepping. No runtime guard enforces exclusivity.
