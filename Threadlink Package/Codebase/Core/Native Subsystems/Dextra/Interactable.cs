namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Propagator;
	using System;
	using UnityEngine;
	using Utilities.Flags;

#if UNITY_EDITOR
#if THREADLINK_INSPECTOR
	using Utilities.Editor.Attributes;
#elif ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#endif

	public abstract class Interactable : LinkableBehaviour
	{
		public enum InteractionOptions : byte
		{
			None = 0,
			InteractOnContact = 1 << 0,
		}

		public bool ActiveState { get => activeArea.enabled; set => activeArea.enabled = value; }
		public string InteractionPrompt => configuration.InteractionPrompt;

		[SerializeField] protected InteractableConfig configuration = null;

#if UNITY_EDITOR && (THREADLINK_INSPECTOR || ODIN_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected Collider activeArea = null;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out activeArea);
		}

		public override void Discard()
		{
			UnsubscribeFromInteraction();

			if (activeArea != null)
			{
				activeArea.enabled = false;
				activeArea = null;
			}

			base.Discard();
		}

		/// <summary>
		/// Fire this interactable's logic.
		/// </summary>
		/// <returns><see langword="true"/> if the interaction happened. <see langword="false"/> otherwise.</returns>
		public abstract bool Interact();

		public virtual void OnDetected()
		{
			Propagator.Publish(PropagatorEvents.OnInteractableDetected, this);

			if (configuration.InteractionOptions.HasFlagUnsafe(InteractionOptions.InteractOnContact)) Interact();
			else Propagator.Subscribe<Func<bool>>(PropagatorEvents.OnInteract, Interact);
		}

		public virtual void OnOutOfRange()
		{
			UnsubscribeFromInteraction();
			PublishOnOutOfRangeEvent();
		}

		protected void UnsubscribeFromInteraction() => Propagator.Unsubscribe<Func<bool>>(PropagatorEvents.OnInteract, Interact);
		protected void PublishOnOutOfRangeEvent() => Propagator.Publish(PropagatorEvents.OnInteractableOutOfRange, this);
	}
}