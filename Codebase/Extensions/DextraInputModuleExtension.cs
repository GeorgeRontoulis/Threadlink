namespace Threadlink.Extensions.Dextra
{
	using Core;
	using Systems.Dextra;
	using UnityEngine;
	using Utilities.Events;

	public abstract class DextraInputModuleExtension : LinkableAsset
	{
		public abstract Dextra.InputMode InputMode { set; }

		public VoidEvent OnInteractButtonPressed => onInteractButtonPressed;
		public VoidEvent OnPauseButtonPressed => onPausePressed;

		private VoidEvent onInteractButtonPressed = new();
		private VoidEvent onPausePressed = new();

		[SerializeField] private VoidDextraAction cancelAction = new();
		[SerializeField] private VoidDextraAction pauseAction = new();
		[SerializeField] private VoidDextraAction interactAction = new();

		public override void Discard()
		{
			interactAction?.Discard();
			pauseAction?.Discard();
			cancelAction?.Discard();
			onPausePressed?.Discard();
			onInteractButtonPressed?.Discard();

			cancelAction = null;
			interactAction = null;
			onPausePressed = null;
			onInteractButtonPressed = null;

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
