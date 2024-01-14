namespace Threadlink.Systems.Dextra
{
	public static class InteractableUserInterfaceExtensions
	{
		public static void ManageDefaultSubscriptionsOf(this InteractableUserInterface ui, DextraButton[] collection, bool subscribe)
		{
			int length = collection.Length;

			if (subscribe)
			{
				for (int i = 0; i < length; i++) collection[i].OnSelectEvent.CSharpAction += ui.UpdateLastSelectedButton;
			}
			else
			{
				for (int i = 0; i < length; i++) collection[i].OnSelectEvent.CSharpAction -= ui.UpdateLastSelectedButton;
			}
		}

		public static void DiscardButtons(this UserInterface ui, ref DextraButton[] buttons)
		{
			int length = buttons.Length;
			for (int i = 0; i < length; i++) buttons[i].Discard();

			buttons = null;
		}
	}

	public abstract class InteractableUserInterface : UserInterface
	{
		protected DextraButton LastSelectedButton { get; set; }

		/// <summary>
		/// Used by events inside the Editor. May also be called manually.
		/// </summary>
		/// <param name="newSelection">The new selected button.</param>
		protected internal void UpdateLastSelectedButton(DextraButton newSelection)
		{
			LastSelectedButton = newSelection;
		}

		public override void Discard()
		{
			LastSelectedButton = null;

			base.Discard();
		}
	}
}
