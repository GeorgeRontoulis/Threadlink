namespace Threadlink.StateMachines
{
	using Sirenix.OdinInspector;
	using System;
	using Threadlink.Systems;
	using UnityEngine;
	using Utilities.Collections;

	public abstract class BaseAbstractStateMachine : ScriptableObject
	{
		[Flags]
		protected enum RuntimeInstantiation
		{
			Nothing = 0,
			Everything = -1,
			Parameters = 1,
			States = 2,
			Processors = 4,
		}

		public bool IsInstance { get; private set; }

		[Space(10)]

		[SerializeField] protected RuntimeInstantiation runtimeInstantiation = 0;

		[Space(15)]

		[SerializeField] protected BaseAbstractParameter[] parameters = new BaseAbstractParameter[0];

		protected abstract void InitializeStatesAndProcessors();

		public ParamType GetParameter<ParamType>(string id) where ParamType : BaseAbstractParameter
		{
			void Notify(params string[] messages) { Scribe.LogError(messages); }

			BaseAbstractParameter paramFound = parameters.BinarySearch(id);

			if (paramFound == null)
			{
				Notify("The requested parameter could not be found! Check your request!");
				return null;
			}

			if (paramFound is ParamType) return paramFound as ParamType;
			else
			{
				Notify("Could not cast the parameter to the requested type! Check your request!");
				return null;
			}
		}

		public static T CreateCopyFrom<T>(T original) where T : BaseAbstractStateMachine
		{
			T copy = Instantiate(original);
			copy.IsInstance = true;

			return copy;
		}
	}

	public abstract class AbstractStateMachine<OwnerType, StateType, ProcessorType> : BaseAbstractStateMachine
	where OwnerType : UnityEngine.Object where StateType : BaseAbstractState where ProcessorType : BaseAbstractProcessor
	{
		public bool IsInDefaultState => CurrentState.Equals(states[0]);

		public StateType CurrentState { get; protected set; }
		public OwnerType Owner { get; protected set; }

		[Space(10)]

		[InfoBox("The first state in the list acts as the starting state for the state machine.")]

		[Space(5)]

		[Required][SerializeField] protected StateType[] states = new StateType[0];

		[Space(10)]

		[SerializeField] protected ProcessorType[] processors = new ProcessorType[0];

#if UNITY_EDITOR
		[PropertySpace(15)]
		[Button] private void SortParametersByID() { parameters.SortByID(this); }
#endif

		public void Initialize(OwnerType owner)
		{
			Owner = owner;

			ManageInstantiation();

			int length = parameters.Length;
			for (int i = 0; i < length; i++) parameters[i].ResetToDefaultValue();

			InitializeStatesAndProcessors();

			CurrentState = states[0];
			CurrentState.OnEnter();
			Iris.SubscribeToUpdate(UpdateCurrentState);
		}

		private void ManageInstantiation()
		{
			bool ShouldInstantiate(Enum flag) { return runtimeInstantiation.HasFlag(flag); }

			void InstantiateCollection(ScriptableObject[] collection)
			{
				int length = collection.Length;
				for (int i = 0; i < length; i++) collection[i] = Instantiate(collection[i]);
			}

			if (ShouldInstantiate(RuntimeInstantiation.Nothing)) return;
			else if (ShouldInstantiate(RuntimeInstantiation.Everything))
			{
				InstantiateCollection(parameters);
				InstantiateCollection(states);
				InstantiateCollection(processors);
			}
			else
			{
				if (ShouldInstantiate(RuntimeInstantiation.Parameters)) InstantiateCollection(parameters);

				if (ShouldInstantiate(RuntimeInstantiation.States)) InstantiateCollection(states);

				if (ShouldInstantiate(RuntimeInstantiation.Processors)) InstantiateCollection(processors);
			}
		}

		private void UpdateCurrentState() { CurrentState.OnUpdate(); }

		public virtual void Discard()
		{
			bool Instantiated(Enum flag) { return runtimeInstantiation.HasFlag(flag); }

			void DestroyCollection(ScriptableObject[] collection)
			{
				int length = collection.Length;
				for (int i = 0; i < length; i++) Destroy(collection[i]);
			}

			int length = processors.Length;

			for (int i = 0; i < length; i++) processors[i].Discard();

			Iris.UnsubscribeFromUpdate(UpdateCurrentState);

			if (Instantiated(RuntimeInstantiation.Nothing)) return;
			else if (Instantiated(RuntimeInstantiation.Everything))
			{
				DestroyCollection(parameters);
				DestroyCollection(states);
				DestroyCollection(processors);
			}
			else
			{
				if (Instantiated(RuntimeInstantiation.Parameters)) DestroyCollection(parameters);

				if (Instantiated(RuntimeInstantiation.States)) DestroyCollection(states);

				if (Instantiated(RuntimeInstantiation.Processors)) DestroyCollection(processors);
			}

			if (IsInstance)
			{
				CurrentState = null;
				Owner = null;
				parameters = null;
				states = null;
				processors = null;

				Destroy(this);
			}
		}

		public void AttemptTransitionTo(StateType newState)
		{
			if (newState == null || newState.Equals(CurrentState)
			|| (CurrentState != null && newState.name.Equals(CurrentState.name))) return;

			CurrentState.OnExit();
			newState.OnEnter();
			CurrentState = newState;
		}

		public void AttemptTransitionTo<ScriptableStateType, DataType>(StateType newState, DataType data)
		where DataType : AbstractStateData where ScriptableStateType : IScriptableState<DataType>
		{
			if (newState == null || newState.Equals(CurrentState)
			|| (CurrentState != null && newState.name.Equals(CurrentState.name))) return;

			try
			{
				IScriptableState<DataType> scriptableState = newState as IScriptableState<DataType>;
				scriptableState.ProcessData(data);
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