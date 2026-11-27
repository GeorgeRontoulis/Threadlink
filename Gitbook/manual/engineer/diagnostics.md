# Diagnostics

Threadlink observes itself without changing behaviour: every subsystem reports its state, and the session keeps an operation log. Both appear live in the [Runtime monitor](editor-tooling.md#runtime-monitor).

## Subsystem reports

Override `Report(MonitorReport report)` on a subsystem. Always set a summary; add sections, fields, meters, references and notes only while `report.Detailed` is true, because summaries are requested many times a second.

```csharp
public override void Report(MonitorReport report)
{
    report.Summary(enemies.Count + " enemies", alert: enemies.Count > limit);
    if (!report.Detailed)
        return;
    report.Section("Spawning");
    report.Meter("Budget", enemies.Count / (float)limit, enemies.Count + " / " + limit, enemies.Count > limit ? MonitorTone.Bad : MonitorTone.Good);
    report.Reference("Spawn table", table);
}
```

Tones are `Normal`, `Muted`, `Good`, `Caution` and `Bad`. Reporting allocates only while the monitor asks for it.

## Operations

```csharp
var save = Threadlink.Begin("Saves", "Autosave");
save.Stage("Writing slot 0");
try
{
    var result = await Sentinel.Saves.SaveAsync(0, game);
    if (result.Succeeded) save.Succeed();
    else save.Unavailable(result.Error.ToString(), RecoveryActions.Retry);
}
catch (Exception error)
{
    save.Fail(error);
    throw;
}
```

An `OperationScope` records stages, waits, traces and exactly one outcome: `Succeeded`, `Cancelled`, `Failed`, `Unsupported`, `Unavailable`, or `Abandoned` when disposed without one. `RecoveryActions` names the options a game policy may offer (retry, reconcile, restore previous, select version, reauthenticate, reconfigure, discard); diagnostics never performs them. A failure that may already have committed never offers retry.

`Threadlink.Operations` is the session's `OperationLog`: counts per outcome, running operations, bounded history (128 completed operations) and opt-in traces. `DiagnosticsOptions.Development()`, `Shipping()` (16) and `Disabled()` configure separate logs created for tools.
