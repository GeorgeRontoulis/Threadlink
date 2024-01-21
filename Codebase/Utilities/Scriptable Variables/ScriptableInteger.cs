namespace Threadlink.Utilities.Collections
{
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Scriptable Variables/Integer")]
	public sealed class ScriptableInteger : GenericScriptableVariable<int>
	{
		public override string TypeName => "int";
	}
}