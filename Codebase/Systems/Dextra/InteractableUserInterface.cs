namespace Threadlink.Systems.Dextra
{
	using Core;
	using Cysharp.Threading.Tasks;
	using Utilities.Events;

	public abstract class InteractableUserInterface<S, T> : UserInterface<S>, IInteractableInterface<T>, IInitializable
	where S : InteractableUserInterface<S, T>
	where T : DextraButton
	{
		public abstract T[] Buttons { get; }
		public T LastSelectedButton { get; set; }

		public override Empty Discard(Empty _ = default)
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
			return base.Discard(_);
		}

		public override void Boot()
		{
			base.Boot();

			var buttons = Buttons;

			if (buttons != null)
			{
				int length = buttons.Length;
				for (int i = 0; i < length; i++) buttons[i].OnSelect.OnInvoke += UpdateLastSelectedButton;
			}
		}

		public virtual void Initialize()
		{
			var buttons = Buttons;
			if (buttons != null && buttons.Length > 0) UpdateLastSelectedButton(buttons[0]);
		}

		protected internal Empty UpdateLastSelectedButton(DextraButton newSelection)
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
