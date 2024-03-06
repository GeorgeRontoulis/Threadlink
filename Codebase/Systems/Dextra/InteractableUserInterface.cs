namespace Threadlink.Systems.Dextra
{
	using Threadlink.Utilities.Events;

	public static class InteractableUserInterfaceExtensions
	{
		public static void ManageDefaultSubscriptionsOf(this InteractableUserInterface ui, DextraButton[] collection, bool subscribe)
		{
			int length = collection.Length;

			if (subscribe)
			{
				for (int i = 0; i < length; i++) collection[i].OnSelectEvent.CSharpAction.TryAddListener(ui.UpdateLastSelectedButton);
			}
			else
			{
				for (int i = 0; i < length; i++) collection[i].OnSelectEvent.CSharpAction.Remove(ui.UpdateLastSelectedButton);
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
		protected internal VoidOutput UpdateLastSelectedButton(DextraButton newSelection)
		{
			LastSelectedButton = newSelection;
			return default;
		}

		public override void Discard()
		{
			LastSelectedButton = null;

			base.Discard();
		}
	}
}
