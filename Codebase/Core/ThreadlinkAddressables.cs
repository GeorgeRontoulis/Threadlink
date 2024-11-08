namespace Threadlink.Core
{
	using Extensions.Addressables;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;

#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[CreateAssetMenu(menuName = "Threadlink/Addressables")]
	public sealed class ThreadlinkAddressables : ScriptableObject
	{
		[SerializeField] internal AddressableScene[] scenes = new AddressableScene[0];

		[Space(10)]

		[SerializeField] internal AddressablePrefab<ThreadlinkSystem>[] coreSystems = new AddressablePrefab<ThreadlinkSystem>[0];

		[Space(10)]

		[SerializeField] internal ThreadlinkAddressablesExtension customExtension = null;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Sort Scenes By ID")]
#endif
#pragma warning disable IDE0051
		private void SortScenesByID() { scenes.SortByID(this); }
#endif
	}
}
