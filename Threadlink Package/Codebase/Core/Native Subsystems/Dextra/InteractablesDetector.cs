namespace Threadlink.Core.Subsystems.Dextra
{
	using Core.Subsystems.Propagator;
	using UnityEngine;

	internal sealed class InteractablesDetector : EntityDetector<CapsuleCollider, Interactable>
	{
		public override PropagatorEvents OnEntityDetectedEvent => PropagatorEvents.OnInteractableDetected;
		public override PropagatorEvents OnEntityOutOfRangeEvent => PropagatorEvents.OnInteractableOutOfRange;

		public override void OnEntityDetected(Interactable entity)
		{
			entity.OnDetected();
			base.OnEntityDetected(entity);
		}

		public override void OnEntityOutOfRange(Interactable entity)
		{
			entity.OnOutOfRange();
			base.OnEntityOutOfRange(entity);
		}
	}
}