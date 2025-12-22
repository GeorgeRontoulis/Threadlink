namespace Threadlink.Core.NativeSubsystems.Aura
{
    using UnityEngine;

    public class AuraZone : AuraSpatialObject
    {
        protected override Vector3 SourcePosition => cachedTransform.position;
    }
}
