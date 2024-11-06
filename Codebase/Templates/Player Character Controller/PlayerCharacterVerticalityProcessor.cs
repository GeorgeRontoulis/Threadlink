namespace Threadlink.Templates.PlayerCharacterController
{
	using StateMachines;
	using Systems;
	using UnityEngine;
	using Utilities.Events;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Verticality")]
	internal sealed class PlayerCharacterVerticalityProcessor : PlayerCharacterProcessor
	{
		private CharacterController Controller { get; set; }

		[SerializeField] private ParameterPointer<float> yVelocity = new();

		[Space(10)]

		[SerializeField] private float gravityMultiplier = 1f;

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Controller = owner.Owner.Controller;

			yVelocity.PointToInternalReferenceOf(owner);

			base.Initialize(owner);
		}

		protected override Empty Run(Empty _)
		{
			float yVelocity = this.yVelocity.CurrentValue;
			var verticalVelocity = yVelocity * Vector3.up;
			float magnitude = Chronos.DeltaTime * (yVelocity > 0f ? 1 : gravityMultiplier);

			Controller.Move(magnitude * verticalVelocity);

			return default;
		}
	}
}