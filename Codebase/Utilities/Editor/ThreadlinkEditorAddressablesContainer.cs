namespace Threadlink.Utilities.Editor
{
	using UnityEngine;

	internal sealed class ThreadlinkEditorAddressablesContainer : ScriptableObject
	{
		[SerializeField] internal Object[] adressableAssets = new Object[0];
	}
}