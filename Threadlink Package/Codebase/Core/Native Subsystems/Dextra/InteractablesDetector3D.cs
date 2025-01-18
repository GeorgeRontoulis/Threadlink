namespace Threadlink.Core.Subsystems.Dextra
{
	using Propagator;
	using UnityEngine;

	internal sealed class InteractablesDetector3D : EntityDetector3D<CapsuleCollider, Interactable3D>
	{
		public override PropagatorEvents OnEntityDetectedEvent => PropagatorEvents.OnInteractableDetected;
		public override PropagatorEvents OnEntityOutOfRangeEvent => PropagatorEvents.OnInteractableOutOfRange;

		public override void OnEntityDetected(Interactable3D entity)
		{
			entity.OnDetected();
			base.OnEntityDetected(entity);
		}

		public override void OnEntityOutOfRange(Interactable3D entity)
		{
			entity.OnOutOfRange();
			base.OnEntityOutOfRange(entity);
		}
	}
}