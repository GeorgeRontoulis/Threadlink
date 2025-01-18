namespace Threadlink.Core.Subsystems.Dextra
{
	using Core;
	using Propagator;
	using System;
	using UnityEngine;
	using Utilities.Flags;

#if UNITY_EDITOR
#if THREADLINK_INSPECTOR
	using Editor.Attributes;
#elif ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif
#endif

	public enum InteractionOptions : byte
	{
		None = 0,
		InteractOnContact = 1 << 0,
	}

	public abstract class Interactable3D : Interactable
	{
		public override bool ActiveState { get => activeArea.enabled; set => activeArea.enabled = value; }

#if UNITY_EDITOR && (THREADLINK_INSPECTOR || ODIN_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected Collider activeArea = null;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out activeArea);
		}

		protected override void DiscardActiveArea()
		{
			if (activeArea != null)
			{
				activeArea.enabled = false;
				activeArea = null;
			}
		}
	}

	public abstract class Interactable2D : Interactable
	{
		public override bool ActiveState { get => activeArea.enabled; set => activeArea.enabled = value; }

#if UNITY_EDITOR && (THREADLINK_INSPECTOR || ODIN_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] protected Collider2D activeArea = null;

		protected override void Reset()
		{
			base.Reset();
			TryGetComponent(out activeArea);
		}

		protected override void DiscardActiveArea()
		{
			if (activeArea != null)
			{
				activeArea.enabled = false;
				activeArea = null;
			}
		}
	}

	public abstract class Interactable : LinkableBehaviour
	{
		public abstract bool ActiveState { get; set; }
		public string InteractionPrompt => configuration.InteractionPrompt;

		[SerializeField] protected InteractableConfig configuration = null;

		protected abstract void DiscardActiveArea();

		public override void Discard()
		{
			UnsubscribeFromInteraction();
			DiscardActiveArea();

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