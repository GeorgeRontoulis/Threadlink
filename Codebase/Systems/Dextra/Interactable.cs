namespace Threadlink.Systems.Dextra
{
	using Threadlink.Core;
	using Threadlink.Utilities.Editor.Attributes;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	public abstract class Interactable : LinkableBehaviour
	{
		[ReadOnly][SerializeField] private Collider effectiveRadius = null;
		[SerializeField] private bool interactOnContact = false;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out effectiveRadius);
		}

		public override void Discard()
		{
			UnsubscribeFromInteractAction();
			SetSensorActiveState(false);
			effectiveRadius = null;
			base.Discard();
		}

		public abstract VoidOutput Interact(VoidInput _ = default);

		public virtual void OnDetected()
		{
			if (interactOnContact) Interact();
			else Dextra.OnInteractButtonPressed += Interact;
		}

		public virtual void OnSkipped()
		{
			if (interactOnContact == false) UnsubscribeFromInteractAction();
		}

		protected virtual void SetSensorActiveState(bool state) { effectiveRadius.enabled = state; }
		protected void UnsubscribeFromInteractAction() { Dextra.OnInteractButtonPressed -= Interact; }
	}
}