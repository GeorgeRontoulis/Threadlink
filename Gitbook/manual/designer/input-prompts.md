# Input Prompts and Rebinding

Input uses Unity Input System action assets. Each player gets their own copy of the actions, paired with that player's devices, so prompts and rebinds for one player never affect another.

## Prompts

A prompt names the control for an action on the device the player used last. If the team supplies glyphs (button icons), prompts show the glyph; otherwise they show the control's readable name, such as `Space` or `Button South`. Prompts switch automatically when the player changes device.

## Glyphs

To show button images instead of names, create **Create ▸ Threadlink ▸ Dextra ▸ Input Glyphs** and add an entry per control:

| Setting | Meaning |
| --- | --- |
| Device | Keyboard & mouse, Xbox, PlayStation, Switch, any gamepad, or any device. The device's own family wins, then "any gamepad", then "any device". |
| Control | An Input System control path, such as `<Gamepad>/buttonSouth` or `<Keyboard>/space`. |
| Sprite | The image. |

Engineering assigns the asset to **Glyphs** in **Project Settings ▸ Threadlink ▸ Dextra**. Prompts then show the glyph of the device the player used last, and the control's name where no glyph matches.

To place a prompt:

- In a UI Toolkit document, add the **Input Prompt** element (`Threadforge.Threadlink.InputPrompt`) and set its **Action** to the action's "Map/Action" path.
- On a uGUI canvas, add **Add Component ▸ Threadlink ▸ Dextra ▸ Input Prompt**, assign the action, an Image for the glyph and, optionally, a Text for the name.

Both follow device changes and rebinds by themselves.

## Rebinding

Rebinding listens for the next control on the player's own devices. Composite bindings such as WASD are rebound one part at a time. Cancelling restores the previous bindings. When a new binding conflicts with another action, the game decides whether to accept it and clear the other binding. Rebinds are saved for each player automatically and restored the next time the game starts; resetting restores the action asset's defaults.

## Contexts

Gameplay, menus and cutscenes enable different action maps through claims with priorities. A blocking claim (for example an open menu) suspends lower-priority input until it is released, then the earlier input returns unchanged.
