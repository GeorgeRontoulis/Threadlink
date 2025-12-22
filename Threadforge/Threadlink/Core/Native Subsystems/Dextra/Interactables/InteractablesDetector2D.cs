namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Iris;
    using UnityEngine;

    public class InteractablesDetector2D : EntityDetector2D<CapsuleCollider2D, Interactable2D>
    {
        protected internal override Iris.Events OnEntityDetectedEvent => Iris.Events.OnInteractableDetected;
        protected internal override Iris.Events OnEntityOutOfRangeEvent => Iris.Events.OnInteractableOutOfRange;

        protected internal override void OnEntityDetected(Interactable2D entity)
        {
            base.OnEntityDetected(entity);
            entity.OnDetected();
        }

        protected internal override void OnEntityOutOfRange(Interactable2D entity)
        {
            base.OnEntityOutOfRange(entity);
            entity.OnOutOfRange();
        }
    }
}