namespace Threadlink.Systems.Dextra
{
	using Extensions.Dextra;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Threadlink.Core;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.InputSystem;
	using UnityEngine.UI;
	using Utilities.Events;
	using Context = UnityEngine.InputSystem.InputAction.CallbackContext;
	using VoidDelegate = Utilities.Events.ThreadlinkDelegate<Utilities.Events.VoidOutput, Utilities.Events.VoidInput>;

	public interface IInputHandler
	{
		void SetUpHandlers();
		void SubscribeInputHandlers();
		void UnsubscribeInputHandlers();
	}

	public struct ControllerVibrationData
	{
		public CustomYieldInstruction waitInstruction;
		public float lowFrequency;
		public float highFrequency;

		public ControllerVibrationData(CustomYieldInstruction waitInstruction, float lowFrequency, float highFrequency)
		{
			this.waitInstruction = waitInstruction;
			this.lowFrequency = lowFrequency;
			this.highFrequency = highFrequency;
		}
	}

	[Serializable]
	public sealed class ActionHandlerReferencePair<T> where T : struct
	{
		public Action<Context> Handler { get; private set; }

		private InputAction InputAction => reference.action;

		[SerializeField] private InputActionReference reference = null;

		public void SetUpHandler(Action action)
		{
			Handler = (Context ctx) => Dextra.PerformContextualAction(action);
		}

		public void SetUpHandler(Action<T> action)
		{
			Handler = (Context ctx) => Dextra.PerformContextualAction(action, ctx.ReadValue<T>());
		}

		public void Subscribe() { InputAction.performed += Handler; }
		public void Unsubscribe() { InputAction.performed -= Handler; }

		public void Dispose()
		{
			Unsubscribe();
			Handler = null;
		}
	}

	/// <summary>
	/// System responsible for managing user interfaces, input and interactions (Input, UI, Interactables).
	/// </summary>
	public sealed class Dextra : ThreadlinkSystem<Dextra, UserInterface>
	{
		public enum InputDevice { MouseKeyboard, XBOXController, DualSense }

		public static VoidGenericEvent<RectTransform> OnElementSelected => Instance.onElementSelected;
		public static VoidGenericEvent<InputDevice> OnInputDeviceChanged => Instance.onInputDeviceChanged;

		public static event VoidDelegate OnInteractButtonPressed
		{
			add { if (CustomInputModule != null) CustomInputModule.OnInteractButtonPressed.TryAddListener(value); }
			remove { if (CustomInputModule != null) CustomInputModule.OnInteractButtonPressed.Remove(value); }
		}

		internal static UserInterface TopInterface => StackedInterfaces.Count <= 0 ? null : StackedInterfaces.Peek();
		internal static InputDevice CurrentInputDevice { get; private set; }

		private static Stack<UserInterface> StackedInterfaces { get; set; }
		private static DextraInputModuleExtension CustomInputModule => Instance.customInputModule;
		private static Gamepad CurrentGamepad => Gamepad.current;
		private static EventSystem EventSystem => Instance.eventSystem;

		private static Coroutine controllerVibration = null;

		[SerializeField] private EventSystem eventSystem = null;
		[SerializeField] private PlayerInput deviceDetector = null;
		[SerializeField] private DextraInputModuleExtension customInputModule = null;

		private readonly VoidGenericEvent<RectTransform> onElementSelected = new();
		private readonly VoidGenericEvent<InputDevice> onInputDeviceChanged = new();

		public override void Discard()
		{
			deviceDetector.onControlsChanged -= UpdateInputDevice;
			if (customInputModule != null) customInputModule.Discard();
			DisconnectAll();

			eventSystem = null;
			deviceDetector = null;
			customInputModule = null;
			Instance = null;

			base.Discard();
		}

		public override void Boot()
		{
			StackedInterfaces = new();
			controllerVibration = null;
			Instance = this;

			deviceDetector.onControlsChanged += UpdateInputDevice;

			if (customInputModule != null)
			{
				customInputModule = Instantiate(customInputModule);
				customInputModule.Boot();
			}

			base.Boot();
		}

		public override void Initialize()
		{
			UserInterface[] interfaces = FindObjectsByType<UserInterface>(FindObjectsSortMode.None);
			int length = interfaces.Length;

			for (int i = 0; i < length; i++) interfaces[i].Boot();

			for (int i = 0; i < length; i++)
			{
				interfaces[i].Initialize();
				Link(interfaces[i]);
			}

			if (customInputModule != null) customInputModule.Initialize();
		}

		#region Interaction Code:
		public static void Cancel()
		{
			if (TopInterface != null && TopInterface.CanBeCancelled)
			{
				var poppedInterface = PopTopInterface();
				poppedInterface.OnCancelled();
			}
		}

