namespace Threadlink.StateMachines.States
{
	using Core.Subsystems.Vault;
	using Core;

	/// <summary>
	/// Representation of a simple state with basic callbacks.
	/// </summary>
	public interface IState : IDiscardable
	{
		public void Boot(Vault parameters);
		public void OnEnter(Vault parameters);
		public void OnExit(Vault parameters);
	}

	/// <summary>
	/// Representation of a scriptable state that can preprocess data.
	/// </summary>
	/// <typeparam name="T">The type of data.</typeparam>
	public interface IScriptableState<T> : IState
	{
		public void Preprocess(T data);
	}

	/// <summary>
	/// Representation of a state that can run in intervals
	/// according to the specified <see cref="UpdateMode"/>.
	/// </summary>
	public interface IUpdatableState : IState
	{
		public UpdateMode UpdateMode { get; }

		public void OnUpdate(Vault parameters);
	}
}