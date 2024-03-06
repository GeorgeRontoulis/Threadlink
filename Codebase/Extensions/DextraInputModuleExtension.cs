namespace Threadlink.Extensions.Dextra
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;

	public abstract class DextraInputModuleExtension : LinkableAsset
	{
		public VoidEvent OnInteractButtonPressed => onInteractButtonPressed;

		private VoidEvent onInteractButtonPressed = new();

		public override void Discard()
		{
			onInteractButtonPressed.Discard();
			onInteractButtonPressed = null;
			base.Discard();
		}

		public void Interact() { onInteractButtonPressed.Invoke(); }

		public abstract void SetPlayerInputMapActiveState(bool playerMapState);
	}
}
