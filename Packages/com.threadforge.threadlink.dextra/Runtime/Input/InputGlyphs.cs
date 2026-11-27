namespace Threadforge.Threadlink
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Layouts;

    /// <summary>The family of a device, for choosing button glyphs.</summary>
    public enum InputDeviceFamily
    {
        /// <summary>Any device; used when no entry for the device's own family matches.</summary>
        Any,

        /// <summary>Keyboard and mouse.</summary>
        KeyboardMouse,

        /// <summary>Any gamepad; used for gamepads without an entry for their own family.</summary>
        Gamepad,

        /// <summary>Xbox and other XInput controllers.</summary>
        Xbox,

        /// <summary>PlayStation controllers (DualShock and DualSense).</summary>
        PlayStation,

        /// <summary>Nintendo Switch Pro controllers.</summary>
        Switch,
    }

    /// <summary>
    /// Designer data: the button glyph (a sprite) for each control on each family of device. Assign it in
    /// Project Settings ▸ Threadlink ▸ Dextra; prompts then show the glyph of the device the player used last.
    /// </summary>
    [CreateAssetMenu(menuName = "Threadlink/Dextra/Input Glyphs", order = 20)]
    public sealed class InputGlyphs : ScriptableObject
    {
        /// <summary>One glyph.</summary>
        [Serializable]
        public sealed class Glyph
        {
            /// <summary>The device family the glyph is for.</summary>
            [Tooltip("The device family the glyph is for. Gamepad covers gamepads without their own entry; Any covers every device.")]
            public InputDeviceFamily Device;

            /// <summary>An Input System control path, such as <c>&lt;Gamepad&gt;/buttonSouth</c> or <c>&lt;Keyboard&gt;/space</c>.</summary>
            [Tooltip("The control, as an Input System control path such as <Gamepad>/buttonSouth or <Keyboard>/space.")]
            public string Control = string.Empty;

            /// <summary>The image shown for the control.</summary>
            [Tooltip("The image shown for the control.")]
            public Sprite Sprite;
        }

        [SerializeField]
        [Tooltip("Glyphs, by device family and control. The most specific family wins.")]
        private List<Glyph> glyphs = new List<Glyph>();

        /// <summary>The glyphs.</summary>
        public IReadOnlyList<Glyph> Glyphs => glyphs;

        /// <summary>Adds a glyph. For tools and tests.</summary>
        public void Add(InputDeviceFamily device, string control, Sprite sprite) =>
            glyphs.Add(new Glyph { Device = device, Control = control ?? throw new ArgumentNullException(nameof(control)), Sprite = sprite });

        /// <summary>
        /// The glyph for <paramref name="control"/>: an entry for the device's own family, else for any gamepad (for a
        /// gamepad), else for any device. Null when none matches.
        /// </summary>
        public Sprite Find(InputControl control)
        {
            if (control == null)
                return null;
            var family = FamilyOf(control.device);
            Sprite gamepad = null,
                any = null;
            foreach (var glyph in glyphs)
            {
                if (glyph == null || glyph.Sprite == null || string.IsNullOrEmpty(glyph.Control) || !InputControlPath.Matches(glyph.Control, control))
                    continue;
                if (glyph.Device == family)
                    return glyph.Sprite;
                if (glyph.Device == InputDeviceFamily.Gamepad && control.device is Gamepad)
                    gamepad ??= glyph.Sprite;
                else if (glyph.Device == InputDeviceFamily.Any)
                    any ??= glyph.Sprite;
            }
            return gamepad ?? any;
        }

        /// <summary>The family of <paramref name="device"/>.</summary>
        public static InputDeviceFamily FamilyOf(InputDevice device)
        {
            if (device is Keyboard || device is Mouse)
                return InputDeviceFamily.KeyboardMouse;
            if (!(device is Gamepad))
                return InputDeviceFamily.Any;
            // Layout names keep this free of platform-specific device classes.
            if (BasedOn(device, "DualShockGamepad"))
                return InputDeviceFamily.PlayStation;
            if (BasedOn(device, "XInputController"))
                return InputDeviceFamily.Xbox;
            if (BasedOn(device, "SwitchProControllerHID"))
                return InputDeviceFamily.Switch;
            return InputDeviceFamily.Gamepad;
        }

        private static bool BasedOn(InputDevice device, string layout)
        {
            try
            {
                return InputSystem.IsFirstLayoutBasedOnSecond(device.layout, layout);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
