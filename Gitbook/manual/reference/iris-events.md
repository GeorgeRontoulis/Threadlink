# Native Iris Events

Values are ordinals allocated in the order listed, from `Iris.Events.Native.txt`; project and module events follow. The delegate is the type that subscribers state and publishers use (see [Iris](../engineer/iris.md)). For `Func` events, the "provider" column names who must subscribe the single listener.

| Value | Event | Delegate | Publisher | Provider / typical listener |
|---|---|---|---|---|
| 0 | `OnNativeSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment | `NativeSubsystemsConfig` |
| 1 | `OnUserSubsystemRegistration` | `Func<List<IThreadlinkSubsystem>>` | Core deployment | `UserSubsystemsConfig` |
| 2 | `OnCoreDeployed` | `Action<Threadlink>` | Core deployment | Aura; project code |
| 3 | `OnUpdate` | `Action` | `ThreadlinkPlayerLoop` (Native mode) or the project (Custom mode) | Chronos, Aura, interfaces |
| 4 | `OnFixedUpdate` | `Action` | As `OnUpdate` | Chronos |
| 5 | `OnLateUpdate` | `Action` | As `OnUpdate` | Interfaces |
| 6 | `OnPlaytimeCountTick` | `Action<float>` | Chronos | Project code |
| 7 | `OnGamePauseRequested` | `Action` | Project code | Project code; no framework use |
| 8 | `OnGameResumeRequested` | `Action` | Project code | Project code; no framework use |
| 9 | `OnGamePaused` | `Action` | Chronos | Project code |
| 10 | `OnGameResumed` | `Action` | Chronos | Project code |
| 11 | `OnInputDeviceChanged` | `Action<Dextra.InputDevice>` | Dextra | `DextraInputIcon` |
| 12 | `OnUICancelled` | `Action<UserInterface>` | Dextra's UI stack | Project code |
| 13 | `OnUIElementSelected` | `Action<GameObject>` | Dextra | Project code |
| 14 | `OnInteract` | `Func<bool>` | Project code, typically from an input action | The interactable in range |
| 15 | `OnInteractableDetected` | `Action<Interactable2D>` or `Action<Interactable3D>` | `InteractablesDetector2D` / `InteractablesDetector3D` | Project code |
| 16 | `OnInteractableOutOfRange` | `Action<Interactable2D>` or `Action<Interactable3D>` | `InteractablesDetector2D` / `InteractablesDetector3D` | Project code |
| 17 | `OnSceneResident` | `Action<Nexus.ISceneEntry>` | Nexus | Project code |
| 18 | `OnSceneUnloading` | `Action<Nexus.ISceneEntry>` | Nexus | Project code |
| 19 | `OnDisplayFaderAsync` | `Func<UniTask>` | Nexus | The project's fader |
| 20 | `OnHideFaderAsync` | `Func<UniTask>` | Nexus | The project's fader |
| 21 | `OnDisplayLoadingScreenAsync` | `Func<UniTask>` | Nexus | The project's loading screen |
| 22 | `OnHideLoadingScreenAsync` | `Func<UniTask>` | Nexus | The project's loading screen |
| 23 | `OnScenePresented` | `Action<Nexus.ISceneEntry>` | Nexus | Aura; project code |
| 24 | `OnSceneLoaded` | `Action<Nexus.SceneLoad>` | Nexus | Project code |
| 25 | `OnSceneUnloaded` | `Action<Scene>` | Nexus | Project code |
| 26 | `OnSentinelStateChanged` | `Action<Sentinel>` | Sentinel | Project code |

The interactable detection events carry the detector's concrete interactable type: a 2D detector publishes `Interactable2D`, a 3D detector `Interactable3D`. A subscriber states the one it expects.
