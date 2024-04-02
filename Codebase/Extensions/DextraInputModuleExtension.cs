namespace Threadlink.Extensions.Dextra
{
	using Threadlink.Core;
	using Threadlink.Systems.Dextra;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	public enum DextraInputMode { UI, Player }

	public abstract class DextraInputModuleExtension : LinkableAsset
	{
		public abstract DextraInputMode InputMode { set; }

		internal VoidEvent OnInteractButtonPressed => onInteractButtonPressed;
		internal VoidEvent OnPauseButtonPressed => onPausePressed;

		private readonly VoidEvent onInteractButtonPressed = new();
		private readonly VoidEvent onPausePressed = new();

		[SerializeField] private ActionHandlerReferencePair<VoidOutput> cancelAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> pauseAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> interactAction = new();

		public override void Discard()
		{
			interactAction?.Discard();
			pauseAction?.Discard();
			cancelAction?.Discard();
			onPausePressed?.Discard();
			onInteractButtonPressed?.Discard();

			cancelAction = null;
			interactAction = null;

			base.Discard();
		}

		public override void Initialize()
		{
			cancelAction?.Handle(Dextra.Cancel);
			pauseAction?.Handle(PauseGame);
			interactAction?.Handle(Interact);
		}

		private void Interact() { onInteractButtonPressed?.Invoke(); }
		private void PauseGame() { onPausePressed?.Invoke(); }
	}
}
