namespace Threadlink.Templates.PlayerCharacterController
{
	using System;
	using Threadlink.StateMachines;
	using Threadlink.Systems.Dextra;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Input")]
	public sealed class PlayerCharacterInputProcessor : PlayerCharacterProcessor
	{
		[SerializeField] private ParameterPointer<Vector2> movementInput = new();

		[Space(10)]

		[SerializeField] private ActionHandlerReferencePair<Vector2> moveAction = new();
		[SerializeField] private ActionHandlerReferencePair<bool> startSprintAction = new();
		[SerializeField] private ActionHandlerReferencePair<bool> stopSprintAction = new();
		[SerializeField] private ActionHandlerReferencePair<bool> jumpAction = new();

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			var character = owner.Owner;

			movementInput.SetUp(owner);

			void TrackMovementInput(Vector2 input) { movementInput.CurrentValue = input; }
			void StartSprinting() { if (movementInput.CurrentValue.magnitude > Mathf.Epsilon) character.IsSprinting = true; }
			void StopSprinting() { character.IsSprinting = false; }
			void Jump()
			{
				if (character.IsGrounded) character.Animator.CrossFadeInFixedTime("Jump", 0.12f, 1, 0, 0);
			}

			RegisterInputAction(moveAction, TrackMovementInput);
			RegisterInputAction(startSprintAction, StartSprinting);
			RegisterInputAction(stopSprintAction, StopSprinting);
			RegisterInputAction(jumpAction, Jump);

			base.Initialize(owner);
		}

		protected override VoidOutput Run(VoidInput input)
		{
			return default;
		}

		private void RegisterInputAction<T>(ActionHandlerReferencePair<T> inputAction, Action action)
		where T : struct
		{
			inputAction.SetUpHandler(action);
			inputAction.Subscribe();
		}

		private void RegisterInputAction<T>(ActionHandlerReferencePair<T> inputAction, Action<T> action)
		where T : struct
		{
			inputAction.SetUpHandler(action);
			inputAction.Subscribe();
		}
	}
}