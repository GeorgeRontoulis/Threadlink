namespace Threadlink.Core
{
	using Extensions.Addressables;
	using Sirenix.OdinInspector;
	using System;
	using System.Collections;
	using UnityEngine;
	using UnityEngine.UI;
	using Utilities.Addressables;
	using Utilities.Collections;

	[CreateAssetMenu(menuName = "Threadlink/Addressables")]
	internal sealed class ThreadlinkAddressables : ScriptableObject
	{
		[Serializable] internal sealed class SystemAddressable : AddressablePrefab<BaseLinkableSystem> { }

		[Space(10)]

		[SerializeField] internal AddressableScene[] scenes = new AddressableScene[0];

		[Space(10)]

		[SerializeField] internal SystemAddressable[] coreSystems = new SystemAddressable[0];

		[SerializeField] internal ThreadlinkAddressablesExtender customExtender = null;

#if UNITY_EDITOR
		[PropertySpace(20)]
		[Button] private void SortScenesByID() { scenes.SortByID(this); }
#endif

		internal IEnumerator[] LoadCoreSystems()
		{
			int length = coreSystems.Length;
			IEnumerator[] coroutines = new IEnumerator[length];

			for (int i = 0; i < length; i++) coroutines[i] = coreSystems[i].LoadingCoroutine();

			return coroutines;
		}
	}
}
