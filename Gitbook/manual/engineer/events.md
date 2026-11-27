# Framework Events

The framework announces its state changes on [Iris](iris.md), so game code reacts to them instead of polling or depending on startup order. Every event is a `readonly struct`, delivered synchronously with `Iris.TryPublish`: a listener that throws is logged and the others still run.

## Listening

An event reaches the subscribers that are listening when it is announced; Iris never replays one. Subscribe before the events you need are announced: a subsystem subscribes while it boots. Iris boots before every other subsystem, and every subsystem boots before any initializes, so a boot-time subscriber hears everything announced during initialization and afterwards, whatever the order between subsystems:

```csharp
public sealed class OnlineBadge : ThreadlinkSubsystem<OnlineBadge>
{
    private SubscriptionToken ready;

    protected override void OnSubsystemBoot() =>
        ready = Iris.Subscribe<PlatformReady>(platform => Debug.Log(platform.Provider + " is ready"));

    protected override void OnSubsystemShutdown() => ready.Dispose();
}
```

A component subscribes with an owner, so the subscription ends when it is destroyed; it hears what is announced while it exists:

```csharp
private void Start() => Iris.Subscribe<DeviceLost>(this, lost => pauseMenu.Open());
```

## Core

| Event | Announced |
| --- | --- |
| `ThreadlinkReady` | Once, when the session has started: every registered subsystem has booted and initialized, or failed. Carries the counts. |
| `SubsystemFailed` | A subsystem fails to boot or initialize, with its type, name and error. For crash reporting. |
| `SessionEnding` | The session starts to shut down, while every subsystem is still available: the last moment to save. |
| `ContentLoadFailed` | A content load fails (cancelled loads are not announced), with the content's identity, request kind and error. |

## Nexus

| Event | Announced |
| --- | --- |
| `SceneTransitionStarted` | A transition starts, before the next scene loads. |
| `SceneChanged` | The next scene is presented. |
| `SceneTransitionFailed` | A transition fails or is cancelled before presentation; the current scene stays. |

## Aura

| Event | Announced |
| --- | --- |
| `MusicChanged` | The music channel changes cue or stops. |
| `VolumeChanged` | A category's volume is set, or a volume fade completes. |
| `MuteChanged` | A category is muted or unmuted. |

## Dextra

| Event | Announced |
| --- | --- |
| `PlayerJoined`, `PlayerLeft` | A player joins or is removed. Startup players are announced during initialization. |
| `DeviceLost`, `DeviceRegained` | A player's device disconnects, and when it reconnects. |
| `ControlsChanged` | A player starts using a different kind of device. |
| `BindingsChanged` | A player's bindings change. |
| `ScreenOpened`, `ScreenClosed` | A screen is presented or closed. |
| `GameplayInputBlockChanged` | The first blocking screen opens, or the last one closes. |
| `InteractionTargetChanged` | An interaction sensor selects a different target. |

## Sentinel

| Event | Announced |
| --- | --- |
| `PlatformReady` | A platform provider is added, such as Steam once it has started. |
| `AccountChanged` | The signed-in account changes, and once for the startup account during initialization. |
| `JoinIntentReceived` | A platform captures a join request. The intent also stays queued until taken. |
| `AchievementUnlocked` | An achievement is unlocked through Sentinel. |
| `SaveWritten` | A save is written. |
| `SaveFailed` | A save cannot be written, read, deleted or restored. An empty slot and a cancelled operation are not failures. |
| `SaveRecovered` | A slot is restored to its previous version. |

## Announcing your own

Follow the same shape for your subsystems' events: a `readonly struct` named in the past tense, announced with `Iris.TryPublish` when the change happens. Announce what other subsystems react to from `OnSubsystemInit` or later, never from `OnSubsystemBoot`, so every subsystem is subscribed.
