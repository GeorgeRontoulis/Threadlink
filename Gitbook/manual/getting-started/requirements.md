# Requirements

## Unity

Threadlink 6.6 is developed and validated against **Unity 6000.6.4f1**. It relies on recent editor APIs, among them the `Unity.Scripting.LifecycleManagement` attributes (`[OnEnteringPlayMode]`, `[OnExitingPlayMode]`, `[AutoStaticsCleanup]`) and the main-toolbar extension API; earlier editor versions are untested.

## Required packages

| Package | Version in the reference project | Used by |
|---|---|---|
| Addressables (`com.unity.addressables`) | 2.11.2 | Every runtime-loaded scene, prefab and asset, and the configuration assets themselves. |
| Input System (`com.unity.inputsystem`) | 1.20.0 | Dextra: devices, action maps, UI input module. |
| uGUI (`com.unity.ugui`) | 2.6.0 | Dextra's interfaces and selectables. |
| Collections (`com.unity.collections`) | 6.6.0 | Unsafe collections and native memory; brings Mathematics (`int2`, `float2`, `quaternion`). |
| Burst (`com.unity.burst`) | 2.0.0 | Dependency of Collections. |

## Optional packages

| Package | Effect |
|---|---|
| Timeline (`com.unity.timeline`) ≥ 1.8.10 | Defines `THREADLINK_TIMELINE`, enabling the Vault Timeline integration. The reference project uses 6.6.0. |
| Localization (`com.unity.localization`) ≥ 1.5.9 | Defines `THREADLINK_LOCALIZATION`, enabling the `LocalizedText` Vault field, localised interaction prompts and the localisation utilities. The reference project uses 1.5.13. |
| Odin Inspector | Defines `ODIN_INSPECTOR`; hash maps, Vault fields and several inspectors draw through Odin. |
| Steamworks.NET (`com.rlabrecque.steamworks.net`) | Required by the Steam Sentinel module, which also requires the `STEAMWORKS_NET` scripting define. Validated against 2025.165.0. |

See [Scripting Defines](../engineer/configuration.md#scripting-defines) for the full list.

## Bundled dependencies

Threadlink ships the following under `Threadlink/Plugins/` and `Threadlink/Editor/`. They need no separate installation.

| Dependency | Purpose |
|---|---|
| UniTask | Every asynchronous API in the framework. `System.Threading.Tasks` is not used. |
| ZString | Allocation-free string building for Scribe and code generation. |
| MessagePack 3.1.4 (with its Unity integration and analyser) | `Threadlink.TrySerialize` / `TryDeserialize`. |
| SerializeReference UI (`Textus.SerializeReferenceUI`) | The managed-reference type pickers used by Vault fields and Sentinel Config when Odin is absent. |
| CSharpier (editor only) | The [Code Formatter](../engineer/editor-tooling.md#code-formatter) window. |
