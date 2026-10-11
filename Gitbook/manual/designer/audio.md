# Audio (Aura)

**Aura** is the audio subsystem. It manages the Music, Atmos (ambience) and SFX channels, and spatial audio zones.

## Aura configuration

| Field | Function |
|---|---|
| Master Audio Mixer | The mixer whose exposed parameters code reads and writes. **Engineering-owned.** |
| Volume Fade Speed | How fast music, ambience and listener volumes change. Higher is faster. |
| Navigation Clip | Played when moving between UI elements. |
| Confirm Clip | Played on UI confirmation. |
| Cancel Clip | Played on UI cancellation. |

Pick clips from the Assets dropdown. [Registering Scenes, Prefabs and Assets](registering-content.md) shows how to add them to it.

## Audio zones

An **AuraZone** is a local sound source in a scene. As the listener gets closer, it turns global music and ambience down.

1. Add an **AuraZone** component to a GameObject.
2. Add an **AudioSource** to the same GameObject and assign its clip. The zone puts it in its **Source** field and sets it to loop from the start at full volume. You can assign an AudioSource on another object to **Source** instead.
3. Set two values:
   - **Radius Coefficient** (0–1): the zone's radius, as a fraction of the AudioSource's maximum distance.
   - **Influence** (0–1): how far the global channels are turned down when the listener is fully inside the zone.

Placing the zone is all it takes. Zones start working when their scene becomes the presented scene, and stop when it is replaced or unloaded. Only the presented scene is heard, so zones in other loaded scenes (such as scenes kept loaded for other players) do nothing.

Zone names must be unique within a scene, ignoring case. A zone with the same name as another is ignored, and a warning is logged.

## Per-scene audio

Each scene has music and ambience tracks with target volumes, set through a scene entry that engineering writes (see [Nexus](../engineer/nexus.md#the-isceneentry-contract)). Choose the tracks and volumes, and include them in the scene's specification.
