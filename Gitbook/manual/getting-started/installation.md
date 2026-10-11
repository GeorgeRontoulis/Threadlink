# Installation and Project Setup

Threadlink is distributed as a folder, not as a UPM package. The repository's `Threadforge/` directory is the framework together with a ready-made project territory and default configuration assets.

## Add the framework

1. Install the [required packages](requirements.md#required-packages).
2. Copy the repository's `Threadforge/` folder into the project as **`Assets/Threadforge/`**, `.meta` files included.

The location is not a convention but a requirement: the core loads the Native Config from the fixed Addressables address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset` (`NativeConstants.Addressables.NATIVE_CONFIG`). The asset references between the shipped configuration assets also rely on the `.meta` GUIDs being preserved.

## Shipped configuration

The framework arrives configured. Nothing below needs to be created; it needs to be reviewed.

| Asset | Location |
|---|---|
| Native Config | `Threadlink/ThreadlinkConfig.Native.asset`, already mapping every native resource. |
| Editor Config | `Threadlink/ThreadlinkConfig.Editor.asset`, already declaring every native identifier domain. |
| User Config | `Threadlink User/Engineering/Configs/ThreadlinkConfig.User.asset` |
| Chronos Config, Sentinel Config | `Threadlink User/Engineering/Configs/` |
| Aura Config, `AuraComponents.prefab`, `AuraMixer.mixer` | `Threadlink User/Engineering/Configs/Aura/` |
| Dextra Config, `DextraComponents.prefab`, `ThreadlinkInputActions.inputactions`, `UIInputActions.inputactions`, `InputSystem.inputsettings.asset` | `Threadlink User/Engineering/Configs/Dextra/` |

Every configuration asset is described in [Configuration and Project Setup](../engineer/configuration.md).

## Make the native assets Addressable

1. Create the project's Addressables settings if it has none, and an Addressable group named **`Threadlink Assets`**.
2. Select **`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`**.

The command reads the Native Config and marks the Native Config itself and every native resource it references as Addressable in the `Threadlink Assets` group, each addressed by its asset path. If that group does not exist, the default group is used and a warning is logged.

## Create the bootstrap scene

Every project needs exactly one **bootstrap scene**, the build's entry point:

1. Create a scene that is **not** Addressable.
2. Make it the first, and ideally the only, scene in Build Settings.
3. Keep it minimal. It stays loaded for the whole run; gameplay content belongs in Addressable scenes loaded through Nexus.
4. Make Play Mode start from it, so the Editor boots as a build does.

Play Mode otherwise starts from whichever scene is open, and that scene takes the bootstrap scene's place. If it is an Addressable gameplay scene, Nexus later loads a second copy of it beside the first. Unity exposes the Play Mode start scene through `EditorSceneManager.playModeStartScene`, for example:

```csharp
#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
internal static class BootstrapPlayModeStart
{
    static BootstrapPlayModeStart()
    {
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
            UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Scenes/Bootstrap.unity");
    }
}
#endif
```

The reasons for this arrangement are set out in [Deployment Overview](../foundations/deployment-overview.md).

## Choose the platform distribution

Open the **Sentinel Config** and confirm the distribution for platforms that offer a choice, such as Windows (Local, Steam, Microsoft Store, Epic or GOG). The shipped default is **Local**, which needs no platform SDK. See [Sentinel](../engineer/sentinel/README.md).

## Verify the deployment

Enter Play Mode from the bootstrap scene. The console lists the woven native subsystems (Sentinel, Chronos, Dextra and Aura), then the user subsystems (or reports that there are none), then `Core successfully deployed.` Sentinel separately reports the platform and distribution it deployed, or why it failed; a failed platform does not stop the deployment.

Deployment aborts with an error if either the Native Config or the User Config cannot be loaded, most often because the native assets were never made Addressable.

> **Previewing a scene without the core.** Setting `ThreadlinkEditorDeployment.Suppressed = true` before `AfterSceneLoad` (for example from a `BeforeSceneLoad` hook) skips Addressables initialisation and core deployment for that Play session. The flag is Editor-only and resets on entering and exiting Play Mode.

## Next steps

- Designers: [The Identifier Workflow](../designer/identifiers.md).
- Engineers: [Architecture](../engineer/architecture.md) and [Implementing a Subsystem](../engineer/implementing-a-subsystem.md).
