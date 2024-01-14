//Project: Aeon's Legacy: Ascension
//Lead Programmer: George Rontoulis

namespace Threadlink.StateMachines
{
	using UnityEngine;

	public interface AbstractStateData { }

	public interface IScriptableState<DataType> where DataType : AbstractStateData
	{
		public void ProcessData(DataType data);
	}

	public abstract class BaseAbstractState : ScriptableObject
	{
		public abstract void OnEnter();
		public abstract void OnUpdate();
		public abstract void OnExit();
	}

	public abstract class AbstractState<SMType> : BaseAbstractState where SMType : BaseAbstractStateMachine
	{
		public abstract void Initialize(SMType owner);
	}
}