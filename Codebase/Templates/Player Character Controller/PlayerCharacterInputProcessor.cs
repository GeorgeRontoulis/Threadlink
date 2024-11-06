namespace Threadlink.Templates.PlayerCharacterController
{
	using Core;
	using Extensions.Dextra;
	using StateMachines;
	using System;
	using Systems.Dextra;
	using UnityEngine;
	using Utilities.Events;

	[CreateAssetMenu(menuName = "Threadlink/Templates/Character Controller/Processors/Input")]
	public sealed class PlayerCharacterInputProcessor : PlayerCharacterProcessor
	{
		private static ThreadlinkEventBus EventBus => Threadlink.EventBus;

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

		public override Empty Discard(Empty _ = default)
		{
			EventBus.OnDextraPausePressed -= StackPauseMenu;
			EventBus.OnDextraInputModeChanged -= OnInputModeChanged;

			moveAction.Discard();
			startSprintAction.Discard();
			stopSprintAction.Discard();
			jumpAction.Discard();

			onJumpInput.Discard();
			onAttackInput.Discard();
			onStartSprintInput.Discard();
			onStopSprintInput.Discard();

			onJumpInput = null;
			onAttackInput = null;
			onStartSprintInput = null;
			onStopSprintInput = null;

			return base.Discard(_);
		}

		public override void Initialize(PlayerCharacterStateMachine owner)
		{
			const IPlayerCharacter.StateFlags movingFlag = IPlayerCharacter.StateFlags.IsMoving;
			const IPlayerCharacter.StateFlags sprintingFlag = IPlayerCharacter.StateFlags.IsSprinting;

			void TrackMovementInput(Vector2 input)
			{
				movementInput.CurrentValue = input;

				if (input.magnitude > Mathf.Epsilon)
					Character.CurrentStateFlags |= movingFlag;
				else
					Character.CurrentStateFlags &= ~movingFlag;
			}

			void StartSprinting()
			{
				if (movementInput.CurrentValue.magnitude > Mathf.Epsilon)
				{
					Character.CurrentStateFlags |= sprintingFlag;
					onStartSprintInput.Invoke();
				}
			}

			void StopSprinting()
			{
				Character.CurrentStateFlags &= ~sprintingFlag;
				onStopSprintInput.Invoke();
			}

			void Jump() { onJumpInput.Invoke(); }
			void Attack() { onAttackInput.Invoke(); }

			Character = owner.Owner;

			movementInput.PointToInternalReferenceOf(owner);

			moveAction.Handle(TrackMovementInput);
			startSprintAction.Handle(StartSprinting);
			stopSprintAction.Handle(StopSprinting);
			jumpAction.Handle(Jump);
			attackAction.Handle(Attack);

			EventBus.OnDextraPausePressed += StackPauseMenu;
			EventBus.OnDextraInputModeChanged += OnInputModeChanged;

			//Scribe.LogInfo(this, "Input Processor initialized!");
			//Dextra.CurrentInputMode = Dextra.InputMode.Player;

			base.Initialize(owner);
		}

		private Empty OnInputModeChanged(Dextra.InputMode input)
		{
			movementInput.CurrentValue = Vector2.zero;
			Character.CurrentStateFlags &= ~IPlayerCharacter.StateFlags.IsSprinting;
			return default;
		}

		private Empty StackPauseMenu(Empty _)
		{
			Dextra.Stack("OverlayUI_PauseMenu");
			return default;
		}

		protected override Empty Run(Empty _)
		{
			//if (movementInput.CurrentValue.magnitude <= Mathf.Epsilon) Character.IsSprinting = false;

			return default;
		}
	}
}