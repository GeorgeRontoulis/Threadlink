namespace Threadlink.Templates.PlayerCharacterController
{
	using Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Ground Checking")]
	internal sealed class PlayerCharacterGroundingProcessor : PlayerCharacterProcessor
	{
		private Collider[] DetectedColliders { get; set; }
		private IPlayerCharacter Character { get; set; }
		private Vector3 Offset { get; set; }

		[SerializeField] private float groundCheckRadious = 0.15f;
		[SerializeField] private LayerMask groundMask = 0;
		[SerializeField] private QueryTriggerInteraction interaction = QueryTriggerInteraction.Ignore;

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Character = owner.Owner;
			DetectedColliders = new Collider[1];
			Offset = Vector3.Scale(Character.Controller.center, new(1, 0, 1));

			base.Initialize(owner);
		}

		protected override Empty Run(Empty _)
		{
			var checkOrigin = Character.SelfTransform.position + Offset;

			Character.CurrentStateFlags = Physics.OverlapSphereNonAlloc(checkOrigin,
			groundCheckRadious, DetectedColliders, groundMask, interaction) > 0
			?
			Character.CurrentStateFlags | IPlayerCharacter.StateFlags.IsGrounded
			:
			Character.CurrentStateFlags & ~IPlayerCharacter.StateFlags.IsGrounded;

			return default;
		}
	}
}