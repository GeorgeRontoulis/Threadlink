namespace Threadlink.Extensions.Dextra
{
	using Core;
	using System;
	using System.Reflection;
	using Systems;
	using Systems.Dextra;
	using UnityEngine;
	using UnityEngine.InputSystem;
	using Utilities.Events;
	using Context = UnityEngine.InputSystem.InputAction.CallbackContext;

	[Serializable]
	public class DextraAction<T> where T : struct
	{
		public bool HeldDownThisFrame => InputAction != null && InputAction.IsPressed();

		private Action<Context> Handler { get; set; }

		private InputAction InputAction => reference == null ? null : reference.action;

		[SerializeField] private InputActionReference reference = null;

		public void Discard()
		{
			Unsubscribe();
			Handler = null;
		}

		private static void LogNullInputActionWarning(MethodInfo method)
		{
			Dextra.Instance.SystemLog(Scribe.WarningNotif, "Input Action has not been assigned for ", method.Name, "!");
		}

		public void Handle(Action action, bool subscribeInstantly = true)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action);
				if (subscribeInstantly) Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		public void Handle(Action<T> action, bool subscribeInstantly = true)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action, ctx.ReadValue<T>());
				if (subscribeInstantly) Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		public void Handle(ThreadlinkDelegate<Empty, T> action, bool subscribeInstantly = true)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action, ctx.ReadValue<T>());
				if (subscribeInstantly) Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		public void Subscribe() { if (InputAction != null) InputAction.performed += Handler; }
		public void Unsubscribe() { if (InputAction != null) InputAction.performed -= Handler; }
	}

	[Serializable] public sealed class VoidDextraAction : DextraAction<Empty> { }

	public abstract class DextraInputModuleExtension : LinkableAsset, IInitializable
	{
		private static ThreadlinkEventBus EventBus => Threadlink.EventBus;

		public abstract Dextra.InputMode InputMode { set; }

		public VoidEvent OnInteractButtonPressed => EventBus.onDextraInteractPressed;

		[SerializeField] private VoidDextraAction cancelAction = new();
		[SerializeField] private VoidDextraAction pauseAction = new();
		[SerializeField] private VoidDextraAction interactAction = new();

		public override Empty Discard(Empty _ = default)
		{
			interactAction.Discard();
			pauseAction.Discard();
			cancelAction.Discard();

			cancelAction = null;
			interactAction = null;

			return base.Discard(_);
		}

		public virtual void Initialize()
		{
			void Interact() { EventBus.InvokeOnDextraInteractPressedEvent(); }
			void PauseGame() { EventBus.InvokeOnDextraPausePressedEvent(); }

			cancelAction.Handle(Dextra.Cancel);
			pauseAction.Handle(PauseGame);
			interactAction.Handle(Interact);
		}
	}
}
