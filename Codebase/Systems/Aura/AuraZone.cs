namespace Threadlink.Systems.Aura
{
	using UnityEngine;

	public sealed class AuraZone : AuraSpatialEntity
	{
		protected override Vector3 SourcePosition => selfTransform.position;
	}
}
