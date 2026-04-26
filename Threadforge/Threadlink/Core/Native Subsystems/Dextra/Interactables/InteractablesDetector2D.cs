namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Shared;
    using UnityEngine;

    public class InteractablesDetector2D : EntityDetector2D<CapsuleCollider2D, Interactable2D>
    {
        protected internal override ThreadlinkIDs.Iris.Events OnEntityDetectedEvent => ThreadlinkIDs.Iris.Events.OnInteractableDetected;
        protected internal override ThreadlinkIDs.Iris.Events OnEntityOutOfRangeEvent => ThreadlinkIDs.Iris.Events.OnInteractableOutOfRange;

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