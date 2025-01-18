namespace Threadlink.Core.Subsystems.Dextra
{
	using Scribe;
	using System;
	using System.Reflection;
	using UnityEngine;
	using UnityEngine.InputSystem;
	using Context = UnityEngine.InputSystem.InputAction.CallbackContext;

	[Serializable]
	public sealed class DextraAction : IDiscardable
	{
		public bool HeldDownThisFrame => InputAction != null && InputAction.IsPressed();

		private InputAction InputAction => reference == null ? null : reference.action;
		private Action<Context> Handler { get; set; }

		[SerializeField] private InputActionReference reference = null;

		private static void LogNullInputActionWarning(MethodInfo method)
		{
			Scribe.FromSubsystem<Dextra>("Input Action has not been assigned for ", method.Name, "!").
			ToUnityConsole(Dextra.Instance, Scribe.WARN);
		}

		public void Discard()
		{
			Unsubscribe();
			Handler = null;
			reference = null;
		}

		public void Subscribe() { if (InputAction != null) InputAction.performed += Handler; }
		public void Unsubscribe() { if (InputAction != null) InputAction.performed -= Handler; }

		public void Handle(Action action, bool subscribeInstantly = true)
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action);

				if (subscribeInstantly) Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}

		public void Handle<T>(Action<T> action, bool subscribeInstantly = true) where T : struct
		{
			if (InputAction != null)
			{
				Handler = (Context ctx) => Dextra.PerformContextualAction(action, ctx.ReadValue<T>());

				if (subscribeInstantly) Subscribe();
			}
			else LogNullInputActionWarning(action.Method);
		}
	}
}
