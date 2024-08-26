namespace Threadlink.Templates.PlayerCharacterController
{
#if THREADLINK_INTEGRATIONS_FINALIK
	using RootMotion.FinalIK;
	using Systems;
#endif
	using Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/IK")]
	internal sealed class PlayerCharacterIKProcessor : PlayerCharacterProcessor
	{
#if THREADLINK_INTEGRATIONS_FINALIK
		private IPlayerCharacter Character { get; set; }
		private Grounder Grounder { get; set; }

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Character = owner.Owner;
			Grounder = Character.Controller.GetComponent<Grounder>();

			base.Initialize(owner);
		}
#endif

		protected override VoidOutput Run(VoidInput _)
		{
#if THREADLINK_INTEGRATIONS_FINALIK
			if (Character.IsGrounded == false || Character.IsMoving) Grounder.weight = 0f;
			else
			{
				float deltaTime = Chronos.DeltaTime;
				Grounder.weight = Mathf.MoveTowards(Grounder.weight, 1, deltaTime + deltaTime);
			}
#endif
			return default;
		}
	}
}