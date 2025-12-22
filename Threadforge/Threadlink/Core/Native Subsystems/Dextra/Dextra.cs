namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Cysharp.Threading.Tasks;
    using Iris;
    using Shared;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.DualShock;
    using UnityEngine.InputSystem.Switch;
    using UnityEngine.InputSystem.UI;

    /// <summary>
    /// Threadlink's Human-Interface Interaction Subsystem.
    /// This is a multipurpose solution offering built-in fuctionality
    /// for both Input and UI.
    /// <para></para>
    /// The Input implementation is based on Unity's modern Input System package,
    /// while the UI is based on Unity's standard UI package.
    /// </summary>
    public sealed partial class Dextra : ThreadlinkSubsystem<Dextra>,
    IInitializable,
    IAddressablesPreloader,
    IDependencyConsumer<EventSystem>,
    IDependencyConsumer<InputActionAsset>,
    IDependencyConsumer<DextraConfig>
    {
        public enum InputMode : byte { Unresponsive, Player, UI }
        public enum InputDevice : byte
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
        private DextraConfig Config { get; set; }

        public bool TryConsumeDependency(EventSystem input)
        {
            if (input != null)
            {
                var eventSystem = Object.Instantiate(input);

                eventSystem.name = input.name;
                eventSystem.gameObject.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(eventSystem);

                if (eventSystem.TryGetComponent(out PlayerInput deviceDetector)
                && eventSystem.TryGetComponent(out InputSystemUIInputModule uiInputModule))
                {
                    UnityEventSystem = eventSystem;
                    InputDeviceDetector = deviceDetector;
                    UIMap = uiInputModule.actionsAsset.FindActionMap(NativeConstants.Input.UI_MAP);
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeDependency(InputActionAsset input) => (PlayerMap = input.FindActionMap(NativeConstants.Input.GAMEPLAY_MAP)) != null;

        public bool TryConsumeDependency(DextraConfig input)
        {
            if (input != null)
            {
                Config = input;
                UIStack = new();
                return true;
            }

            return false;
        }

        public async UniTask<bool> TryPreloadAssetsAsync()
        {
            var loadedResources = await Threadlink.Instance.NativeConfig.LoadDextraResourcesAsync();
            var config = loadedResources.Item2;

            var binaryConsumptionTask = config.ConsumeBinariesAsync();
            var uiLoadingTask = config.LoadAllUserInterfacesAsync();

            await UniTask.WhenAll(binaryConsumptionTask, uiLoadingTask);

            return TryConsumeDependency(config)
            && TryConsumeDependency(loadedResources.Item1)
            && TryConsumeDependency(InputSystem.actions);
        }

        public override void Discard()
        {
            CurrentInputMode = InputMode.Unresponsive;
            InputDeviceDetector.onControlsChanged -= UpdateInputDevice;
            UnityEventSystem = null;
            InputDeviceDetector = null;
            PlayerMap = UIMap = null;

            if (UIStack != null)
            {
                UIStack.Discard();
                Config.UnloadAllUserInterfaces();

                UIStack = null;
            }

            Config = null;

            base.Discard();
        }

        public override void Boot()
        {
            base.Boot();

            UIStack.CreateAllInterfaces(Config.InterfacePointers);
            UIStack.Boot();

            InputDeviceDetector.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            InputDeviceDetector.onControlsChanged += UpdateInputDevice;
            CurrentInputMode = InputMode.Unresponsive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize()
        {
            UIStack.Initialize();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputIcon(InputDevice device, System.Guid actionID, out Sprite result)
        {
            if (Config == null)
            {
                result = null;
                return false;
            }

            return Config.TryGetInputIcon(device, actionID, out result);
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
