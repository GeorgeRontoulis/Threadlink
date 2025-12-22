namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Iris;
    using UnityEngine;

    public class InteractablesDetector3D : EntityDetector3D<CapsuleCollider, Interactable3D>
    {
        protected internal override Iris.Events OnEntityDetectedEvent => Iris.Events.OnInteractableDetected;
        protected internal override Iris.Events OnEntityOutOfRangeEvent => Iris.Events.OnInteractableOutOfRange;

        protected internal override void OnEntityDetected(Interactable3D entity)
        {
            base.OnEntityDetected(entity);
            entity.OnDetected();
        }

        protected internal override void OnEntityOutOfRange(Interactable3D entity)
        {
            base.OnEntityOutOfRange(entity);
            entity.OnOutOfRange();
        }
    }
}