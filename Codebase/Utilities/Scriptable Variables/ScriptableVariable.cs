namespace Threadlink.Utilities.Collections
{
	using UnityEngine;

	public abstract class ScriptableVariable : ScriptableObject
	{
		public abstract string TypeName { get; }
		public abstract dynamic Value { get; }
	}

	public abstract class GenericScriptableVariable<T> : ScriptableVariable
	{
		public override dynamic Value => GenericValue;

		[SerializeField] public T GenericValue = default;
	}
}