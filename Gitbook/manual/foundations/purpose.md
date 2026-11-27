# Purpose

Threadlink packages the systems most Unity games rebuild for every project: event dispatch, session-wide systems and their startup, content loading with clear ownership, scene transitions, input contexts and screen navigation, audio cues, typed definitions, transactional saves with recovery, platform services, deterministic numbers and randomness, and operation diagnostics.

Three rules shape every package:

- **The framework answers where things live.** Session-wide systems are subsystems owned by the Threadlink core: registered automatically, ready before the first Awake, shut down in reverse order, reached from anywhere. Game code never constructs a framework system or decides where to keep it.
- **Engineers own code, designers own data.** Composition is code; engineering settings live in Project Settings; designers edit cues, definitions, scene music, screens and tuning, which never select code. SDKs such as Steam stay off until an engineer enables them.
- **Unity stays authoritative.** Packages build on Unity's Input System, Addressables, AudioSource and AudioMixer, SceneManager, UI Toolkit and UGUI, and the profiler. Addressables keeps its own resource accounting; Threadlink adds request ownership and typed identities, not a second cache.

Operations that can fail report how they failed. Cancellation, partial failure, account changes and interrupted saves have defined outcomes, and [Diagnostics](../engineer/diagnostics.md) records them without changing behavior. Install only what you use: features are separate packages, and integrations between them compile only when both are installed.
