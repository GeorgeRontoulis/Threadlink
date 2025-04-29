namespace Threadlink.StateMachines
{
	using Core;
	using Core.ExtensionMethods;
	using Core.Subsystems.Propagator;
	using Core.Subsystems.Vault;
	using Processors;
	using States;
	using System;

	namespace ExtensionMethods
	{
		using Core.ExtensionMethods;

		public static class ExtensionMethods
		{
			/// <summary>
			/// Timesaver method for deploying a copy of an existing state machine reference.
			/// Can be useful if you have derived from the base class to create your own state machines.
			/// Always pass instances of processors and states when calling this method!
			/// </summary>
			/// <typeparam name="T">The desried type of state machine.</typeparam>
			/// <returns>The state machine copy as the specified type.</returns>
			public static T Deploy<T>(this StateMachine original, Vault parameters, IProcessor[] processors, IState[] states)
			where T : StateMachine
			{
				var clone = original.Clone();

				clone.States = states;
				clone.Processors = processors;
				clone.Parameters = parameters.IsInstance ? parameters : parameters.Clone();

				clone.Boot();

				return clone as T;
			}
		}
	}

	public enum UpdateMode : byte { Update, FixedUpdate, LateUpdate }

	public abstract class StateMachine : LinkableAsset, IBootable
	{
		public IState[] States { get; internal set; }
		public IProcessor[] Processors { get; internal set; }

		public IState CurrentState { get; protected set; }

		public Vault Parameters { get; internal set; }

		protected event Action<Vault> OnUpdate = null;
		protected event Action<Vault> OnFixedUpdate = null;
		protected event Action<Vault> OnLateUpdate = null;

		/// <summary>
		/// Timesaver method for deploying a new state machine. 
		/// Always pass instances of processors and states when calling this method!
		/// </summary>
		/// <typeparam name="T">The desried type of state machine.</typeparam>
		/// <returns>The new state machine as the specified type.</returns>
		public static T Deploy<T>(string name, Vault parameters, IProcessor[] processors, IState[] states)
		where T : StateMachine
		{
			var newStateMachine = Create<T>(name);

			newStateMachine.States = states;
			newStateMachine.Processors = processors;
			newStateMachine.Parameters = parameters.IsInstance ? parameters : parameters.Clone();

			newStateMachine.Boot();

			return newStateMachine;
		}

		public override void Discard()
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, Update);
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnFixedUpdate, FixedUpdate);
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnLateUpdate, LateUpdate);

			OnUpdate = null;
			OnFixedUpdate = null;
			OnLateUpdate = null;

			if (CurrentState != default) CurrentState.Discard();

			int length = Processors.Length;
			for (int i = 0; i < length; i++) Processors[i].Discard();

			length = States.Length;
			for (int i = 0; i < length; i++) States[i].Discard();

			CurrentState = default;
			States = null;
			Processors = null;

			if (IsInstance && Parameters != null)
			{
				if (Parameters.IsInstance) Parameters.Discard();
				Parameters = null;
			}

			base.Discard();
		}

		public virtual void Boot()
		{
			int length = Processors.Length;
			for (int i = 0; i < length; i++)
			{
				var processor = Processors[i];

				processor.Boot(Parameters);

				switch (processor.Mode)
				{
					case UpdateMode.Update:
					OnUpdate += processor.Run;
					break;
					case UpdateMode.FixedUpdate:
					OnFixedUpdate += processor.Run;
					break;
					case UpdateMode.LateUpdate:
					OnLateUpdate += processor.Run;
					break;
				}
			}

			length = States.Length;
			for (int i = 0; i < length; i++) States[i].Boot(Parameters);

			Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, Update);
			Propagator.Subscribe<Action>(PropagatorEvents.OnFixedUpdate, FixedUpdate);
			Propagator.Subscribe<Action>(PropagatorEvents.OnLateUpdate, LateUpdate);

			if (length > 0) Enter(States[0]);
		}

		public virtual void SwitchTo(IState newState)
		{
			ExitCurrentState();
			Enter(newState);
		}

		public virtual void SwitchTo<T>(IScriptableState<T> newState, T stateData)
		{
			ExitCurrentState();
			newState.Preprocess(stateData);
			Enter(newState);
		}

		private void ExitCurrentState()
		{
			if (CurrentState != default)
			{
				if (CurrentState is IUpdatableState updatableState)
				{
					Action<Vault> action = updatableState.OnUpdate;

					switch (updatableState.UpdateMode)
					{
						case UpdateMode.Update:
						OnUpdate -= action;
						break;
						case UpdateMode.FixedUpdate:
						OnFixedUpdate -= action;
						break;
						case UpdateMode.LateUpdate:
						OnLateUpdate -= action;
						break;
					}
				}

				CurrentState.OnExit(Parameters);
				CurrentState = default;
			}
		}

		private void Enter(IState newState)
		{
			newState.OnEnter(Parameters);

			if (newState is IUpdatableState newUpdatableState)
			{
				Action<Vault> action = newUpdatableState.OnUpdate;

				switch (newUpdatableState.UpdateMode)
				{
					case UpdateMode.Update:
					OnUpdate += action;
					break;
					case UpdateMode.FixedUpdate:
					OnFixedUpdate += action;
					break;
					case UpdateMode.LateUpdate:
					OnLateUpdate += action;
					break;
				}
			}

			CurrentState = newState;
		}

		private void Update() => OnUpdate?.Invoke(Parameters);
		private void FixedUpdate() => OnFixedUpdate?.Invoke(Parameters);
		private void LateUpdate() => OnLateUpdate?.Invoke(Parameters);
	}
}