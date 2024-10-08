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
		public bool CanBeCancelled => callbackSettings.HasFlag(UIStackingCallbackSettings.CanBeCancelled);
		public bool UpdatingAlpha { get; private set; }

		private float TargetAlpha { get; set; }

		[SerializeField] private UIStackingCallbackSettings callbackSettings = UIStackingCallbackSettings.Default;

		[Space(10)]

		[SerializeField] private CanvasGroup canvasGroup = null;

		public override VoidOutput Discard(VoidInput _ = default)
		{
			Iris.UnsubscribeFromUpdate(MoveTowardsTargetAlpha);
			UpdatingAlpha = false;
			canvasGroup = null;
			return base.Discard(_);
		}

		private void UpdateAlpha()
		{
			UpdatingAlpha = true;
			Iris.SubscribeToUpdate(MoveTowardsTargetAlpha);
		}

		private VoidOutput MoveTowardsTargetAlpha(VoidInput _)
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

		public virtual void OnStacked()
		{
			Display();
			if (callbackSettings.HasFlag(UIStackingCallbackSettings.EnableInteractabilityOnStack)) SetInteractableState(true);
		}

		public virtual void OnCovered()
		{
			if (callbackSettings.HasFlag(UIStackingCallbackSettings.HideGraphicsOnCover)) Hide();

			if (callbackSettings.HasFlag(UIStackingCallbackSettings.DisableInteractabilityOnCover)) SetInteractableState(false);
		}

		public virtual void OnResurfaced()
		{
			Display();
			if (callbackSettings.HasFlag(UIStackingCallbackSettings.EnableInteractabilityOnStack)) SetInteractableState(true);
		}

		public virtual void OnPopped()
		{
			Hide();
			SetInteractableState(false);
		}

		public abstract void OnCancelled();
	}
}