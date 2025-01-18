namespace Threadlink.StateMachines
{
	using Core;
	using Core.StorageAPI;
	using Core.StorageAPI.ExtensionMethods;
	using Core.Subsystems.Propagator;
	using Processors;
	using States;
	using System;

	namespace ExtensionMethods
	{
		using Core.ExtensionMethods;
		using Core.StorageAPI.ExtensionMethods;

		public static class ExtensionMethods
		{
			/// <summary>
			/// Timesaver method for deploying a copy of the original state machine provided. 
			/// Always pass instances of processors and states when calling this method!
			/// </summary>
			/// <typeparam name="T">The desried type of state machine.</typeparam>
			/// <returns>The state machine copy in the desired type.</returns>
			public static T Deploy<T>(this BaseStateMachine original, ref ThreadlinkStorage paramStorage, IProcessor[] processors, IState[] states)
			where T : BaseStateMachine
			{
				var clone = original.Clone();

				clone.States = states;
				clone.Processors = processors;
				clone.ParameterStorage = paramStorage.Deploy();

				clone.Boot(paramStorage);

				return clone as T;
			}
		}
	}

	public enum UpdateMode { Update, FixedUpdate, LateUpdate }

	public abstract class BaseStateMachine : LinkableAsset
	{
		public IState[] States { get; internal set; }
		public IProcessor[] Processors { get; internal set; }

		public IState CurrentState { get; protected set; }

		internal ThreadlinkStorage ParameterStorage { get; set; }

		protected event Action<ThreadlinkStorage> OnUpdate = null;
		protected event Action<ThreadlinkStorage> OnFixedUpdate = null;
		protected event Action<ThreadlinkStorage> OnLateUpdate = null;


		/// <summary>
		/// Timesaver method for deploying a new state machine. 
		/// Always pass instances of processors and states when calling this method!
		/// </summary>
		/// <typeparam name="T">The desried type of state machine.</typeparam>
		/// <returns>The newly created state machine in the desired type.</returns>
		public static T Deploy<T>(string name, ref ThreadlinkStorage paramStorage, IProcessor[] processors, IState[] states)
		where T : BaseStateMachine
		{
			var newStateMachine = Create<T>(name);

			newStateMachine.States = states;
			newStateMachine.Processors = processors;
			newStateMachine.ParameterStorage = paramStorage.Deploy();

			newStateMachine.Boot(paramStorage);

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

			if (CurrentState is IDiscardable currentState) currentState.Discard();

			int length = Processors.Length;

			for (int i = 0; i < length; i++) if (Processors[i] is IDiscardable processor) processor.Discard();

			length = States.Length;

			for (int i = 0; i < length; i++) if (States[i] is IDiscardable processor) processor.Discard();

			CurrentState = default;
			States = null;
			Processors = null;

			if (IsInstance)
			{
				ParameterStorage.Discard();
				ParameterStorage = null;
			}

			base.Discard();
		}

		public virtual void Boot(ThreadlinkStorage paramStorage)
		{
			int length = Processors.Length;
			for (int i = 0; i < length; i++)
			{
				var processor = Processors[i];

				processor.Boot(paramStorage);

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
			for (int i = 0; i < length; i++) States[i].Boot(paramStorage);

			Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, Update);
			Propagator.Subscribe<Action>(PropagatorEvents.OnFixedUpdate, FixedUpdate);
			Propagator.Subscribe<Action>(PropagatorEvents.OnLateUpdate, LateUpdate);

			if (length > 0)
			{
				CurrentState = States[0];
				CurrentState.OnEnter(ParameterStorage);

				if (CurrentState is IUpdatableState updatableState)
				{
					switch (updatableState.UpdateMode)
					{
						case UpdateMode.Update:
						OnUpdate += updatableState.OnUpdate;
						break;
						case UpdateMode.FixedUpdate:
						OnFixedUpdate += updatableState.OnUpdate;
						break;
						case UpdateMode.LateUpdate:
						OnLateUpdate += updatableState.OnUpdate;
						break;
					}
				}
			}
		}

		public virtual void SwitchTo(IState newState)
		{
			if (CurrentState != default)
			{
				if (CurrentState is IUpdatableState updatableState)
				{
					switch (updatableState.UpdateMode)
					{
						case UpdateMode.Update:
						OnUpdate -= updatableState.OnUpdate;
						break;
						case UpdateMode.FixedUpdate:
						OnFixedUpdate -= updatableState.OnUpdate;
						break;
						case UpdateMode.LateUpdate:
						OnLateUpdate -= updatableState.OnUpdate;
						break;
					}
				}

				CurrentState.OnExit(ParameterStorage);
			}

			CurrentState = newState;
			CurrentState.OnEnter(ParameterStorage);

			if (CurrentState is IUpdatableState newUpdatableState)
			{
				switch (newUpdatableState.UpdateMode)
				{
					case UpdateMode.Update:
					OnUpdate += newUpdatableState.OnUpdate;
					break;
					case UpdateMode.FixedUpdate:
					OnFixedUpdate += newUpdatableState.OnUpdate;
					break;
					case UpdateMode.LateUpdate:
					OnLateUpdate += newUpdatableState.OnUpdate;
					break;
				}
			}
		}

		public virtual void SwitchTo<T>(IState newState, T stateData)
		{
			if (CurrentState != default)
			{
				if (CurrentState is IUpdatableState updatableState)
				{
					switch (updatableState.UpdateMode)
					{
						case UpdateMode.Update:
						OnUpdate -= updatableState.OnUpdate;
						break;
						case UpdateMode.FixedUpdate:
						OnFixedUpdate -= updatableState.OnUpdate;
						break;
						case UpdateMode.LateUpdate:
						OnLateUpdate -= updatableState.OnUpdate;
						break;
					}
				}

				CurrentState.OnExit(ParameterStorage);
			}

			CurrentState = newState;

			if (CurrentState is IScriptableState<T> scriptableState) scriptableState.Preprocess(stateData);

			CurrentState.OnEnter(ParameterStorage);

			if (CurrentState is IUpdatableState newUpdatableState)
			{
				switch (newUpdatableState.UpdateMode)
				{
					case UpdateMode.Update:
					OnUpdate += newUpdatableState.OnUpdate;
					break;
					case UpdateMode.FixedUpdate:
					OnFixedUpdate += newUpdatableState.OnUpdate;
					break;
					case UpdateMode.LateUpdate:
					OnLateUpdate += newUpdatableState.OnUpdate;
					break;
				}
			}
		}

		private void Update() => OnUpdate?.Invoke(ParameterStorage);
		private void FixedUpdate() => OnFixedUpdate?.Invoke(ParameterStorage);
		private void LateUpdate() => OnLateUpdate?.Invoke(ParameterStorage);
	}
}