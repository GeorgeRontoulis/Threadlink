namespace Threadlink.StateMachines
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using System;
	using Core;
	using Systems;
	using Utilities.Events;
	using UnityEngine;
	using Utilities.Collections;

	public interface IStateMachinePointer
	{
		public void PointToInternalReferenceOf(BaseAbstractStateMachine owner);
	}

	public abstract class BaseAbstractStateMachine : LinkableAsset
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

		[SerializeField] protected BaseAbstractParameter[] parameters = new BaseAbstractParameter[0];

		public override void Discard()
		{
			if (IsInstance) parameters = null;
			base.Discard();
		}

		public override void Boot() { }
		public override void Initialize() { }

		protected abstract void InitializeStatesAndProcessors();

		public void GetParameter<T>(string id, out AbstractParameter<T> result)
		{
			parameters.BinarySearch(id, out var paramFound);

			if (paramFound == null)
			{
				Scribe.LogError("The requested parameter could not be found! Check your request!");
				result = null;
				return;
			}

			result = paramFound as AbstractParameter<T>;
		}

		public abstract void GetProcessor<T>(string id, out AbstractProcessor<T> result) where T : BaseAbstractStateMachine;
	}

	public abstract class AbstractStateMachine<OwnerType, StateType, ProcessorType> : BaseAbstractStateMachine
	where StateType : BaseAbstractState
	where ProcessorType : BaseAbstractProcessor
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
		[ContextMenu("Sort Parameters By ID")]
#endif
#pragma warning disable IDE0051
		private void SortParametersByID() { parameters.SortByID(this); }
#pragma warning restore IDE0051
#endif


		public override void GetProcessor<T>(string id, out AbstractProcessor<T> result)
		{
			processors.BinarySearch(id, out var processorFound);

			if (processorFound == null)
			{
				Scribe.LogError("The requested processor could not be found! Check your request!");
				result = null;
				return;
			}

			result = processorFound as AbstractProcessor<T>;
		}

		public virtual void Initialize(OwnerType owner)
		{
			Owner = owner;

			ManageInstantiation();

			int length = parameters.Length;
			for (int i = 0; i < length; i++) parameters[i].ResetToDefaultValue();

			InitializeStatesAndProcessors();

			if (states.Length > 0)
			{
				CurrentState = states[0];

				if (CurrentState != null)
				{
					CurrentState.OnEnter();
					Iris.SubscribeToUpdate(UpdateCurrentState);
				}
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

		private VoidOutput UpdateCurrentState(VoidInput _)
		{
			CurrentState.OnUpdate();
			return default;
		}

		public override void Discard()
		{
			static void DiscardCollection(LinkableAsset[] collection)
			{
				int length = collection.Length;
				for (int i = 0; i < length; i++) collection[i].Discard();
			}

			bool Instantiated(Enum flag) { return runtimeInstantiation.HasFlag(flag); }

			Iris.UnsubscribeFromUpdate(UpdateCurrentState);

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

			base.Discard();
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
			catch (Exception exception)
			{
				Scribe.LogException(exception);
			}

			CurrentState.OnExit();
			newState.OnEnter();
			CurrentState = newState;
		}
	}
}