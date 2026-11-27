namespace Threadforge.Threadlink
{
    using UnityEngine.InputSystem;

    /// <summary>
    /// Announced on Iris when a local player joins. Players that exist at startup, such as player 0, are announced during
    /// the initialization phase, to subscribers that subscribed while booting.
    /// </summary>
    public readonly struct PlayerJoined
    {
        /// <summary>The player.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>The player's index in <see cref="Dextra.Players"/>.</summary>
        public readonly int Index;

        /// <summary>Creates the event.</summary>
        public PlayerJoined(PlayerInputContext player, int index)
        {
            Player = player;
            Index = index;
        }
    }

    /// <summary>Announced on Iris after a local player is removed. Later players have moved down one index.</summary>
    public readonly struct PlayerLeft
    {
        /// <summary>The removed player, already disposed.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>The index it had.</summary>
        public readonly int Index;

        /// <summary>Creates the event.</summary>
        public PlayerLeft(PlayerInputContext player, int index)
        {
            Player = player;
            Index = index;
        }
    }

    /// <summary>Announced on Iris when a device paired to a player disconnects, such as a gamepad running out of battery: pause the game.</summary>
    public readonly struct DeviceLost
    {
        /// <summary>The player that lost the device.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>The disconnected device.</summary>
        public readonly InputDevice Device;

        /// <summary>Creates the event.</summary>
        public DeviceLost(PlayerInputContext player, InputDevice device)
        {
            Player = player;
            Device = device;
        }
    }

    /// <summary>Announced on Iris when a lost device reconnects and returns to its player.</summary>
    public readonly struct DeviceRegained
    {
        /// <summary>The player the device returned to.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>The reconnected device.</summary>
        public readonly InputDevice Device;

        /// <summary>Creates the event.</summary>
        public DeviceRegained(PlayerInputContext player, InputDevice device)
        {
            Player = player;
            Device = device;
        }
    }

    /// <summary>
    /// Announced on Iris when a player starts using a different kind of device, such as a gamepad after keyboard and mouse:
    /// switch input prompts. Keyboard and mouse count as one kind.
    /// </summary>
    public readonly struct ControlsChanged
    {
        /// <summary>The player.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>The device now in use, as <see cref="PlayerInputContext.LastDevice"/>.</summary>
        public readonly InputDevice Device;

        /// <summary>Creates the event.</summary>
        public ControlsChanged(PlayerInputContext player, InputDevice device)
        {
            Player = player;
            Device = device;
        }
    }

    /// <summary>Announced on Iris after a rebind, reset or conflict removal changes a player's bindings: refresh prompts and settings menus.</summary>
    public readonly struct BindingsChanged
    {
        /// <summary>The player whose bindings changed.</summary>
        public readonly PlayerInputContext Player;

        /// <summary>Creates the event.</summary>
        public BindingsChanged(PlayerInputContext player) => Player = player;
    }

    /// <summary>Announced on Iris after a screen is presented.</summary>
    public readonly struct ScreenOpened
    {
        /// <summary>The screen.</summary>
        public readonly ScreenHandle Screen;

        /// <summary>Creates the event.</summary>
        public ScreenOpened(ScreenHandle screen) => Screen = screen;
    }

    /// <summary>Announced on Iris after a screen is closed, including screens closed when the session ends.</summary>
    public readonly struct ScreenClosed
    {
        /// <summary>The closed screen.</summary>
        public readonly ScreenHandle Screen;

        /// <summary>Creates the event.</summary>
        public ScreenClosed(ScreenHandle screen) => Screen = screen;
    }

    /// <summary>
    /// Announced on Iris when the first screen that blocks gameplay input opens, and when the last one closes: pause
    /// gameplay, show the cursor, hide the HUD.
    /// </summary>
    public readonly struct GameplayInputBlockChanged
    {
        /// <summary>Whether gameplay input is now blocked.</summary>
        public readonly bool Blocked;

        /// <summary>Creates the event.</summary>
        public GameplayInputBlockChanged(bool blocked) => Blocked = blocked;
    }
}
