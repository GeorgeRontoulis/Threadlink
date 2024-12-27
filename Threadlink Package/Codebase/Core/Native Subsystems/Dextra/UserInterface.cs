namespace Threadlink.Core.Subsystems.Dextra
{
	using Chronos;
	using Core;
	using Propagator;
	using System;
	using UnityEngine;
	using Utilities.Flags;

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

		public override void Discard()
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, MoveTowardsTargetAlpha);
			canvasGroup = null;
			base.Discard();
		}

		private void UpdateAlpha(float newAlpha)
		{
			TargetAlpha = newAlpha;
			UpdatingAlpha = true;
			Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, MoveTowardsTargetAlpha);
		}

		private void MoveTowardsTargetAlpha()
		{
			canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, TargetAlpha, 4 * Chronos.UnscaledDeltaTime);

			if (Mathf.Approximately(canvasGroup.alpha, TargetAlpha))
			{
				Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, MoveTowardsTargetAlpha);
				canvasGroup.alpha = TargetAlpha;
				UpdatingAlpha = false;
			}
		}

		protected void SetInteractableState(bool state)
		{
			canvasGroup.interactable = state;
			canvasGroup.blocksRaycasts = state;
		}

		protected void Display() => UpdateAlpha(1f);
		protected void Hide() => UpdateAlpha(0f);

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
			if (stackingFeatures.HasFlagUnsafe(StackingFeatures.IsInteractableWhenOnTop)) SetInteractableState(true);
		}

		public virtual void OnCovered()
		{
			if (stackingFeatures.HasFlagUnsafe(StackingFeatures.HideOnCover)) Hide();

			if (stackingFeatures.HasFlagUnsafe(StackingFeatures.IsUninteractableWhenCovered)) SetInteractableState(false);
		}

		public virtual void OnResurfaced()
		{
			Display();
			if (stackingFeatures.HasFlagUnsafe(StackingFeatures.IsInteractableWhenOnTop)) SetInteractableState(true);
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