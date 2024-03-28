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

		private DextraInputModuleExtension InputModule { get; set; }
		private IPlayerCharacter Character { get; set; }

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();

		[Space(10)]

		[SerializeField] private ActionHandlerReferencePair<Vector2> moveAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> startSprintAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> stopSprintAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> jumpAction = new();
		[SerializeField] private ActionHandlerReferencePair<VoidOutput> attackAction = new();

		[Space(10)]

		private readonly VoidEvent onJumpInput = new();
		private readonly VoidEvent onAttackInput = new();

		public override void Discard()
		{
			Dextra.OnPauseButtonPressed -= StackPauseMenu;
			Chronos.OnGamePaused.Remove(OnGamePaused);
			Chronos.OnGameResumed.Remove(OnGameResumed);

			moveAction.Discard();
			startSprintAction.Discard();
			stopSprintAction.Discard();
			jumpAction.Discard();

			onJumpInput.Discard();
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
			InputModule = Dextra.GetCustomInputModule<DextraInputModuleExtension>();

			movementInput.PointToInternalReferenceOf(owner);

			moveAction.Handle(TrackMovementInput);
			startSprintAction.Handle(StartSprinting);
			stopSprintAction.Handle(StopSprinting);
			jumpAction.Handle(Jump);
			attackAction.Handle(Attack);

			Chronos.OnGamePaused.TryAddListener(OnGamePaused);
			Chronos.OnGameResumed.TryAddListener(OnGameResumed);
			Dextra.OnPauseButtonPressed += StackPauseMenu;

			if (InputModule != null) InputModule.InputMode = DextraInputMode.Player;

			base.Initialize(owner);
		}

		VoidOutput StackPauseMenu(VoidInput input)
		{
			Dextra.StackInterface("OverlayUI_PauseMenu");
			return default;
		}

		private VoidOutput OnGamePaused(VoidInput input)
		{
			if (InputModule != null) InputModule.InputMode = DextraInputMode.UI;
			movementInput.CurrentValue = Vector2.zero;
			return default;
		}

		private VoidOutput OnGameResumed(VoidInput input)
		{
			if (InputModule != null) InputModule.InputMode = DextraInputMode.Player;
			return default;
		}

		protected override VoidOutput Run(VoidInput input)
		{
			if (movementInput.CurrentValue.magnitude <= Mathf.Epsilon) Character.IsSprinting = false;

			return default;
		}
	}
}