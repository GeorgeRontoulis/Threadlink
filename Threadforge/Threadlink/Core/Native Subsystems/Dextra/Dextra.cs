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
    using NativeResources = Shared.ThreadlinkIDs.Addressables.NativeResources;

    /// <summary>
    /// Threadlink's Human-Interface Interaction Subsystem.
    /// This is a multipurpose solution offering built-in fuctionality
    /// for both Input and UI.
    /// <para></para>
    /// The Input implementation is based on Unity's modern Input System package,
    /// while the UI is based on Unity's standard UGUI package.
    /// </summary>
    public sealed partial class Dextra : ThreadlinkSubsystem<Dextra>,
    IInitializable,
    IAddressablesPreloader,
    IDependencyConsumer<EventSystem>,
    IDependencyConsumer<DextraConfig>
    {
        public enum InputDevice : byte
        {
            MouseAndKeyboard,
            XBOXController,
            PSController,
            SwitchProController
        }

        public ThreadlinkIDs.Dextra.InputModes CurrentInputMode { get; private set; } = ThreadlinkIDs.Dextra.InputModes.Unresponsive;
        public InputDevice CurrentInputDevice { get; private set; } = InputDevice.MouseAndKeyboard;

        private EventSystem UnityEventSystem { get; set; }
        private PlayerInput InputDeviceDetector { get; set; }
        private DextraConfig Config { get; set; }

        public bool TryConsumeDependency(EventSystem input)
        {
            if (input != null)
            {
                var eventSystem = Object.Instantiate(input);

                eventSystem.name = input.name;

                if (Config.HideEventSystemInHierarchy)
                    eventSystem.gameObject.hideFlags = HideFlags.HideInHierarchy;

                Object.DontDestroyOnLoad(eventSystem);

                if (eventSystem.TryGetComponent(out PlayerInput deviceDetector))
                {
                    UnityEventSystem = eventSystem;
                    InputDeviceDetector = deviceDetector;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (!Threadlink.TryGetSingleton(out var core))
                return false;

            var nativeConfig = core.NativeConfig;

            var loadedResources = await UniTask.WhenAll
            (
                nativeConfig.LoadNativeResourceAsync<DextraConfig>(NativeResources.DextraConfig),
                nativeConfig.LoadNativeResourceAsync<GameObject>(NativeResources.DextraComponentsPrefab)
            );

            if (TryConsumeDependency(loadedResources.Item1))
            {
                await Config.LoadAllUserInterfacesAsync();

                return TryConsumeDependency(loadedResources.Item2.GetComponent<EventSystem>());
            }

            return false;
        }

        public override void Discard()
        {
            Iris.Unsubscribe<System.Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
            CurrentInputMode = ThreadlinkIDs.Dextra.InputModes.Unresponsive;
            InputDeviceDetector.onControlsChanged -= UpdateInputDevice;
            UnityEventSystem = null;
            InputDeviceDetector = null;

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

            if (Config.TryGetInterfacePointers(out var pointers))
            {
                UIStack.CreateAllInterfaces(pointers);
                UIStack.Boot();
            }

            Iris.Subscribe<System.Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
            InputDeviceDetector.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            InputDeviceDetector.onControlsChanged += UpdateInputDevice;
            CurrentInputMode = ThreadlinkIDs.Dextra.InputModes.Unresponsive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize()
        {
            UIStack.Initialize();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputIcon(InputDevice device, DextraInputControlPath inputControlPath, out Sprite result)
        {
            if (Config == null)
            {
                result = null;
                return false;
            }

            return Config.TryGetInputIcon(device, inputControlPath, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetInputMap(ThreadlinkIDs.Dextra.InputModes mode, out InputActionMap result)
        {
            if (Config == null)
            {
                result = null;
                return false;
            }

            return Config.TryGetInputMap(mode, out result);
        }

        private void OnCoreDeployed(Threadlink core)
        {
            if (!core.HasLinked(TypeHash))
                return;

            var inputIcons = Object.FindObjectsByType<DextraInputIcon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (inputIcons != null)
            {
                int length = inputIcons.Length;

                for (int i = 0; i < length; i++)
                    inputIcons[i].ListenForInputDeviceChanges(true);
            }
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
                var allGamepads = Gamepad.all;
                int length = allGamepads.Count;

                for (int i = 0; i < length; i++)
                    allGamepads[i].SetMotorSpeeds(0f, 0f);

                Iris.Publish(ThreadlinkIDs.Iris.Events.OnInputDeviceChanged, CurrentInputDevice);
            }
        }
    }
}
