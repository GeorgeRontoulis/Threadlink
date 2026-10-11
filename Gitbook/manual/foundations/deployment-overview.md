# Deployment Overview

The core deploys itself through Unity's runtime-initialisation hooks. No initialiser needs placing in a scene.

## The bootstrap scene

Every project needs one **bootstrap scene**, for three reasons:

- **Unity needs a scene as the entry point.** A player starts from the first scene in Build Settings.
- **An Addressable scene can't be that scene.** Addressable scenes are not in the build's scene list, and Addressables only initialise after the first scene loads.
- **So one scene must be non-Addressable**: the first, and ideally only, scene in Build Settings. That is the bootstrap scene. It stays loaded for the whole run, and every other scene is Addressable and loads beside it through [Nexus](../engineer/nexus.md).

[Installation and Project Setup](../getting-started/installation.md#create-the-bootstrap-scene) shows how to set it up.

## The deployment

1. Assemblies load. Weaving factories register, native and project code subscribe to their registration events, and Sentinel platform modules register.
2. Once the bootstrap scene has loaded, the core initialises Addressables and loads the **Native Config**, then the **User Config** it references.
3. The core creates itself, then weaves, preloads, boots and initialises the native subsystems, then the project's.
4. The core sets `IsDeployed` and publishes `OnCoreDeployed`.
5. The discoverable objects in the bootstrap scene boot.

Deployment needs two Addressable assets:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

[Deployment and Initialisation](../engineer/deployment.md) gives the exact sequence; [Configuration and Project Setup](../engineer/configuration.md) covers the assets.
