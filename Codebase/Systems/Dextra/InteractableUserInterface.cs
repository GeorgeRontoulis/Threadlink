namespace Threadlink.Systems.Dextra
{
	using Cysharp.Threading.Tasks;
	using Threadlink.Utilities.Events;

	public abstract class InteractableUserInterface<T> : UserInterface where T : DextraButton
	{
		protected abstract T[] Buttons { get; }

		protected T LastSelectedButton { get; set; }

		public override VoidOutput Discard(VoidInput _ = default)
		{
			var buttons = Buttons;

			if (buttons != null)
			{
				int length = buttons.Length;
				for (int i = 0; i < length; i++) buttons[i].Discard();
			}

			LastSelectedButton = null;
			return base.Discard(_);
		}

		public override void Boot()
		{
			var buttons = Buttons;

			if (buttons != null)
			{
				int length = buttons.Length;
				for (int i = 0; i < length; i++) buttons[i].OnSelect.TryAddListener(UpdateLastSelectedButton);
			}
		}

		public override void Initialize()
		{
			var buttons = Buttons;
			if (buttons != null && buttons.Length > 0) UpdateLastSelectedButton(buttons[0]);
		}

		protected internal VoidOutput UpdateLastSelectedButton(DextraButton newSelection)
		{
			LastSelectedButton = newSelection as T;
			return default;
		}

		protected internal virtual void SelectLastSelectedButton()
		{
			Dextra.SelectUIElement(LastSelectedButton.gameObject).Forget();
		}

		public override void OnStacked()
		{
			base.OnStacked();
			SelectLastSelectedButton();
		}

		public override void OnResurfaced()
		{
			base.OnResurfaced();
			SelectLastSelectedButton();
		}
	}
}
