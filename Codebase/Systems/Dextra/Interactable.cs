namespace Threadlink.Systems.Dextra
{
	using Core;
#if UNITY_EDITOR && THREADLINK_INSPECTOR
	using Utilities.Editor.Attributes;
#elif UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
	using Utilities.Events;
	using UnityEngine;

	public abstract class Interactable : LinkableBehaviour
	{
#if UNITY_EDITOR && (THREADLINK_INSPECTOR || ODIN_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private Collider effectiveRadius = null;

		[Space(10)]

		[SerializeField] protected bool interactOnContact = false;

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

		public virtual void SetSensorActiveState(bool state) { effectiveRadius.enabled = state; }
		protected void UnsubscribeFromInteractAction() { Dextra.OnInteractButtonPressed -= Interact; }
	}
}