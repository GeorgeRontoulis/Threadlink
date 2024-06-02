namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.StateMachines;
	using Threadlink.Systems;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/States/Locomotion")]
	public sealed class PlayerCharacterLocomotionState : PlayerCharacterState
	{
		private Transform PlayerTransform { get; set; }
		private Transform CameraTransform { get; set; }
		private CharacterController Controller { get; set; }

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();
		[SerializeField] private ParameterPointer<float> turnSpeed = new();
		[SerializeField] private ParameterPointer<float> xzVelocity = new();

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			var player = owner.Owner;

			PlayerTransform = player.Transform;
			CameraTransform = Camera.main.transform;
			Controller = player.Controller;

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
			float x = movementInput.CurrentValue.x;
			float z = movementInput.CurrentValue.y;
			float deltaTime = Chronos.DeltaTime;

			if (Mathf.Abs(x) > Mathf.Epsilon || Mathf.Abs(z) > Mathf.Epsilon)
				TurnToMovementDirection(Move3D(deltaTime), deltaTime);
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
		private Vector3 Move3D(float deltaTime)
		{
			float x = movementInput.CurrentValue.x;
			float z = movementInput.CurrentValue.y;
			Vector3 scalar = new(1, 0, 1);

			var cameraRight = Vector3.Scale(CameraTransform.right, scalar);
			var cameraForward = Vector3.Scale(CameraTransform.forward, scalar);

			var moveDirection = Vector3.Scale(x * cameraRight + z * cameraForward, scalar).normalized;

			Controller.Move(deltaTime * xzVelocity.CurrentValue * moveDirection);

			return moveDirection;
		}
#endif

		private void TurnToMovementDirection(Vector3 direction, float deltaTime)
		{
			if (direction.Equals(Vector3.zero)) return;

			var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
			var turnSpeed = deltaTime * this.turnSpeed.CurrentValue;
			PlayerTransform.rotation = Quaternion.SlerpUnclamped(PlayerTransform.rotation, targetRotation, turnSpeed);
		}
	}
}