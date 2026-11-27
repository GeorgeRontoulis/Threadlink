# Showcase

The Showcase is a gallery with a runnable demo for every Threadlink feature. Each demo shows its controls, the packages it uses, an explanation of what it demonstrates and a live log.

## Open it

- From the Hub: **Threadlink ▸ Workspace ▸ Hub ▸ Install Showcase**, then **Open Showcase**. The Hub imports the sample and opens its scene.
- From the Package Manager: install `com.threadforge.threadlink.showcase`, import its **Showcase** sample and open `Scenes/Showcase.unity`.

Enter Play mode and select a demo on the left.

## Demos

| Demo | Shows | Packages |
|---|---|---|
| Events | Synchronous events, worker posts, frame callbacks, owner-bound subscriptions | core |
| Awaited Events | Awaited events in order, requests with one responder | core |
| Subsystems | A project subsystem: registration, dependencies, `Instance` and `TryGetSingleton` | core |
| Content | Typed IDs, owner-bound requests, a custom content backend | core |
| Scenes | Transition steps; a failed transition keeps the current scene | `nexus` |
| Audio | Cues, category volume, fades, the music channel | `aura` |
| Input | Players, priority claims, action queries, prompts | `dextra` |
| Screens | UI Toolkit documents as screens on layers | `dextra` |
| Interaction | An `Interactable` found by an `InteractionSensor` | `dextra` |
| Vault | Designer assets prepared into immutable values with `Vault.Get` | core |
| Saving | Declared save types, slots, conflicts and restoring the previous version | `sentinel` |
| Platform | Local profiles, capability routing and explicit results | `sentinel` |
| Deterministic | Order-independent streams, fixed-point arithmetic | `deterministic` |
| Memory | Native blocks and cross-machine hashing | core |
| Operations | The session's operation log and subsystem reports | core |

The demos run without project setup in the live session: content uses a small generated-texture backend through `Threadlink.UseContentBackend`, audio uses generated tones, and saves use slots 900 and above. Each demo's code is one file under `Runtime/Demos` in the Showcase package and uses only the public API. Open **Threadlink ▸ Runtime Monitor** while the gallery runs.

## Self-check

Every demo has a smoke check that exercises its features without input. The package's tests run all of them, and a player started with `-showcaseSmoke` runs them, writes `player-result.txt` next to the executable and quits.
