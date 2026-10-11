# Chronos: Time

Chronos's time members are static; `Start()` and `Stop()` are instance methods.

| Member | Semantics |
|---|---|
| `TimeScale` | Accepts only 0 or 1; other values, and the current value, are ignored. Setting it publishes `OnGamePaused` or `OnGameResumed`. |
| `RawTimeScale` | Same restriction; publishes nothing. |
| `DeltaTime`, `SmoothDeltaTime`, `UnscaledDeltaTime` | Cached once per `OnUpdate`. |
| `FixedDeltaTime` | Cached once per `OnFixedUpdate`. |
| `CurrentFramerate` | `1 / DeltaTime`. |
| `CurrentTimeSinceDeployment` | Unity's `Time.time`, cached each `OnUpdate`. |
| `TotalPlaytime`, `CountTotalPlaytime`, `PlaytimeCountingMode`, `ClearTotalPlaytime()` | `PlaytimeCountMode` is `Scaled` (the default) or `Unscaled`. |
| `Start()`, `Stop()` | Subscribe and unsubscribe the tick handlers. |

`Boot()` resets `TotalPlaytime` to zero, turns on `CountTotalPlaytime` and starts the tick handlers. While `CountTotalPlaytime` is on, each frame publishes `OnPlaytimeCountTick` with the running total.

Chronos updates its values from the Iris update events. In `Custom` update-loop mode, they are only as current as the project's own `OnUpdate` and `OnFixedUpdate` publications.

## Manual physics simulation

With `ChronosConfig.IrisPhysicsUpdate` on, `Chronos.Boot()` sets `Physics.simulationMode = SimulationMode.Script` and calls `Physics.Simulate(Time.fixedDeltaTime)` on each `OnFixedUpdate`.

Leave it off if another framework owns the simulation. The simulation mode is global: if other code also steps physics in script mode, physics steps twice. Nothing guards against this.
