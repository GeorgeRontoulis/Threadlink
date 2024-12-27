namespace Threadlink.StateMachines.States
{
	using Core;
	using Core.StorageAPI;

	/// <summary>
	/// Representation of a simple state with basic callbacks.
	/// </summary>
	public interface IState : IBootable
	{
		public void OnEnter(ref ThreadlinkStorage paramStorage);
		public void OnExit(ref ThreadlinkStorage paramStorage);
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
	/// Representation of a state that can run per-frame logic.
	/// </summary>
	public interface IUpdatableState : IState
	{
		public void OnUpdate(ref ThreadlinkStorage paramStorage);
	}
}