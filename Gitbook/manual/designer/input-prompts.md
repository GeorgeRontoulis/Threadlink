# Input Prompts

Threadlink resolves the correct button glyph for the active input device and substitutes it automatically on device change. Four device families are distinguished: mouse and keyboard, Xbox controller, PlayStation controller, and Switch Pro controller.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Configure the control it represents (its input control path).

Inside one of Dextra's interfaces, that is all. Anywhere else, an engineer's component must boot and discard it (see [Dextra](../engineer/dextra.md#device-resolution)).

The control-and-device to sprite mapping resides on the Dextra Config asset. Populating it is shared work: designers supply and assign sprites, engineers configure control paths. Sprites are Addressable assets; see [Registering Scenes, Prefabs and Assets](registering-content.md).

If no sprite is assigned for the active device, the image is hidden and an error is logged.
