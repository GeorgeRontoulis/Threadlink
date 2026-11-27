# Chronos: Time

Chronos is the session's game time, in the core package (`com.threadforge.threadlink`): pausing, the time scale and playtime.

## Pausing

Pausing is a claim. The game is paused while any claim is held, so a pause menu, a disconnected gamepad and a cutscene each pause and resume without undoing each other:

```csharp
private IDisposable pause;

private void OnDeviceLost(DeviceLost lost) => pause ??= Chronos.Pause();

private void OnDeviceRegained(DeviceRegained regained)
{
    pause?.Dispose();
    pause = null;
}
```

| API | Behavior |
| --- | --- |
| `Chronos.Pause()` | Pauses until the returned claim is disposed. Disposing a claim twice releases it once. |
| `Chronos.IsPaused`, `PauseClaims` | Whether any claim is held, and how many. |
| `Chronos.TimeScale` | The game's time scale while it is not paused, such as 0.5 for slow motion. Set the time scale here, not on `Time.timeScale`. |

While paused, `Time.timeScale` is 0; otherwise it is `Chronos.TimeScale`. Unscaled time keeps running, so UI, Aura fades and screen transitions work in a pause menu. `PauseChanged` is announced on Iris when the first claim is taken and when the last is released. When the session ends, the time scale returns to what it was before the session.

To pause whenever a screen blocks gameplay, such as a pause menu, turn on **Pause While Blocked** in **Project Settings ▸ Threadlink ▸ Dextra** ([Dextra](dextra.md#screens)).

## Playtime

`Chronos.Playtime` is the real time played while the game is not paused. Store it in your save and restore it with `Chronos.SetPlaytime` when the save loads. Set `Chronos.CountsPlaytime` to false where time should not count, such as the title screen.

```csharp
save.Playtime = Chronos.Playtime.TotalSeconds;                      // when saving
Chronos.SetPlaytime(TimeSpan.FromSeconds(loaded.Playtime));          // when loading
```

The [Runtime monitor](editor-tooling.md#runtime-monitor) shows the pause claims, time scale and playtime.
