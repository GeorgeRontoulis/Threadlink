# Dextra: Input, Screens and Interaction

Dextra (`com.threadforge.threadlink.dextra`) covers player input, screens and interaction. Assign the project's input actions, choose the gameplay and interface maps from their dropdowns and set the screen panel settings in **Project Settings ▸ Threadlink ▸ Dextra**.

## Players

Player 0 exists before the first Awake, with its own copy of the input actions and the gameplay map enabled. It uses every device no other player owns, including devices connected later; assigning one of those devices to another player (`AddPlayer`, `ReassignDevices`) moves it there. A device assigned explicitly stays with its player.

```csharp
var player = Dextra.Player();                              // player 0
Vector2 move = player.Read(GameInput.Gameplay.Move);       // InputId<Vector2>: the value type comes with the ID
if (player.WasPressed(GameInput.Gameplay.Jump)) Jump();
jumpPrompt.text = player.Prompt(GameInput.Gameplay.Jump);  // follows the last used device

using (player.Claim(200, true, GameInput.Maps.UI)) { }     // a blocking interface claim
var conflicts = await player.RebindAsync(GameInput.Gameplay.Jump);

var second = Dextra.AddPlayer(Gamepad.all[1]);      // local multiplayer
Dextra.RemovePlayer(second);
```

| API | Behaviour |
| --- | --- |
| `player.Action(id)`, `WasPressed`, `WasReleased`, `IsPressed`, `Read(InputId<T>)` | Action queries by `InputId`. `Read` returns the action's value type. |
| `player.Claim(priority, blocksLower, maps)` | Enables `InputMapId` maps by priority while the returned claim lives; a blocking claim suspends lower maps. |
| `player.RebindAsync(id, binding = 0)`, `GetConflicts(id, binding = 0)` | Interactive rebinding on the player's own devices; returns conflicts. |
| `player.ResetBindings()`, `ExportOverrides()`, `ImportOverrides(json)` | Binding overrides. |
| `Dextra.AddPlayer(devices)` / `AddPlayer(actions, devices)` | Joins a player with the project's or explicit actions. |
| `player.LastDevice` | The device the player used last, which prompts follow. |

Dextra announces input changes on Iris (see [Framework Events](events.md)):

| Event | When |
| --- | --- |
| `PlayerJoined`, `PlayerLeft` | A player joins or is removed. Players present at startup, such as player 0, are announced during the initialization phase. |
| `DeviceLost`, `DeviceRegained` | A player's device disconnects, such as a gamepad running out of battery (pause the game), and when it reconnects and returns to the player. |
| `ControlsChanged` | A player starts using a different kind of device, such as a gamepad after keyboard and mouse: switch prompts. Keyboard and mouse count as one kind. |
| `BindingsChanged` | A rebind, reset or conflict removal changes a player's bindings: refresh prompts and settings menus. |

## Input IDs

Threadlink generates `GameInput` from the input actions in **Project Settings ▸ Threadlink ▸ Dextra** (see [Generated IDs](editor-tooling.md#generated-ids)):

| Member | Type | Example |
| --- | --- | --- |
| `GameInput.Maps.<Map>` | `InputMapId` | `GameInput.Maps.Gameplay` |
| `GameInput.<Map>.<Action>` | `InputId<T>` with the value type of the action's control type (`Vector2`, `float`, …), or `InputId` when the action has none | `GameInput.Gameplay.Move` |

IDs are the actions' stable IDs, so renaming an action or map in the asset keeps code working; the generated member follows the new name and the old one stays as an obsolete tombstone. A player whose actions come from another asset, such as a sample's own actions, resolves an ID by its `Map/Action` path instead; an ID found neither way fails with an `ArgumentException` naming the action. `id.Find(asset)` applies the same lookup to any actions asset. Actions created at runtime have IDs too: `InputId<Vector2>.Of(action)` and `InputMapId.Of(map)`. Settings fields that name a map use `[InputMapReference]`, which shows a dropdown of the project's maps.

Rebinds are saved per player under `persistentDataPath/Threadlink/Input` and restored on the next run (turn off with **Save Bindings**).

## Screens

```csharp
var pause = await Dextra.Screens.OpenAsync(pauseDocument);               // a UI Toolkit document
pause.Root.Q<Button>("resume").clicked += () => Dextra.Screens.CloseAsync(pause).Forget();

var map = await Dextra.Screens.OpenAsync(ContentPrefabs.Screens.Map, ScreenLayer.Overlay);   // a mapped prefab
```

A screen prepares before it shows, takes focus, and by default blocks gameplay input by claiming the interface map above gameplay. Closing it restores focus and input to what was below. Layers are `Hud`, `Overlay`, `Menu` and `Modal`. Prefab screens carry a `ToolkitScreen` or, with uGUI installed, a `UGuiScreen`. `ScreenHandle.Root` is the root element of a UI Toolkit screen; `ScreenHandle.Instance` its GameObject. `Dextra.Screens.DefaultPanel` overrides the configured panel settings at runtime.

`ScreenOpened` and `ScreenClosed` announce every screen on Iris, and `GameplayInputBlockChanged` announces when the first blocking screen opens and the last one closes: pause gameplay, show the cursor, hide the HUD.

Components implementing `IScreenListener` on a screen hear when it shows, hides and regains focus. With Aura installed, **Threadlink ▸ Dextra ▸ Screen Sounds** plays open, close and focus cues; a rejected sound never affects the screen.

## Interaction

```csharp
public sealed class Chest : Interactable
{
    public override string Prompt => "Open chest";

    protected override async UniTask InteractAsync(GameObject actor, CancellationToken token)
    {
        await animator.PlayOpenAsync(token);
        Inventory.Add(loot);
    }
}
```

Add **Threadlink ▸ Dextra ▸ Interaction Sensor** to the player. Every frame it finds interactables through 3D or 2D physics within its radius and selects the closest available one; `sensor.Prompt` is the selected target's prompt and `await sensor.InteractAsync()` runs one interaction at a time. Iris publishes `InteractionTargetChanged` when the selection changes. Objects that cannot derive from `Interactable` implement `IInteractable`.
