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
		[SerializeField] protected Collider effectiveRadius = null;

		[Space(10)]

		[SerializeField] protected bool interactOnContact = false;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out effectiveRadius);
		}

		public override Empty Discard(Empty _ = default)
		{
			UnsubscribeFromInteractAction();
			SetSensorActiveState(false);
			effectiveRadius = null;
			return base.Discard(_);
		}

		public abstract Empty Interact(Empty _ = default);

		public virtual void OnDetected()
		{
			if (interactOnContact) Interact();
			else Threadlink.EventBus.OnDextraInteractPressed += Interact;
		}

		public virtual void OnSkipped()
		{
			if (interactOnContact == false) UnsubscribeFromInteractAction();
		}

		public virtual void SetSensorActiveState(bool state) { effectiveRadius.enabled = state; }
		protected void UnsubscribeFromInteractAction() { Threadlink.EventBus.OnDextraInteractPressed -= Interact; }
	}
}