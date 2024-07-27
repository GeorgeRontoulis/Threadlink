namespace Threadlink.Systems.Nexus
{
	using Core;
	using Extensions.Nexus;
	using System.Collections;
	using UnityEngine;
	using UnityEngine.SceneManagement;
	using Utilities.Addressables;

	public abstract class SceneEntry : ScriptableObject
	{
		internal AddressableScene AddressableScene
		{
			get
			{
				Threadlink.FindAddressableScene(sceneInfo.assetAddress, out var scene);
				return scene;
			}
		}

		[SerializeField] private AssetGroupAddressPair sceneInfo = new();

		[Space(10)]

		[SerializeField] internal LoadSceneMode loadingMode = LoadSceneMode.Additive;
		[SerializeField] internal PlayerLoadingAction playerLoadingAction = PlayerLoadingAction.Load;

		[Space(10)]

		[SerializeField] internal Vector3[] playerSpawnPoints = new Vector3[0];

		public abstract IEnumerator PostLoadingCoroutine();
	}
}