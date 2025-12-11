namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Cysharp.Threading.Tasks;
    using Iris;
    using Shared;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.DualShock;
    using UnityEngine.InputSystem.Switch;
    using UnityEngine.InputSystem.UI;

    /// <summary>
    /// Threadlink's Human-Interface Interaction System.
    /// </summary>
    public sealed class Dextra : ThreadlinkSubsystem<Dextra>,
    IThreadlinkDependency<EventSystem>,
    IThreadlinkDependency<InputActionAsset>,
    IAddressablesPreloader
    {
        public enum InputMode : byte { Unresponsive, Player, UI }
        internal enum InputDevice : byte
        {
            MouseAndKeyboard,
            XBOXController,
            PSController,
            SwitchProController
        }

        public InputMode CurrentInputMode
        {
            set
            {
                switch (value)
                {
                    case InputMode.Unresponsive:
                        PlayerMap.Disable();
                        UIMap.Disable();
                        break;
                    case InputMode.Player:
                        PlayerMap.Enable();
                        UIMap.Disable();
                        break;
                    case InputMode.UI:
                        PlayerMap.Disable();
                        UIMap.Enable();
                        break;
                }
            }
        }

        internal InputDevice CurrentInputDevice { get; private set; } = InputDevice.MouseAndKeyboard;

        private EventSystem UnityEventSystem { get; set; }
        private PlayerInput InputDeviceDetector { get; set; }
        private InputActionMap PlayerMap { get; set; }
        private InputActionMap UIMap { get; set; }

        public bool TryConsumeDependency(EventSystem input)
        {
            if (input != null
            && input.TryGetComponent(out PlayerInput deviceDetector)
            && input.TryGetComponent(out InputSystemUIInputModule uiInputModule))
            {
                UnityEventSystem = input;
                InputDeviceDetector = deviceDetector;
                UIMap = uiInputModule.actionsAsset.FindActionMap(NativeConstants.Input.UI_MAP);
                return true;
            }

            return false;
        }

        public bool TryConsumeDependency(InputActionAsset input)
        {
            if (input != null)
            {
                PlayerMap = input.FindActionMap(NativeConstants.Input.GAMEPLAY_MAP);
                return PlayerMap != null;
            }

            return false;
        }

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            var loadedPrefab = await Threadlink.Instance.NativeConfig.LoadDextraDependenciesAsync();

            if (loadedPrefab == null)
                return false;

            var dependencyInstance = Object.Instantiate(loadedPrefab);

            dependencyInstance.name = loadedPrefab.name;
            Object.DontDestroyOnLoad(dependencyInstance);

            return TryConsumeDependency(dependencyInstance) && TryConsumeDependency(InputSystem.actions);
        }

        public override void Discard()
        {
            CurrentInputMode = InputMode.Unresponsive;
            InputDeviceDetector.onControlsChanged -= UpdateInputDevice;
            UnityEventSystem = null;
            InputDeviceDetector = null;
            PlayerMap = UIMap = null;
            base.Discard();
        }

        public override void Boot()
        {
            base.Boot();

            //uiStack.Boot();

            InputDeviceDetector.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            InputDeviceDetector.onControlsChanged += UpdateInputDevice;
            CurrentInputMode = InputMode.Unresponsive;
        }

        private void UpdateInputDevice(PlayerInput input)
        {
            var oldDevice = CurrentInputDevice;

            CurrentInputDevice = input.currentControlScheme switch
            {
                NativeConstants.Input.MKB_DEVICE => InputDevice.MouseAndKeyboard,

                NativeConstants.Input.GAMEPAD_DEVICE when Gamepad.current != null => Gamepad.current switch
                {
                    DualShockGamepad => InputDevice.PSController,
                    SwitchProControllerHID => InputDevice.SwitchProController,
                    _ => InputDevice.XBOXController,
                },

                _ => InputDevice.MouseAndKeyboard,
            };

            if (CurrentInputDevice != oldDevice)
            {
                Iris.Publish(Iris.Events.OnInputDeviceChanged, CurrentInputDevice);

                if (CurrentInputDevice is InputDevice.MouseAndKeyboard)
                {
                    var allGamepads = Gamepad.all;
                    int length = allGamepads.Count;

                    for (int i = 0; i < length; i++)
                        allGamepads[i].SetMotorSpeeds(0f, 0f);
                }
            }
        }
    }
}
