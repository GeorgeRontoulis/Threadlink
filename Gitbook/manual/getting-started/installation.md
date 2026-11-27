# Installation and Project Setup

Threadlink is a set of Unity packages published on [OpenUPM](https://openupm.com), a scoped registry that the Unity Package Manager installs from with dependencies resolved automatically. All Threadlink packages share one version; this manual describes **4.1.0**.

## Install with the OpenUPM command-line tool

With [openupm-cli](https://openupm.com/docs/getting-started-cli.html) installed, run from the project folder:

```bash
openupm add com.threadforge.threadlink
```

The command adds the OpenUPM registry with the `com.threadforge` and `com.cysharp` scopes and installs the core with its UniTask dependency. Add feature packages the same way, or install them all from the Hub (below). To install the whole framework in one command:

```bash
openupm add com.threadforge.threadlink com.threadforge.threadlink.nexus com.threadforge.threadlink.aura com.threadforge.threadlink.dextra com.threadforge.threadlink.sentinel com.threadforge.threadlink.deterministic
```

## Install through the Package Manager

1. Add the OpenUPM registry to `Packages/manifest.json`:

   ```json
   {
     "scopedRegistries": [
       {
         "name": "OpenUPM",
         "url": "https://package.openupm.com",
         "scopes": ["com.threadforge", "com.cysharp"]
       }
     ],
     "dependencies": {
     }
   }
   ```

2. Open **Window ▸ Package Manager**, select **My Registries**, and install **Threadlink**, then the features you want.
3. Alternatively, install by name with **+ ▸ Install package by name**, for example `com.threadforge.threadlink.aura`.

Unity 6 asks for confirmation the first time a project installs a package that a scoped registry serves without a Unity signature. Packages from OpenUPM are unsigned. Review the listed packages and accept; the Package Manager remembers the decision for the project.

## The Hub

The core includes **Threadlink ▸ Workspace ▸ Hub**: every package with its description and documentation, one-click install, update and removal, **Install Complete Framework**, sample import, and the [Showcase](showcase.md). If the OpenUPM registry is missing, the Hub offers to add it.

## What installing does

Installed subsystems join the session automatically: they register at subsystem registration, deploy before the first scene's Awake and are usable from any script. Nothing is added to scenes. Packages with engineering settings work with defaults until you change them in **Project Settings ▸ Threadlink**. Optional integrations inside a package (Aura scene music with Nexus, screen sounds with Aura, uGUI screens) compile only when the package they need is installed.

Scripts in `Assembly-CSharp` see every Threadlink assembly. Assembly definitions reference the assemblies they use, for example `Threadforge.Threadlink` and `Threadforge.Threadlink.Aura`; add `Unity.ResourceManager` when using Addressables scene IDs.

## Set up the project

In the Hub, select **Set Up Project**. It creates the `Assets/Threadforge/Threadlink User` folders and two assembly definitions, each referencing every installed Threadlink runtime assembly:

| Assembly | Folder | Compiles |
| --- | --- | --- |
| `Threadlink.User.Generated` | `Engineering/Codebase/Generated` | The generated IDs (`GameScenes`, `GameInput`, `ContentAssets` and the rest), so any assembly can reference them |
| `Threadlink.User` | `Engineering/Codebase` | The project's code. It also references the generated IDs, UniTask, and the Addressables, Input System and Localization assemblies that are installed |

An assembly definition already in `Engineering/Codebase` keeps its name; Set Up adds the references it lacks. Run Set Up again after installing a package to add its assembly. The Hub's Project row and the **Project setup** validator show what is missing. Generating IDs also gives the generated folder its own assembly when no assembly definition compiles it.

## Offline installation

Every package is also available as a `.tgz` archive. In the Package Manager use **+ ▸ Install package from tarball** and install dependencies before the packages that need them, following each `package.json`. Archives of one release always share its version.

## Packages

| Package | Assembly | Contents |
| --- | --- | --- |
| `com.threadforge.threadlink` | `Threadforge.Threadlink` | The core: subsystems and the session, Iris events, content loading by typed ID (Addressables 2.11.2), Vault definitions, engineering settings, diagnostics, memory toolkits, and the Editor workspace with the Hub and Runtime monitor |
| `com.threadforge.threadlink.nexus` | `.Nexus` | Scene transitions for Build Profile and Addressables scenes |
| `com.threadforge.threadlink.aura` | `.Aura` | Audio cues, categories, music channel and scene music |
| `com.threadforge.threadlink.dextra` | `.Dextra` | Players and input (Input System 1.20.1), screens, interaction |
| `com.threadforge.threadlink.sentinel` | `.Sentinel` | Accounts, platform capabilities and saves |
| `com.threadforge.threadlink.sentinel.steam` | `.Sentinel.Steam` | Steam provider |
| `com.threadforge.threadlink.deterministic` | `.Deterministic` | Fixed point and StatelessRNG |
| `com.threadforge.threadlink.starter` | `.Starter` | A small complete game ([Starter Project](starter.md)) |
| `com.threadforge.threadlink.showcase` | `.Showcase` | A runnable demo for every feature ([Showcase](showcase.md)) |
| `com.threadforge.dependencies.messagepack` | — | MessagePack 3.1.12 runtime, annotations and source generator, used by Sentinel saves |
