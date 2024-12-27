namespace Threadlink.StateMachines
{
	using Core;
	using Core.StorageAPI;
	using Core.Subsystems.Propagator;
	using Processors;
	using States;
	using System;
	using UnityEngine;

	public abstract class StateMachine : LinkableAsset, IBootable
	{
		protected delegate void RefAction<T>(ref T paramStorage);

		protected event RefAction<ThreadlinkStorage> OnUpdate = null;
		protected event RefAction<ThreadlinkStorage> OnFixedUpdate = null;
		protected event RefAction<ThreadlinkStorage> OnLateUpdate = null;

		public IState[] States { protected get; set; }
		public IProcessor[] Processors { protected get; set; }

		public IState CurrentState { get; protected set; }

		[SerializeField] protected ThreadlinkStorage parameterStorage = null;

		public override void Discard()
		{
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnUpdate, Update);
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnFixedUpdate, FixedUpdate);
			Propagator.Unsubscribe<Action>(PropagatorEvents.OnLateUpdate, LateUpdate);

			OnUpdate = null;
			OnFixedUpdate = null;
			OnLateUpdate = null;

			CurrentState = default;
			States = null;
			Processors = null;

			if (IsInstance)
			{
				parameterStorage.Discard();
				parameterStorage = null;
			}

			base.Discard();
		}

		public void Boot()
		{
			int length = Processors.Length;
			for (int i = 0; i < length; i++)
			{
				var processor = Processors[i];

				processor.Boot();

				switch (processor.Mode)
				{
					case IProcessor.UpdateMode.Update:
					OnUpdate += processor.Run;
					break;
					case IProcessor.UpdateMode.FixedUpdate:
					OnFixedUpdate += processor.Run;
					break;
					case IProcessor.UpdateMode.LateUpdate:
					OnLateUpdate += processor.Run;
					break;
				}
			}

			length = States.Length;
			for (int i = 0; i < length; i++) States[i].Boot();

			Propagator.Subscribe<Action>(PropagatorEvents.OnUpdate, Update);
			Propagator.Subscribe<Action>(PropagatorEvents.OnFixedUpdate, FixedUpdate);
			Propagator.Subscribe<Action>(PropagatorEvents.OnLateUpdate, LateUpdate);

			CurrentState = States[0];
			CurrentState.OnEnter(ref parameterStorage);

			if (CurrentState is IUpdatableState updatableState) OnUpdate += updatableState.OnUpdate;
		}

		public void SwitchTo(IState newState)
		{
			if (CurrentState != default)
			{
				if (CurrentState is IUpdatableState updatableState) OnUpdate -= updatableState.OnUpdate;

				CurrentState.OnExit(ref parameterStorage);
			}

			CurrentState = newState;
			CurrentState.OnEnter(ref parameterStorage);

			if (CurrentState is IUpdatableState newUpdatableState) OnUpdate += newUpdatableState.OnUpdate;
		}

		public void SwitchTo<T>(IState newState, T stateData)
		{
			if (CurrentState != default)
			{
				if (CurrentState is IUpdatableState updatableState) OnUpdate -= updatableState.OnUpdate;

				CurrentState.OnExit(ref parameterStorage);
			}

			CurrentState = newState;

			if (CurrentState is IScriptableState<T> scriptableState) scriptableState.Preprocess(stateData);

			CurrentState.OnEnter(ref parameterStorage);

			if (CurrentState is IUpdatableState newUpdatableState) OnUpdate += newUpdatableState.OnUpdate;
		}

		private void Update() => OnUpdate?.Invoke(ref parameterStorage);
		private void FixedUpdate() => OnFixedUpdate?.Invoke(ref parameterStorage);
		private void LateUpdate() => OnLateUpdate?.Invoke(ref parameterStorage);
	}
}