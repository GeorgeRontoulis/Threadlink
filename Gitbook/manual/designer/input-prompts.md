# Input Prompts and Rebinding

Input uses Unity Input System action assets. Each player gets their own copy of the actions, paired with that player's devices, so prompts and rebinds for one player never affect another.

## Prompts

A prompt names the control for an action on the device the player used last. If the team supplies glyphs (button icons), prompts show the glyph; otherwise they show the control's readable name, such as `Space` or `Button South`. Prompts switch automatically when the player changes device.

## Rebinding

Rebinding listens for the next control on the player's own devices. Composite bindings such as WASD are rebound one part at a time. Cancelling restores the previous bindings. When a new binding conflicts with another action, the game decides whether to accept it and clear the other binding. Rebinds are saved for each player automatically and restored the next time the game starts; resetting restores the action asset's defaults.

## Contexts

Gameplay, menus and cutscenes enable different action maps through claims with priorities. A blocking claim (for example an open menu) suspends lower-priority input until it is released, then the earlier input returns unchanged.
