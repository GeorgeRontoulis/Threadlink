namespace Threadlink.Core
{
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	using Extensions.Addressables;
	using System;
	using System.Collections;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Collections;

	[CreateAssetMenu(menuName = "Threadlink/Addressables")]
	public sealed class ThreadlinkAddressables : ScriptableObject
	{
		[Serializable] internal sealed class SystemAddressable : AddressablePrefab<LinkableBehaviour> { }

		[Space(10)]

		[SerializeField] internal AddressableScene[] scenes = new AddressableScene[0];

		[Space(10)]

		[SerializeField] internal SystemAddressable[] coreSystems = new SystemAddressable[0];

		[SerializeField] internal ThreadlinkAddressablesExtension customExtension = null;

#if UNITY_EDITOR
#pragma warning disable IDE0051
#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("Sort Scenes By ID")]
#endif
		private void SortScenesByID() { scenes.SortByID(this); }
#pragma warning restore IDE0051
#endif

		internal IEnumerator[] LoadCoreSystems()
		{
			int length = coreSystems.Length;
			var coroutines = new IEnumerator[length];

			for (int i = 0; i < length; i++) coroutines[i] = coreSystems[i].LoadingCoroutine();

			return coroutines;
		}
	}
}
