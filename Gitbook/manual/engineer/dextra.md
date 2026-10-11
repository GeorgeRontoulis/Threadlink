# Dextra: Input and UI

Dextra is built on Unity's Input System and uGUI. Its preloader loads the Dextra Config, every interface prefab it lists, and the **Dextra Components** prefab, whose `EventSystem` (with an `InputSystemUIInputModule`) Dextra instantiates as a `DontDestroyOnLoad` object, hidden in the Hierarchy when the config's hide flag is set.

## Device resolution

`Dextra.InputDevice` enumerates `MouseAndKeyboard`, `XBOXController`, `PSController` and `SwitchProController`. `CurrentInputDevice` updates automatically from the first control that changes on any device: DualShock and DualSense gamepads resolve to `PSController`, Switch Pro controllers to `SwitchProController`, any other gamepad to `XBOXController`, and keyboard or mouse to `MouseAndKeyboard`. On a change, every gamepad's rumble is stopped and `OnInputDeviceChanged` is published.

`TryGetInputIcon(device, controlPath, out Sprite)` resolves the corresponding glyph from the Dextra Config, where a `DextraInputControlPath` is an Input System control path.

A `DextraInputIcon` is a bootable `LinkableBehaviour`, not a discoverable one. It shows the glyph of its control from `Boot` until it is discarded, and hides its image when no glyph exists for the active device:

- In one of Dextra's interfaces, the interface boots it and discards it.
- Anywhere else, the developer's code that owns it does.

## Input modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The mode-to-`InputActionReference` mapping resides on the Dextra Config as a `FieldHashMap`. A mode resolves to the action map that its referenced action belongs to. `InputModes.Unresponsive` (zero) is the reserved sentinel.

## Pointer and selection

| Member | Semantics |
|---|---|
| `CurrentPointedObject`, `OnPointerEnter`, `OnPointerExit` | The `Selectable` under the mouse, from the UI input module's last raycast, polled once per frame after late update. A change must persist for three polls before it is reported, which absorbs the module's double-firing. |
| `SelectUIElement(GameObject)` | Selects an element through the EventSystem and publishes `OnUIElementSelected`. Returns a `UniTaskVoid`; call `.Forget()`. Passing `null` clears the selection. |
| `ClearEventSystemSelection()` | Clears the selection and publishes `OnUIElementSelected` with `null`. |
| `SetEventSystemActive(bool)`, `SetUIInputModuleActive(bool)` | Enable or disable the EventSystem, or activate or deactivate its UI input module. |
| `TryGetEventSystem(out EventSystem)`, `TryGetUIInputModule(out InputSystemUIInputModule)` | Access to the instantiated objects. |

A selection made natively by the EventSystem is accepted only while the top interface is interactable (`IInteractableInterface`); otherwise it is cleared on the next frame.

## The UI stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and require a `CanvasGroup`. They are not discovered by Initium: Dextra instantiates them from `DextraConfig.interfacePointers` (Addressable prefab identifiers), marks them `DontDestroyOnLoad`, forces their alpha to zero, makes them non-interactable, and boots and initialises them. One instance exists per concrete interface type. Dextra discards them in its own `Discard`. Each interface discards its selectables, and boots and discards its input icons.

Introducing a stacked interface therefore requires constructing the prefab, mapping it through the Addressables Mapping Window, and adding its identifier to `interfacePointers`.

```csharp
dextra.Stack<PauseMenuUI>();
dextra.Stack<ShopUI, ShopData>(data);   // ShopUI implements IStackingDataPreprocessor<ShopData>
dextra.PopTopInterface();
dextra.Cancel();
dextra.ClearStackedInterfaces();
Dextra.IsTopInterface<PauseMenuUI>();
Dextra.IsStacked<PauseMenuUI>();
bool interactable = dextra.TopInterfaceIsInteractable;
```

