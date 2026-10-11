# Deployment Overview

The core deploys itself through Unity's runtime-initialisation hooks, so no scene-placed initialiser is required.

## The bootstrap scene

Every project needs one **bootstrap scene**, for three reasons:

- **Unity needs a scene as the entry point.** A player starts from the first scene in Build Settings.
- **An Addressable scene can't be that scene.** Unity keeps Addressable scenes out of the build's scene list, and Addressables only initialise once the first scene has loaded.
- **So every project needs one non-Addressable scene**, first and ideally only in Build Settings. That is the bootstrap scene. It stays loaded for the whole run, as the persistent scene every other scene loads beside (see [Nexus](../engineer/nexus.md)), and every other scene is Addressable and loaded through Nexus.

Setting it up is covered in [Installation and Project Setup](../getting-started/installation.md#create-the-bootstrap-scene).

## The deployment

1. Assemblies load. Native and project subsystem factories register, and both tiers subscribe to their respective registration events. Sentinel platform modules register with the module registry.
2. Once the bootstrap scene has loaded, the core initialises Addressables, loads the **Native Config**, and loads the **User Config** referenced by it.
3. The core constructs itself, then registers, preloads, boots and initialises native subsystems, followed by project subsystems.
4. The core publishes `OnCoreDeployed`.
5. Discoverable objects present in the bootstrap scene are booted.

Deployment requires two assets addressable through the Addressables system:

- **`ThreadlinkConfig.Native.asset`**, at address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset`.
- **`ThreadlinkConfig.User.asset`**, referenced by the Native Config.

The precise sequence is specified in [Deployment and Initialisation](../engineer/deployment.md); configuration is detailed in [Configuration and Project Setup](../engineer/configuration.md).
