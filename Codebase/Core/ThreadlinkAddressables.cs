namespace Threadlink.Core
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using Extensions.Addressables;
	using System.Collections;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;

	[CreateAssetMenu(menuName = "Threadlink/Addressables")]
	public sealed class ThreadlinkAddressables : ScriptableObject
	{
		[SerializeField] internal AddressableScene[] scenes = new AddressableScene[0];

		[Space(10)]

		[SerializeField] internal AddressablePrefab<LinkableBehaviour>[] coreSystems = new AddressablePrefab<LinkableBehaviour>[0];

		[SerializeField] internal ThreadlinkAddressablesExtension customExtension = null;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Sort Scenes By ID")]
#endif
#pragma warning disable IDE0051
		private void SortScenesByID() { scenes.SortByID(this); }
#pragma warning restore IDE0051
#endif
	}
}