- **Stacking** an interface that is already on top does nothing. Otherwise the current top is covered, and the new interface is pushed and notified. `Stack<U, D>` hands the payload to `IStackingDataPreprocessor<D>.Preprocess` before the interface is shown, and logs a warning if the interface is not a preprocessor of that type.
- **Popping** removes the top interface and resurfaces the one beneath it.
- **Cancelling** acts only on an `ICancellableInterface` on top. If it reports `IsInSubPanel`, it receives `OnSubPanelCancelled()` and stays. Otherwise it is popped, the interface beneath resurfaces, it receives `OnCancelled()`, and `OnUICancelled` is published with it as payload. Either way Aura's cancel sound plays.
- **Clearing** pops every stacked interface.

### Interface lifecycle

`UserInterface` reacts to the stack through protected virtual hooks:

| Hook | Default behaviour |
|---|---|
| `OnStacked()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnCovered()` | Fades out unless it is an `IPersistentInterface`; becomes non-interactable. |
| `OnResurfaced()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnPopped()` | Fades out; becomes non-interactable. |

Fades run on `OnUpdate` at unscaled time, over the Dextra Config's fade-in and fade-out durations. `Display()` and `Hide()` start them from derived code; `ForceCanvasGroupAlphaTo(float)` sets alpha immediately, and `SetInteractableState(bool)` toggles interactivity and raycast blocking. `IsVisible` and `IsHidden` report a fully shown or fully hidden interface. An interface with a `Canvas` disables it while fully transparent.

### Marker interfaces

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Receives `OnCancelled()` and `OnSubPanelCancelled()`; reports `IsInSubPanel` |
| `IPersistentInterface` | Exempt from concealment when covered |
| `IInteractableInterface`, `IInteractableInterface<T>` | Declares selectable content; the generic form exposes `Selectables` and `LastSelectable` |
| `IStackingDataPreprocessor<T>` | Preprocesses the stacking payload |

### Selectables

`DextraSelectable` wraps a uGUI `Selectable`, raising `OnSelected` and `OnDeselected`, and turns mouse hover into selection so gamepad and mouse navigation behave alike. `DextraSelectable<T>` exposes the wrapped component as `GenericUnitySelectable`; `DextraButton` wraps a `Button` and exposes its `OnClick`. `ClearSelectionListeners()` detaches selection listeners without a full discard, for pooled selectables.

`InteractableUserInterface<Singleton, Selectable>` is the base for an interactable singleton interface. It registers its `Selectables` at boot, remembers the last one selected, reselects it when stacked or resurfaced, and discards its selectables in its own `Discard`.

`NonDrawingGraphic` is a raycast target that draws nothing.

Scene-placed interfaces outside the stack are not managed by it. `ClearStackedInterfaces()` does not affect them, and `DontDestroyOnLoad` instances persist across transitions. Concealment must be invoked explicitly.

## Interactables

`Interactable` derives from `LinkableBehaviour` and carries an `InteractableConfig`; `Interactable2D` and `Interactable3D` bind it to a 2D or 3D collider on the same GameObject, which acts as its active area. Like every framework type it isn't discoverable: the developer's code discards it, which disables its collider and stops it answering `OnInteract`.

`InteractablesDetector2D` (a `CapsuleCollider2D`) and `InteractablesDetector3D` (a `CapsuleCollider`) are the trigger components that find them, publishing `OnInteractableDetected` and `OnInteractableOutOfRange` with the interactable as payload. Both derive from the generic `EntityDetector2D<Area, Entity>` and `EntityDetector3D<Area, Entity>`, which project code can extend for other entity types.

On detection, an interactable subscribes its `Interact` method as `Func<bool>` to `OnInteract`, and unsubscribes when out of range. Project code publishes `OnInteract`, typically from an input action, to interact with whatever is in range. An `InteractableConfig` with `InteractionOptions.InteractOnContact` interacts immediately on detection instead. `InteractionPrompt` exposes the config's prompt text, localised when `THREADLINK_LOCALIZATION` is defined.

`OnInteract` is a `Func` event, so at most one interactable may be in range at any time. Overlapping active areas violate this constraint and throw.
