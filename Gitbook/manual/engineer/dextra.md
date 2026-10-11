# Dextra: Input and UI

Dextra is built on Unity's Input System and uGUI. Its preloader loads the Dextra Config, every interface prefab the config lists, and the **Dextra Components** prefab. Dextra instantiates that prefab's `EventSystem` (with an `InputSystemUIInputModule`) as a `DontDestroyOnLoad` object, hidden in the Hierarchy if the config says so.

## Device resolution

`Dextra.InputDevice` is `MouseAndKeyboard`, `XBOXController`, `PSController` or `SwitchProController`. `CurrentInputDevice` follows whichever device last had a control change: DualShock and DualSense gamepads map to `PSController`, Switch Pro controllers to `SwitchProController`, other gamepads to `XBOXController`, and keyboard or mouse to `MouseAndKeyboard`. On a change, every gamepad's rumble stops and `OnInputDeviceChanged` is published.

`TryGetInputIcon(device, controlPath, out Sprite)` gets the glyph from the Dextra Config. A `DextraInputControlPath` is an Input System control path.

A `DextraInputIcon` is a bootable `LinkableBehaviour`, but not discoverable. From `Boot` until it is discarded it shows its control's glyph, and it hides its image when the active device has none:

- In one of Dextra's interfaces, the interface boots and discards it.
- Anywhere else, the code that owns it must.

## Input modes

```csharp
dextra.SetInputMapActive(ThreadlinkIDs.Dextra.InputModes.Gameplay, true);
dextra.TryGetInputMap(mode, out InputActionMap map);
```

The Dextra Config maps each mode to an `InputActionReference` in a `FieldHashMap`. A mode resolves to the action map that action belongs to. `InputModes.Unresponsive` (zero) is reserved.

## Pointer and selection

| Member | Semantics |
|---|---|
| `CurrentPointedObject`, `OnPointerEnter`, `OnPointerExit` | The `Selectable` under the mouse, from the UI input module's last raycast, checked once per frame after late update. A change is reported only after it holds for three checks, which filters out the module's double-firing. |
| `SelectUIElement(GameObject)` | Selects an element through the EventSystem and publishes `OnUIElementSelected`. Returns a `UniTaskVoid`; call `.Forget()`. `null` clears the selection. |
| `ClearEventSystemSelection()` | Clears the selection and publishes `OnUIElementSelected` with `null`. |
| `SetEventSystemActive(bool)`, `SetUIInputModuleActive(bool)` | Turn the EventSystem, or its UI input module, on or off. |
| `TryGetEventSystem(out EventSystem)`, `TryGetUIInputModule(out InputSystemUIInputModule)` | The instantiated objects. |

A selection the EventSystem makes by itself is kept only while the top interface is interactable (`IInteractableInterface`); otherwise it is cleared on the next frame.

## The UI stack

Interfaces derive from `UserInterface`, or `UserInterface<S>` for a singleton, and need a `CanvasGroup`. Initium doesn't discover them. Dextra instantiates them from `DextraConfig.interfacePointers` (Addressable prefab identifiers), marks them `DontDestroyOnLoad`, sets their alpha to zero, makes them non-interactable, and boots and initialises them. There is one instance per concrete interface type, and Dextra discards them in its own `Discard`. Each interface discards its selectables, and boots and discards its input icons.

So to add an interface: build the prefab, map it in the Addressables Mapping Window, and add its identifier to `interfacePointers`.

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

- **Stacking** an interface already on top does nothing. Otherwise the current top is covered, and the new interface is pushed and notified. `Stack<U, D>` passes the payload to `IStackingDataPreprocessor<D>.Preprocess` before showing the interface, and warns if the interface doesn't implement it for that type.
- **Popping** removes the top interface and resurfaces the one beneath.
- **Cancelling** only affects an `ICancellableInterface` on top. If it reports `IsInSubPanel`, it gets `OnSubPanelCancelled()` and stays. Otherwise it is popped, the one beneath resurfaces, it gets `OnCancelled()`, and `OnUICancelled` is published with it as payload. Either way, Aura's cancel sound plays.
- **Clearing** pops every stacked interface.

