namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/2D")]
	internal sealed class PlayerCharacter2DProcessor : PlayerCharacterProcessor
	{
#if THREADLINK_TEMPLATES_CONTROLLER_2D
		private Transform CharacterTransform { get; set; }
		private float OriginalZ { get; set; }

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			CharacterTransform = owner.Owner.Transform;
			OriginalZ = CharacterTransform.position.z;
			base.Initialize(owner);
		}
#endif
		protected override VoidOutput Run(VoidInput _)
		{
#if THREADLINK_TEMPLATES_CONTROLLER_2D
			Vector3 currentPosition = CharacterTransform.position;
			CharacterTransform.position = new(currentPosition.x, currentPosition.y, OriginalZ);
#endif
			return default;
		}
	}
}