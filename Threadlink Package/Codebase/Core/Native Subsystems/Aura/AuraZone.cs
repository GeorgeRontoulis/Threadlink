namespace Threadlink.Core.Subsystems.Aura
{
	using UnityEngine;

	public sealed class AuraZone : AuraSpatialEntity
	{
		protected override Vector3 SourcePosition => cachedTransform.position;
	}
}
