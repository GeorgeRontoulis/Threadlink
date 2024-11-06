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

		[SerializeField] private bool solveWhileMoving = true;

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Character = owner.Owner;
			Grounder = Character.Controller.GetComponent<Grounder>();

			base.Initialize(owner);
		}
#endif

		protected override Empty Run(Empty _)
		{
#if THREADLINK_INTEGRATIONS_FINALIK
			var flags = Character.CurrentStateFlags;

			if (flags.HasFlag(IPlayerCharacter.StateFlags.IsGrounded) == false
			|| (solveWhileMoving == false && flags.HasFlag(IPlayerCharacter.StateFlags.IsMoving))) Grounder.weight = 0f;
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