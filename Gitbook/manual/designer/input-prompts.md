# Input Prompts

Threadlink shows the right button glyph for the active input device, and swaps it when the device changes. It recognises four device families: mouse and keyboard, Xbox, PlayStation, and Switch Pro controllers.

1. Add a **`DextraInputIcon`** component to a UI Image.
2. Set the control it shows (its input control path).

Inside one of Dextra's interfaces, that's all. Elsewhere, an engineer's component must boot and discard it (see [Dextra](../engineer/dextra.md#device-resolution)).

The Dextra Config maps each control and device to a sprite. Designers supply and assign the sprites; engineers set the control paths. Sprites are Addressable assets (see [Registering Scenes, Prefabs and Assets](registering-content.md)).

If the active device has no sprite, the image is hidden and an error is logged.
