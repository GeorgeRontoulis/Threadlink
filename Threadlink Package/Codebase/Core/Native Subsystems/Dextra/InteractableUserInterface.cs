namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Cysharp.Threading.Tasks;

	public abstract class InteractableUserInterface<S, T> : UserInterface<S>, IInteractableInterface<T>, IInitializable
	where S : InteractableUserInterface<S, T>
	where T : DextraButton
	{
		public abstract T[] Buttons { get; }
		public T LastSelectedButton { get; set; }

		public override void Discard()
		{
			var buttons = Buttons;

			if (buttons != null)
			{
				int length = buttons.Length;

				for (int i = 0; i < length; i++)
				{
					ref var button = ref buttons[i];

					button.Discard();
					button = null;
				}
			}

			LastSelectedButton = null;
			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();

			var buttons = Buttons;

			if (buttons != null)
			{
				int length = buttons.Length;
				for (int i = 0; i < length; i++) buttons[i].OnSelect += UpdateLastSelectedButton;
			}
		}

		public virtual void Initialize()
		{
			var buttons = Buttons;
			if (buttons != null && buttons.Length > 0) UpdateLastSelectedButton(buttons[0]);
		}

		protected internal void UpdateLastSelectedButton(DextraButton newSelection) => LastSelectedButton = newSelection as T;

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