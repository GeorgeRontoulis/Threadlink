namespace Threadlink.Systems.Nexus
{
	using System.Collections;
	using Threadlink.Core;
	using Threadlink.Extensions.Nexus;
	using UnityEngine;
	using UnityEngine.SceneManagement;
	using Utilities.Addressables;

	public abstract class SceneEntry : ScriptableObject
	{
		internal AddressableScene AddressableScene => Threadlink.FindAddressableScene(sceneInfo.assetAddress);

		[SerializeField] private AssetGroupAddressPair sceneInfo = new();

		[Space(10)]

		[SerializeField] internal LoadSceneMode loadingMode = LoadSceneMode.Additive;
		[SerializeField] internal PlayerLoadingAction playerLoadingAction = PlayerLoadingAction.Load;

		public abstract IEnumerator PostLoadingCoroutine();
	}
}