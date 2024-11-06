namespace Threadlink.Systems.Dextra
{
	using Core;
	using Utilities.Events;
	using UnityEngine;

	public abstract class UserInterface : LinkableBehaviour
	{
		public bool IsOnTop => Dextra.IsTopInterface(this);
		public bool IsVisible => Mathf.Approximately(canvasGroup.alpha, 1f);
		public bool IsHidden => Mathf.Approximately(canvasGroup.alpha, 0f);
		public bool UpdatingAlpha { get; private set; }
		private float TargetAlpha { get; set; }

		[SerializeField] private StackingFeatures stackingFeatures = StackingFeatures.Default;

		[Space(10)]

		[SerializeField] private CanvasGroup canvasGroup = null;

		public override Empty Discard(Empty _ = default)
		{
			Iris.OnUpdate -= MoveTowardsTargetAlpha;
			canvasGroup = null;
			return base.Discard(_);
		}

		private void UpdateAlpha(float newAlpha)
		{
			TargetAlpha = newAlpha;
			UpdatingAlpha = true;
			Iris.OnUpdate += MoveTowardsTargetAlpha;
		}

		private Empty MoveTowardsTargetAlpha(Empty _ = default)
		{
			canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, TargetAlpha, 4 * Chronos.UnscaledDeltaTime);

			if (Mathf.Approximately(canvasGroup.alpha, TargetAlpha))
			{
				Iris.OnUpdate -= MoveTowardsTargetAlpha;
				canvasGroup.alpha = TargetAlpha;
				UpdatingAlpha = false;
			}

			return default;
		}

		protected void SetInteractableState(bool state)
		{
			canvasGroup.interactable = state;
			canvasGroup.blocksRaycasts = state;
		}

		protected void Display() { UpdateAlpha(1f); }
		protected void Hide() { UpdateAlpha(0f); }

		protected void ForceAlphaTo(float alpha)
		{
			UpdatingAlpha = true;
			TargetAlpha = alpha;
			canvasGroup.alpha = alpha;
			UpdatingAlpha = false;
		}

		public virtual void OnStacked()
		{
			Display();
			if (stackingFeatures.HasFlag(StackingFeatures.IsInteractableWhenOnTop)) SetInteractableState(true);
		}

		public virtual void OnCovered()
		{
			if (stackingFeatures.HasFlag(StackingFeatures.HideOnCover)) Hide();

			if (stackingFeatures.HasFlag(StackingFeatures.IsUninteractableWhenCovered)) SetInteractableState(false);
		}

		public virtual void OnResurfaced()
		{
			Display();
			if (stackingFeatures.HasFlag(StackingFeatures.IsInteractableWhenOnTop)) SetInteractableState(true);
		}

		public virtual void OnPopped()
		{
			Hide();
			SetInteractableState(false);
		}
	}

	public abstract class UserInterface<S> : UserInterface, IThreadlinkSingleton<S>
	where S : UserInterface<S>
	{
		public static S Instance { get; private set; }

		public virtual void Boot() { Instance = this as S; }
	}
}