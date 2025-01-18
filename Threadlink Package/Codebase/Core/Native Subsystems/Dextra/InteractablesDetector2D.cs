namespace Threadlink.Core.Subsystems.Dextra
{
	using Propagator;
	using UnityEngine;

	internal sealed class InteractablesDetector2D : EntityDetector2D<CapsuleCollider2D, Interactable2D>
	{
		public override PropagatorEvents OnEntityDetectedEvent => PropagatorEvents.OnInteractableDetected;
		public override PropagatorEvents OnEntityOutOfRangeEvent => PropagatorEvents.OnInteractableOutOfRange;

		public override void OnEntityDetected(Interactable2D entity)
		{
			entity.OnDetected();
			base.OnEntityDetected(entity);
		}

		public override void OnEntityOutOfRange(Interactable2D entity)
		{
			entity.OnOutOfRange();
			base.OnEntityOutOfRange(entity);
		}
	}
}