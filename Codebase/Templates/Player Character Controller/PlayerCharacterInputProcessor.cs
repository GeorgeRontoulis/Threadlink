namespace Threadlink.Templates.PlayerCharacterController
{
	using StateMachines;
	using System;
	using Systems.Dextra;
	using UnityEngine;
	using Utilities.Events;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Input")]
	public sealed class PlayerCharacterInputProcessor : PlayerCharacterProcessor
	{
		public VoidEvent OnJumpInput => onJumpInput;
		public VoidEvent OnAttackInput => onAttackInput;
		public VoidEvent OnStartSprintInput => onStartSprintInput;
		public VoidEvent OnStopSprintInput => onStopSprintInput;

		private IPlayerCharacter Character { get; set; }

		[SerializeField] private ParameterPointer<Vector2> movementInput = new();

		[Space(10)]

		[SerializeField] private DextraAction<Vector2> moveAction = new();
		[SerializeField] private VoidDextraAction startSprintAction = new();
		[SerializeField] private VoidDextraAction stopSprintAction = new();
		[SerializeField] private VoidDextraAction jumpAction = new();
		[SerializeField] private VoidDextraAction attackAction = new();

		[Space(10)]

		[NonSerialized] private VoidEvent onJumpInput = new();
		[NonSerialized] private VoidEvent onAttackInput = new();
		[NonSerialized] private VoidEvent onStartSprintInput = new();
		[NonSerialized] private VoidEvent onStopSprintInput = new();

		public override VoidOutput Discard(VoidInput _ = default)
		{
			Dextra.OnPauseButtonPressed -= StackPauseMenu;

			moveAction.Discard();
			startSprintAction.Discard();
			stopSprintAction.Discard();
			jumpAction.Discard();

			onJumpInput?.Discard();
			onAttackInput?.Discard();
			onStartSprintInput?.Discard();
			onStopSprintInput?.Discard();

			onJumpInput = null;
			onAttackInput = null;
			onStartSprintInput = null;
			onStopSprintInput = null;

			return base.Discard(_);
		}

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			void TrackMovementInput(Vector2 input) { movementInput.CurrentValue = input; }

			void StartSprinting()
			{
				if (movementInput.CurrentValue.magnitude > Mathf.Epsilon)
				{
					Character.IsSprinting = true;
					onStartSprintInput?.Invoke();
				}
			}

			void StopSprinting()
			{
				Character.IsSprinting = false;
				onStopSprintInput?.Invoke();
			}

			void Jump() { onJumpInput?.Invoke(); }
			void Attack() { onAttackInput?.Invoke(); }

			Character = owner.Owner;

			movementInput.PointToInternalReferenceOf(owner);

			moveAction?.Handle(TrackMovementInput);
			startSprintAction?.Handle(StartSprinting);
			stopSprintAction?.Handle(StopSprinting);
			jumpAction?.Handle(Jump);
			attackAction?.Handle(Attack);

			Dextra.OnPauseButtonPressed += StackPauseMenu;
			Dextra.OnInputModeChanged.TryAddListener(OnInputModeChanged);

			//Dextra.CurrentInputMode = Dextra.InputMode.Player;

			base.Initialize(owner);
		}

		private VoidOutput OnInputModeChanged(Dextra.InputMode input)
		{
			movementInput.CurrentValue = Vector2.zero;
			Character.IsSprinting = false;
			return default;
		}

		private VoidOutput StackPauseMenu(VoidInput _)
		{
			Dextra.StackInterface("OverlayUI_PauseMenu");
			return default;
		}

		protected override VoidOutput Run(VoidInput _)
		{
			//if (movementInput.CurrentValue.magnitude <= Mathf.Epsilon) Character.IsSprinting = false;

			return default;
		}
	}
}