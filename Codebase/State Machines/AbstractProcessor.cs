namespace Threadlink.StateMachines
{
	using UnityEngine;
	using Threadlink.Systems;

	public abstract class BaseAbstractProcessor : ScriptableObject
	{
		private enum UpdateMode { Update, FixedUpdate, LateUpdate }

		[SerializeField] private UpdateMode runIn = UpdateMode.Update;
		[SerializeField] protected bool startUpdatingOnInit = true;

		protected abstract void Run();

		public virtual void Discard()
		{
			SetRunningState(false);
		}

		internal void SetRunningState(bool state)
		{
			if (state)
			{
				switch (runIn)
				{
					case UpdateMode.Update: Iris.SubscribeToUpdate(Run); break;
					case UpdateMode.FixedUpdate: Iris.SubscribeToFixedUpdate(Run); break;
					case UpdateMode.LateUpdate: Iris.SubscribeToLateUpdate(Run); break;
				}
			}
			else
			{
				switch (runIn)
				{
					case UpdateMode.Update: Iris.UnsubscribeFromUpdate(Run); break;
					case UpdateMode.FixedUpdate: Iris.UnsubscribeFromFixedUpdate(Run); break;
					case UpdateMode.LateUpdate: Iris.UnsubscribeFromLateUpdate(Run); break;
				}
			}
		}
	}

	public abstract class AbstractProcessor<SMType> : BaseAbstractProcessor where SMType : BaseAbstractStateMachine
	{
		public virtual void Initialize(SMType owner) { if (startUpdatingOnInit) SetRunningState(true); }
	}
}