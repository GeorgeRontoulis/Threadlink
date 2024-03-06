namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.StateMachines;
	using Threadlink.Systems;
	using Threadlink.Utilities.Events;
	using UnityEngine;

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

			yVelocity.SetUp(owner);

			base.Initialize(owner);
		}

		protected override VoidOutput Run(VoidInput input)
		{
			float yVelocity = this.yVelocity.CurrentValue;
			Vector3 verticalVelocity = Vector3.up * yVelocity;
			float magnitude = Chronos.DeltaTime * (yVelocity > 0f ? 1 : gravityMultiplier);

			Controller.Move(verticalVelocity * magnitude);

			return default;
		}
	}
}