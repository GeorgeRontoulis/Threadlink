namespace Threadlink.Systems.Dextra
{
	using Threadlink.Utilities.Events;

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
