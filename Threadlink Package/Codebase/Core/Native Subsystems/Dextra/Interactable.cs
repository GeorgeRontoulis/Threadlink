namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Propagator;
	using System;
	using UnityEngine;

#if UNITY_EDITOR
#if THREADLINK_INSPECTOR
	using Utilities.Editor.Attributes;
#elif ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#endif

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

		public override void Discard()
		{
			UnsubscribeFromInteractAction();
			SetSensorActiveState(false);
			effectiveRadius = null;
			base.Discard();
		}

		public abstract void Interact();

		public virtual void OnDetected()
		{
			if (interactOnContact) Interact();
			else Propagator.Subscribe<Action>(PropagatorEvents.OnInteract, Interact);
		}

		public virtual void OnSkipped()
		{
			if (interactOnContact == false) UnsubscribeFromInteractAction();
		}

		public virtual void SetSensorActiveState(bool state) => effectiveRadius.enabled = state;
		protected void UnsubscribeFromInteractAction() => Propagator.Unsubscribe<Action>(PropagatorEvents.OnInteract, Interact);
	}
}