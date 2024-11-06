namespace Threadlink.StateMachines
{
	using Core;
	using Core.ExtensionMethods;
	using System;
	using Systems;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.Events;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	public interface IStateMachinePointer
	{
		public void PointToInternalReferenceOf(AbstractStateMachine owner);
	}

	public abstract class AbstractStateMachine : LinkableAsset
	{
		[Flags]
		protected enum RuntimeInstantiation
		{
			Parameters = 1 << 0,
			States = 1 << 1,
			Processors = 1 << 2,
		}

		[Space(10)]

		[SerializeField] protected RuntimeInstantiation runtimeInstantiation = 0;

		[Space(15)]

		[SerializeField] protected AbstractParameter[] parameters = new AbstractParameter[0];

		public override Empty Discard(Empty _ = default)
		{
			if (IsInstance) parameters = null;
			return base.Discard(_);
		}

		protected abstract void InitializeProcessorsAndStates();

		public void GetParameter<T>(string id, out AbstractParameter<T> result)
		{
			parameters.BinarySearch(id, out var paramFound);

			if (paramFound == null) this.LogException<ParameterNotFoundException>();

			result = paramFound as AbstractParameter<T>;
		}

		public abstract void GetProcessor<T>(string id, out AbstractProcessor<T> result) where T : AbstractStateMachine;
	}

	public abstract class AbstractStateMachine<OwnerType, StateType, ProcessorType> : AbstractStateMachine
	where StateType : AbstractState
	where ProcessorType : AbstractProcessor
	{
		public bool IsInDefaultState => CurrentState.Equals(states[0]);

		public StateType CurrentState { get; protected set; }
		public OwnerType Owner { get; protected set; }

		[Space(10)]

#if ODIN_INSPECTOR
		[Required]
#endif
		[SerializeField] protected StateType[] states = new StateType[0];

		[Space(10)]

		[SerializeField] protected ProcessorType[] processors = new ProcessorType[0];

#if UNITY_EDITOR
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Sort Parameters")]
#endif
#pragma warning disable IDE0051
		private void SortParameters() { parameters.SortByID(this); }

#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Sort Processors")]
#endif
		private void SortProcessors() { processors.SortByID(this); }
#pragma warning restore IDE0051
#endif

		public override void GetProcessor<T>(string id, out AbstractProcessor<T> result)
		{
			processors.BinarySearch(id, out var processorFound);

			if (processorFound == null) this.LogException<ProcessorNotFoundException>();

			result = processorFound as AbstractProcessor<T>;
		}

		public virtual void Boot(OwnerType owner)
		{
			Owner = owner;

			ManageInstantiation();

			int length = parameters.Length;
			for (int i = 0; i < length; i++) parameters[i].ResetToDefaultValue();

			InitializeProcessorsAndStates();

			if (states.Length > 0 && states[0] != null)
			{
				CurrentState = states[0];
				CurrentState.OnEnter();
				Iris.OnUpdate += UpdateCurrentState;
			}
		}

		private void ManageInstantiation()
		{
			static void CloneCollection(LinkableAsset[] collection)
			{
				int length = collection.Length;
				for (int i = 0; i < length; i++) collection[i] = collection[i].Clone();
			}

			bool ShouldInstantiate(Enum flag) { return runtimeInstantiation.HasFlag(flag); }

			if (ShouldInstantiate(RuntimeInstantiation.Parameters)) CloneCollection(parameters);

			if (ShouldInstantiate(RuntimeInstantiation.States)) CloneCollection(states);

			if (ShouldInstantiate(RuntimeInstantiation.Processors)) CloneCollection(processors);
		}

		private Empty UpdateCurrentState(Empty _)
		{
			CurrentState.OnUpdate();
			return default;
		}

		public override Empty Discard(Empty _ = default)
		{
			static void DiscardCollection(LinkableAsset[] collection)
			{
				int length = collection.Length;
				for (int i = 0; i < length; i++) collection[i].Discard();
			}

			bool Instantiated(Enum flag) { return runtimeInstantiation.HasFlag(flag); }

			Iris.OnUpdate -= UpdateCurrentState;

			int length = processors.Length;
			for (int i = 0; i < length; i++) processors[i].Discard();

			if (Instantiated(RuntimeInstantiation.Parameters)) DiscardCollection(parameters);

			if (Instantiated(RuntimeInstantiation.States)) DiscardCollection(states);

			if (Instantiated(RuntimeInstantiation.Processors)) DiscardCollection(processors);

			if (IsInstance)
			{
				CurrentState = null;
				Owner = default;
				parameters = null;
				states = null;
				processors = null;
			}

			return base.Discard(_);
		}

		public void AttemptTransitionTo(StateType newState)
		{
			if (newState == null || newState.Equals(CurrentState)
			|| (CurrentState != null && newState.name.Equals(CurrentState.name))) return;

			CurrentState.OnExit();
			newState.OnEnter();
			CurrentState = newState;
		}

		public void AttemptTransitionTo<DataType>(StateType newState, DataType data)
		{
			if (newState == null || newState.Equals(CurrentState)
			|| (CurrentState != null && newState.name.Equals(CurrentState.name))) return;

			try
			{
				(newState as IScriptableState<DataType>).ProcessData(data);
			}
			catch { this.LogException<InvalidScriptableStateCastException>(); }

			CurrentState.OnExit();
			newState.OnEnter();
			CurrentState = newState;
		}
	}
}