The [Runtime Monitor](editor-tooling.md#runtime-monitor) shows the stack, top first.

### Interface lifecycle

`UserInterface` reacts to the stack through protected virtual hooks:

| Hook | Default behaviour |
|---|---|
| `OnStacked()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnCovered()` | Fades out unless it is an `IPersistentInterface`; becomes non-interactable. |
| `OnResurfaced()` | Fades in; becomes interactable if it is an `IInteractableInterface`. |
| `OnPopped()` | Fades out; becomes non-interactable. |

Fades run on `OnUpdate` in unscaled time, over the Dextra Config's fade durations. Derived code starts them with `Display()` and `Hide()`. `ForceCanvasGroupAlphaTo(float)` sets alpha at once, and `SetInteractableState(bool)` toggles interactivity and raycast blocking. `IsVisible` and `IsHidden` are true when fully shown or fully hidden. An interface with a `Canvas` disables it while fully transparent.

### Marker interfaces

| Interface | Effect |
|---|---|
| `ICancellableInterface` | Receives `OnCancelled()` and `OnSubPanelCancelled()`; reports `IsInSubPanel` |
| `IPersistentInterface` | Stays visible when covered |
| `IInteractableInterface`, `IInteractableInterface<T>` | Has selectable content; the generic form exposes `Selectables` and `LastSelectable` |
| `IStackingDataPreprocessor<T>` | Preprocesses the stacking payload |

### Selectables

`DextraSelectable` wraps a uGUI `Selectable`, raises `OnSelected` and `OnDeselected`, and turns mouse hover into selection so gamepad and mouse navigation behave the same. `DextraSelectable<T>` exposes the wrapped component as `GenericUnitySelectable`; `DextraButton` wraps a `Button` and exposes its `OnClick`. `ClearSelectionListeners()` removes selection listeners without a full discard, for pooled selectables.

`InteractableUserInterface<Singleton, Selectable>` is the base for an interactable singleton interface. It registers its `Selectables` at boot, remembers the last one selected, reselects it when stacked or resurfaced, and discards its selectables in its own `Discard`.

`NonDrawingGraphic` is a raycast target that draws nothing.

The stack doesn't manage interfaces placed in scenes. `ClearStackedInterfaces()` doesn't affect them, and `DontDestroyOnLoad` instances survive transitions, so hide them yourself.

## Interactables

`Interactable` is a `LinkableBehaviour` with an `InteractableConfig`. `Interactable2D` and `Interactable3D` use a 2D or 3D collider on the same GameObject as the active area. Like the other framework types it isn't discoverable: your code discards it, which disables its collider and stops it answering `OnInteract`.

`InteractablesDetector2D` (a `CapsuleCollider2D`) and `InteractablesDetector3D` (a `CapsuleCollider`) are the triggers that find interactables. Each needs a `Rigidbody2D` or `Rigidbody` on the same GameObject, which it keeps kinematic, frozen and free of gravity. They publish `OnInteractableDetected` and `OnInteractableOutOfRange` with the interactable as payload. Both derive from the generic `EntityDetector2D<Area, Entity>` and `EntityDetector3D<Area, Entity>`, which project code can extend for other entity types.

Derived interactables implement `Interact()`, which returns whether the interaction happened. When detected, an interactable subscribes it to `OnInteract` as a `Func<bool>`, and unsubscribes when out of range. Project code publishes `OnInteract`, typically from an input action, to interact with whatever is in range. With `InteractionOptions.InteractOnContact` in its config, an interactable interacts as soon as it is detected instead. `InteractionPrompt` is the config's prompt text, localised when `THREADLINK_LOCALIZATION` is defined.

Because `OnInteract` is a `Func` event, only one interactable can be in range at a time. Overlapping active areas throw.
