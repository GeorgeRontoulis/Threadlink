namespace Threadlink.Templates.PlayerCharacterController
{
	using StateMachines;
	using Systems;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/States/Locomotion")]
	public sealed class PlayerCharacterLocomotionState : PlayerCharacterState
	{
		private Transform PlayerTransform { get; set; }
		private Transform CameraTransform { get; set; }
		private CharacterController Controller { get; set; }
		private Animator Animator { get; set; }

		private Vector3 Scalar { get; set; }
		private Vector3 UpVector { get; set; }
		private Vector3 ZeroVector { get; set; }

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();
		[SerializeField] private ParameterPointer<float> turnSpeed = new();
		[SerializeField] private ParameterPointer<float> xzVelocity = new();

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			var character = owner.Owner;

			Animator = character.Animator;
			PlayerTransform = character.Transform;
			CameraTransform = Camera.main.transform;
			Controller = character.Controller;
			Scalar = new(1, 0, 1);
			UpVector = Vector3.up;
			ZeroVector = Vector3.zero;

			movementInput.PointToInternalReferenceOf(owner);
			turnSpeed.PointToInternalReferenceOf(owner);
			xzVelocity.PointToInternalReferenceOf(owner);
		}

		public override void OnEnter()
		{
			movementInput.CurrentValue = Vector2.zero;
		}

		public override void OnUpdate()
		{
#if THREADLINK_TEMPLATES_CONTROLLER_2D

			float absoluteX = Mathf.Abs(movementInput.CurrentValue.x);

			float deltaTime = Chronos.DeltaTime;

			if (absoluteX > Mathf.Epsilon) TurnToMovementDirection(Move2D(deltaTime), deltaTime);
			else //Ensure that the player's look direction perfectly aligns with the X Axis.
			{
				Vector3 lookDirection = PlayerTransform.forward;

				float dotRight = Vector3.Dot(lookDirection, Vector3.right);
				float dotLeft = Vector3.Dot(lookDirection, -Vector3.right);

				TurnToMovementDirection(dotRight > dotLeft ? Vector3.right : -Vector3.right, deltaTime);
			}

#elif THREADLINK_TEMPLATES_CONTROLLER_3D
			float deltaTime = Chronos.DeltaTime;
			var isPlanted = Animator.GetCurrentAnimatorStateInfo(0).IsTag("Plant");
			TurnToMovementDirection(Move3D(deltaTime, isPlanted), deltaTime, isPlanted);
#endif
		}

		public override void OnExit()
		{
			movementInput.CurrentValue = Vector2.zero;
		}

#if THREADLINK_TEMPLATES_CONTROLLER_2D
		private Vector3 Move2D(float deltaTime)
		{
			var cameraRight = Vector3.Scale(CameraTransform.right, new(1, 0, 1));
			var moveDirection = movementInput.CurrentValue.x * cameraRight;

			moveDirection = Vector3.Scale(moveDirection, new(1, 0, 0)).normalized;

			Controller.Move(deltaTime * xzVelocity.CurrentValue * moveDirection);

			return moveDirection;
		}
#elif THREADLINK_TEMPLATES_CONTROLLER_3D
		private Vector3 Move3D(float deltaTime, bool isPlanted)
		{
			var input = movementInput.CurrentValue;
			float x = input.x;
			float z = input.y;
			float animationVelocity = xzVelocity.CurrentValue;

			var cameraRelativeInputDirection = Vector3.Scale(x * CameraTransform.right +
			z * CameraTransform.forward, Scalar).normalized;

			Vector3 desiredDirection;

			if (Mathf.Approximately(Mathf.Clamp01(input.magnitude), 0f) || isPlanted)
			{
				if (animationVelocity > 0f)
					desiredDirection = animationVelocity * PlayerTransform.forward;
				else
					desiredDirection = ZeroVector;
			}
			else desiredDirection = animationVelocity * cameraRelativeInputDirection;

			Controller.Move(deltaTime * desiredDirection);

			return desiredDirection;
		}
#endif

		private void TurnToMovementDirection(Vector3 direction, float deltaTime, bool isPlanted)
		{
			if (isPlanted || xzVelocity.CurrentValue < Mathf.Epsilon
			|| Mathf.Approximately(Vector3.Angle(direction.normalized, UpVector), 0f)) return;

			var targetRotation = Quaternion.LookRotation(direction.normalized, UpVector);
			var turnSpeed = deltaTime * this.turnSpeed.CurrentValue;
			PlayerTransform.rotation = Quaternion.SlerpUnclamped(PlayerTransform.rotation, targetRotation, turnSpeed);
		}
	}
}