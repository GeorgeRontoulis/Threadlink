namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Core.ExtensionMethods;
	using Cysharp.Threading.Tasks;
	using Propagator;
	using System;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.InputSystem;
	using UnityEngine.InputSystem.DualShock;
	using UnityEngine.InputSystem.UI;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
using Editor.Attributes;
#endif
#endif

	/// <summary>
	/// System responsible for managing user interfaces, input and interactions.
	/// </summary>
	public sealed class Dextra : UnityWeaver<Dextra, UserInterface>, IInitializable, IAddressablesPreloader
	{
		public enum InputDevice : byte { MouseKeyboard, XBOXController, DualSense }
		public enum InputMode : byte { Unresponsive, UI, Player }

		public static InputMode CurrentInputMode
		{
			get => Instance.currentInputMode;

			set
			{
				Instance.currentInputMode = value;

				var inputAsset = InputSystem.actions;
				var playerMap = inputAsset.FindActionMap("Player");
				var interfaceMap = inputAsset.FindActionMap("UI");

				switch (value)
				{
					case InputMode.Unresponsive:
					playerMap.Disable();
					interfaceMap.Disable();
					break;
					case InputMode.UI:
					playerMap.Disable();
					interfaceMap.Enable();
					break;
					case InputMode.Player:
					playerMap.Enable();
					interfaceMap.Disable();
					break;
				}

				Propagator.Publish(PropagatorEvents.OnInputModeChanged, value);
			}
		}

#if UNITY_EDITOR && ODIN_INSPECTOR
		[ShowInInspector, ReadOnly]
#endif
		public static InputDevice CurrentInputDevice { get; private set; }

		private static Gamepad CurrentGamepad => Gamepad.current;
		private static EventSystem EventSystem => Instance.eventSystem;

		[Header("Input:")]
		[SerializeField] private EventSystem eventSystem = null;
		[SerializeField] private InputSystemUIInputModule uiModule = null;
		[SerializeField] private PlayerInput deviceDetector = null;

		[Space(10)]

#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private InputMode currentInputMode = InputMode.Unresponsive;

		[Header("UI:")]
		[SerializeField] private UIStateMachine uiStateMachine = null;

		public override void Discard()
		{
			if (uiStateMachine != null) uiStateMachine.Discard();

			deviceDetector.onControlsChanged -= UpdateInputDevice;

			SeverAll();

			uiStateMachine = null;
			eventSystem = null;
			deviceDetector = null;

			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();

			if (uiStateMachine != null)
			{
				uiStateMachine = uiStateMachine.Clone();
				uiStateMachine.Boot();
			}

			deviceDetector.onControlsChanged += UpdateInputDevice;
		}

		public void Initialize()
		{
			if (uiStateMachine != null) uiStateMachine.Initialize();
		}

		public async UniTask PreloadAssetsAsync()
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) await sm.PreloadAssetsAsync();
		}

		#region Interaction Code:
		public static void Cancel()
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) sm.Cancel();
		}

		public static void ClearStackedInterfaces()
		{
			var sm = Instance.uiStateMachine;

			if (sm != null && sm.StackedInterfacesCount > 0) sm.ClearStack();
		}
		#endregion

		#region UI Code:
		public static bool IsTopInterface(UserInterface userInterface)
		{
			var sm = Instance.uiStateMachine;

			return sm != null && sm.IsTopInterface(userInterface);
		}

		public static void Stack(string userInterfaceID)
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) sm.Stack(userInterfaceID);
		}

		public static void Stack<T>(string userInterfaceID, T data)
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) sm.Stack(userInterfaceID, data);
		}

		public static void Stack(UserInterface userInterface)
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) sm.Stack(userInterface);
		}

		public static void Stack<T>(UserInterface userInterface, T data)
		{
			var sm = Instance.uiStateMachine;

			if (sm != null) sm.Stack(userInterface, data);
		}

		public static void SyncSelection()
		{
			var selectedObject = EventSystem.currentSelectedGameObject;

			if (selectedObject != null)
				Propagator.Publish(PropagatorEvents.OnElementSelected, selectedObject.transform as RectTransform);
		}

		public static void ClearEventSystemSelection()
		{
			EventSystem.SetSelectedGameObject(null);
			PublishEmptySelectionEvent();
		}

		public static void PublishEmptySelectionEvent()
		{
			Propagator.Publish<RectTransform>(PropagatorEvents.OnElementSelected, null);
		}

		public static async UniTask SelectUIElement(GameObject element, bool syncSelection = true)
		{
			if (element == null)
			{
				ClearEventSystemSelection();
				return;
			}
			else if (element.Equals(EventSystem.currentSelectedGameObject)) return;

			await UniTask.NextFrame();

			ClearEventSystemSelection();
			SetEventSystemActiveState(false);

			await UniTask.NextFrame();

			SetEventSystemActiveState(true);
			EventSystem.SetSelectedGameObject(element);

			await UniTask.NextFrame();

			var sm = Instance.uiStateMachine;

			if (syncSelection && sm != null && sm.TopInterface is IInteractableInterface)
			{
				SyncSelection();
			}
			else PublishEmptySelectionEvent();
		}
		#endregion

		#region Input Code:
		private static void UpdateInputDevice(PlayerInput input)
		{
			var newDevice = CurrentInputDevice;
			string currentControlScheme = input.currentControlScheme;

			if (string.IsNullOrEmpty(currentControlScheme)) return;

			if (currentControlScheme.Equals("KeyboardAndMouse")) newDevice = InputDevice.MouseKeyboard;
			else if (currentControlScheme.Equals("Gamepad") && CurrentGamepad != null)
			{
				if (CurrentGamepad is DualShockGamepad)
					newDevice = InputDevice.DualSense;
				else
					newDevice = InputDevice.XBOXController;
			}

			if (newDevice.Equals(CurrentInputDevice) == false)
			{
				CurrentInputDevice = newDevice;
				Propagator.Publish(PropagatorEvents.OnInputDeviceChanged, newDevice);
			}

			//ForceStopControllerVibration();
		}

		public static void PerformContextualAction(Action action) { action?.Invoke(); }
		public static void PerformContextualAction<T>(Action<T> action, T arg) { action?.Invoke(arg); }
		public static void PerformContextualAction<T>(Action<T[]> action, params T[] args) { action?.Invoke(args); }

		public static void SetEventSystemActiveState(bool state)
		{
			var sys = EventSystem;

			sys.sendNavigationEvents = state;
			sys.enabled = Instance.uiModule.enabled = state;
		}
		#endregion
	}
}