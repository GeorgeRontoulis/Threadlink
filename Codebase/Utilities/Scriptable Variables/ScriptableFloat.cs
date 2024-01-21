namespace Threadlink.Utilities.Collections
{
	using UnityEngine;

	[CreateAssetMenu(menuName = "Threadlink/Scriptable Variables/Float")]
	public sealed class ScriptableFloat : GenericScriptableVariable<float>
	{
		public override string TypeName => "float";
	}
}