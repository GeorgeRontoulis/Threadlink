namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Core.ExtensionMethods;
	using Cysharp.Threading.Tasks;
	using Extensions;
	using Initium;
	using Propagator;
	using System;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.InputSystem;
	using UnityEngine.InputSystem.DualShock;
	using UnityEngine.InputSystem.UI;

	/// <summary>
	/// System responsible for managing user interfaces, input and interactions.
	/// </summary>
	public sealed class Dextra : UnityWeaver<Dextra, UserInterface>, IInitializable, IAddressablesPreloader
	{
		public enum InputDevice { MouseKeyboard, XBOXController, DualSense }
		public enum InputMode { Unresponsive = -1, UI = 0, Player = 1 }

		public static InputMode CurrentInputMode
		{
			set
			{
				var module = CustomInputModule;

				if (module != null) module.InputMode = value;

				Propagator.Publish(PropagatorEvents.OnInputModeChanged, value);
			}
		}

		public static InputDevice CurrentInputDevice { get; private set; }

		private static DextraInputModuleExtension CustomInputModule => Instance.customInputModule;
		private static Gamepad CurrentGamepad => Gamepad.current;
		private static EventSystem EventSystem => Instance.eventSystem;

		[Header("Input:")]
		[SerializeField] private EventSystem eventSystem = null;
		[SerializeField] private InputSystemUIInputModule uiModule = null;
		[SerializeField] private PlayerInput deviceDetector = null;
		[SerializeField] private DextraInputModuleExtension customInputModule = null;

		[Header("UI:")]
		[SerializeField] private UIStateMachine uiStateMachine = null;

		public override void Discard()
		{
			if (uiStateMachine != null) uiStateMachine.Discard();

			deviceDetector.onControlsChanged -= UpdateInputDevice;

			if (customInputModule != null) customInputModule.Discard();

			SeverAll();

			uiStateMachine = null;
			eventSystem = null;
			deviceDetector = null;
			customInputModule = null;

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

			if (customInputModule != null)
			{
				customInputModule = customInputModule.Clone();
				Initium.Boot(customInputModule);
			}
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

			if (sm != null && sm.StackedInterfacesCount > 1) sm.Cancel();
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

			if (syncSelection && sm != null && sm.StackedInterfacesCount > 1 && sm.TopInterface is IInteractableInterface)
			{
				SyncSelection();
			}
			else PublishEmptySelectionEvent();
		}
		#endregion

		#region Input Code:
		public static T GetCustomInputModule<T>() where T : DextraInputModuleExtension
		{
			return CustomInputModule == null ? null : CustomInputModule as T;
		}

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