# Aura: Audio

Aura's preloader loads the Aura Config and the **Aura Components** prefab, which it instantiates as a `DontDestroyOnLoad` object. The prefab needs three children, `Music`, `Atmos` and `SFX`, each with an `AudioSource`; without them, preloading fails. At boot Aura mutes the music and ambience channels and creates its own hidden `AudioListener`.

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

- **The listener** is moved, not parented. Something must call `DriveAudioListener` every frame, usually a camera controller on `OnLateUpdate`; otherwise the listener stays where it was last put. Take care during scene transitions, when the old driver is destroyed before the new one exists.
- **Fades** step once per frame at the config's Volume Fade Speed, in unscaled time, and clamp their targets to 0–1.
- **Audio scenarios.** `TransitionToAudioScenarioAsync` fades both channels out, swaps their clips and fades them in to the requested volumes, capped by the maxima from `SetGlobalVolumesMax`. Nexus calls both when it presents a scene (see [Nexus](nexus.md#the-isceneentry-contract)).
- **UI sounds.** `PlayUISFX` loads the config's navigation, confirm or cancel clip and plays it on the SFX channel. With `oneShot: false`, it stops the channel first.
- **Mixer parameters.** `TryGetMixerValue` and `TrySetMixerValue` read and write the master mixer's exposed parameters.

## Spatial objects

Aura links only the **presented** scene's `AuraSpatialObject`s (including `AuraZone`s). Once the core has deployed, and on every `OnScenePresented`, it disconnects every spatial object and links the active ones in the active scene. Other resident scenes are never linked, so a scene kept loaded for another player is silent.

While anything is linked, each `OnUpdate` Aura adds up the objects' influence at the listener and moves music and ambience towards their maxima minus that total. An object's influence falls linearly from its `Influence` at the source to zero at its radius. The radius is the AudioSource's maximum distance times the Radius Coefficient, read at runtime from the `Source` field or, if that is empty, the AudioSource on the same GameObject. Custom spatial objects derive from `AuraSpatialObject` and supply `SourcePosition`; they can set `Radius` themselves or override `GetSpatialInfluence`.

Aura never drops its links on scene events. A spatial object unlinks itself, and stops its source, when discarded. It isn't discoverable, so the code that owns it must discard it before its scene unloads; otherwise Aura keeps evaluating a destroyed object until the next presentation. Aura's own `Discard` disconnects whatever is left, such as the persistent scene's.

Links are keyed by `LinkableBehaviour.ID`, a case-insensitive hash of the object's name, so spatial object names must be unique within a scene. A duplicate is not linked, and a warning names both objects. The [Runtime Monitor](editor-tooling.md#runtime-monitor) lists what is linked.
