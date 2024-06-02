namespace Threadlink.StateMachines
{
	using UnityEngine;
	using Threadlink.Systems;
	using Threadlink.Utilities.Events;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using System.Collections.Generic;
	using System;
	using Threadlink.Utilities.Reflection;
	using Threadlink.Utilities.Collections;
	using Threadlink.Core;

	[Serializable]
	public sealed class ProcessorPointer<T> : IStateMachinePointer where T : BaseAbstractStateMachine
	{
		public AbstractProcessor<T> Target { get; private set; }

#if UNITY_EDITOR && ODIN_INSPECTOR
		private IEnumerable<ValueDropdownItem> AvailableMatches => Reflection.CreateNameDropdownFor<AbstractProcessor<T>>();

		[ValueDropdown("AvailableMatches")]
#endif
		[SerializeField] private string processorID = string.Empty;

		public void PointToInternalReferenceOf(BaseAbstractStateMachine owner)
		{
			Target = owner.GetProcessor<T>(processorID);
		}
	}

	public abstract class BaseAbstractProcessor : LinkableAsset, IIdentifiable
	{
		private enum UpdateMode { Update, FixedUpdate, LateUpdate }

		[SerializeField] private UpdateMode runIn = UpdateMode.Update;
		[SerializeField] protected bool startUpdatingOnInit = true;

		protected abstract VoidOutput Run(VoidInput _);

		public override void Discard()
		{
			SetRunningState(false);
			base.Discard();
		}

		public override void Boot() { }
		public override void Initialize() { }

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