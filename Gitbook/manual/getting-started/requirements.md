# Requirements

## Unity

Threadlink packages declare Unity **6000.6**; the reference project and the starter's tested set use **6000.6.9f1**. Use the project's recorded Editor version for development and builds.

## Unity packages used by Threadlink packages

| Unity package | Version | Required by |
|---|---|---|
| Addressables (`com.unity.addressables`) | 2.11.2 | The core (content loading) |
| Collections (`com.unity.collections`) | 6.6.0 | The core (memory toolkits) |
| Input System (`com.unity.inputsystem`) | 1.20.1 | Dextra |
| Localization (`com.unity.localization`) | 1.5.14 | Dextra, Starter, Showcase (player-visible text) |
| Audio, Physics, Physics 2D and UI Elements modules | 1.0.0 | Aura, Dextra, the core's Editor workspace |
| uGUI (`com.unity.ugui`) | 2.6.0 | Optional: uGUI screens in Dextra |

## Optional packages

| Package | Effect |
|---|---|
| Universal or High Definition Render Pipeline (`com.unity.render-pipelines.universal`, `com.unity.render-pipelines.high-definition`), shipped with the Editor | Managed on **Threadlink ▸ Workspace ▸ Rendering**: install, create pipeline assets, choose the default and per-quality-level assets. Without one the project uses the Built-in Render Pipeline. |
| Steamworks API (`com.unity.steamworks-api`) | Enables the Unity backend of `sentinel.steam`. Import the SDK libraries through **Project Settings ▸ Steamworks**. |
| Steamworks.NET (`com.rlabrecque.steamworks.net`) 2024.8.0 or later | Enables the Steamworks.NET backend of `sentinel.steam`. Choose one Steam wrapper per project. |

Integrations compile only when the package they need is installed; no scripting define is needed.

## Bundled dependencies

| Package | Purpose |
|---|---|
| `com.threadforge.dependencies.messagepack` 3.1.12 | MessagePack runtime, annotations, source generator and analyzers, with System.Collections.Immutable and its string tools, for Sentinel saves. |

It keeps its upstream licenses and notices. Do not install duplicate copies of its DLLs under `Assets`.

## Scripting runtime

Threadlink targets .NET Standard 2.1 APIs, uses [UniTask](https://github.com/Cysharp/UniTask) 2.5.11 (`com.cysharp.unitask`, installed from OpenUPM with the core) for asynchronous work and does not depend on domain reload, `AppDomain`, `BinaryFormatter` or Mono-specific APIs, in preparation for Unity's CoreCLR runtime. Its validation runs Play mode with domain reload both enabled and disabled.
