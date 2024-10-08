namespace Threadlink.Systems.Dextra
{
	using Core;
	using Extensions.Dextra;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.InputSystem;
	using UnityEngine.InputSystem.DualShock;
	using Utilities.Events;
	using Context = UnityEngine.InputSystem.InputAction.CallbackContext;
	using VoidDelegate = Utilities.Events.ThreadlinkDelegate<Utilities.Events.VoidOutput, Utilities.Events.VoidInput>;

#if ODIN_INSPECTOR
	using Threadlink.Utilities.Addressables;
	using Cysharp.Threading.Tasks;
#endif

	[Flags]
	public enum UIStackingCallbackSettings
	{
		Default = HideGraphicsOnCover | DisableInteractabilityOnCover,
		HideGraphicsOnCover = 1 << 0,
		DisableInteractabilityOnCover = 1 << 1,
		EnableInteractabilityOnStack = 1 << 2,
		CanBeCancelled = 1 << 3,
	}

	public interface IScriptableStackingData { }
	public interface IScriptableStackingDataProcessor<T> where T : IScriptableStackingData
	{
		public void Process(T data);
	}

	[Serializable]
	public class DextraAction<T> where T : struct
	{
		public bool HeldDownThisFrame => InputAction != null && InputAction.IsPressed();

		private Action<Context> Handler { get; set; }

		private InputAction InputAction => reference == null ? null : reference.action;

		[SerializeField] private InputActionReference reference = null;

		public void Discard()
		{
			if (InputAction != null) InputAction.performed -= Handler;
			Handler = null;
		}

		private static void LogNullInputActionWarning(MethodInfo method)
		{
			Scribe.SystemLog(Dextra.Instance.LinkID, Scribe.WarningNotif,
			"Input Action has not been assigned for ", method.Name, "!");
		}

		public void Handle(Action action)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action);
				Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		public void Handle(Action<T> action)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action, ctx.ReadValue<T>());
				Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		private void Subscribe() { InputAction.performed += Handler; }
	}

	[Serializable] public sealed class VoidDextraAction : DextraAction<VoidOutput> { }

	/// <summary>
	/// System responsible for managing user interfaces, input and interactions (Input, UI, Interactables).
	/// </summary>
	public sealed class Dextra : UnitySystem<Dextra, UserInterface>, IAssetPreloader
	{
		public enum InputDevice { MouseKeyboard, XBOXController, DualSense }
		public enum InputMode { Invalid = -1, UI = 0, Player = 1 }

		public static VoidGenericEvent<RectTransform> OnElementSelected => Instance.onElementSelected;
		public static VoidGenericEvent<InputDevice> OnInputDeviceChanged => Instance.onInputDeviceChanged;
		public static VoidGenericEvent<InputMode> OnInputModeChanged => Instance.onInputModeChanged;
		public static InputMode CurrentInputMode
		{
			set
			{
				var module = CustomInputModule;

				if (module != null) module.InputMode = value;
				Instance.onInputModeChanged?.Invoke(value);
			}
		}

		public static event VoidDelegate OnInteractButtonPressed
		{
			add { if (CustomInputModule != null) CustomInputModule.OnInteractButtonPressed?.TryAddListener(value); }
			remove { if (CustomInputModule != null) CustomInputModule.OnInteractButtonPressed?.Remove(value); }
		}

		public static event VoidDelegate OnPauseButtonPressed
		{
			add { if (CustomInputModule != null) CustomInputModule.OnPauseButtonPressed?.TryAddListener(value); }
			remove { if (CustomInputModule != null) CustomInputModule.OnPauseButtonPressed?.Remove(value); }
		}

		public static event VoidDelegate OnInterfaceCancelled
		{
			add { Instance.onInterfaceCancelled.TryAddListener(value); }
			remove { Instance.onInterfaceCancelled.Remove(value); }
		}

		internal static UserInterface TopInterface => StackedInterfaces.Count <= 0 ? null : StackedInterfaces.Peek();
		internal static InputDevice CurrentInputDevice { get; private set; }

		private static Stack<UserInterface> StackedInterfaces { get; set; }
		private static DextraInputModuleExtension CustomInputModule => Instance.customInputModule;
		private static Gamepad CurrentGamepad => Gamepad.current;
		private static EventSystem EventSystem => Instance.eventSystem;

		[SerializeField] private EventSystem eventSystem = null;
		[SerializeField] private PlayerInput deviceDetector = null;
		[SerializeField] private DextraInputModuleExtension customInputModule = null;

		[Space(10)]

		[SerializeField] private AddressablePrefab<UserInterface>[] userInterfaceReferences = new AddressablePrefab<UserInterface>[0];

		[NonSerialized] private VoidGenericEvent<RectTransform> onElementSelected = new();
		[NonSerialized] private VoidGenericEvent<InputDevice> onInputDeviceChanged = new();
		[NonSerialized] private VoidGenericEvent<InputMode> onInputModeChanged = new();
		[NonSerialized] private VoidEvent onInterfaceCancelled = new();

		public override VoidOutput Discard(VoidInput _ = default)
		{
			deviceDetector.onControlsChanged -= UpdateInputDevice;

			onInputModeChanged?.Discard();
			onInputDeviceChanged?.Discard();
			onElementSelected?.Discard();
			onInterfaceCancelled?.Discard();

			if (customInputModule != null) customInputModule.Discard();

			SeverAll();

			int length = userInterfaceReferences.Length;
			for (int i = 0; i < length; i++) userInterfaceReferences[i].Unload();

			userInterfaceReferences = null;
			eventSystem = null;
			deviceDetector = null;
			customInputModule = null;
			Instance = null;
			onInputModeChanged = null;
			onInputDeviceChanged = null;
			onElementSelected = null;
			onInterfaceCancelled = null;
			return base.Discard(_);
		}

		public async UniTask PreloadAssetsAsync()
		{
			int length = userInterfaceReferences.Length;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = userInterfaceReferences[i].LoadAsync();

			await UniTask.WhenAll(tasks);
		}

		public override void Boot()
		{
			base.Boot();
			StackedInterfaces = new();

			deviceDetector.onControlsChanged += UpdateInputDevice;

			if (customInputModule != null)
			{
				customInputModule = customInputModule.Clone();
				customInputModule.Boot();
			}
		}

		public override void Initialize()
		{
			var interfaces = new UserInterface[userInterfaceReferences.Length];
			int length = interfaces.Length;

			for (int i = 0; i < length; i++)
			{
				var ui = Weave(new(userInterfaceReferences[i].Result));

				ui.Boot();
				interfaces[i] = ui;
			}

			for (int i = 0; i < length; i++) interfaces[i].Initialize();

			if (customInputModule != null) customInputModule.Initialize();
		}

		#region Interaction Code:
		public static async UniTaskVoid Cancel()
		{
			if (TopInterface != null && TopInterface.CanBeCancelled)
			{
				var poppedInterface = await PopTopInterface();

				if (poppedInterface != null)
				{
					poppedInterface.OnCancelled();
					Instance.onInterfaceCancelled?.Invoke();
				}
			}
		}

		public static T GetCustomInputModule<T>() where T : DextraInputModuleExtension
		{
			return CustomInputModule == null ? null : CustomInputModule as T;
		}
		#endregion

		#region UI Code:

		private static void LogFailedInterfaceSearch()
		{
			Scribe.SystemLog<ArgumentException>(Instance.LinkID,
			"Could not find the requested managed interface! This should never happen!");
		}

		private static void LogInterfaceAlreadyAtTopWarning()
		{
			Scribe.SystemLog(Instance.LinkID, Scribe.WarningNotif, "The requested interface to stack is already at the top!");
		}

		public static void StackInterface(string interfaceID)
		{
			Instance.FindManagedEntity(interfaceID, out var target);

			if (target == null) LogFailedInterfaceSearch();
			else StackInterface(target);
		}

		public static void StackInterface<T>(string interfaceID, T stackingData)
		where T : IScriptableStackingData
		{
			Instance.FindManagedEntity(interfaceID, out var target);

			if (target == null) LogFailedInterfaceSearch();
			else StackInterface(target, stackingData);
		}

		public static void StackInterface(UserInterface target)
		{
			if (target.Equals(TopInterface))
			{
				LogInterfaceAlreadyAtTopWarning();
				return;
			}

			if (TopInterface != null) TopInterface.OnCovered();

			StackedInterfaces.Push(target);
			target.OnStacked();
		}

		public static void StackInterface<T>(UserInterface target, T stackingData)
		where T : IScriptableStackingData
		{
			if (target.Equals(TopInterface))
			{
				LogInterfaceAlreadyAtTopWarning();
				return;
			}

			if (TopInterface != null) TopInterface.OnCovered();

			StackedInterfaces.Push(target);

			try
			{
				(target as IScriptableStackingDataProcessor<T>).Process(stackingData);
			}
			catch (Exception exception)
			{
				Scribe.LogError<InvalidOperationException>(exception.Message);
			}

			target.OnStacked();
		}

		public static async UniTask<UserInterface> PopTopInterface()
		{
			if (TopInterface != null && TopInterface.UpdatingAlpha == false)
			{
				await SelectUIElement(null);

				var previous = StackedInterfaces.Pop();

				previous.OnPopped();

				if (TopInterface != null) TopInterface.OnResurfaced();

				return previous;
			}
			else return null;
		}

		public static void SyncSelection()
		{
			var selectedObject = EventSystem.currentSelectedGameObject;

			if (selectedObject != null) Instance.onElementSelected?.Invoke(selectedObject.transform as RectTransform);
		}

		public static async UniTask SelectUIElement(GameObject element, bool syncSelection = true)
		{
			static void Select(GameObject selectable) { EventSystem.SetSelectedGameObject(selectable); }
			static void InvokeSelectionEvent() { Instance.onElementSelected?.Invoke(default); }

			var eventSysGO = EventSystem.gameObject;

			await UniTask.NextFrame();

			if (element != null)
			{
				Select(null);
				eventSysGO.SetActive(false);
				await UniTask.NextFrame();
				eventSysGO.SetActive(true);
				Select(element);

				await UniTask.NextFrame();

				if (syncSelection) SyncSelection();
				else InvokeSelectionEvent();
			}
			else
			{
				Select(null);
				InvokeSelectionEvent();
			}

			return;
		}
		#endregion

		#region Input Code:

		private static void UpdateInputDevice(PlayerInput input)
		{
			InputDevice newDevice = 0;
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

			CurrentInputDevice = newDevice;
			Instance.onInputDeviceChanged?.Invoke(newDevice);

			//ForceStopControllerVibration();
		}

		public static void PerformContextualAction(VoidDelegate action) { action?.Invoke(default); }
		public static void PerformContextualAction(Action action) { action(); }
		public static void PerformContextualAction<T>(Action<T> action, T arg) { action(arg); }
		public static void PerformContextualAction<T>(Action<T[]> action, params T[] args) { action(args); }
		public static void SetEventSystemActiveState(bool state) { EventSystem.gameObject.SetActive(state); }
		#endregion
	}
}