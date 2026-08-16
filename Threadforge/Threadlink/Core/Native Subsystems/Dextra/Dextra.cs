namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Cysharp.Threading.Tasks;
    using Generated;
    using global::Threadlink.Utilities.Collections;
    using Iris;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.DualShock;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.InputSystem.Switch;
    using UnityEngine.InputSystem.UI;
    using UnityEngine.UI;
    using NativeResources = Generated.ThreadlinkIDs.Addressables.NativeResources;

    /// <summary>
    /// Threadlink's Human-Interface Interaction Subsystem.
    /// This is a multipurpose solution offering built-in fuctionality
    /// for both Input and UI.
    /// <para></para>
    /// The Input implementation is based on Unity's modern Input System package,
    /// while the UI is based on Unity's standard UGUI package.
    /// </summary>
    public sealed partial class Dextra : ThreadlinkSubsystem<Dextra>,
    IDisposable,
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

        public InputDevice CurrentInputDevice { get; private set; } = InputDevice.MouseAndKeyboard;

        private EventSystem UnityEventSystem { get; set; } = null;
        private InputSystemUIInputModule UIInputModule { get; set; } = null;
        private DextraConfig Config { get; set; } = null;

        public event Action<GameObject> OnPointerEnter = null;
        public event Action<GameObject> OnPointerExit = null;

        private CancellationTokenSource tokenSource = null;

        public bool TryConsumeDependency(EventSystem input)
        {
            if (input != null)
            {
                var eventSystem = UnityEngine.Object.Instantiate(input);

                eventSystem.name = input.name;

                if (eventSystem.TryGetComponent(out InputSystemUIInputModule module))
                    UIInputModule = module;

                if (Config.HideEventSystemInHierarchy)
                    eventSystem.gameObject.hideFlags = HideFlags.HideInHierarchy;

                UnityEngine.Object.DontDestroyOnLoad(eventSystem);

                UnityEventSystem = eventSystem;
                return true;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            StopPolling();
            InputSystem.onEvent -= OnAnyInputEvent;
        }

        public override void Discard()
        {
            Dispose();

            Iris.Unsubscribe<Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
            UnityEventSystem = null;

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

            Iris.Subscribe<Action<Threadlink>>(ThreadlinkIDs.Iris.Events.OnCoreDeployed, OnCoreDeployed);
            InputSystem.onEvent += OnAnyInputEvent;
            this.PreventEditorMemoryLeaks();
            StartPolling();

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize()
        {
            UIStack.Initialize();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetEventSystem(out EventSystem result)
        {
            if (UnityEventSystem != null)
            {
                result = UnityEventSystem;
                return true;
            }

            result = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetUIInputModule(out InputSystemUIInputModule result)
        {
            if (UIInputModule != null)
            {
                result = UIInputModule;
                return true;
            }
            else if (UnityEventSystem != null && UnityEventSystem.TryGetComponent(out result))
                return true;

            result = null;
            return false;
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

        public void SetInputMapActive(ThreadlinkIDs.Dextra.InputModes mode, bool active)
        {
            if (TryGetInputMap(mode, out var map))
            {
                if (active)
                    map.Enable();
                else
                    map.Disable();
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StartPolling()
        {
            StopPolling(); // guard against double-start
            tokenSource = new CancellationTokenSource();
            PollLoop(tokenSource.Token).Forget();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StopPolling()
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();
            tokenSource = null;
        }

        private async UniTaskVoid PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
                TrackPointerRaycasting();
            }
        }

        private const int ConfirmFrames = 3; //lowest value that reliably absorbs double-firing

        private GameObject _confirmedHover;
        private bool hasPendingChange;
        private int pendingCountdown;

        public GameObject CurrentPointedObject => _confirmedHover;

        private void TrackPointerRaycasting()
        {
            var uiModule = UIInputModule;
            if (uiModule == null || Mouse.current == null)
                return;

            var result = uiModule.GetLastRaycastResult(Mouse.current.deviceId);
            GameObject raw = null; // non-interactive hits: assume null

            if (!result.isValid)
            {
                raw = null;
            }
            else
            {
                var selectable = result.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null)
                    raw = selectable.gameObject;
            }

            if (raw == _confirmedHover)
            {
                hasPendingChange = false; // agrees with confirmed state — cancel any pending flip
                return;
            }

            if (!hasPendingChange)
            {
                hasPendingChange = true;
                pendingCountdown = ConfirmFrames;
            }

            if (--pendingCountdown > 0)
                return; // disagreement not confirmed yet — absorb this poll

            var leaving = _confirmedHover;
            _confirmedHover = raw;
            hasPendingChange = false;

            if (leaving != null) OnPointerExit?.Invoke(leaving);
            if (raw != null) OnPointerEnter?.Invoke(raw);
        }

        private void OnCoreDeployed(Threadlink core)
        {
            if (!core.HasLinked(TypeHash))
                return;

            var inputIcons = UnityEngine.Object.FindObjectsByType<DextraInputIcon>(FindObjectsInactive.Exclude);

            if (inputIcons != null)
            {
                int length = inputIcons.Length;

                for (int i = 0; i < length; i++)
                    inputIcons[i].ListenForInputDeviceChanges(true);
            }
        }

        private void OnAnyInputEvent(InputEventPtr eventPtr, UnityEngine.InputSystem.InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            foreach (var _ in eventPtr.EnumerateChangedControls(device, InputSystem.settings.defaultButtonPressPoint))
            {
                UpdateInputDevice(device);
                break; //Just the first iteration is enough to switch to the new device.
            }
        }

        private void UpdateInputDevice(UnityEngine.InputSystem.InputDevice device)
        {
            var oldDevice = CurrentInputDevice;

            CurrentInputDevice = device switch
            {
                DualShockGamepad => InputDevice.PSController,
                SwitchProControllerHID => InputDevice.SwitchProController,
                Gamepad => InputDevice.XBOXController,
                Keyboard or Mouse => InputDevice.MouseAndKeyboard,
                _ => oldDevice,
            };

            if (CurrentInputDevice == oldDevice) return;

            var allGamepads = Gamepad.all;
            int length = allGamepads.Count;
            for (int i = 0; i < length; i++)
                allGamepads[i].SetMotorSpeeds(0f, 0f);

            Iris.Publish(ThreadlinkIDs.Iris.Events.OnInputDeviceChanged, CurrentInputDevice);
        }
    }
}
