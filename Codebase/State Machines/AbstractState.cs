//Project: Aeon's Legacy: Ascension
//Lead Programmer: George Rontoulis

namespace Threadlink.StateMachines
{
	using Threadlink.Core;
	using UnityEngine;

	public interface IScriptableState<DataType>
	{
		public void ProcessData(DataType data);
	}

	public abstract class BaseAbstractState : LinkableAsset
	{
		public override void Boot() { }
		public override void Initialize() { }

		public abstract void OnEnter();
		public abstract void OnUpdate();
		public abstract void OnExit();
	}

	public abstract class AbstractState<SMType> : BaseAbstractState where SMType : BaseAbstractStateMachine
	{
		public abstract void Initialize(SMType owner);
	}
}