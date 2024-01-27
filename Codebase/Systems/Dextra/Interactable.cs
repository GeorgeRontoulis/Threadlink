namespace Threadlink.Systems.Dextra
{
	using Sirenix.OdinInspector;
	using Threadlink.Core;
	using Threadlink.Utilities.Editor;
	using UnityEngine;

	public abstract class Interactable : LinkableEntity
	{
		[ReadOnly][SerializeField] private Collider effectiveRadius = null;

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			if (EditorUtilities.EditorInOrWillChangeToPlaymode) return;

			base.OnValidate();
			this.TrySetAttachedComponent(ref effectiveRadius);
		}
#endif

		public override void Discard()
		{
			UnsubscribeFromInteractAction();
			SetSensorActiveState(false);
			effectiveRadius = null;
			base.Discard();
		}

		public abstract void Interact();

		public virtual void OnDetected() { Dextra.OnInteractButtonPressed += Interact; }
		public virtual void OnSkipped() { UnsubscribeFromInteractAction(); }

		protected virtual void SetSensorActiveState(bool state) { effectiveRadius.enabled = state; }
		protected void UnsubscribeFromInteractAction() { Dextra.OnInteractButtonPressed -= Interact; }
	}

	public abstract class GenericInteractable<Input, Output> : Interactable
	where Input : IInteractionInput
	where Output : IInteractionOutput
	{
		protected abstract IInteractionHandler<Input, Output> InteractionHandler { get; }
		protected Input InteractionInput { get; }

		public override void Interact()
		{
			var output = InteractionHandler.HandleInteraction(InteractionInput);
			OnInteractionHandled(output);
		}

		protected abstract void OnInteractionHandled(Output input);
	}
}