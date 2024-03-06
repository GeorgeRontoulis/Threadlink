namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.StateMachines;
	using Threadlink.Systems;
	using Threadlink.Utilities.Animation;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Animation")]
	public sealed class PlayerCharacterAnimationProcessor : PlayerCharacterProcessor
	{
		private IPlayerCharacter Character { get; set; }

		[SerializeField] private AnimatorHash momentumHash = null;
		[SerializeField] private AnimatorHash yVelocityHash = null;
		[SerializeField] private AnimatorHash xzVelocityHash = null;
		[SerializeField] private AnimatorHash groundedHash = null;

		[Space(10)]

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();
		[SerializeField] private ParameterPointer<float> xzVelocity = new();
		[SerializeField] private ParameterPointer<float> yVelocity = new();

		[Space(10)]

		[SerializeField] private float idleMomentum = 0f;
		[SerializeField] private float walkMomentum = 0.5f;
		[SerializeField] private float jobMomentum = 1f;
		[SerializeField] private float sprintMomentum = 1.5f;

		[Space(10)]

		[SerializeField] private float motionDamping = 0.12f;

		[Tooltip("Use a positive value to gradually update the velocity of the character. " +
		"Set to 0 to disable this feature and update the velocity instantly.")]
		[SerializeField] private float xzVelocitySharpness = 0f;
		[Tooltip("Use a positive value to gradually update the velocity of the character. " +
		"Set to 0 to disable this feature and update the velocity instantly.")]
		[SerializeField] private float yVelocitySharpness = 0f;

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			Character = owner.Owner;

			movementInput.SetUp(owner);
			xzVelocity.SetUp(owner);
			yVelocity.SetUp(owner);

			base.Initialize(owner);
		}

		protected override VoidOutput Run(VoidInput input)
		{
			Animator animator = Character.Animator;
			float deltaTime = Chronos.DeltaTime;
			float absoluteX = Mathf.Abs(movementInput.CurrentValue.x);
			float momentum;

			if (Character.IsSprinting) momentum = sprintMomentum;
			else if (absoluteX > Mathf.Epsilon && absoluteX <= walkMomentum) momentum = walkMomentum;
			else if (absoluteX > walkMomentum && absoluteX < sprintMomentum) momentum = jobMomentum;
			else momentum = idleMomentum;

			animator.SetFloat(momentumHash.Value, momentum, motionDamping, deltaTime);

			float tempXZVelocity = animator.GetFloat(xzVelocityHash.Value);
			float tempYVelocity = animator.GetFloat(yVelocityHash.Value);

			if (xzVelocitySharpness > Mathf.Epsilon)
			{
				xzVelocity.CurrentValue = Mathf.MoveTowards(xzVelocity.CurrentValue, tempXZVelocity, deltaTime * xzVelocitySharpness);
			}
			else xzVelocity.CurrentValue = tempXZVelocity;

			if (yVelocitySharpness > Mathf.Epsilon)
			{
				yVelocity.CurrentValue = Mathf.MoveTowards(yVelocity.CurrentValue, tempYVelocity, deltaTime * yVelocitySharpness);
			}
			else yVelocity.CurrentValue = tempYVelocity;

			animator.SetBool(groundedHash.Value, Character.IsGrounded);
			animator.SetFloat("IdleFidgetState", Character.IsStandingOnEdge ? 2 : 0, 0.2f, deltaTime);

			return default;
		}
	}
}