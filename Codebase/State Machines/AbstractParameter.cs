namespace Threadlink.StateMachines
{
	using Core;
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using Utilities.Collections;
	using Utilities.Reflection;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[Serializable]
	public sealed class ParameterPointer<T> : IStateMachinePointer
	{
#if ODIN_INSPECTOR
		[ReadOnly]
		[ShowInInspector]
#endif
		public T CurrentValue
		{
			get => Reference == null ? default : Reference.CurrentValue;
			set => Reference.CurrentValue = value;
		}

		private AbstractParameter<T> Reference { get; set; }

#if UNITY_EDITOR && ODIN_INSPECTOR
#pragma warning disable IDE0051
		private IEnumerable<ValueDropdownItem> AvailableMatches => Reflection.CreateNameDropdownFor<AbstractParameter<T>>();

		[ValueDropdown("AvailableMatches")]
#endif
		[SerializeField] private string parameterID = string.Empty;

		public void PointToInternalReferenceOf(AbstractStateMachine owner)
		{
			owner.GetParameter<T>(parameterID, out var reference);
			Reference = reference;
		}
	}

	public abstract class AbstractParameter : LinkableAsset, IIdentifiable
	{
		public abstract void ResetToDefaultValue();
	}

	public abstract class AbstractParameter<T> : AbstractParameter
	{
#if ODIN_INSPECTOR
		[ReadOnly]
		[ShowInInspector]
#endif
		public T CurrentValue { get; set; }

		[SerializeField] private T defaultValue = default;

		public override void ResetToDefaultValue() { CurrentValue = defaultValue; }
	}
}