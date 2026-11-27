# Audio Cues

An **audio cue** describes one sound the game can play: which clips, how loud, how varied, where in space and how important. Create one with **Create ▸ Threadlink ▸ Aura ▸ Audio Cue**.

| Setting | Effect |
|---|---|
| Variations | Clips with weights. A weighted random variation plays each time; Avoid Immediate Repeat (on by default) skips the clip that just played. |
| Volume and pitch | Ranges sampled per playback. |
| Loop | Plays until stopped. |
| Spatial Blend, Minimum and Maximum Distance | From 2D (0) to fully 3D (1), with the distance range for 3D rolloff. |
| Category | Groups cues for shared volume, muting, fades and voice limits; choose one of the project's categories from the dropdown (the default is `Effects`). |
| Mixer Group | The Audio Mixer group the sound plays through. |
| Priority | Decides which voices survive when a limit is reached (128 by default). |
| Voice Limit and Overflow | How many instances may play at once (16 by default), and whether a new playback is rejected (the default) or steals a lower-priority voice. |
| Loading Policy | Selected Variation prepares only the clip about to play; Preload Variations prepares every variation ahead of playback. |

Players' volume settings adjust category volumes. Mixer snapshots (for example a muffled pause-menu mix) are authored in the Audio Mixer as usual.

## Scene music

Add **Add Component ▸ Threadlink ▸ Aura ▸ Scene Music** to any object in a scene and assign a looping cue. When the scene starts, the music crossfades to it (leave the cue empty to fade the music out). Set a crossfade on the component, or leave it negative to use the project default. When scenes change through Nexus, the music is prepared while the next scene loads and starts as it appears, so a failed transition never interrupts the current track. A music cue that cannot start (for example because its category is full) never affects the scene; use a stealing overflow policy for music.

## Audio zones

An **Audio Zone** turns some sounds down near a place, such as the music under a waterfall, and can play a sound there.

1. Add **Add Component ▸ Threadlink ▸ Aura ▸ Audio Zone** to an object at the place.
2. Set the **Radius**: beyond it the zone does nothing. The Scene view shows it as a sphere when the object is selected.
3. Under **Lowers**, choose each category to turn down and how much (1 silences it at the zone's center). The effect grows from nothing at the radius to the full amount at the center.
4. Optionally assign a looping **Cue** that plays at the zone while it is enabled.

## Screen sounds

Add **Add Component ▸ Threadlink ▸ Dextra ▸ Screen Sounds** to a screen prefab and assign open, close and focus cues, and, for UI Toolkit screens, navigate and submit cues played as the player moves between and presses controls. A screen that fails to open makes no sound, and a sound that cannot play never blocks a screen.
