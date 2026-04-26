namespace Threadlink.Core.NativeSubsystems.Dextra
{
    using Shared;
    using UnityEngine;

    public class InteractablesDetector3D : EntityDetector3D<CapsuleCollider, Interactable3D>
    {
        protected internal override ThreadlinkIDs.Iris.Events OnEntityDetectedEvent => ThreadlinkIDs.Iris.Events.OnInteractableDetected;
        protected internal override ThreadlinkIDs.Iris.Events OnEntityOutOfRangeEvent => ThreadlinkIDs.Iris.Events.OnInteractableOutOfRange;

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