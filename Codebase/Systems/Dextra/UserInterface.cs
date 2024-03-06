namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
	using Threadlink.Systems;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	public abstract class UserInterface : LinkableBehaviour
	{
		public bool IsOnTop => Equals(Dextra.TopInterface);
		public bool IsVisible => Mathf.Approximately(canvasGroup.alpha, 1f);
		public bool CanBeCancelled => canBeCancelled;
		public bool UpdatingAlpha { get; private set; }

		private float TargetAlpha { get; set; }

		[SerializeField] private CanvasGroup canvasGroup = null;
		[SerializeField] private bool canBeCancelled = false;

		public override void Discard()
		{
			Iris.UnsubscribeFromUpdate(MoveTowardsTargetAlpha);
			UpdatingAlpha = false;
			canvasGroup = null;
			base.Discard();
		}

		private void UpdateAlpha()
		{
			UpdatingAlpha = true;
			Iris.SubscribeToUpdate(MoveTowardsTargetAlpha);
		}

		private VoidOutput MoveTowardsTargetAlpha(VoidInput input)
		{
			canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, TargetAlpha, 4 * Chronos.UnscaledDeltaTime);

			if (Mathf.Approximately(canvasGroup.alpha, TargetAlpha))
			{
				Iris.UnsubscribeFromUpdate(MoveTowardsTargetAlpha);
				UpdatingAlpha = false;
			}

			return default;
		}

		protected void SetInteractableState(bool state)
		{
			canvasGroup.interactable = state;
			canvasGroup.blocksRaycasts = state;
		}

		protected void Display()
		{
			TargetAlpha = 1f;
			if (UpdatingAlpha == false) UpdateAlpha();
		}

		protected void Hide()
		{
			TargetAlpha = 0f;
			if (UpdatingAlpha == false) UpdateAlpha();
		}

		public abstract void OnStacked();
		public abstract void OnCovered();
		public abstract void OnResurfaced();
		public abstract void OnPopped();
		public abstract void OnCancelled();
	}
}