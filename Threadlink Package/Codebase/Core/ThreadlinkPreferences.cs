namespace Threadlink.Core
{
	using Addressables;
	using AYellowpaper.SerializedCollections;
	using UnityEngine;
	using UnityEngine.AddressableAssets;

	[CreateAssetMenu(fileName = "Threadlink User Data", menuName = "Threadlink/Preferences Asset")]
	public sealed class ThreadlinkPreferences : ScriptableObject
	{
		public enum CoreDeploymentMethod : byte { Automatic, Manual }

		[Tooltip("Automatic: Automatically loads and deploys Threadlink when entering playmode, or in the first scene in a Built Player." +
		" Manual: You will be responsible for deploying the core using Threadlink's Lifecycle API in your custom logic.")]
		public CoreDeploymentMethod coreDeployment;

		[Space(10)]

		public AssetReferenceGameObject[] subsystemDatabase = new AssetReferenceGameObject[0];

		[Space(10)]

		public SceneAssetReference[] sceneDatabase = new SceneAssetReference[0];

		[Space(10)]

		public SerializedDictionary<ThreadlinkAddressableGroup, AssetReference[]> assetDatabase = new();

		[Space(10)]

		public SerializedDictionary<ThreadlinkAddressableGroup, AssetReferenceGameObject[]> prefabDatabase = new();
	}
}
