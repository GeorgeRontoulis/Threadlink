namespace Threadlink.Utilities.Editor
{
#if UNITY_EDITOR
	using UnityEngine;

	internal sealed class ThreadlinkEditorAddressablesContainer : ScriptableObject
	{
		[SerializeField] internal Object[] adressableAssets = new Object[0];
	}
#endif
}