# Installation and Project Setup

Threadlink ships as a folder, not a UPM package. The repository's `Threadforge/` folder holds the framework, a ready-made project area and default configuration assets.

## Add the framework

1. Install the [required packages](requirements.md#required-packages).
2. Copy the repository's `Threadforge/` folder into the project as **`Assets/Threadforge/`**, `.meta` files included.

This location is required. The core loads the Native Config from the fixed address `Assets/Threadforge/Threadlink/ThreadlinkConfig.Native.asset` (`NativeConstants.Addressables.NATIVE_CONFIG`), and the shipped assets reference each other by the GUIDs in their `.meta` files.

## Shipped configuration

The framework comes configured. Review these assets; there is nothing to create.

| Asset | Location |
|---|---|
| Native Config | `Threadlink/ThreadlinkConfig.Native.asset`, already mapping every native resource. |
| Editor Config | `Threadlink/ThreadlinkConfig.Editor.asset`, already declaring every native identifier domain. |
| User Config | `Threadlink User/Engineering/Configs/ThreadlinkConfig.User.asset` |
| Chronos Config, Sentinel Config | `Threadlink User/Engineering/Configs/` |
| Aura Config, `AuraComponents.prefab`, `AuraMixer.mixer` | `Threadlink User/Engineering/Configs/Aura/` |
| Dextra Config, `DextraComponents.prefab`, `ThreadlinkInputActions.inputactions`, `UIInputActions.inputactions`, `InputSystem.inputsettings.asset` | `Threadlink User/Engineering/Configs/Dextra/` |

[Configuration and Project Setup](../engineer/configuration.md) describes each one.

## Make the native assets Addressable

1. Create the project's Addressables settings if it has none, and an Addressable group named **`Threadlink Assets`**.
2. Select **`Threadlink ▸ Addressables ▸ Mark Native Assets as Addressable`**.

This makes the Native Config, and every native resource it references, Addressable in the `Threadlink Assets` group, each addressed by its asset path. Without that group, it uses the default group and logs a warning.

## Create the bootstrap scene

Every project needs one **bootstrap scene**: the build's entry point.

1. Create a scene that is **not** Addressable.
2. Make it the first, and ideally the only, scene in Build Settings.
3. Keep it minimal. It stays loaded for the whole run; gameplay belongs in Addressable scenes loaded through Nexus.
4. Make Play Mode start from it, so the Editor boots as a build does.

Otherwise Play Mode starts from whichever scene is open, which then stands in for the bootstrap scene. If that is an Addressable gameplay scene, Nexus later loads a second copy of it. Set the start scene with `EditorSceneManager.playModeStartScene`:

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

[Deployment Overview](../foundations/deployment-overview.md) explains why.

## Choose the platform distribution

Open the **Sentinel Config** and check the distribution for each platform that offers a choice, such as Windows (Local, Steam, Microsoft Store, Epic or GOG). The default, **Local**, needs no platform SDK. See [Sentinel](../engineer/sentinel/README.md).

## Verify the deployment

Enter Play Mode from the bootstrap scene. The console lists the native subsystems (Sentinel, Chronos, Dextra and Aura), then any user subsystems, then `Core successfully deployed.` Sentinel also reports its platform and distribution, or why it failed; a failed platform does not stop deployment. **`Threadlink ▸ Runtime Monitor`** shows the result live.

If the Native Config or User Config cannot be loaded, deployment stops with an error. The usual cause is that the native assets were never made Addressable.

> **Previewing a scene without the core.** Set `ThreadlinkEditorDeployment.Suppressed = true` before `AfterSceneLoad` (for example from a `BeforeSceneLoad` hook) to skip Addressables initialisation and deployment for that Play session. The flag is Editor-only and resets on entering and exiting Play Mode.

## Next steps

- Designers: [The Identifier Workflow](../designer/identifiers.md).
- Engineers: [Architecture](../engineer/architecture.md) and [Implementing a Subsystem](../engineer/implementing-a-subsystem.md).
