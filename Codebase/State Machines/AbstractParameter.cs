namespace Threadlink.StateMachines
{
	using Sirenix.OdinInspector;
	using UnityEngine;
	using Utilities.Collections;

	public abstract class BaseAbstractParameter : ScriptableObject, IIdentifiable
	{
		public string LinkID => name;

		public abstract void ResetToDefaultValue();
	}

	public abstract class AbstractParameter<T> : BaseAbstractParameter
	{
		[ReadOnly] public T CurrentValue { get; set; }

		public override void ResetToDefaultValue() { CurrentValue = default; }
	}
}