		public static T GetCustomInputModule<T>() where T : DextraInputModuleExtension
		{
			return CustomInputModule == null ? null : CustomInputModule as T;
		}
		#endregion

		#region UI Code:
		public static void StackInterface(string interfaceID)
		{
			UserInterface target = Instance.FindManagedEntity(interfaceID);

			if (target == null)
			{
				Scribe.SystemLog(Instance.LinkID, Utilities.UnityLogging.DebugNotificationType.Error,
				"Could not find the requested managed interface! This should never happen!");
			}
			else StackInterface(target);
		}

		public static void StackInterface(UserInterface target)
		{
			if (target.Equals(TopInterface))
			{
				Scribe.SystemLog(Instance.LinkID, Utilities.UnityLogging.DebugNotificationType.Warning,
				"The requested interface to stack is already at the top!");
				return;
			}

			if (TopInterface != null)
			{
				if (TopInterface.UpdatingAlpha) return;

				TopInterface.OnCovered();
			}

			StackedInterfaces.Push(target);
			target.OnStacked();
		}

		public static UserInterface PopTopInterface()
		{
			if (TopInterface != null && TopInterface.UpdatingAlpha == false)
			{
				SelectUIElement(null);

				UserInterface previous = StackedInterfaces.Pop();

				previous.OnPopped();

				if (TopInterface != null) TopInterface.OnResurfaced();

				return previous;
			}
			else return null;
		}

		public static void SyncSelection()
		{
			Instance.onElementSelected.
			Invoke(EventSystem.currentSelectedGameObject.transform as RectTransform);
		}

		public static void SelectUIElement(Selectable element)
		{
			void Select(GameObject selectable) { EventSystem.SetSelectedGameObject(selectable); }

			if (element != null)
			{
				IEnumerator Selection()
				{
					IEnumerator WaitForOneFrame() { yield return Threadlink.WaitForFrameCount(1); }

					GameObject gameObject = element.gameObject;

					yield return WaitForOneFrame();

					Select(null);

					yield return WaitForOneFrame();

					Select(gameObject);
					SyncSelection();
				}

				Threadlink.LaunchCoroutine(Selection(), false);
			}
			else
			{
				Select(null);
				Instance.onElementSelected.Invoke(default);
			}
		}
		#endregion

		#region Input Code:

		private static void UpdateInputDevice(PlayerInput input)
		{
			InputDevice newDevice = 0;
			string currentControlScheme = input.currentControlScheme;

			if (string.IsNullOrEmpty(currentControlScheme)) return;

			if (currentControlScheme.Equals("KeyboardAndMouse"))
			{
				newDevice = InputDevice.MouseKeyboard;
				ForceStopControllerVibration();
			}
			else if (currentControlScheme.Equals("Gamepad"))
			{
				newDevice = InputDevice.XBOXController;
			}

			CurrentInputDevice = newDevice;
			Instance.onInputDeviceChanged.Invoke(newDevice);

			ForceStopControllerVibration();
		}

		public static void PerformContextualAction(Action action) { action(); }
		public static void PerformContextualAction<T>(Action<T> action, T arg) { action(arg); }
		public static void PerformContextualAction<T>(Action<T[]> action, params T[] args) { action(args); }
		public static void SetEventSystemActiveState(bool state) { EventSystem.gameObject.SetActive(state); }

		/// <summary>
		/// Vibrates the currently connected controller using the specified data.
		/// IMPORTANT: Cancels the current vibration if the controller is already vibrating.
		/// Does nothing if no controller is found.
		/// </summary>
		/// <param name="vibrationData">The data used for the vibration.</param>
		public static void VibrateController(ControllerVibrationData vibrationData)
		{
			Gamepad currentGamepad = CurrentGamepad;

			if (currentGamepad != null)
			{
				IEnumerator VibrateForSeconds()
				{
					currentGamepad.SetMotorSpeeds(vibrationData.lowFrequency, vibrationData.highFrequency);

					yield return vibrationData.waitInstruction;

					currentGamepad.ResetHaptics();
					controllerVibration = null;
				}

				Threadlink.StopCoroutine(ref controllerVibration);
				controllerVibration = Threadlink.LaunchCoroutine(VibrateForSeconds(), false);
			}
		}

		public static void VibrateControllerThisFrame(ControllerVibrationData vibrationData)
		{
			Gamepad currentGamepad = CurrentGamepad;

			if (currentGamepad != null)
			{
				currentGamepad.SetMotorSpeeds(vibrationData.lowFrequency, vibrationData.highFrequency);
			}
		}

		public static void ForceStopControllerVibration()
		{
			Gamepad currentGamepad = CurrentGamepad;

			if (controllerVibration != null) Threadlink.StopCoroutine(ref controllerVibration);
			if (currentGamepad != null) currentGamepad.ResetHaptics();
		}
		#endregion
	}
}