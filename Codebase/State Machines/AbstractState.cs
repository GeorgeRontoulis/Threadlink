namespace Threadlink.StateMachines
{
	using Core;

	public interface IScriptableState<DataType>
	{
		public void ProcessData(DataType data);
	}

	public abstract class AbstractState : LinkableAsset
	{
		public abstract void OnEnter();
		public abstract void OnUpdate();
		public abstract void OnExit();
	}

	public abstract class AbstractState<SMType> : AbstractState
	where SMType : AbstractStateMachine
	{
		public abstract void Initialize(SMType owner);
	}
}