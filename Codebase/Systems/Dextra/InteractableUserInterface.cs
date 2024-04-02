namespace Threadlink.Systems.Dextra
{
	using Threadlink.Utilities.Events;

	public abstract class InteractableUserInterface : UserInterface
	{
		protected abstract DextraButton[] Buttons { get; }

		protected DextraButton LastSelectedButton { get; set; }

		public override void Discard()
		{
			LastSelectedButton = null;

			base.Discard();
		}

		public override void Boot()
		{
			int length = Buttons.Length;
			for (int i = 0; i < length; i++) Buttons[i].OnSelect.TryAddListener(UpdateLastSelectedButton);
		}

		public override void Initialize()
		{
			if (Buttons.Length > 0) UpdateLastSelectedButton(Buttons[0]);
		}

		protected internal VoidOutput UpdateLastSelectedButton(DextraButton newSelection)
		{
			LastSelectedButton = newSelection;
			return default;
		}

		protected internal void SelectLastSelectedButton()
		{
			Dextra.SelectUIElement(LastSelectedButton.gameObject);
		}
	}
}
