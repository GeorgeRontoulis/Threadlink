namespace Threadlink.Templates.PlayerCharacterController
{
	using Threadlink.Extensions.Dextra;
	using Threadlink.StateMachines;
	using Threadlink.Systems;
	using Threadlink.Systems.Dextra;
	using Threadlink.Utilities.Events;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Input")]
	public sealed class PlayerCharacterInputProcessor : PlayerCharacterProcessor
	{
		public VoidEvent OnJumpInput => onJumpInput;
		public VoidEvent OnAttackInput => onAttackInput;

		private IPlayerCharacter Character { get; set; }

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();

		[Space(10)]

		[SerializeField] private DextraAction<Vector2> moveAction = new();
		[SerializeField] private VoidDextraAction startSprintAction = new();
		[SerializeField] private VoidDextraAction stopSprintAction = new();
		[SerializeField] private VoidDextraAction jumpAction = new();
		[SerializeField] private VoidDextraAction attackAction = new();

		[Space(10)]

		private VoidEvent onJumpInput = new();
		private VoidEvent onAttackInput = new();

		public override void Discard()
		{
			Dextra.OnPauseButtonPressed -= StackPauseMenu;
			Chronos.OnGamePaused.Remove(OnGamePaused);
			Chronos.OnGameResumed.Remove(OnGameResumed);

			moveAction.Discard();
			startSprintAction.Discard();
			stopSprintAction.Discard();
			jumpAction.Discard();

			onJumpInput?.Discard();
			onAttackInput?.Discard();

			onJumpInput = null;
			onAttackInput = null;

			base.Discard();
		}

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			void TrackMovementInput(Vector2 input) { movementInput.CurrentValue = input; }
			void StartSprinting() { if (movementInput.CurrentValue.magnitude > Mathf.Epsilon) Character.IsSprinting = true; }
			void StopSprinting() { Character.IsSprinting = false; }
			void Jump() { onJumpInput?.Invoke(); }
			void Attack() { onAttackInput?.Invoke(); }

			Character = owner.Owner;

			movementInput.PointToInternalReferenceOf(owner);

			moveAction?.Handle(TrackMovementInput);
			startSprintAction?.Handle(StartSprinting);
			stopSprintAction?.Handle(StopSprinting);
			jumpAction?.Handle(Jump);
			attackAction?.Handle(Attack);

			Chronos.OnGamePaused.TryAddListener(OnGamePaused);
			Chronos.OnGameResumed.TryAddListener(OnGameResumed);
			Dextra.OnPauseButtonPressed += StackPauseMenu;

			Dextra.GetCustomInputModule<DextraInputModuleExtension>().InputMode = DextraInputMode.Player;

			base.Initialize(owner);
		}

		private VoidOutput StackPauseMenu(VoidInput _)
		{
			Dextra.StackInterface("OverlayUI_PauseMenu");
			return default;
		}

		private VoidOutput OnGamePaused(VoidInput _)
		{
			movementInput.CurrentValue = Vector2.zero;
			return default;
		}

		private VoidOutput OnGameResumed(VoidInput _)
		{
			return default;
		}

		protected override VoidOutput Run(VoidInput _)
		{
			//if (movementInput.CurrentValue.magnitude <= Mathf.Epsilon) Character.IsSprinting = false;

			return default;
		}
	}
}