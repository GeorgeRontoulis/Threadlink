# Aura: Audio

Aura (`com.threadforge.threadlink.aura`) plays `AudioCue` assets on pooled Unity audio sources. No audio component needs to be placed; playback needs exactly one active `AudioListener` (`Aura.UseListener` selects one).

```csharp
Aura.Play(footstep, transform);          // follows the emitter, stops when it is destroyed
Aura.Play(explosion, hitPoint);          // at a position
var voice = Aura.Play(chime);            // 2D
if (voice != null)
    await voice.FadeToAsync(0, 1f, stopWhenFinished: true);

Aura.PlayMusic(forestTheme, crossfadeSeconds: 2f);
Aura.SetVolume(AudioCategories.Music, 0.7f);
await Aura.FadeVolumeAsync(AudioCategories.Effects, 0, 0.5f);
```

| API | Behavior |
| --- | --- |
| `Aura.Play(cue[, position \| emitter])` | Returns a `PlaybackHandle`, or null when the category or cue voice limit rejects the voice. |
| `Aura.PlayAsync(ContentAssetId)` | Loads a mapped cue, such as `ContentAssets.Audio.Thunder`; it stays loaded while it plays. |
| `PlaybackHandle` | `IsPlaying`, `RemainingSeconds`, `Stop()`, `FadeToAsync(gain, seconds, stopWhenFinished)`, `WaitAsync()` (how the voice ended). |
| `Aura.PlayMusic(cue, crossfade)` / `StopMusic(fade)` / `CurrentMusic` | One looping music channel with crossfades. Playing the current music again does nothing. |
| `Aura.SetVolume`, `SetMute`, `FadeVolumeAsync`, `SetVoiceLimit` | Per-category player volume, mute and limits, by `AudioCategory`. |

`AuraPlaylist` (a component) crossfades through a list of cues; `AmbienceLayers` keeps several looping layers.

Aura announces on Iris when the music channel changes cue or stops (`MusicChanged`), when a category's volume is set or a volume fade completes (`VolumeChanged`), and when a category is muted or unmuted (`MuteChanged`). A settings menu that subscribes stays in step with volumes changed elsewhere, such as from a loaded save. See [Framework Events](events.md).

## Categories

Project categories and their voice limits are engineering settings in **Project Settings ▸ Threadlink ▸ Aura**, together with the default scene-music crossfade. Threadlink generates `AudioCategories` from that list (see [Generated IDs](editor-tooling.md#generated-ids)), so code names a category as `AudioCategories.Music` rather than a string, and a cue's **Category** field is a dropdown of the same list. `cue.Category` is the cue's category as an `AudioCategory` (its name is `cue.CategoryName`); `new AudioCategory(name)` covers categories added at runtime.

## Scene music

Scene music is designer data: add **Threadlink ▸ Aura ▸ Scene Music** to a scene and assign a looping cue. When the scene starts, Aura crossfades to it. With Nexus installed, the cue's audio is also loaded while the scene is being prepared and the music starts as the scene is presented.
