# Aura: Audio

Aura's preloader loads the Aura Config and the **Aura Components** prefab, which it instantiates as a `DontDestroyOnLoad` object. The prefab must contain three children named `Music`, `Atmos` and `SFX`, each carrying an `AudioSource`; without them, deployment fails during preloading. At boot Aura silences the music and ambience channels and creates its own hidden `AudioListener`.

```csharp
aura.DriveAudioListener(position, rotation);     // position-only and rotation-only overloads exist
aura.PlayUISFX(Aura.UISFX.Confirm);              // Cancel, Navigate or Confirm; optional oneShot and volume
await aura.FadeAudioListenerVolumeAsync(0f);
await aura.TransitionToAudioScenarioAsync(music, atmos, musicVolume, atmosVolume);
aura.SetGlobalVolumesMax(musicVolume, atmosVolume);
aura.TryGetMixerValue(name, out float value);
aura.TrySetMixerValue(name, value);
await aura.FadeAudiosourceVolumeAsync(source, target);
aura.MoveTowardsVolume(source, target);
```

- **The listener.** The AudioListener transform is driven rather than parented: some component must invoke `DriveAudioListener` each frame, conventionally a camera controller on `OnLateUpdate`. Absent a driver, the listener retains its last assigned transform. Scene transitions require particular attention, as the previous driver is destroyed before its successor exists.
- **Fades** advance at the config's Volume Fade Speed on unscaled time, one step per frame, and clamp their targets to 0–1.
- **Audio scenarios.** `TransitionToAudioScenarioAsync` fades both channels out, swaps their clips, and fades them in to the requested volumes, capped by the maxima set through `SetGlobalVolumesMax`. Nexus calls both when a scene is presented (see [Nexus](nexus.md#the-isceneentry-contract)).
- **UI sounds.** `PlayUISFX` loads the config's navigation, confirm or cancel clip and plays it on the SFX channel. With `oneShot: false`, the channel is stopped first.
- **Mixer parameters.** `TryGetMixerValue` and `TrySetMixerValue` read and write exposed parameters of the config's master mixer.

## Spatial objects

Aura links the `AuraSpatialObject`s (including `AuraZone`s) of the **presented** scene only: once the core has deployed, and on every `OnScenePresented`, it disconnects all spatial objects and links the active ones in the active scene. Spatial objects of other resident scenes are never linked, so a scene kept loaded for another player is inaudible.

While anything is linked, Aura sums the linked objects' influence at the listener's position on each `OnUpdate` and moves the music and ambience volumes toward their maxima minus that influence. An object's influence falls linearly from its `Influence` at its source to zero at its radius, which is the AudioSource's maximum distance scaled by the Radius Coefficient. Custom spatial objects derive from `AuraSpatialObject`, supply `SourcePosition`, and may override `GetSpatialInfluence`.

Aura never tears its links down on a scene event. A spatial object unlinks itself when discarded, stopping its source. It isn't discoverable, so the developer's code that owns it discards it before its scene unloads; otherwise Aura keeps evaluating a destroyed object until the next presentation relinks. Aura's own `Discard` disconnects whatever is left, such as the persistent scene's.

The registry is keyed by `LinkableBehaviour.ID`, a hash of the object's name that ignores case. Spatial object names must therefore be unique within a scene: a colliding object is not linked, and a warning names both objects.
