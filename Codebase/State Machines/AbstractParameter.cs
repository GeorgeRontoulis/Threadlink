namespace Threadlink.StateMachines
{
	using System;
	using System.Collections.Generic;
	using Threadlink.Utilities.Reflection;
	using UnityEngine;
	using Utilities.Collections;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[Serializable]
	public sealed class ParameterPointer<T>
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
		private IEnumerable<ValueDropdownItem> AvailableMatches => Reflection.CreateNameDropdownFor<AbstractParameter<T>>();

		[ValueDropdown("AvailableMatches")]
#endif
		[SerializeField] private string parameterID = string.Empty;

		public void SetUp(BaseAbstractStateMachine owner)
		{
			Reference = owner.GetParameter<T>(parameterID);
		}
	}

	public abstract class BaseAbstractParameter : ScriptableObject, IIdentifiable
	{
		public string LinkID => name;

		public abstract void ResetToDefaultValue();
	}

	public abstract class AbstractParameter<T> : BaseAbstractParameter
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