# Audio (Aura)

**Aura** is the audio subsystem, managing the Music, Atmos (ambience) and SFX channels and supporting spatial audio zones.

## Aura configuration

| Field | Function |
|---|---|
| Master Audio Mixer | The mixer whose exposed parameters engineering reads and writes. **Engineering-owned.** |
| Volume Fade Speed | Rate at which music, ambience and listener volumes transition. Higher values produce faster transitions. |
| Navigation Clip | Effect played on UI element traversal. |
| Confirm Clip | Effect played on UI confirmation. |
| Cancel Clip | Effect played on UI cancellation. |

Clips are selected from the Assets dropdown; [Registering Scenes, Prefabs and Assets](registering-content.md) covers the procedure for populating that dropdown.

## Audio zones

An **AuraZone** is a scene component producing a localised sound source that attenuates global music and ambience by inverse-distance influence as the listener approaches.

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same GameObject, assign its clip, and assign the AudioSource to the zone's **Source** field. Editing the zone configures the source for looping playback on awake, at full volume.
3. Configure two parameters:
   - **Radius Coefficient** (0–1): scales the influence radius relative to the AudioSource's maximum distance.
   - **Influence** (0–1): attenuation applied to the global channels when the listener is fully within the zone.

Zones are linked automatically when their scene becomes the presented scene and disconnected when it is replaced or unloaded; placement is the only required action. Only the presented scene is audible, so zones in other loaded scenes (for example, scenes kept loaded for other players in a multiplayer session) have no effect.

Zone names must be unique within a scene, ignoring case: a zone sharing another zone's name is not linked, and a warning is logged.

## Per-scene audio

Each scene declares music and ambience tracks with target volumes through a scene entry implemented by engineering (see [Nexus](../engineer/nexus.md#the-isceneentry-contract)). Track selection and volume levels are design decisions and should be communicated as part of the scene specification.
