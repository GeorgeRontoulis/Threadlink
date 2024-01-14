namespace Threadlink.Extensions.Dextra
{
	using Threadlink.Core;
	using Threadlink.Utilities.Events;

	public abstract class DextraInputModuleExtension : LinkableAsset
	{
		public event VoidDelegate OnInteractButtonPressed
		{
			add
			{
				if (onInteractButtonPressed == null) onInteractButtonPressed += value;
				else if (onInteractButtonPressed.Contains(value) == false) onInteractButtonPressed += value;
			}
			remove
			{
				onInteractButtonPressed -= value;

				if (onInteractButtonPressed != null && onInteractButtonPressed.GetListenerCount() <= 0)
					onInteractButtonPressed = null;
			}
		}

		private event VoidDelegate onInteractButtonPressed = null;

		public override void Discard()
		{
			onInteractButtonPressed = null;
			base.Discard();
		}

		public void Interact() { onInteractButtonPressed?.Invoke(); }
	}
}
