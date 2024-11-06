namespace Threadlink.StateMachines
{
	using Core;
	using System;
	using System.Collections.Generic;
	using Systems;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.Events;
	using Utilities.Reflection;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[Serializable]
	public sealed class ProcessorPointer<T> : IStateMachinePointer where T : AbstractStateMachine
	{
		public AbstractProcessor<T> Target { get; private set; }

#if UNITY_EDITOR && ODIN_INSPECTOR
#pragma warning disable IDE0051
		private IEnumerable<ValueDropdownItem> AvailableMatches => Reflection.CreateNameDropdownFor<AbstractProcessor<T>>();

		[ValueDropdown("AvailableMatches")]
#endif
		[SerializeField] private string processorID = string.Empty;

		public void PointToInternalReferenceOf(AbstractStateMachine owner)
		{
			owner.GetProcessor<T>(processorID, out var target);
			Target = target;
		}
	}

	public abstract class AbstractProcessor : LinkableAsset, IIdentifiable
	{
		private enum UpdateMode { Update, FixedUpdate, LateUpdate }

		[SerializeField] private UpdateMode runIn = UpdateMode.Update;
		[SerializeField] protected bool startUpdatingOnInit = true;

		public override Empty Discard(Empty _ = default)
		{
			SetRunningState(false);
			return base.Discard(_);
		}

		public void SetRunningState(bool state)
		{
			if (state)
			{
				switch (runIn)
				{
					case UpdateMode.Update: Iris.OnUpdate += Run; break;
					case UpdateMode.FixedUpdate: Iris.OnFixedUpdate += Run; break;
					case UpdateMode.LateUpdate: Iris.OnLateUpdate += Run; break;
				}
			}
			else
			{
				switch (runIn)
				{
					case UpdateMode.Update: Iris.OnUpdate -= Run; break;
					case UpdateMode.FixedUpdate: Iris.OnFixedUpdate -= Run; break;
					case UpdateMode.LateUpdate: Iris.OnLateUpdate -= Run; break;
				}
			}
		}

		protected abstract Empty Run(Empty _);
	}

	public abstract class AbstractProcessor<SMType> : AbstractProcessor
	where SMType : AbstractStateMachine
	{
		public virtual void Initialize(SMType owner)
		{
			if (startUpdatingOnInit) SetRunningState(true);
		}
	}
}